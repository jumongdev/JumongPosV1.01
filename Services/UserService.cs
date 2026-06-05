using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public static class UserService
{
    public static List<User> GetAll()
    {
        var list = new List<User>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT * FROM Users ORDER BY Username", conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            list.Add(Map(rdr));
        return list;
    }

    public static string? Save(User u, string modifiedBy = "")
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();

        var exists = "SELECT COUNT(*) FROM Users WHERE Username = @u AND Id != @id";
        using var chk = new SQLiteCommand(exists, conn);
        chk.Parameters.AddWithValue("@u", u.Username);
        chk.Parameters.AddWithValue("@id", u.Id);
        if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
            return $"Username '{u.Username}' is already taken.";

        if (u.Id == 0)
        {
            var sql = "INSERT INTO Users (Username, FullName, PasswordHash, Role, IsActive, ModifiedBy) VALUES (@u, @f, @p, @r, @a, @mb)";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", u.Username);
            cmd.Parameters.AddWithValue("@f", u.FullName);
            cmd.Parameters.AddWithValue("@p", u.PasswordHash);
            cmd.Parameters.AddWithValue("@r", u.Role);
            cmd.Parameters.AddWithValue("@a", u.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@mb", modifiedBy);
            cmd.ExecuteNonQuery();
            using var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", conn);
            u.Id = Convert.ToInt32(idCmd.ExecuteScalar());
        }
        else
        {
            var sql = "UPDATE Users SET Username=@u, FullName=@f, PasswordHash=@p, Role=@r, IsActive=@a, ModifiedBy=@mb WHERE Id=@id";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", u.Username);
            cmd.Parameters.AddWithValue("@f", u.FullName);
            cmd.Parameters.AddWithValue("@p", u.PasswordHash);
            cmd.Parameters.AddWithValue("@r", u.Role);
            cmd.Parameters.AddWithValue("@a", u.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", u.Id);
            cmd.Parameters.AddWithValue("@mb", modifiedBy);
            cmd.ExecuteNonQuery();
        }
        _ = SyncService.SyncUser(u);
        return null;
    }

    public static void Delete(int id)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("DELETE FROM Users WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    private static User Map(SQLiteDataReader rdr)
    {
        return new User
        {
            Id = Convert.ToInt32(rdr["Id"]),
            Username = rdr["Username"].ToString() ?? "",
            FullName = rdr["FullName"].ToString() ?? "",
            PasswordHash = rdr["PasswordHash"].ToString() ?? "",
            Role = rdr["Role"].ToString() ?? "Cashier",
            IsActive = Convert.ToBoolean(rdr["IsActive"])
        };
    }
}
