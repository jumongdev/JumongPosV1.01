using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public class ExpenseService
{
    public static void SaveExpense(decimal amount, string category, string description, string refNo, string username, string receiptImage = "")
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand(
            "INSERT INTO Expenses (Amount, Category, Description, ReferenceNo, CashierUsername, ReceiptImage, Timestamp) " +
            "VALUES (@amount, @category, @description, @refNo, @username, @img, datetime('now','localtime'))", conn);
        cmd.Parameters.AddWithValue("@amount", amount);
        cmd.Parameters.AddWithValue("@category", category);
        cmd.Parameters.AddWithValue("@description", description);
        cmd.Parameters.AddWithValue("@refNo", (object?)refNo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@img", receiptImage);
        cmd.ExecuteNonQuery();
        using var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", conn);
        var expenseId = Convert.ToInt32(idCmd.ExecuteScalar());
        _ = SyncService.SyncExpense(new Expense { Id = expenseId, Amount = amount, Category = category, Description = description, ReferenceNo = refNo, CashierUsername = username, Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), ReceiptImage = receiptImage });
    }

    public static List<Expense> GetExpensesByCashier(string username)
    {
        var list = new List<Expense>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand(
            "SELECT * FROM Expenses WHERE CashierUsername = @username ORDER BY Timestamp DESC", conn);
        cmd.Parameters.AddWithValue("@username", username);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(MapExpense(rdr));
        }
        return list;
    }

    public static decimal GetTotalExpensesForCurrentShift(string username)
    {
        var since = DailyCloseService.GetLastCloseTime();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT COALESCE(SUM(Amount), 0) FROM Expenses WHERE CashierUsername = @username";
        if (!string.IsNullOrEmpty(since))
            sql += " AND Timestamp > @since";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@username", username);
        if (!string.IsNullOrEmpty(since))
            cmd.Parameters.AddWithValue("@since", since);
        return Convert.ToDecimal(cmd.ExecuteScalar());
    }

    public static List<Expense> GetExpensesForCurrentShift()
    {
        var since = DailyCloseService.GetLastCloseTime();
        var list = new List<Expense>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM Expenses";
        if (!string.IsNullOrEmpty(since))
            sql += " WHERE Timestamp > @since";
        sql += " ORDER BY Timestamp DESC";
        using var cmd = new SQLiteCommand(sql, conn);
        if (!string.IsNullOrEmpty(since))
            cmd.Parameters.AddWithValue("@since", since);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(MapExpense(rdr));
        }
        return list;
    }

    public static List<Expense> GetExpensesBetween(string? since, DateTime end)
    {
        var list = new List<Expense>();
        var endStr = end.ToString("yyyy-MM-dd HH:mm:ss");
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM Expenses WHERE Timestamp <= @end";
        if (!string.IsNullOrEmpty(since))
            sql += " AND Timestamp > @since";
        sql += " ORDER BY Timestamp DESC";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@end", endStr);
        if (!string.IsNullOrEmpty(since))
            cmd.Parameters.AddWithValue("@since", since);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            list.Add(MapExpense(rdr));
        return list;
    }

    public static decimal GetTotalExpensesByCategoryForCurrentShift(string category)
    {
        var since = DailyCloseService.GetLastCloseTime();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT COALESCE(SUM(Amount), 0) FROM Expenses WHERE Category = @category";
        if (!string.IsNullOrEmpty(since))
            sql += " AND Timestamp > @since";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@category", category);
        if (!string.IsNullOrEmpty(since))
            cmd.Parameters.AddWithValue("@since", since);
        return Convert.ToDecimal(cmd.ExecuteScalar());
    }

    private static Expense MapExpense(SQLiteDataReader rdr)
    {
        return new Expense
        {
            Id = Convert.ToInt32(rdr["Id"]),
            Amount = Convert.ToDecimal(rdr["Amount"]),
            Category = rdr["Category"].ToString() ?? "",
            Description = rdr["Description"].ToString() ?? "",
            ReferenceNo = rdr["ReferenceNo"]?.ToString(),
            CashierUsername = rdr["CashierUsername"].ToString() ?? "",
            ReceiptImage = SafeString(rdr, "ReceiptImage"),
            Timestamp = rdr["Timestamp"].ToString() ?? ""
        };
    }

    private static string SafeString(SQLiteDataReader rdr, string column)
    {
        try { return rdr[column]?.ToString() ?? ""; }
        catch { return ""; }
    }
}
