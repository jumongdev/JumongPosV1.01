using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public static class AuthService
{
    public static User? Login(string username, string password)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM Users WHERE Username = @u AND PasswordHash = @p AND IsActive = 1";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", password);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read())
        {
            return new User
            {
                Id = Convert.ToInt32(rdr["Id"]),
                Username = rdr["Username"].ToString() ?? "",
                PasswordHash = rdr["PasswordHash"].ToString() ?? "",
                Role = rdr["Role"].ToString() ?? "Cashier",
                IsActive = Convert.ToBoolean(rdr["IsActive"])
            };
        }
        return null;
    }
}
