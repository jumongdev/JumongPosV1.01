using System.Data;
using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using Npgsql;

namespace JumongPosV1._01.Services;

public class StockService
{
    private static string StoreId => SyncService.StoreId;

    public static Product? GetByBarcode(string barcode)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT * FROM Products WHERE Barcode = @b AND IsActive = 1", conn);
        cmd.Parameters.AddWithValue("@b", barcode);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read()) return Map(rdr);
        return null;
    }

    public static List<Product> Search(string keyword)
    {
        var list = new List<Product>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT * FROM Products WHERE IsActive = 1 AND (Name LIKE @kw OR Barcode LIKE @kw) ORDER BY Name LIMIT 30", conn);
        cmd.Parameters.AddWithValue("@kw", $"%{keyword}%");
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(Map(rdr));
        return list;
    }

    public static string? ConfirmReceiving(List<(int ProductId, string ProductName, string Barcode, int StockBefore, int Qty)> items, int userId, string userName, string reference)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            foreach (var (productId, productName, barcode, stockBefore, qty) in items)
            {
                var effBefore = stockBefore;
                var stockAfter = effBefore + qty;
                var now = TimeHelper.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // STOCK LINK (2026-09-02): kung ang natanggap na product ay linked child, ang stock
                // ay pumapasok sa PARENT (qty × ratio) — ang trail ay nasa parent product.
                int stockTargetId = productId;
                var trailName = productName;
                var trailBarcode = barcode;
                var addQty = qty;
                using (var lk = new SQLiteCommand("SELECT StockParentId, StockLinkRatio FROM Products WHERE Id = @pid", conn))
                {
                    lk.Parameters.AddWithValue("@pid", productId);
                    using var lkr = lk.ExecuteReader();
                    if (lkr.Read())
                    {
                        var sp = Convert.ToInt32(lkr["StockParentId"]);
                        if (sp > 0)
                        {
                            stockTargetId = sp;
                            var r = Convert.ToInt32(lkr["StockLinkRatio"]);
                            addQty = qty * (r > 0 ? r : 1);
                        }
                    }
                }
                using (var gsr = new SQLiteCommand("SELECT StockQty, Name, COALESCE(Barcode,'') FROM Products WHERE Id = @pid", conn))
                {
                    gsr.Parameters.AddWithValue("@pid", stockTargetId);
                    using var gr = gsr.ExecuteReader();
                    gr.Read();
                    effBefore = Convert.ToInt32(gr["StockQty"]);
                    trailName = gr.GetString(1);
                    trailBarcode = gr.GetString(2);
                }
                stockAfter = effBefore + addQty;

                using var upd = new SQLiteCommand("UPDATE Products SET StockQty = @new WHERE Id = @id", conn);
                upd.Parameters.AddWithValue("@new", stockAfter);
                upd.Parameters.AddWithValue("@id", stockTargetId);
                upd.ExecuteNonQuery();

                using var ins = new SQLiteCommand(@"
                    INSERT INTO StockTrail (ProductId, ProductName, Barcode, QuantityAdded, StockBefore, StockAfter, Reference, UserId, UserName, InvoiceNo, CustomerName, CreatedAt)
                    VALUES (@pid, @pn, @bc, @qa, @sb, @sa, @ref, @uid, @un, '', '', @ca)", conn);
                ins.Parameters.AddWithValue("@pid", stockTargetId);
                ins.Parameters.AddWithValue("@pn", trailName);
                ins.Parameters.AddWithValue("@bc", trailBarcode);
                ins.Parameters.AddWithValue("@qa", addQty);
                ins.Parameters.AddWithValue("@sb", effBefore);
                ins.Parameters.AddWithValue("@sa", stockAfter);
                ins.Parameters.AddWithValue("@ref", reference);
                ins.Parameters.AddWithValue("@uid", userId);
                ins.Parameters.AddWithValue("@un", userName);
                ins.Parameters.AddWithValue("@ca", now);
                ins.ExecuteNonQuery();
                using var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", conn);
                var trailId = Convert.ToInt32(idCmd.ExecuteScalar());
                var trail = new StockTrail { Id = trailId, ProductId = stockTargetId, ProductName = trailName, Barcode = trailBarcode, QuantityAdded = addQty, StockBefore = effBefore, StockAfter = stockAfter, Reference = reference, UserId = userId, UserName = userName, CreatedAt = now };
                var capturedTrailId = trailId;
                Task.Run(async () =>
                {
                    try
                    {
                        var ok = await SyncService.SyncStockTrail(trail);
                        using var updConn = DatabaseHelper.GetConnection();
                        updConn.Open();
                        using var upd = new SQLiteCommand("UPDATE StockTrail SET Synced = @synced WHERE Id = @id", updConn);
                        upd.Parameters.AddWithValue("@synced", ok ? 1 : 0);
                        upd.Parameters.AddWithValue("@id", capturedTrailId);
                        upd.ExecuteNonQuery();
                    }
                    catch (Exception ex) { ErrorLogger.Log("StockService.ConfirmReceiving(syncUpdate)", ex); }
                });
                _ = SyncService.SyncProduct(ProductService.GetById(productId));

                // Also update PostgreSQL stock
                if (CloudDatabaseHelper.IsConfigured)
                {
                    try
                    {
                        using var pgConn = CloudDatabaseHelper.GetConnection()!;
                        pgConn.Open();
                        using var pgCmd = new NpgsqlCommand("UPDATE products SET stock_qty = @new WHERE pos_id = @id AND store_id = @sid", pgConn);
                        pgCmd.Parameters.AddWithValue("new", stockAfter);
                        pgCmd.Parameters.AddWithValue("id", productId);
                        pgCmd.Parameters.AddWithValue("sid", StoreId);
                        pgCmd.ExecuteNonQuery();
                    }
                    catch (Exception ex) { ErrorLogger.Log("StockService.ConfirmReceiving(updatePg)", ex); }
                }
            }

            tx.Commit();
            return null;
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return ex.Message;
        }
    }

    public static List<StockTrail> GetTrail(int? productId = null, int limit = 100)
    {
        var list = new List<StockTrail>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = productId.HasValue
            ? "SELECT * FROM StockTrail WHERE ProductId = @pid ORDER BY Id DESC LIMIT @lim"
            : "SELECT * FROM StockTrail ORDER BY Id DESC LIMIT @lim";
        using var cmd = new SQLiteCommand(sql, conn);
        if (productId.HasValue) cmd.Parameters.AddWithValue("@pid", productId.Value);
        cmd.Parameters.AddWithValue("@lim", limit);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(MapTrail(rdr));
        }
        return list;
    }

    public static List<StockTrail> GetTrailByDateRange(DateTime from, DateTime to, int limit = 500)
    {
        var list = new List<StockTrail>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM StockTrail WHERE CreatedAt >= @from AND CreatedAt < @to AND QuantityAdded > 0 ORDER BY Id DESC LIMIT @lim";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@lim", limit);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(MapTrail(rdr));
        }
        return list;
    }

    private static StockTrail MapTrail(SQLiteDataReader rdr)
    {
        return new StockTrail
        {
            Id = Convert.ToInt32(rdr["Id"]),
            ProductId = Convert.ToInt32(rdr["ProductId"]),
            ProductName = rdr["ProductName"].ToString() ?? "",
            Barcode = rdr["Barcode"].ToString() ?? "",
            QuantityAdded = Convert.ToDecimal(rdr["QuantityAdded"]),
            StockBefore = Convert.ToInt32(rdr["StockBefore"]),
            StockAfter = Convert.ToInt32(rdr["StockAfter"]),
            Reference = rdr["Reference"].ToString() ?? "",
            InvoiceNo = rdr["InvoiceNo"].ToString() ?? "",
            CustomerName = rdr["CustomerName"].ToString() ?? "",
            UserId = Convert.ToInt32(rdr["UserId"]),
            UserName = rdr["UserName"].ToString() ?? "",
            CreatedAt = rdr["CreatedAt"].ToString() ?? ""
        };
    }

    private static Product Map(SQLiteDataReader rdr)
    {
        return new Product
        {
            Id = Convert.ToInt32(rdr["Id"]),
            Name = rdr["Name"].ToString() ?? "",
            Barcode = rdr["Barcode"].ToString() ?? "",
            Category = rdr["Category"].ToString() ?? "",
            Price = Convert.ToDecimal(rdr["Price"]),
            Cost = Convert.ToDecimal(rdr["Cost"]),
            StockQty = Convert.ToInt32(rdr["StockQty"]),
            IsActive = Convert.ToBoolean(rdr["IsActive"]),
            CreatedAt = DateTime.SpecifyKind(DateTime.Parse(rdr["CreatedAt"].ToString()!), DateTimeKind.Local),
            StockParentId = rdr["StockParentId"] != DBNull.Value ? Convert.ToInt32(rdr["StockParentId"]) : 0,
            StockLinkRatio = rdr["StockLinkRatio"] != DBNull.Value ? Convert.ToInt32(rdr["StockLinkRatio"]) : 1
        };
    }
}