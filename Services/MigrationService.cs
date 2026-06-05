using System.Data.SQLite;
using JumongPosV1._01.Data;

namespace JumongPosV1._01.Services;

public static class MigrationService
{
    public static string MigrateFromOldDb(string oldDbPath)
    {
        if (!File.Exists(oldDbPath))
            return "File not found.";

        try
        {
            using var oldConn = new SQLiteConnection($"Data Source={oldDbPath};Version=3;Read Only=True");
            oldConn.Open();

            using var newConn = DatabaseHelper.GetConnection();
            newConn.Open();

            var tables = ListTables(oldConn);

            using var trans = newConn.BeginTransaction();

            try
            {
                if (tables.Contains("Settings"))
                    CopySettings(oldConn, newConn);

                if (tables.Contains("Users"))
                    CopyUsers(oldConn, newConn);

                if (tables.Contains("Products"))
                    CopyProducts(oldConn, newConn);

                if (tables.Contains("ProductUnits"))
                    CopyProductUnits(oldConn, newConn);

                if (tables.Contains("Customers"))
                    CopyCustomers(oldConn, newConn);

                if (tables.Contains("Sales"))
                    CopySales(oldConn, newConn);

                if (tables.Contains("SaleItems"))
                    CopySaleItems(oldConn, newConn);

                if (tables.Contains("VoidLog"))
                    CopyVoidLog(oldConn, newConn);

                if (tables.Contains("StockTrail"))
                    CopyStockTrail(oldConn, newConn);

                if (tables.Contains("CreditTransactions"))
                    CopyCreditTransactions(oldConn, newConn);

                if (tables.Contains("HeldCarts"))
                    CopyHeldCarts(oldConn, newConn);

                if (tables.Contains("DailyClose"))
                    CopyDailyClose(oldConn, newConn);

                UpdateSequences(newConn);

                trans.Commit();
                oldConn.Close();
            }
            catch
            {
                trans.Rollback();
                oldConn.Close();
                throw;
            }

            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    static List<string> ListTables(SQLiteConnection conn)
    {
        var list = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(rdr.GetString(0));
        return list;
    }

    static void CopySettings(SQLiteConnection src, SQLiteConnection dst)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM Settings";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            using var ins = dst.CreateCommand();
            ins.CommandText = "INSERT OR IGNORE INTO Settings (Key, Value) VALUES (@k, @v)";
            ins.Parameters.AddWithValue("@k", rdr["Key"].ToString() ?? "");
            ins.Parameters.AddWithValue("@v", rdr["Value"].ToString() ?? "");
            ins.ExecuteNonQuery();
        }
    }

