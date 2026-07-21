using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public static class InventoryService
{
    public static string StartSession(string countedBy)
    {
        var sessionId = Guid.NewGuid().ToString("N")[..12].ToUpper();
        var now = TimeHelper.Now.ToString("yyyy-MM-dd HH:mm:ss");

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand(
            "INSERT INTO InventorySession (SessionId, CountedBy, StartedAt, Status) VALUES (@sid, @cb, @sa, 'Active')", conn);
        cmd.Parameters.AddWithValue("@sid", sessionId);
        cmd.Parameters.AddWithValue("@cb", countedBy);
        cmd.Parameters.AddWithValue("@sa", now);
        cmd.ExecuteNonQuery();

        return sessionId;
    }

    public static InventorySession? GetActiveSession()
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand(
            "SELECT * FROM InventorySession WHERE Status = 'Active' ORDER BY StartedAt DESC LIMIT 1", conn);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read())
            return MapSession(rdr);
        return null;
    }

    public static InventorySession? GetSession(string sessionId)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand(
            "SELECT * FROM InventorySession WHERE SessionId = @sid", conn);
        cmd.Parameters.AddWithValue("@sid", sessionId);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read())
            return MapSession(rdr);
        return null;
    }

    public static Product? GetProductByBarcode(string barcode)
    {
        return ProductService.GetByBarcode(barcode);
    }

    public static int SubmitCount(string sessionId, int productId, string barcode, string productName,
        int systemQty, int actualQty, string countedBy)
    {
        var now = TimeHelper.Now.ToString("yyyy-MM-dd HH:mm:ss");

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();

        // Check if product already counted in this session
        using var checkCmd = new SQLiteCommand(
            "SELECT Id FROM InventoryCount WHERE SessionId = @sid AND ProductId = @pid", conn);
        checkCmd.Parameters.AddWithValue("@sid", sessionId);
        checkCmd.Parameters.AddWithValue("@pid", productId);
        var existingId = checkCmd.ExecuteScalar();

        if (existingId != null)
        {
            // Update existing count
            var id = Convert.ToInt32(existingId);
            using var updCmd = new SQLiteCommand(
                "UPDATE InventoryCount SET ActualQty = @aq, CreatedAt = @ca WHERE Id = @id", conn);
            updCmd.Parameters.AddWithValue("@aq", actualQty);
            updCmd.Parameters.AddWithValue("@ca", now);
            updCmd.Parameters.AddWithValue("@id", id);
            updCmd.ExecuteNonQuery();
            return id;
        }
        else
        {
            // Insert new count
            using var insCmd = new SQLiteCommand(@"
                INSERT INTO InventoryCount (SessionId, ProductId, Barcode, ProductName, SystemQty, ActualQty, CountedBy, CreatedAt)
                VALUES (@sid, @pid, @bc, @pn, @sq, @aq, @cb, @ca)", conn);
            insCmd.Parameters.AddWithValue("@sid", sessionId);
            insCmd.Parameters.AddWithValue("@pid", productId);
            insCmd.Parameters.AddWithValue("@bc", barcode);
            insCmd.Parameters.AddWithValue("@pn", productName);
            insCmd.Parameters.AddWithValue("@sq", systemQty);
            insCmd.Parameters.AddWithValue("@aq", actualQty);
            insCmd.Parameters.AddWithValue("@cb", countedBy);
            insCmd.Parameters.AddWithValue("@ca", now);
            insCmd.ExecuteNonQuery();

            using var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", conn);
            return Convert.ToInt32(idCmd.ExecuteScalar());
        }
    }

    public static List<InventoryCount> GetSessionCounts(string sessionId)
    {
        var list = new List<InventoryCount>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand(
            "SELECT * FROM InventoryCount WHERE SessionId = @sid ORDER BY CreatedAt ASC", conn);
        cmd.Parameters.AddWithValue("@sid", sessionId);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            list.Add(MapCount(rdr));
        return list;
    }

    public static List<InventorySession> GetAllSessions()
    {
        var list = new List<InventorySession>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand(
            "SELECT * FROM InventorySession ORDER BY StartedAt DESC", conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            var s = MapSession(rdr);
            PopulateSessionStats(conn, s);
            list.Add(s);
        }
        return list;
    }

    public static string? EndSession(string sessionId)
    {
        var session = GetSession(sessionId);
        if (session == null) return "Session not found.";

        var counts = GetSessionCounts(sessionId);
        if (counts.Count == 0) return "No items counted in this session.";

        var now = TimeHelper.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var userId = 0;
        var userName = "Inventory";

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            foreach (var c in counts)
            {
                if (c.Variance == 0 || c.Adjusted) continue;

                var stockBefore = c.SystemQty;
                var stockAfter = c.ActualQty;
                var qtyDelta = c.ActualQty - c.SystemQty;

                // Update product stock
                using var upd = new SQLiteCommand("UPDATE Products SET StockQty = @new WHERE Id = @id", conn);
                upd.Parameters.AddWithValue("@new", stockAfter);
                upd.Parameters.AddWithValue("@id", c.ProductId);
                upd.ExecuteNonQuery();

                // Insert stock trail
                var reference = $"Inventory Count - {sessionId}";
                using var ins = new SQLiteCommand(@"
                    INSERT INTO StockTrail (ProductId, ProductName, Barcode, QuantityAdded, StockBefore, StockAfter, Reference, UserId, UserName, InvoiceNo, CustomerName, CreatedAt)
                    VALUES (@pid, @pn, @bc, @qa, @sb, @sa, @ref, @uid, @un, '', '', @ca)", conn);
                ins.Parameters.AddWithValue("@pid", c.ProductId);
                ins.Parameters.AddWithValue("@pn", c.ProductName);
                ins.Parameters.AddWithValue("@bc", c.Barcode);
                ins.Parameters.AddWithValue("@qa", qtyDelta);
                ins.Parameters.AddWithValue("@sb", stockBefore);
                ins.Parameters.AddWithValue("@sa", stockAfter);
                ins.Parameters.AddWithValue("@ref", reference);
                ins.Parameters.AddWithValue("@uid", userId);
                ins.Parameters.AddWithValue("@un", userName);
                ins.Parameters.AddWithValue("@ca", now);
                ins.ExecuteNonQuery();

                // Mark count as adjusted
                using var adj = new SQLiteCommand("UPDATE InventoryCount SET Adjusted = 1 WHERE Id = @id", conn);
                adj.Parameters.AddWithValue("@id", c.Id);
                adj.ExecuteNonQuery();

                // Sync stock trail to cloud (fire-and-forget)
                using var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", conn);
                var trailId = Convert.ToInt32(idCmd.ExecuteScalar());
                var trail = new StockTrail
                {
                    Id = trailId,
                    ProductId = c.ProductId,
                    ProductName = c.ProductName,
                    Barcode = c.Barcode,
                    QuantityAdded = qtyDelta,
                    StockBefore = stockBefore,
                    StockAfter = stockAfter,
                    Reference = reference,
                    UserId = userId,
                    UserName = userName,
                    CreatedAt = now
                };
                var capturedTrailId = trailId;
                Task.Run(async () =>
                {
                    try
                    {
                        var ok = await SyncService.SyncStockTrail(trail);
                        using var updConn = DatabaseHelper.GetConnection();
                        updConn.Open();
                        using var upd2 = new SQLiteCommand("UPDATE StockTrail SET Synced = @synced WHERE Id = @id", updConn);
                        upd2.Parameters.AddWithValue("@synced", ok ? 1 : 0);
                        upd2.Parameters.AddWithValue("@id", capturedTrailId);
                        upd2.ExecuteNonQuery();
                    }
                    catch { }
                });

                // Sync product to cloud
                _ = SyncService.SyncProduct(ProductService.GetById(c.ProductId));
            }

            // End session
            using var endCmd = new SQLiteCommand(
                "UPDATE InventorySession SET Status = 'Completed', EndedAt = @ea WHERE SessionId = @sid", conn);
            endCmd.Parameters.AddWithValue("@ea", now);
            endCmd.Parameters.AddWithValue("@sid", sessionId);
            endCmd.ExecuteNonQuery();

            tx.Commit();
            return null;
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return ex.Message;
        }
    }

    public static string GetSessionReport(string sessionId)
    {
        var session = GetSession(sessionId);
        if (session == null) return "Session not found.";

        var counts = GetSessionCounts(sessionId);
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("=== INVENTORY COUNT REPORT ===");
        sb.AppendLine($"Session: {sessionId}");
        sb.AppendLine($"Counted By: {session.CountedBy}");
        sb.AppendLine($"Date: {session.StartedAt}");
        if (!string.IsNullOrEmpty(session.EndedAt))
            sb.AppendLine($"Ended: {session.EndedAt}");
        sb.AppendLine();

        sb.AppendLine($"{"Product",-30} {"System",8} {"Actual",8} {"Var",8}");
        sb.AppendLine(new string('-', 60));

        var itemsWithVar = 0;
        foreach (var c in counts)
        {
            var productName = c.ProductName.Length > 28 ? c.ProductName[..25] + "..." : c.ProductName;
            var variance = c.ActualQty - c.SystemQty;
            var varStr = variance >= 0 ? $"+{variance}" : variance.ToString();
            sb.AppendLine($"{productName,-30} {c.SystemQty,8} {c.ActualQty,8} {varStr,8}");
            if (variance != 0) itemsWithVar++;
        }

        sb.AppendLine();
        sb.AppendLine($"Total Items: {counts.Count}");
        sb.AppendLine($"Items with Variance: {itemsWithVar}");
        sb.AppendLine("===============================");

        return sb.ToString();
    }

    private static void PopulateSessionStats(SQLiteConnection conn, InventorySession session)
    {
        using var totalCmd = new SQLiteCommand(
            "SELECT COUNT(*), SUM(CASE WHEN (ActualQty - SystemQty) != 0 THEN 1 ELSE 0 END) FROM InventoryCount WHERE SessionId = @sid", conn);
        totalCmd.Parameters.AddWithValue("@sid", session.SessionId);
        using var rdr = totalCmd.ExecuteReader();
        if (rdr.Read())
        {
            session.TotalItems = Convert.ToInt32(rdr[0]);
            session.ItemsWithVariance = rdr[1] != DBNull.Value ? Convert.ToInt32(rdr[1]) : 0;
        }
    }

    private static InventorySession MapSession(SQLiteDataReader rdr)
    {
        return new InventorySession
        {
            SessionId = rdr["SessionId"].ToString() ?? "",
            CountedBy = rdr["CountedBy"].ToString() ?? "",
            StartedAt = rdr["StartedAt"].ToString() ?? "",
            EndedAt = rdr["EndedAt"]?.ToString(),
            Status = rdr["Status"].ToString() ?? ""
        };
    }

    private static InventoryCount MapCount(SQLiteDataReader rdr)
    {
        return new InventoryCount
        {
            Id = Convert.ToInt32(rdr["Id"]),
            SessionId = rdr["SessionId"].ToString() ?? "",
            ProductId = Convert.ToInt32(rdr["ProductId"]),
            Barcode = rdr["Barcode"].ToString() ?? "",
            ProductName = rdr["ProductName"].ToString() ?? "",
            SystemQty = Convert.ToInt32(rdr["SystemQty"]),
            ActualQty = Convert.ToInt32(rdr["ActualQty"]),
            CountedBy = rdr["CountedBy"].ToString() ?? "",
            CreatedAt = rdr["CreatedAt"].ToString() ?? "",
            Adjusted = Convert.ToInt32(rdr["Adjusted"]) == 1
        };
    }
}
