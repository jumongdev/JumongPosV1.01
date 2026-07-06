using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public class DailyCloseService
{
    public static decimal GetLastCashOnHand()
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand(
            "SELECT CashOnHand FROM DailyClose ORDER BY Id DESC LIMIT 1", conn);
        var val = cmd.ExecuteScalar();
        return val == null || val == DBNull.Value ? 0m : Convert.ToDecimal(val);
    }

    public static string? GetLastCloseTime()
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT CreatedAt FROM DailyClose ORDER BY Id DESC LIMIT 1", conn);
        var val = cmd.ExecuteScalar()?.ToString();
        return val;
    }

    public static (decimal TotalSales, decimal TotalCash, decimal TotalEWallet, decimal TotalCredit,
        decimal TotalVoided, decimal CreditPayCash, decimal CreditPayEWallet, decimal TotalExpenses) GetShiftTotals()
    {
        var since = GetLastCloseTime();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();

        var cond = string.IsNullOrEmpty(since) ? "1=1" : "s.SaleDate > @since";

        // All totals from SaleItems (item-level) to correctly handle partial voids
        var sql = $@"SELECT 
            COALESCE(SUM(CASE WHEN s.PaymentMethod = 'Cash' AND si.IsVoided = 0 THEN si.TotalPrice ELSE 0 END), 0),
            COALESCE(SUM(CASE WHEN s.PaymentMethod = 'E-Wallet' AND si.IsVoided = 0 THEN si.TotalPrice ELSE 0 END), 0),
            COALESCE(SUM(CASE WHEN s.PaymentMethod = 'Credit' AND si.IsVoided = 0 THEN si.TotalPrice ELSE 0 END), 0),
            COALESCE(SUM(CASE WHEN si.IsVoided = 0 THEN si.TotalPrice ELSE 0 END), 0),
            COALESCE(SUM(CASE WHEN si.IsVoided = 1 THEN si.TotalPrice ELSE 0 END), 0)
            FROM SaleItems si JOIN Sales s ON si.SaleId = s.Id WHERE {cond}";

        using var cmd = new SQLiteCommand(sql, conn);
        if (!string.IsNullOrEmpty(since))
            cmd.Parameters.AddWithValue("@since", since);

        decimal total, cash, ewallet, credit, voided;
        using (var rdr = cmd.ExecuteReader())
        {
            if (rdr.Read())
            {
                cash = Convert.ToDecimal(rdr[0]);
                ewallet = Convert.ToDecimal(rdr[1]);
                credit = Convert.ToDecimal(rdr[2]);
                total = Convert.ToDecimal(rdr[3]);
                voided = Convert.ToDecimal(rdr[4]);
            }
            else
            {
                return (0, 0, 0, 0, 0, 0, 0, 0);
            }
        }

        // Add split payment portions
        var splitSql = $@"SELECT 
            COALESCE(SUM(CASE WHEN s.IsVoided = 0 THEN s.GrandTotal - s.EwPaid ELSE 0 END), 0),
            COALESCE(SUM(CASE WHEN s.IsVoided = 0 THEN s.EwPaid ELSE 0 END), 0)
            FROM Sales s WHERE s.PaymentMethod = 'Split' AND {cond}";
        using var splitCmd = new SQLiteCommand(splitSql, conn);
        if (!string.IsNullOrEmpty(since))
            splitCmd.Parameters.AddWithValue("@since", since);
        using (var splitRdr = splitCmd.ExecuteReader())
        {
            if (splitRdr.Read())
            {
                cash += Convert.ToDecimal(splitRdr[0]);
                ewallet += Convert.ToDecimal(splitRdr[1]);
            }
        }

        // Credit payments received in cash
        var payCashSql = "SELECT COALESCE(SUM(Credit), 0) FROM CreditTransactions " +
                         "WHERE Type = 'Payment' AND PaymentMethod = 'Cash'";
        if (!string.IsNullOrEmpty(since))
            payCashSql += " AND CreatedAt > @since_pc";
        using var payCashCmd = new SQLiteCommand(payCashSql, conn);
        if (!string.IsNullOrEmpty(since))
            payCashCmd.Parameters.AddWithValue("@since_pc", since);
        var creditPayCash = Convert.ToDecimal(payCashCmd.ExecuteScalar());

        // Credit payments received in ewallet
        var payEwSql = "SELECT COALESCE(SUM(Credit), 0) FROM CreditTransactions " +
                       "WHERE Type = 'Payment' AND PaymentMethod = 'E-Wallet'";
        if (!string.IsNullOrEmpty(since))
            payEwSql += " AND CreatedAt > @since_pe";
        using var payEwCmd = new SQLiteCommand(payEwSql, conn);
        if (!string.IsNullOrEmpty(since))
            payEwCmd.Parameters.AddWithValue("@since_pe", since);
        var creditPayEWallet = Convert.ToDecimal(payEwCmd.ExecuteScalar());

        // Total expenses for current shift
        var expSql = "SELECT COALESCE(SUM(Amount), 0) FROM Expenses";
        if (!string.IsNullOrEmpty(since))
            expSql += " WHERE Timestamp > @since_exp";
        using var expCmd = new SQLiteCommand(expSql, conn);
        if (!string.IsNullOrEmpty(since))
            expCmd.Parameters.AddWithValue("@since_exp", since);
        var totalExpenses = Convert.ToDecimal(expCmd.ExecuteScalar());

        return (total, cash, ewallet, credit, voided, creditPayCash, creditPayEWallet, totalExpenses);
    }

    public static string? SaveClose(DailyClose dc)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        try
        {
            var sql = @"INSERT INTO DailyClose (CloseDate, TotalSales, TotalCash, TotalEWallet, TotalCredit,
                        TotalVoided, TotalExpenses, OpeningCash, Denom1000, Denom500, Denom200, Denom100, Denom50, Denom20, DenomCoins,
                        CashOnHand, Difference, Notes, UserId, UserName)
                        VALUES (@d, @ts, @tc, @te, @tcr, @tv, @texp, @opn, @d1k, @d5h, @d2h, @d1h, @d50, @d20, @coins,
                        @coh, @diff, @notes, @uid, @uname)";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@d", dc.CloseDate);
            cmd.Parameters.AddWithValue("@ts", dc.TotalSales);
            cmd.Parameters.AddWithValue("@tc", dc.TotalCash);
            cmd.Parameters.AddWithValue("@te", dc.TotalEWallet);
            cmd.Parameters.AddWithValue("@tcr", dc.TotalCredit);
            cmd.Parameters.AddWithValue("@tv", dc.TotalVoided);
            cmd.Parameters.AddWithValue("@texp", dc.TotalExpenses);
            cmd.Parameters.AddWithValue("@opn", dc.OpeningCash);
            cmd.Parameters.AddWithValue("@d1k", dc.Denom1000);
            cmd.Parameters.AddWithValue("@d5h", dc.Denom500);
            cmd.Parameters.AddWithValue("@d2h", dc.Denom200);
            cmd.Parameters.AddWithValue("@d1h", dc.Denom100);
            cmd.Parameters.AddWithValue("@d50", dc.Denom50);
            cmd.Parameters.AddWithValue("@d20", dc.Denom20);
            cmd.Parameters.AddWithValue("@coins", dc.DenomCoins);
            cmd.Parameters.AddWithValue("@coh", dc.CashOnHand);
            cmd.Parameters.AddWithValue("@diff", dc.Difference);
            cmd.Parameters.AddWithValue("@notes", dc.Notes);
            cmd.Parameters.AddWithValue("@uid", dc.UserId);
            cmd.Parameters.AddWithValue("@uname", dc.UserName);
            cmd.ExecuteNonQuery();
            using var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", conn);
            dc.Id = Convert.ToInt32(idCmd.ExecuteScalar());
            _ = SyncService.SyncDailyClose(dc);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static List<(string InvoiceNo, string SaleDate, decimal Amount, string ReferenceNo)> GetGcashTransactionsSinceLastClose(DateTime? endDate = null)
    {
        var list = new List<(string, string, decimal, string)>();
        var since = GetLastCloseTime();
        var end = endDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? TimeHelper.Now.ToString("yyyy-MM-dd HH:mm:ss");

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT InvoiceNo, SaleDate, CASE WHEN PaymentMethod = 'Split' THEN EwPaid ELSE GrandTotal END, ReferenceNo FROM Sales " +
                  "WHERE (PaymentMethod = 'E-Wallet' OR (PaymentMethod = 'Split' AND EwPaid > 0)) AND IsVoided = 0";
        if (!string.IsNullOrEmpty(since))
            sql += " AND SaleDate > @since";
        sql += " AND SaleDate <= @end ORDER BY SaleDate";

        using var cmd = new System.Data.SQLite.SQLiteCommand(sql, conn);
        if (!string.IsNullOrEmpty(since))
            cmd.Parameters.AddWithValue("@since", since);
        cmd.Parameters.AddWithValue("@end", end);

        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add((
                rdr["InvoiceNo"].ToString() ?? "",
                rdr["SaleDate"].ToString() ?? "",
                Convert.ToDecimal(rdr[2]),
                rdr["ReferenceNo"].ToString() ?? ""
            ));
        }
        return list;
    }

    public static List<(string InvoiceNo, string SaleDate, decimal Amount, string ReferenceNo)> GetGcashTransactionsBetween(string? since, DateTime end)
    {
        var list = new List<(string, string, decimal, string)>();
        var endStr = end.ToString("yyyy-MM-dd HH:mm:ss");

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT InvoiceNo, SaleDate, CASE WHEN PaymentMethod = 'Split' THEN EwPaid ELSE GrandTotal END AS GrandTotal, ReferenceNo FROM Sales " +
                  "WHERE (PaymentMethod = 'E-Wallet' OR (PaymentMethod = 'Split' AND EwPaid > 0)) AND IsVoided = 0";
        if (!string.IsNullOrEmpty(since))
            sql += " AND SaleDate > @since";
        sql += " AND SaleDate <= @end ORDER BY SaleDate";

        using var cmd = new System.Data.SQLite.SQLiteCommand(sql, conn);
        if (!string.IsNullOrEmpty(since))
            cmd.Parameters.AddWithValue("@since", since);
        cmd.Parameters.AddWithValue("@end", endStr);

        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add((
                rdr["InvoiceNo"]?.ToString() ?? "",
                rdr["SaleDate"]?.ToString()?[..16] ?? "",
                Convert.ToDecimal(rdr["GrandTotal"]),
                rdr["ReferenceNo"]?.ToString() ?? ""
            ));
        }
        return list;
    }

    public static List<(string Name, decimal Amount)> GetCreditCustomersSinceLastClose(DateTime? endDate = null)
    {
        var list = new List<(string, decimal)>();
        var since = GetLastCloseTime();
        var end = endDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? TimeHelper.Now.ToString("yyyy-MM-dd HH:mm:ss");

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT c.Name, COALESCE(SUM(si.TotalPrice), 0) AS Total FROM Sales s " +
                  "LEFT JOIN Customers c ON s.CustomerId = c.Id " +
                  "JOIN SaleItems si ON si.SaleId = s.Id " +
                  "WHERE s.PaymentMethod = 'Credit' AND s.IsVoided = 0 AND si.IsVoided = 0";
        if (!string.IsNullOrEmpty(since))
            sql += " AND s.SaleDate > @since";
        sql += " AND s.SaleDate <= @end GROUP BY c.Id, c.Name ORDER BY s.SaleDate";

        using var cmd = new SQLiteCommand(sql, conn);
        if (!string.IsNullOrEmpty(since))
            cmd.Parameters.AddWithValue("@since", since);
        cmd.Parameters.AddWithValue("@end", end);

        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add((
                rdr["Name"]?.ToString() ?? "Unknown",
                Convert.ToDecimal(rdr["Total"])
            ));
        }
        return list;
    }

    public static List<DailyClose> GetHistory()
    {
        var list = new List<DailyClose>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM DailyClose ORDER BY CloseDate DESC LIMIT 500";
        using var cmd = new SQLiteCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new DailyClose
            {
                Id = Convert.ToInt32(rdr["Id"]),
                CloseDate = rdr["CloseDate"].ToString() ?? "",
                TotalSales = Convert.ToDecimal(rdr["TotalSales"]),
                TotalCash = Convert.ToDecimal(rdr["TotalCash"]),
                TotalEWallet = Convert.ToDecimal(rdr["TotalEWallet"]),
                TotalCredit = Convert.ToDecimal(rdr["TotalCredit"]),
                TotalVoided = Convert.ToDecimal(rdr["TotalVoided"]),
                TotalExpenses = Convert.ToDecimal(rdr["TotalExpenses"]),
                OpeningCash = Convert.ToDecimal(rdr["OpeningCash"]),
                CashOnHand = Convert.ToDecimal(rdr["CashOnHand"]),
                Difference = Convert.ToDecimal(rdr["Difference"]),
                Denom1000 = Convert.ToInt32(rdr["Denom1000"]),
                Denom500 = Convert.ToInt32(rdr["Denom500"]),
                Denom200 = Convert.ToInt32(rdr["Denom200"]),
                Denom100 = Convert.ToInt32(rdr["Denom100"]),
                Denom50 = Convert.ToInt32(rdr["Denom50"]),
                Denom20 = rdr["Denom20"] != DBNull.Value ? Convert.ToInt32(rdr["Denom20"]) : 0,
                DenomCoins = Convert.ToDecimal(rdr["DenomCoins"]),
                Notes = rdr["Notes"]?.ToString() ?? "",
                UserId = Convert.ToInt32(rdr["UserId"]),
                UserName = rdr["UserName"]?.ToString() ?? ""
            });
        }
        return list;
    }

    public static string? GetPreviousCloseTime(int currentCloseId)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT CloseDate FROM DailyClose WHERE Id < @id ORDER BY Id DESC LIMIT 1";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", currentCloseId);
        return cmd.ExecuteScalar()?.ToString();
    }

    public static List<(string Name, decimal Amount)> GetCreditCustomersBetween(string? since, DateTime end)
    {
        var list = new List<(string, decimal)>();
        var endStr = end.ToString("yyyy-MM-dd HH:mm:ss");

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT c.Name, COALESCE(SUM(si.TotalPrice), 0) AS Total FROM Sales s " +
                  "LEFT JOIN Customers c ON s.CustomerId = c.Id " +
                  "JOIN SaleItems si ON si.SaleId = s.Id " +
                  "WHERE s.PaymentMethod = 'Credit' AND s.IsVoided = 0 AND si.IsVoided = 0";
        if (!string.IsNullOrEmpty(since))
            sql += " AND s.SaleDate > @since";
        sql += " AND s.SaleDate <= @end GROUP BY c.Id, c.Name ORDER BY s.SaleDate";

        using var cmd = new System.Data.SQLite.SQLiteCommand(sql, conn);
        if (!string.IsNullOrEmpty(since))
            cmd.Parameters.AddWithValue("@since", since);
        cmd.Parameters.AddWithValue("@end", endStr);

        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add((
                rdr["Name"]?.ToString() ?? "Unknown",
                Convert.ToDecimal(rdr["Total"])
            ));
        }
        return list;
    }

    public static List<(string CustomerName, string PaymentMethod, decimal Amount, string Timestamp)> GetCreditPaymentsSinceLastClose()
    {
        var list = new List<(string, string, decimal, string)>();
        var since = GetLastCloseTime();

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = @"SELECT c.Name, ct.PaymentMethod, ct.Credit, ct.CreatedAt
                    FROM CreditTransactions ct
                    LEFT JOIN Customers c ON ct.CustomerId = c.Id
                    WHERE ct.Type = 'Payment'";
        if (!string.IsNullOrEmpty(since))
            sql += " AND ct.CreatedAt > @since";
        sql += " ORDER BY ct.CreatedAt";

        using var cmd = new SQLiteCommand(sql, conn);
        if (!string.IsNullOrEmpty(since))
            cmd.Parameters.AddWithValue("@since", since);

        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add((
                rdr["Name"]?.ToString() ?? "Unknown",
                rdr["PaymentMethod"]?.ToString() ?? "",
                Convert.ToDecimal(rdr["Credit"]),
                rdr["CreatedAt"]?.ToString() ?? ""
            ));
        }
        return list;
    }

    public static List<(string CustomerName, string PaymentMethod, decimal Amount, string Timestamp)> GetCreditPaymentsBetween(string? since, DateTime end)
    {
        var list = new List<(string, string, decimal, string)>();
        var endStr = end.ToString("yyyy-MM-dd HH:mm:ss");

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = @"SELECT c.Name, ct.PaymentMethod, ct.Credit, ct.CreatedAt
                    FROM CreditTransactions ct
                    LEFT JOIN Customers c ON ct.CustomerId = c.Id
                    WHERE ct.Type = 'Payment' AND ct.CreatedAt <= @end";
        if (!string.IsNullOrEmpty(since))
            sql += " AND ct.CreatedAt > @since";
        sql += " ORDER BY ct.CreatedAt";

        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@end", endStr);
        if (!string.IsNullOrEmpty(since))
            cmd.Parameters.AddWithValue("@since", since);

        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add((
                rdr["Name"]?.ToString() ?? "Unknown",
                rdr["PaymentMethod"]?.ToString() ?? "",
                Convert.ToDecimal(rdr["Credit"]),
                rdr["CreatedAt"]?.ToString() ?? ""
            ));
        }
        return list;
    }

    private static decimal SafeDecimal(System.Data.SQLite.SQLiteDataReader rdr, string column)
    {
        try { return Convert.ToDecimal(rdr[column]); }
        catch { return 0m; }
    }

    public static List<DailyVariance> GetDailyVariance()
    {
        var list = new List<DailyVariance>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = @"SELECT date(CloseDate) AS Day,
                           COUNT(*) AS Shifts,
                           GROUP_CONCAT(DISTINCT UserName) AS Cashiers,
                           SUM(TotalSales) AS Sales,
                           SUM(TotalCash) AS Cash,
                           SUM(TotalEWallet) AS EWallet,
                           SUM(CashOnHand) AS CashCounted,
                           SUM(Difference) AS TotalVariance
                    FROM DailyClose
                    GROUP BY date(CloseDate)
                    ORDER BY Day DESC
                    LIMIT 90";
        using var cmd = new SQLiteCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new DailyVariance
            {
                Date = rdr["Day"].ToString() ?? "",
                Shifts = Convert.ToInt32(rdr["Shifts"]),
                Cashiers = rdr["Cashiers"]?.ToString() ?? "",
                TotalSales = Convert.ToDecimal(rdr["Sales"]),
                TotalCash = Convert.ToDecimal(rdr["Cash"]),
                TotalEWallet = Convert.ToDecimal(rdr["EWallet"]),
                CashCounted = Convert.ToDecimal(rdr["CashCounted"]),
                TotalVariance = Convert.ToDecimal(rdr["TotalVariance"])
            });
        }
        return list;
    }

    public static List<(string Date, int ShiftCount, decimal TotalSales, decimal TotalExpenses, decimal AvgVariance)> GetShiftComparison()
    {
        var list = new List<(string, int, decimal, decimal, decimal)>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = @"SELECT date(CloseDate) AS Day,
                           COUNT(*) AS Shifts,
                           SUM(TotalSales) AS Sales,
                           SUM(TotalExpenses) AS Expenses,
                           AVG(Difference) AS AvgDiff
                    FROM DailyClose
                    GROUP BY date(CloseDate)
                    ORDER BY Day DESC
                    LIMIT 60";
        using var cmd = new SQLiteCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add((
                rdr["Day"].ToString() ?? "",
                Convert.ToInt32(rdr["Shifts"]),
                Convert.ToDecimal(rdr["Sales"]),
                Convert.ToDecimal(rdr["Expenses"]),
                Math.Round(Convert.ToDecimal(rdr["AvgDiff"]), 2)
            ));
        }
        return list;
    }
}