    static void CopyUsers(SQLiteConnection src, SQLiteConnection dst)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT Id, Username, PasswordHash, Role, IsActive, FullName FROM Users";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            using var ins = dst.CreateCommand();
            ins.CommandText = "INSERT OR IGNORE INTO Users (Id, Username, PasswordHash, Role, IsActive, FullName) VALUES (@id, @u, @p, @r, @a, @fn)";
            ins.Parameters.AddWithValue("@id", Convert.ToInt32(rdr["Id"]));
            ins.Parameters.AddWithValue("@u", rdr["Username"].ToString() ?? "");
            ins.Parameters.AddWithValue("@p", rdr["PasswordHash"].ToString() ?? "");
            ins.Parameters.AddWithValue("@r", rdr["Role"].ToString() ?? "Cashier");
            ins.Parameters.AddWithValue("@a", Convert.ToInt32(rdr["IsActive"]));
            ins.Parameters.AddWithValue("@fn", rdr["FullName"].ToString() ?? "");
            ins.ExecuteNonQuery();
        }
    }

    static void CopyProducts(SQLiteConnection src, SQLiteConnection dst)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Barcode, Category, Price, Cost, StockQty, IsActive, CreatedAt, SourceId FROM Products";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            using var ins = dst.CreateCommand();
            ins.CommandText = "INSERT OR IGNORE INTO Products (Id, Name, Barcode, Category, Price, Cost, StockQty, IsActive, CreatedAt, SourceId) VALUES (@id, @n, @bc, @cat, @pr, @co, @qty, @act, @dt, @src)";
            ins.Parameters.AddWithValue("@id", Convert.ToInt32(rdr["Id"]));
            ins.Parameters.AddWithValue("@n", rdr["Name"].ToString() ?? "");
            ins.Parameters.AddWithValue("@bc", rdr["Barcode"].ToString() ?? "");
            ins.Parameters.AddWithValue("@cat", rdr["Category"].ToString() ?? "");
            ins.Parameters.AddWithValue("@pr", Convert.ToDecimal(rdr["Price"]));
            ins.Parameters.AddWithValue("@co", Convert.ToDecimal(rdr["Cost"]));
            ins.Parameters.AddWithValue("@qty", Convert.ToInt32(rdr["StockQty"]));
            ins.Parameters.AddWithValue("@act", Convert.ToInt32(rdr["IsActive"]));
            ins.Parameters.AddWithValue("@dt", rdr["CreatedAt"].ToString() ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            ins.Parameters.AddWithValue("@src", rdr["SourceId"]?.ToString() ?? "");
            ins.ExecuteNonQuery();
        }
    }

    static void CopyProductUnits(SQLiteConnection src, SQLiteConnection dst)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT Id, ProductId, UnitName, Price, Cost, QtyPerUnit, IsDefault FROM ProductUnits";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            using var ins = dst.CreateCommand();
            ins.CommandText = "INSERT OR IGNORE INTO ProductUnits (Id, ProductId, UnitName, Price, Cost, QtyPerUnit, IsDefault) VALUES (@id, @pid, @un, @pr, @co, @qpu, @def)";
            ins.Parameters.AddWithValue("@id", Convert.ToInt32(rdr["Id"]));
            ins.Parameters.AddWithValue("@pid", Convert.ToInt32(rdr["ProductId"]));
            ins.Parameters.AddWithValue("@un", rdr["UnitName"].ToString() ?? "");
            ins.Parameters.AddWithValue("@pr", Convert.ToDecimal(rdr["Price"]));
            ins.Parameters.AddWithValue("@co", Convert.ToDecimal(rdr["Cost"]));
            ins.Parameters.AddWithValue("@qpu", Convert.ToInt32(rdr["QtyPerUnit"]));
            ins.Parameters.AddWithValue("@def", Convert.ToInt32(rdr["IsDefault"]));
            ins.ExecuteNonQuery();
        }
    }

    static void CopyCustomers(SQLiteConnection src, SQLiteConnection dst)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Phone, Email, LoyaltyPoints, CreatedAt, CreditBalance, Address FROM Customers";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            using var ins = dst.CreateCommand();
            ins.CommandText = "INSERT OR IGNORE INTO Customers (Id, Name, Phone, Email, LoyaltyPoints, CreatedAt, CreditBalance, Address) VALUES (@id, @n, @p, @e, @pt, @dt, @cb, @addr)";
            ins.Parameters.AddWithValue("@id", Convert.ToInt32(rdr["Id"]));
            ins.Parameters.AddWithValue("@n", rdr["Name"].ToString() ?? "");
            ins.Parameters.AddWithValue("@p", rdr["Phone"].ToString() ?? "");
            ins.Parameters.AddWithValue("@e", rdr["Email"].ToString() ?? "");
            ins.Parameters.AddWithValue("@pt", Convert.ToInt32(rdr["LoyaltyPoints"]));
            ins.Parameters.AddWithValue("@dt", rdr["CreatedAt"].ToString() ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            ins.Parameters.AddWithValue("@cb", Convert.ToDecimal(rdr["CreditBalance"]));
            ins.Parameters.AddWithValue("@addr", rdr["Address"].ToString() ?? "");
            ins.ExecuteNonQuery();
        }
    }

    static void CopySales(SQLiteConnection src, SQLiteConnection dst)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT Id, InvoiceNo, SaleDate, SubTotal, Discount, Tax, GrandTotal, AmountPaid, Change, PaymentMethod, CustomerId, UserId, IsVoided, VoidedAt, ReferenceNo, OrderType FROM Sales";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            using var ins = dst.CreateCommand();
            ins.CommandText = "INSERT OR IGNORE INTO Sales (Id, InvoiceNo, SaleDate, SubTotal, Discount, Tax, GrandTotal, AmountPaid, Change, PaymentMethod, CustomerId, UserId, IsVoided, VoidedAt, ReferenceNo, OrderType) VALUES (@id, @inv, @dt, @sub, @disc, @tax, @total, @paid, @chg, @pm, @cid, @uid, @void, @vat, @ref, @otype)";
            ins.Parameters.AddWithValue("@id", Convert.ToInt32(rdr["Id"]));
            ins.Parameters.AddWithValue("@inv", rdr["InvoiceNo"].ToString() ?? "");
            ins.Parameters.AddWithValue("@dt", rdr["SaleDate"].ToString() ?? "");
            ins.Parameters.AddWithValue("@sub", Convert.ToDecimal(rdr["SubTotal"]));
            ins.Parameters.AddWithValue("@disc", Convert.ToDecimal(rdr["Discount"]));
            ins.Parameters.AddWithValue("@tax", Convert.ToDecimal(rdr["Tax"]));
            ins.Parameters.AddWithValue("@total", Convert.ToDecimal(rdr["GrandTotal"]));
            ins.Parameters.AddWithValue("@paid", Convert.ToDecimal(rdr["AmountPaid"]));
            ins.Parameters.AddWithValue("@chg", Convert.ToDecimal(rdr["Change"]));
            ins.Parameters.AddWithValue("@pm", rdr["PaymentMethod"].ToString() ?? "Cash");
            ins.Parameters.AddWithValue("@cid", rdr["CustomerId"] != DBNull.Value ? Convert.ToInt32(rdr["CustomerId"]) : (object)DBNull.Value);
            ins.Parameters.AddWithValue("@uid", rdr["UserId"] != DBNull.Value ? Convert.ToInt32(rdr["UserId"]) : (object)DBNull.Value);
            ins.Parameters.AddWithValue("@void", Convert.ToInt32(rdr["IsVoided"]));
            ins.Parameters.AddWithValue("@vat", rdr["VoidedAt"]?.ToString() ?? (object)DBNull.Value);
            ins.Parameters.AddWithValue("@ref", rdr["ReferenceNo"].ToString() ?? "");
            ins.Parameters.AddWithValue("@otype", rdr["OrderType"].ToString() ?? "Walk-in");
            ins.ExecuteNonQuery();
        }
    }

    static void CopySaleItems(SQLiteConnection src, SQLiteConnection dst)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT Id, SaleId, ProductId, ProductName, Barcode, Price, Quantity, TotalPrice, IsVoided, UnitName, QtyPerUnit FROM SaleItems";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            using var ins = dst.CreateCommand();
            ins.CommandText = "INSERT OR IGNORE INTO SaleItems (Id, SaleId, ProductId, ProductName, Barcode, Price, Quantity, TotalPrice, IsVoided, UnitName, QtyPerUnit) VALUES (@id, @sid, @pid, @pn, @bc, @pr, @qty, @tot, @void, @un, @qpu)";
            ins.Parameters.AddWithValue("@id", Convert.ToInt32(rdr["Id"]));
            ins.Parameters.AddWithValue("@sid", Convert.ToInt32(rdr["SaleId"]));
            ins.Parameters.AddWithValue("@pid", Convert.ToInt32(rdr["ProductId"]));
            ins.Parameters.AddWithValue("@pn", rdr["ProductName"].ToString() ?? "");
            ins.Parameters.AddWithValue("@bc", rdr["Barcode"].ToString() ?? "");
            ins.Parameters.AddWithValue("@pr", Convert.ToDecimal(rdr["Price"]));
            ins.Parameters.AddWithValue("@qty", Convert.ToInt32(rdr["Quantity"]));
            ins.Parameters.AddWithValue("@tot", Convert.ToDecimal(rdr["TotalPrice"]));
            ins.Parameters.AddWithValue("@void", Convert.ToInt32(rdr["IsVoided"]));
            ins.Parameters.AddWithValue("@un", rdr["UnitName"].ToString() ?? "");
            ins.Parameters.AddWithValue("@qpu", Convert.ToInt32(rdr["QtyPerUnit"]));
            ins.ExecuteNonQuery();
        }
    }

    static void CopyVoidLog(SQLiteConnection src, SQLiteConnection dst)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT Id, SaleId, SaleItemId, Action, Reason, InvoiceNo, ProductName, Quantity, Amount, CreatedAt FROM VoidLog";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            using var ins = dst.CreateCommand();
            ins.CommandText = "INSERT OR IGNORE INTO VoidLog (Id, SaleId, SaleItemId, Action, Reason, InvoiceNo, ProductName, Quantity, Amount, CreatedAt) VALUES (@id, @sid, @siid, @act, @r, @inv, @pn, @qty, @amt, @dt)";
            ins.Parameters.AddWithValue("@id", Convert.ToInt32(rdr["Id"]));
            ins.Parameters.AddWithValue("@sid", Convert.ToInt32(rdr["SaleId"]));
            ins.Parameters.AddWithValue("@siid", rdr["SaleItemId"] != DBNull.Value ? Convert.ToInt32(rdr["SaleItemId"]) : (object)DBNull.Value);
            ins.Parameters.AddWithValue("@act", rdr["Action"].ToString() ?? "");
            ins.Parameters.AddWithValue("@r", rdr["Reason"].ToString() ?? "");
            ins.Parameters.AddWithValue("@inv", rdr["InvoiceNo"].ToString() ?? "");
            ins.Parameters.AddWithValue("@pn", rdr["ProductName"].ToString() ?? "");
            ins.Parameters.AddWithValue("@qty", Convert.ToInt32(rdr["Quantity"]));
            ins.Parameters.AddWithValue("@amt", Convert.ToDecimal(rdr["Amount"]));
            ins.Parameters.AddWithValue("@dt", rdr["CreatedAt"].ToString() ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            ins.ExecuteNonQuery();
        }
    }

    static void CopyStockTrail(SQLiteConnection src, SQLiteConnection dst)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT Id, ProductId, ProductName, Barcode, QuantityAdded, StockBefore, StockAfter, Reference, UserId, UserName, CreatedAt FROM StockTrail";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            using var ins = dst.CreateCommand();
            ins.CommandText = "INSERT OR IGNORE INTO StockTrail (Id, ProductId, ProductName, Barcode, QuantityAdded, StockBefore, StockAfter, Reference, UserId, UserName, CreatedAt) VALUES (@id, @pid, @pn, @bc, @qa, @sb, @sa, @ref, @uid, @un, @dt)";
            ins.Parameters.AddWithValue("@id", Convert.ToInt32(rdr["Id"]));
            ins.Parameters.AddWithValue("@pid", Convert.ToInt32(rdr["ProductId"]));
            ins.Parameters.AddWithValue("@pn", rdr["ProductName"].ToString() ?? "");
            ins.Parameters.AddWithValue("@bc", rdr["Barcode"].ToString() ?? "");
            ins.Parameters.AddWithValue("@qa", Convert.ToDecimal(rdr["QuantityAdded"]));
            ins.Parameters.AddWithValue("@sb", Convert.ToInt32(rdr["StockBefore"]));
            ins.Parameters.AddWithValue("@sa", Convert.ToInt32(rdr["StockAfter"]));
            ins.Parameters.AddWithValue("@ref", rdr["Reference"].ToString() ?? "");
            ins.Parameters.AddWithValue("@uid", Convert.ToInt32(rdr["UserId"]));
            ins.Parameters.AddWithValue("@un", rdr["UserName"].ToString() ?? "");
            ins.Parameters.AddWithValue("@dt", rdr["CreatedAt"].ToString() ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            ins.ExecuteNonQuery();
        }
    }

    static void CopyCreditTransactions(SQLiteConnection src, SQLiteConnection dst)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT Id, CustomerId, SaleId, Type, Description, Debit, Credit, Balance, CreatedAt FROM CreditTransactions";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            using var ins = dst.CreateCommand();
            ins.CommandText = "INSERT OR IGNORE INTO CreditTransactions (Id, CustomerId, SaleId, Type, Description, Debit, Credit, Balance, CreatedAt) VALUES (@id, @cid, @sid, @type, @desc, @db, @cr, @bal, @dt)";
            ins.Parameters.AddWithValue("@id", Convert.ToInt32(rdr["Id"]));
            ins.Parameters.AddWithValue("@cid", Convert.ToInt32(rdr["CustomerId"]));
            ins.Parameters.AddWithValue("@sid", rdr["SaleId"] != DBNull.Value ? Convert.ToInt32(rdr["SaleId"]) : (object)DBNull.Value);
            ins.Parameters.AddWithValue("@type", rdr["Type"].ToString() ?? "");
            ins.Parameters.AddWithValue("@desc", rdr["Description"].ToString() ?? "");
            ins.Parameters.AddWithValue("@db", Convert.ToDecimal(rdr["Debit"]));
            ins.Parameters.AddWithValue("@cr", Convert.ToDecimal(rdr["Credit"]));
            ins.Parameters.AddWithValue("@bal", Convert.ToDecimal(rdr["Balance"]));
            ins.Parameters.AddWithValue("@dt", rdr["CreatedAt"].ToString() ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            ins.ExecuteNonQuery();
        }
    }

    static void CopyHeldCarts(SQLiteConnection src, SQLiteConnection dst)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT Id, OrderType, CustomerId, CustomerName, ItemsJson, CreatedAt FROM HeldCarts";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            using var ins = dst.CreateCommand();
            ins.CommandText = "INSERT OR IGNORE INTO HeldCarts (Id, OrderType, CustomerId, CustomerName, ItemsJson, CreatedAt) VALUES (@id, @otype, @cid, @cn, @json, @dt)";
            ins.Parameters.AddWithValue("@id", Convert.ToInt32(rdr["Id"]));
            ins.Parameters.AddWithValue("@otype", rdr["OrderType"].ToString() ?? "Walk-in");
            ins.Parameters.AddWithValue("@cid", rdr["CustomerId"] != DBNull.Value ? Convert.ToInt32(rdr["CustomerId"]) : (object)DBNull.Value);
            ins.Parameters.AddWithValue("@cn", rdr["CustomerName"].ToString() ?? "");
            ins.Parameters.AddWithValue("@json", rdr["ItemsJson"].ToString() ?? "[]");
            ins.Parameters.AddWithValue("@dt", rdr["CreatedAt"].ToString() ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            ins.ExecuteNonQuery();
        }
    }

    static void CopyDailyClose(SQLiteConnection src, SQLiteConnection dst)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT Id, CloseDate, TotalSales, TotalCash, TotalEWallet, TotalCredit, TotalVoided, Denom1000, Denom500, Denom200, Denom100, Denom50, DenomCoins, CashOnHand, Difference, Notes, CreatedAt, UserId, UserName FROM DailyClose";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            using var ins = dst.CreateCommand();
            ins.CommandText = "INSERT OR IGNORE INTO DailyClose (Id, CloseDate, TotalSales, TotalCash, TotalEWallet, TotalCredit, TotalVoided, Denom1000, Denom500, Denom200, Denom100, Denom50, DenomCoins, CashOnHand, Difference, Notes, CreatedAt, UserId, UserName) VALUES (@id, @cd, @ts, @tc, @tew, @tcr, @tv, @d1k, @d500, @d200, @d100, @d50, @coins, @coh, @diff, @notes, @dt, @uid, @un)";
            ins.Parameters.AddWithValue("@id", Convert.ToInt32(rdr["Id"]));
            ins.Parameters.AddWithValue("@cd", rdr["CloseDate"].ToString() ?? "");
            ins.Parameters.AddWithValue("@ts", Convert.ToDecimal(rdr["TotalSales"]));
            ins.Parameters.AddWithValue("@tc", Convert.ToDecimal(rdr["TotalCash"]));
            ins.Parameters.AddWithValue("@tew", Convert.ToDecimal(rdr["TotalEWallet"]));
            ins.Parameters.AddWithValue("@tcr", Convert.ToDecimal(rdr["TotalCredit"]));
            ins.Parameters.AddWithValue("@tv", Convert.ToDecimal(rdr["TotalVoided"]));
            ins.Parameters.AddWithValue("@d1k", Convert.ToInt32(rdr["Denom1000"]));
            ins.Parameters.AddWithValue("@d500", Convert.ToInt32(rdr["Denom500"]));
            ins.Parameters.AddWithValue("@d200", Convert.ToInt32(rdr["Denom200"]));
            ins.Parameters.AddWithValue("@d100", Convert.ToInt32(rdr["Denom100"]));
            ins.Parameters.AddWithValue("@d50", Convert.ToInt32(rdr["Denom50"]));
            ins.Parameters.AddWithValue("@coins", Convert.ToDecimal(rdr["DenomCoins"]));
            ins.Parameters.AddWithValue("@coh", Convert.ToDecimal(rdr["CashOnHand"]));
            ins.Parameters.AddWithValue("@diff", Convert.ToDecimal(rdr["Difference"]));
            ins.Parameters.AddWithValue("@notes", rdr["Notes"].ToString() ?? "");
            ins.Parameters.AddWithValue("@dt", rdr["CreatedAt"].ToString() ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            ins.Parameters.AddWithValue("@uid", Convert.ToInt32(rdr["UserId"]));
            ins.Parameters.AddWithValue("@un", rdr["UserName"].ToString() ?? "");
            ins.ExecuteNonQuery();
        }
    }

    static void UpdateSequences(SQLiteConnection conn)
    {
        var tables = new[] { "Products", "ProductUnits", "Customers", "Users", "Sales", "SaleItems", "VoidLog", "StockTrail", "CreditTransactions", "HeldCarts", "DailyClose" };
        foreach (var table in tables)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 0) FROM [{table}]) WHERE name = @tbl";
            cmd.Parameters.AddWithValue("@tbl", table);
            var rows = cmd.ExecuteNonQuery();
            if (rows == 0)
            {
                using var ins = conn.CreateCommand();
                ins.CommandText = $"INSERT INTO sqlite_sequence (name, seq) SELECT @tbl, COALESCE(MAX(Id), 0) FROM [{table}]";
                ins.Parameters.AddWithValue("@tbl", table);
                ins.ExecuteNonQuery();
            }
        }
    }
}
