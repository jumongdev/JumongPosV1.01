using System.Data.SQLite;
using JumongPosV1._01.Data;

namespace JumongPosV1._01.Services;

public static class ShiftSessionService
{
    public static bool IsShiftActive()
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'ShiftSessionActive'", conn);
            return cmd.ExecuteScalar()?.ToString() == "1";
        }
        catch { return false; }
    }

    public static decimal GetOpeningBalance()
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'ShiftOpeningBalance'", conn);
            var val = cmd.ExecuteScalar()?.ToString();
            return decimal.TryParse(val, out var d) ? d : 0m;
        }
        catch { return 0m; }
    }

    public static string? GetStartedBy()
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'ShiftStartedBy'", conn);
            return cmd.ExecuteScalar()?.ToString();
        }
        catch { return null; }
    }

    public static string? GetStartedAt()
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'ShiftStartedAt'", conn);
            return cmd.ExecuteScalar()?.ToString();
        }
        catch { return null; }
    }

    public static void StartSession(decimal openingBalance, string startedBy)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        Upsert(conn, "ShiftSessionActive", "1");
        Upsert(conn, "ShiftOpeningBalance", openingBalance.ToString("F2"));
        Upsert(conn, "ShiftStartedBy", startedBy);
        Upsert(conn, "ShiftStartedAt", TimeHelper.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    public static void EndSession()
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        Upsert(conn, "ShiftSessionActive", "0");
    }

    private static void Upsert(SQLiteConnection conn, string key, string value)
    {
        using var cmd = new SQLiteCommand("INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@k, @v)", conn);
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", value);
        cmd.ExecuteNonQuery();
    }
}
