using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public class AuditLogService
{
    public static void Log(string action, string settingKey, string oldValue, string newValue, string userName)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        LogTransaction(conn, action, settingKey, oldValue, newValue, userName);
    }

    public static void LogTransaction(SQLiteConnection conn, string action, string settingKey, string oldValue, string newValue, string userName)
    {
        var sql = @"INSERT INTO AuditLog (Action, SettingKey, OldValue, NewValue, UserName)
                    VALUES (@act, @key, @old, @new, @user)";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@act", action);
        cmd.Parameters.AddWithValue("@key", settingKey);
        cmd.Parameters.AddWithValue("@old", oldValue);
        cmd.Parameters.AddWithValue("@new", newValue);
        cmd.Parameters.AddWithValue("@user", userName);
        cmd.ExecuteNonQuery();
    }

    public static List<AuditLog> GetHistory(int limit = 100)
    {
        var list = new List<AuditLog>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT * FROM AuditLog ORDER BY Id DESC LIMIT @lim", conn);
        cmd.Parameters.AddWithValue("@lim", limit);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new AuditLog
            {
                Id = Convert.ToInt32(rdr["Id"]),
                Action = rdr["Action"].ToString() ?? "",
                SettingKey = rdr["SettingKey"].ToString() ?? "",
                OldValue = rdr["OldValue"].ToString() ?? "",
                NewValue = rdr["NewValue"].ToString() ?? "",
                UserName = rdr["UserName"].ToString() ?? "",
                CreatedAt = rdr["CreatedAt"].ToString() ?? ""
            });
        }
        return list;
    }
}
