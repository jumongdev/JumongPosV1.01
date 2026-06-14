using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public class SaleService
{
    public static string GenerateInvoiceNo()
    {
        var date = DateTime.Now.ToString("yyyyMMdd");
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();

        var counterKey = $"InvCounter_{date}";
        using var getCmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = @key", conn);
        getCmd.Parameters.AddWithValue("@key", counterKey);
        var val = getCmd.ExecuteScalar()?.ToString();
        var count = (int.TryParse(val, out var c) ? c : 0) + 1;

        using var setCmd = new SQLiteCommand("INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@key, @val)", conn);
        setCmd.Parameters.AddWithValue("@key", counterKey);
        setCmd.Parameters.AddWithValue("@val", count.ToString());
        setCmd.ExecuteNonQuery();

        return $"INV-{SyncService.StoreId[^4..]}-{date}-{count:D4}";
    }

    public static int SaveSale(Sale sale)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            var sql = @"INSERT INTO Sales (InvoiceNo, SaleDate, SubTotal, Discount, Tax, GrandTotal, 
                        AmountPaid, Change, PaymentMethod, ReferenceNo, CustomerId, UserId, OrderType, CashPaid, EwPaid)
                        VALUES (@inv, @dt, @sub, @disc, @tax, @total, @paid, @chg, @pm, @ref, @cid, @uid, @otype, @cp, @ep);
                        SELECT last_insert_rowid();";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@inv", sale.InvoiceNo);
            cmd.Parameters.AddWithValue("@dt", sale.SaleDate.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@sub", sale.SubTotal);
            cmd.Parameters.AddWithValue("@disc", sale.Discount);
            cmd.Parameters.AddWithValue("@tax", sale.Tax);
            cmd.Parameters.AddWithValue("@total", sale.GrandTotal);
            cmd.Parameters.AddWithValue("@paid", sale.AmountPaid);
            cmd.Parameters.AddWithValue("@chg", sale.Change);
            cmd.Parameters.AddWithValue("@pm", sale.PaymentMethod);
            cmd.Parameters.AddWithValue("@ref", sale.ReferenceNo);
            cmd.Parameters.AddWithValue("@cid", (object?)sale.CustomerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@uid", (object?)sale.UserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@otype", sale.OrderType);
            cmd.Parameters.AddWithValue("@cp", sale.CashPaid);
            cmd.Parameters.AddWithValue("@ep", sale.EwPaid);

            var saleId = Convert.ToInt32(cmd.ExecuteScalar());

            foreach (var item in sale.Items)
            {
                // Determine unit cost: use ProductUnit cost if unit has its own, else product base cost
                var unitCost = item.UnitCost;
                if (unitCost == 0)
                {
                    using var getCost = new SQLiteCommand("SELECT COALESCE((SELECT pu.Cost FROM ProductUnits pu WHERE pu.ProductId = @pid2 AND pu.UnitName = @un2 LIMIT 1), p.Cost) FROM Products p WHERE p.Id = @pid3", conn);
                    getCost.Parameters.AddWithValue("@pid2", item.ProductId);
                    getCost.Parameters.AddWithValue("@un2", item.UnitName);
                    getCost.Parameters.AddWithValue("@pid3", item.ProductId);
                    var result = getCost.ExecuteScalar();
                    unitCost = result != null ? Convert.ToDecimal(result) : 0;
                }

                var itemSql = @"INSERT INTO SaleItems (SaleId, ProductId, ProductName, Barcode, Price, Quantity, TotalPrice, UnitName, QtyPerUnit, UnitCost)
                                VALUES (@sid, @pid, @pn, @bc, @pr, @qty, @tot, @un, @qpu, @uc)";
                using var itemCmd = new SQLiteCommand(itemSql, conn);
                itemCmd.Parameters.AddWithValue("@sid", saleId);
                itemCmd.Parameters.AddWithValue("@pid", item.ProductId);
                itemCmd.Parameters.AddWithValue("@pn", item.ProductName);
                itemCmd.Parameters.AddWithValue("@bc", item.Barcode);
                itemCmd.Parameters.AddWithValue("@pr", item.Price);
                itemCmd.Parameters.AddWithValue("@qty", item.Quantity);
                itemCmd.Parameters.AddWithValue("@tot", item.TotalPrice);
                itemCmd.Parameters.AddWithValue("@un", item.UnitName);
                itemCmd.Parameters.AddWithValue("@qpu", item.QtyPerUnit);
                itemCmd.Parameters.AddWithValue("@uc", unitCost);
                itemCmd.ExecuteNonQuery();
                using var itemIdCmd = new SQLiteCommand("SELECT last_insert_rowid()", conn);
                item.Id = Convert.ToInt32(itemIdCmd.ExecuteScalar());

                var deductQty = item.Quantity * item.QtyPerUnit;
                var getStock = new SQLiteCommand("SELECT StockQty FROM Products WHERE Id = @pid", conn);
                getStock.Parameters.AddWithValue("@pid", item.ProductId);
                var stockBefore = Convert.ToInt32(getStock.ExecuteScalar());
                var stockAfter = stockBefore - deductQty;

                var updSql = "UPDATE Products SET StockQty = StockQty - @qty WHERE Id = @pid";
                using var updCmd = new SQLiteCommand(updSql, conn);
                updCmd.Parameters.AddWithValue("@qty", deductQty);
                updCmd.Parameters.AddWithValue("@pid", item.ProductId);
                updCmd.ExecuteNonQuery();

                using var trail = new SQLiteCommand(
                    "INSERT INTO StockTrail (ProductId, ProductName, Barcode, QuantityAdded, StockBefore, StockAfter, Reference, UserId, UserName, InvoiceNo, CustomerName) " +
                    "VALUES (@pid, @pn, @bc, @qa, @sb, @sa, @ref, @uid, @un, @inv, @cust)", conn);
                trail.Parameters.AddWithValue("@pid", item.ProductId);
                trail.Parameters.AddWithValue("@pn", item.ProductName);
                trail.Parameters.AddWithValue("@bc", item.Barcode ?? "");
                trail.Parameters.AddWithValue("@qa", -deductQty);
                trail.Parameters.AddWithValue("@sb", stockBefore);
                trail.Parameters.AddWithValue("@sa", stockAfter);
                trail.Parameters.AddWithValue("@ref", sale.InvoiceNo);
                trail.Parameters.AddWithValue("@uid", sale.UserId ?? 0);
                trail.Parameters.AddWithValue("@un", sale.CashierName);
                trail.Parameters.AddWithValue("@inv", sale.InvoiceNo);
                trail.Parameters.AddWithValue("@cust", sale.CustomerName ?? "Walk-in");
                trail.ExecuteNonQuery();
            }

            trans.Commit();
            sale.Id = saleId;

            foreach (var item in sale.Items)
                _ = SyncService.SyncProduct(ProductService.GetById(item.ProductId));

            _ = SyncService.SyncSale(sale, sale.Items);
            return saleId;
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    public static Sale? GetByInvoiceNo(string invoiceNo)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = @"SELECT s.*, c.Name AS CustomerName FROM Sales s 
                    LEFT JOIN Customers c ON s.CustomerId = c.Id 
                    WHERE s.InvoiceNo = @inv";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@inv", invoiceNo);
        using var rdr = cmd.ExecuteReader();
        if (!rdr.Read()) return null;

        var sale = new Sale
        {
            Id = Convert.ToInt32(rdr["Id"]),
            InvoiceNo = rdr["InvoiceNo"].ToString() ?? "",
            SaleDate = DateTime.Parse(rdr["SaleDate"].ToString()!),
            SubTotal = Convert.ToDecimal(rdr["SubTotal"]),
            Discount = Convert.ToDecimal(rdr["Discount"]),
            Tax = Convert.ToDecimal(rdr["Tax"]),
            GrandTotal = Convert.ToDecimal(rdr["GrandTotal"]),
            AmountPaid = Convert.ToDecimal(rdr["AmountPaid"]),
            Change = Convert.ToDecimal(rdr["Change"]),
            PaymentMethod = rdr["PaymentMethod"].ToString() ?? "",
            ReferenceNo = rdr["ReferenceNo"].ToString() ?? "",
            OrderType = rdr["OrderType"].ToString() ?? "Walk-in",
            CustomerId = rdr["CustomerId"] == DBNull.Value ? null : Convert.ToInt32(rdr["CustomerId"]),
            UserId = rdr["UserId"] == DBNull.Value ? null : Convert.ToInt32(rdr["UserId"]),
            CustomerName = rdr["CustomerName"]?.ToString() ?? "",
            CashPaid = rdr["CashPaid"] != DBNull.Value ? Convert.ToDecimal(rdr["CashPaid"]) : 0,
            EwPaid = rdr["EwPaid"] != DBNull.Value ? Convert.ToDecimal(rdr["EwPaid"]) : 0,
            IsVoided = Convert.ToBoolean(rdr["IsVoided"]),
            VoidedAt = rdr["VoidedAt"]?.ToString()
        };

        var itemsSql = "SELECT * FROM SaleItems WHERE SaleId = @sid";
        using var itemsCmd = new SQLiteCommand(itemsSql, conn);
        itemsCmd.Parameters.AddWithValue("@sid", sale.Id);
        using var itemsRdr = itemsCmd.ExecuteReader();
        while (itemsRdr.Read())
        {
            sale.Items.Add(new SaleItem
            {
                Id = Convert.ToInt32(itemsRdr["Id"]),
                SaleId = Convert.ToInt32(itemsRdr["SaleId"]),
                ProductId = Convert.ToInt32(itemsRdr["ProductId"]),
                ProductName = itemsRdr["ProductName"].ToString() ?? "",
                Barcode = itemsRdr["Barcode"].ToString() ?? "",
                Price = Convert.ToDecimal(itemsRdr["Price"]),
                Quantity = Convert.ToInt32(itemsRdr["Quantity"]),
                TotalPrice = Convert.ToDecimal(itemsRdr["TotalPrice"]),
                UnitName = itemsRdr["UnitName"].ToString() ?? "",
                QtyPerUnit = Convert.ToInt32(itemsRdr["QtyPerUnit"])
            });
        }
        return sale;
    }

    public static List<Sale> GetSales(DateTime? from = null, DateTime? to = null, string? invoiceNo = null, bool? synced = null)
    {
        var list = new List<Sale>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = @"SELECT s.*, COALESCE(SUM(CASE WHEN si.IsVoided = 0 THEN si.TotalPrice ELSE 0 END), 0) AS EffectiveTotal
                     FROM Sales s
                     LEFT JOIN SaleItems si ON si.SaleId = s.Id";
        var conditions = new List<string>();
        if (from.HasValue) conditions.Add("s.SaleDate >= @from");
        if (to.HasValue) conditions.Add("s.SaleDate <= @to");
        if (!string.IsNullOrEmpty(invoiceNo)) conditions.Add("s.InvoiceNo LIKE @inv");
        if (synced.HasValue) conditions.Add("s.Synced = @synced");
        if (conditions.Count > 0)
            sql += " WHERE " + string.Join(" AND ", conditions);
        sql += " GROUP BY s.Id ORDER BY s.SaleDate DESC";

        using var cmd = new SQLiteCommand(sql, conn);
        if (from.HasValue) cmd.Parameters.AddWithValue("@from", from.Value.ToString("yyyy-MM-dd 00:00:00"));
        if (to.HasValue) cmd.Parameters.AddWithValue("@to", to.Value.ToString("yyyy-MM-dd 23:59:59"));
        if (!string.IsNullOrEmpty(invoiceNo)) cmd.Parameters.AddWithValue("@inv", $"%{invoiceNo}%");
        if (synced.HasValue) cmd.Parameters.AddWithValue("@synced", synced.Value ? 1 : 0);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            list.Add(MapSale(rdr));
        return list;
    }

    public static List<Sale> GetSalesByCustomer(int customerId)
    {
        var list = new List<Sale>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = @"SELECT s.*, COALESCE(SUM(CASE WHEN si.IsVoided = 0 THEN si.TotalPrice ELSE 0 END), 0) AS EffectiveTotal
                     FROM Sales s
                     LEFT JOIN SaleItems si ON si.SaleId = s.Id
                     WHERE s.CustomerId = @cid
                     GROUP BY s.Id
                     ORDER BY s.SaleDate DESC";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cid", customerId);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            list.Add(MapSale(rdr));
        return list;
    }

    public static Sale? GetById(int id)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = @"SELECT s.*, COALESCE(SUM(CASE WHEN si.IsVoided = 0 THEN si.TotalPrice ELSE 0 END), 0) AS EffectiveTotal
                     FROM Sales s
                     LEFT JOIN SaleItems si ON si.SaleId = s.Id
                     WHERE s.Id = @id
                     GROUP BY s.Id";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read())
        {
            var sale = MapSale(rdr);
            sale.Items = GetItems(id, conn);
            return sale;
        }
        return null;
    }

    public static List<SaleItem> GetSaleItems(int saleId)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        return GetItems(saleId, conn);
    }

    private static List<SaleItem> GetItems(int saleId, SQLiteConnection conn)
    {
        var list = new List<SaleItem>();
        var sql = "SELECT * FROM SaleItems WHERE SaleId = @sid";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sid", saleId);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new SaleItem
            {
                Id = Convert.ToInt32(rdr["Id"]),
                SaleId = Convert.ToInt32(rdr["SaleId"]),
                ProductId = Convert.ToInt32(rdr["ProductId"]),
                ProductName = rdr["ProductName"].ToString() ?? "",
                Barcode = rdr["Barcode"].ToString() ?? "",
                Price = Convert.ToDecimal(rdr["Price"]),
                Quantity = Convert.ToInt32(rdr["Quantity"]),
                TotalPrice = Convert.ToDecimal(rdr["TotalPrice"]),
                UnitName = rdr["UnitName"].ToString() ?? "",
                QtyPerUnit = rdr["QtyPerUnit"] != DBNull.Value ? Convert.ToInt32(rdr["QtyPerUnit"]) : 1,
                UnitCost = rdr["UnitCost"] != DBNull.Value ? Convert.ToDecimal(rdr["UnitCost"]) : 0,
                IsVoided = rdr["IsVoided"] != DBNull.Value && Convert.ToBoolean(rdr["IsVoided"])
            });
        }
        return list;
    }

    private static Sale MapSale(SQLiteDataReader rdr)
    {
        return new Sale
        {
            Id = Convert.ToInt32(rdr["Id"]),
            InvoiceNo = rdr["InvoiceNo"].ToString() ?? "",
            SaleDate = DateTime.Parse(rdr["SaleDate"].ToString()!),
            SubTotal = Convert.ToDecimal(rdr["SubTotal"]),
            Discount = Convert.ToDecimal(rdr["Discount"]),
            Tax = Convert.ToDecimal(rdr["Tax"]),
            GrandTotal = Convert.ToDecimal(rdr["GrandTotal"]),
            AmountPaid = Convert.ToDecimal(rdr["AmountPaid"]),
            Change = Convert.ToDecimal(rdr["Change"]),
            PaymentMethod = rdr["PaymentMethod"].ToString() ?? "Cash",
            CustomerId = rdr["CustomerId"] != DBNull.Value ? Convert.ToInt32(rdr["CustomerId"]) : null,
            UserId = rdr["UserId"] != DBNull.Value ? Convert.ToInt32(rdr["UserId"]) : null,
            OrderType = rdr["OrderType"]?.ToString() ?? "Walk-in",
            ReferenceNo = rdr["ReferenceNo"]?.ToString() ?? "",
            IsVoided = rdr["IsVoided"] != DBNull.Value && Convert.ToBoolean(rdr["IsVoided"]),
            VoidedAt = rdr["VoidedAt"]?.ToString(),
            EffectiveTotal = Convert.ToDecimal(rdr["EffectiveTotal"]),
            Synced = rdr["Synced"] != DBNull.Value && Convert.ToInt32(rdr["Synced"]) == 1
        };
    }

    public static void VoidSale(int saleId, string reason, int voidedByUserId, string voidedByUserName)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            var sale = GetById(saleId);
            if (sale == null || sale.IsVoided) return;

            foreach (var item in sale.Items.Where(x => !x.IsVoided))
            {
                var deductQty = item.Quantity * item.QtyPerUnit;

                var getStock = new SQLiteCommand("SELECT StockQty FROM Products WHERE Id = @pid", conn);
                getStock.Parameters.AddWithValue("@pid", item.ProductId);
                var stockBefore = Convert.ToInt32(getStock.ExecuteScalar());

                var upd = new SQLiteCommand("UPDATE Products SET StockQty = StockQty + @qty WHERE Id = @pid", conn);
                upd.Parameters.AddWithValue("@qty", deductQty);
                upd.Parameters.AddWithValue("@pid", item.ProductId);
                upd.ExecuteNonQuery();

                var voidItem = new SQLiteCommand("UPDATE SaleItems SET IsVoided = 1 WHERE Id = @id", conn);
                voidItem.Parameters.AddWithValue("@id", item.Id);
                voidItem.ExecuteNonQuery();

                var log = new SQLiteCommand(
                    "INSERT INTO VoidLog (SaleId, SaleItemId, Action, Reason, InvoiceNo, ProductName, Quantity, Amount, UserId, UserName) " +
                    "VALUES (@sid, @siid, 'VoidItem', @r, @inv, @pn, @qty, @amt, @uid, @uname)", conn);
                log.Parameters.AddWithValue("@sid", saleId);
                log.Parameters.AddWithValue("@siid", item.Id);
                log.Parameters.AddWithValue("@r", reason);
                log.Parameters.AddWithValue("@inv", sale.InvoiceNo);
                log.Parameters.AddWithValue("@pn", item.ProductName);
                log.Parameters.AddWithValue("@qty", item.Quantity);
                log.Parameters.AddWithValue("@amt", item.TotalPrice);
                log.Parameters.AddWithValue("@uid", voidedByUserId);
                log.Parameters.AddWithValue("@uname", voidedByUserName);
                log.ExecuteNonQuery();

                using var trail = new SQLiteCommand(
                    "INSERT INTO StockTrail (ProductId, ProductName, Barcode, QuantityAdded, StockBefore, StockAfter, Reference, UserId, UserName, InvoiceNo, CustomerName) " +
                    "VALUES (@pid, @pn, @bc, @qa, @sb, @sa, @ref, @uid, @un, @inv, @cust)", conn);
                trail.Parameters.AddWithValue("@pid", item.ProductId);
                trail.Parameters.AddWithValue("@pn", item.ProductName);
                trail.Parameters.AddWithValue("@bc", item.Barcode ?? "");
                trail.Parameters.AddWithValue("@qa", deductQty);
                trail.Parameters.AddWithValue("@sb", stockBefore);
                trail.Parameters.AddWithValue("@sa", stockBefore + deductQty);
                trail.Parameters.AddWithValue("@ref", $"{sale.InvoiceNo} - void ({reason})");
                trail.Parameters.AddWithValue("@uid", sale.UserId ?? 0);
                trail.Parameters.AddWithValue("@un", voidedByUserName);
                trail.Parameters.AddWithValue("@inv", sale.InvoiceNo);
                trail.Parameters.AddWithValue("@cust", "");
                trail.ExecuteNonQuery();
            }

            var voidSale = new SQLiteCommand("UPDATE Sales SET IsVoided = 1, VoidedAt = datetime('now','localtime') WHERE Id = @id", conn);
            voidSale.Parameters.AddWithValue("@id", saleId);
            voidSale.ExecuteNonQuery();

            var logSale = new SQLiteCommand(
                "INSERT INTO VoidLog (SaleId, Action, Reason, InvoiceNo, Amount, UserId, UserName) VALUES (@sid, 'VoidSale', @r, @inv, 0, @uid, @uname)", conn);
            logSale.Parameters.AddWithValue("@sid", saleId);
            logSale.Parameters.AddWithValue("@r", reason);
            logSale.Parameters.AddWithValue("@inv", sale.InvoiceNo);
            logSale.Parameters.AddWithValue("@uid", voidedByUserId);
            logSale.Parameters.AddWithValue("@uname", voidedByUserName);
            logSale.ExecuteNonQuery();

            // Reverse credit balance if this was a credit sale
            if (sale.PaymentMethod == "Credit" && sale.CustomerId.HasValue)
            {
                var voidAmount = sale.Items.Where(x => !x.IsVoided).Sum(x => x.TotalPrice);
                if (voidAmount > 0)
                {
                    var getBal = new SQLiteCommand("SELECT CreditBalance FROM Customers WHERE Id = @cid", conn);
                    getBal.Parameters.AddWithValue("@cid", sale.CustomerId.Value);
                    var curBal = Convert.ToDecimal(getBal.ExecuteScalar());
                    var newBal = curBal - voidAmount;

                    var insCt = new SQLiteCommand(
                        "INSERT INTO CreditTransactions (CustomerId, SaleId, Type, Description, Debit, Credit, Balance, UserId, UserName) " +
                        "VALUES (@cid, @sid, 'Void', @desc, 0, @amt, @bal, @uid, @uname)", conn);
                    insCt.Parameters.AddWithValue("@cid", sale.CustomerId.Value);
                    insCt.Parameters.AddWithValue("@sid", saleId);
                    insCt.Parameters.AddWithValue("@desc", $"Void receipt {sale.InvoiceNo} - {reason}");
                    insCt.Parameters.AddWithValue("@amt", voidAmount);
                    insCt.Parameters.AddWithValue("@bal", newBal);
                    insCt.Parameters.AddWithValue("@uid", voidedByUserId);
                    insCt.Parameters.AddWithValue("@uname", voidedByUserName);
                    insCt.ExecuteNonQuery();

                    var updCust = new SQLiteCommand("UPDATE Customers SET CreditBalance = @bal WHERE Id = @cid", conn);
                    updCust.Parameters.AddWithValue("@bal", newBal);
                    updCust.Parameters.AddWithValue("@cid", sale.CustomerId.Value);
                    updCust.ExecuteNonQuery();
                }
            }

            trans.Commit();
            var updatedSale = GetById(saleId);
            if (updatedSale != null)
            {
                _ = SyncService.SyncSale(updatedSale, updatedSale.Items);
                try
                {
                    using var sc = DatabaseHelper.GetConnection();
                    sc.Open();

                    var voidLogs = new SQLiteCommand("SELECT * FROM VoidLog WHERE SaleId = @sid ORDER BY Id", sc);
                    voidLogs.Parameters.AddWithValue("@sid", saleId);
                    using var vlRdr = voidLogs.ExecuteReader();
                    while (vlRdr.Read())
                    {
                        _ = SyncService.SyncVoidLog(new VoidLog
                        {
                            Id = Convert.ToInt32(vlRdr["Id"]),
                            SaleId = Convert.ToInt32(vlRdr["SaleId"]),
                            SaleItemId = vlRdr["SaleItemId"] != DBNull.Value ? Convert.ToInt32(vlRdr["SaleItemId"]) : null,
                            Action = vlRdr["Action"].ToString() ?? "",
                            Reason = vlRdr["Reason"].ToString() ?? "",
                            InvoiceNo = vlRdr["InvoiceNo"].ToString() ?? "",
                            ProductName = (vlRdr["ProductName"]?.ToString()) ?? "",
                            Quantity = Convert.ToInt32(vlRdr["Quantity"]),
                            Amount = Convert.ToDecimal(vlRdr["Amount"]),
                            UserId = Convert.ToInt32(vlRdr["UserId"]),
                            UserName = vlRdr["UserName"]?.ToString() ?? "",
                            CreatedAt = vlRdr["CreatedAt"]?.ToString() ?? ""
                        });
                    }

                    var trails = new SQLiteCommand("SELECT * FROM StockTrail WHERE InvoiceNo = @inv AND QuantityAdded > 0 ORDER BY Id", sc);
                    trails.Parameters.AddWithValue("@inv", updatedSale.InvoiceNo);
                    using var trRdr = trails.ExecuteReader();
                    while (trRdr.Read())
                    {
                        _ = SyncService.SyncStockTrail(new StockTrail
                        {
                            Id = Convert.ToInt32(trRdr["Id"]),
                            ProductId = Convert.ToInt32(trRdr["ProductId"]),
                            ProductName = trRdr["ProductName"]?.ToString() ?? "",
                            Barcode = trRdr["Barcode"]?.ToString() ?? "",
                            QuantityAdded = Convert.ToDecimal(trRdr["QuantityAdded"]),
                            StockBefore = Convert.ToInt32(trRdr["StockBefore"]),
                            StockAfter = Convert.ToInt32(trRdr["StockAfter"]),
                            Reference = trRdr["Reference"]?.ToString() ?? "",
                            InvoiceNo = trRdr["InvoiceNo"]?.ToString() ?? "",
                            CustomerName = trRdr["CustomerName"]?.ToString() ?? "",
                            UserId = Convert.ToInt32(trRdr["UserId"]),
                            UserName = trRdr["UserName"]?.ToString() ?? "",
                            CreatedAt = trRdr["CreatedAt"]?.ToString() ?? ""
                        });
                    }

                    var ctCmd = new SQLiteCommand("SELECT * FROM CreditTransactions WHERE SaleId = @sid ORDER BY Id", sc);
                    ctCmd.Parameters.AddWithValue("@sid", saleId);
                    using var ctRdr = ctCmd.ExecuteReader();
                    while (ctRdr.Read())
                    {
                        _ = SyncService.SyncCreditTransaction(new CreditTransaction
                        {
                            Id = Convert.ToInt32(ctRdr["Id"]),
                            CustomerId = Convert.ToInt32(ctRdr["CustomerId"]),
                            SaleId = ctRdr["SaleId"] != DBNull.Value ? Convert.ToInt32(ctRdr["SaleId"]) : null,
                            Type = ctRdr["Type"]?.ToString() ?? "",
                            Description = ctRdr["Description"]?.ToString() ?? "",
                            Debit = Convert.ToDecimal(ctRdr["Debit"]),
                            Credit = Convert.ToDecimal(ctRdr["Credit"]),
                            Balance = Convert.ToDecimal(ctRdr["Balance"]),
                            UserId = Convert.ToInt32(ctRdr["UserId"]),
                            UserName = ctRdr["UserName"]?.ToString() ?? "",
                            CreatedAt = ctRdr["CreatedAt"]?.ToString() ?? ""
                        });
                    }
                }
                catch { }
            }
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    public static void VoidItem(int itemId, string reason, int voidedByUserId, string voidedByUserName)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            var sql = "SELECT si.*, s.InvoiceNo, s.PaymentMethod, s.CustomerId, s.UserId, s.IsVoided AS SaleIsVoided, si.IsVoided AS ItemIsVoided FROM SaleItems si JOIN Sales s ON si.SaleId = s.Id WHERE si.Id = @id";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", itemId);
            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read()) return;
            var saleVoided = Convert.ToBoolean(rdr["SaleIsVoided"]);
            var itemVoided = Convert.ToBoolean(rdr["ItemIsVoided"]);
            if (saleVoided || itemVoided) return;
            var saleId = Convert.ToInt32(rdr["SaleId"]);
            var invoiceNo = rdr["InvoiceNo"].ToString() ?? "";
            var paymentMethod = rdr["PaymentMethod"].ToString() ?? "";
            var customerId = rdr["CustomerId"] != DBNull.Value ? Convert.ToInt32(rdr["CustomerId"]) : (int?)null;
            var userId = rdr["UserId"] != DBNull.Value ? Convert.ToInt32(rdr["UserId"]) : 0;
            var productId = Convert.ToInt32(rdr["ProductId"]);
            var productName = rdr["ProductName"].ToString() ?? "";
            var barcode = rdr["Barcode"].ToString() ?? "";
            var qty = Convert.ToInt32(rdr["Quantity"]);
            var qpu = rdr["QtyPerUnit"] != DBNull.Value ? Convert.ToInt32(rdr["QtyPerUnit"]) : 1;
            var restockQty = qty * qpu;
            var total = Convert.ToDecimal(rdr["TotalPrice"]);
            rdr.Close();

            var getStock = new SQLiteCommand("SELECT StockQty FROM Products WHERE Id = @pid", conn);
            getStock.Parameters.AddWithValue("@pid", productId);
            var stockBefore = Convert.ToInt32(getStock.ExecuteScalar());

            var upd = new SQLiteCommand("UPDATE SaleItems SET IsVoided = 1 WHERE Id = @id", conn);
            upd.Parameters.AddWithValue("@id", itemId);
            upd.ExecuteNonQuery();

            var restock = new SQLiteCommand("UPDATE Products SET StockQty = StockQty + @qty WHERE Id = @pid", conn);
            restock.Parameters.AddWithValue("@qty", restockQty);
            restock.Parameters.AddWithValue("@pid", productId);
            restock.ExecuteNonQuery();

            using var trail = new SQLiteCommand(
                "INSERT INTO StockTrail (ProductId, ProductName, Barcode, QuantityAdded, StockBefore, StockAfter, Reference, UserId, UserName, InvoiceNo, CustomerName) " +
                "VALUES (@pid, @pn, @bc, @qa, @sb, @sa, @ref, @uid, @un, @inv, @cust)", conn);
            trail.Parameters.AddWithValue("@pid", productId);
            trail.Parameters.AddWithValue("@pn", productName);
            trail.Parameters.AddWithValue("@bc", barcode);
            trail.Parameters.AddWithValue("@qa", restockQty);
            trail.Parameters.AddWithValue("@sb", stockBefore);
            trail.Parameters.AddWithValue("@sa", stockBefore + restockQty);
            trail.Parameters.AddWithValue("@ref", $"{invoiceNo} - void ({reason})");
            trail.Parameters.AddWithValue("@uid", userId);
            trail.Parameters.AddWithValue("@un", voidedByUserName);
            trail.Parameters.AddWithValue("@inv", invoiceNo);
            trail.Parameters.AddWithValue("@cust", "");
            trail.ExecuteNonQuery();

            var log = new SQLiteCommand(
                "INSERT INTO VoidLog (SaleId, SaleItemId, Action, Reason, InvoiceNo, ProductName, Quantity, Amount, UserId, UserName) " +
                "VALUES (@sid, @siid, 'VoidItem', @r, @inv, @pn, @qty, @amt, @uid, @uname)", conn);
            log.Parameters.AddWithValue("@sid", saleId);
            log.Parameters.AddWithValue("@siid", itemId);
            log.Parameters.AddWithValue("@r", reason);
            log.Parameters.AddWithValue("@inv", invoiceNo);
            log.Parameters.AddWithValue("@pn", productName);
            log.Parameters.AddWithValue("@qty", qty);
            log.Parameters.AddWithValue("@amt", total);
            log.Parameters.AddWithValue("@uid", voidedByUserId);
            log.Parameters.AddWithValue("@uname", voidedByUserName);
            log.ExecuteNonQuery();

            // Reverse credit balance if this was a credit sale
            if (paymentMethod == "Credit" && customerId.HasValue)
            {
                var getBal = new SQLiteCommand("SELECT CreditBalance FROM Customers WHERE Id = @cid", conn);
                getBal.Parameters.AddWithValue("@cid", customerId.Value);
                var curBal = Convert.ToDecimal(getBal.ExecuteScalar());
                var newBal = curBal - total;

                var insCt = new SQLiteCommand(
                    "INSERT INTO CreditTransactions (CustomerId, SaleId, Type, Description, Debit, Credit, Balance, UserId, UserName) " +
                    "VALUES (@cid, @sid, 'Void', @desc, 0, @amt, @bal, @uid, @uname)", conn);
                insCt.Parameters.AddWithValue("@cid", customerId.Value);
                insCt.Parameters.AddWithValue("@sid", saleId);
                insCt.Parameters.AddWithValue("@desc", $"Void item {productName} x{qty} from {invoiceNo} - {reason}");
                insCt.Parameters.AddWithValue("@amt", total);
                insCt.Parameters.AddWithValue("@bal", newBal);
                insCt.Parameters.AddWithValue("@uid", voidedByUserId);
                insCt.Parameters.AddWithValue("@uname", voidedByUserName);
                insCt.ExecuteNonQuery();

                var updCust = new SQLiteCommand("UPDATE Customers SET CreditBalance = @bal WHERE Id = @cid", conn);
                updCust.Parameters.AddWithValue("@bal", newBal);
                updCust.Parameters.AddWithValue("@cid", customerId.Value);
                updCust.ExecuteNonQuery();
            }

            trans.Commit();
            _ = SyncService.SyncVoidLog(new VoidLog { SaleId = saleId, SaleItemId = itemId, Action = "VoidItem", Reason = reason, InvoiceNo = invoiceNo, ProductName = productName, Quantity = qty, Amount = total, UserId = voidedByUserId, UserName = voidedByUserName, CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
            _ = SyncService.SyncStockTrail(new StockTrail { ProductId = productId, ProductName = productName, Barcode = barcode, QuantityAdded = restockQty, StockBefore = stockBefore, StockAfter = stockBefore + restockQty, Reference = $"{invoiceNo} - void ({reason})", UserId = userId, UserName = voidedByUserName, InvoiceNo = invoiceNo, CustomerName = "", CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
            // Re-sync the sale so cloud knows which items are voided
            try
            {
                var updatedSale = GetById(saleId);
                if (updatedSale != null && updatedSale.Items.Count > 0)
                    _ = SyncService.SyncSale(updatedSale, updatedSale.Items);
            }
            catch { }
            try
            {
                if (paymentMethod == "Credit" && customerId.HasValue)
                {
                    using var ctConn = DatabaseHelper.GetConnection();
                    ctConn.Open();
                    var ctCmd = new SQLiteCommand("SELECT * FROM CreditTransactions WHERE SaleId = @sid AND Type = 'Void' ORDER BY Id", ctConn);
                    ctCmd.Parameters.AddWithValue("@sid", saleId);
                    using var ctRdr = ctCmd.ExecuteReader();
                    while (ctRdr.Read())
                    {
                        _ = SyncService.SyncCreditTransaction(new CreditTransaction
                        {
                            Id = Convert.ToInt32(ctRdr["Id"]),
                            CustomerId = Convert.ToInt32(ctRdr["CustomerId"]),
                            SaleId = ctRdr["SaleId"] != DBNull.Value ? Convert.ToInt32(ctRdr["SaleId"]) : null,
                            Type = ctRdr["Type"]?.ToString() ?? "",
                            Description = ctRdr["Description"]?.ToString() ?? "",
                            Debit = Convert.ToDecimal(ctRdr["Debit"]),
                            Credit = Convert.ToDecimal(ctRdr["Credit"]),
                            Balance = Convert.ToDecimal(ctRdr["Balance"]),
                            UserId = Convert.ToInt32(ctRdr["UserId"]),
                            UserName = ctRdr["UserName"]?.ToString() ?? "",
                            CreatedAt = ctRdr["CreatedAt"]?.ToString() ?? ""
                        });
                    }
                }
            }
            catch { }
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    public static List<VoidLog> GetVoidLogs()
    {
        var list = new List<VoidLog>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT * FROM VoidLog ORDER BY Id DESC", conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new VoidLog
            {
                Id = Convert.ToInt32(rdr["Id"]),
                SaleId = Convert.ToInt32(rdr["SaleId"]),
                SaleItemId = rdr["SaleItemId"] != DBNull.Value ? Convert.ToInt32(rdr["SaleItemId"]) : null,
                Action = rdr["Action"].ToString() ?? "",
                Reason = rdr["Reason"].ToString() ?? "",
                InvoiceNo = rdr["InvoiceNo"].ToString() ?? "",
                ProductName = rdr["ProductName"].ToString() ?? "",
                Quantity = Convert.ToInt32(rdr["Quantity"]),
                Amount = Convert.ToDecimal(rdr["Amount"]),
                UserId = SafeInt(rdr, "UserId"),
                UserName = SafeString(rdr, "UserName"),
                CreatedAt = rdr["CreatedAt"].ToString() ?? ""
            });
        }
        return list;
    }

    private static int SafeInt(SQLiteDataReader rdr, string column)
    {
        try { return Convert.ToInt32(rdr[column]); }
        catch { return 0; }
    }

    private static string SafeString(SQLiteDataReader rdr, string column)
    {
        try { return rdr[column]?.ToString() ?? ""; }
        catch { return ""; }
    }
}
