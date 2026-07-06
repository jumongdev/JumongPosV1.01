using System.Data;
using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;
using Npgsql;

namespace JumongPosV1._01.Services;

public static class UserService
{
    private static string StoreId => SyncService.StoreId;

    public static async Task TryWriteToPgAsync(User u)
    {
        if (!CloudDatabaseHelper.IsConfigured) return;
        try
        {
            using var pgConn = CloudDatabaseHelper.GetConnection()!;
            await pgConn.OpenAsync();
            await CloudDatabaseHelper.EnsureSchemaAsync(pgConn);
            var sql = @"INSERT INTO users (pos_id, store_id, username, password_hash, full_name, role, is_active)
                        VALUES (@pid, @sid, @un, @ph, @fn, @r, @a)
                        ON CONFLICT (store_id, pos_id) DO UPDATE SET
                            username=@un, password_hash=@ph, full_name=@fn, role=@r, is_active=@a";
            using var cmd = new NpgsqlCommand(sql, pgConn);
            cmd.Parameters.AddWithValue("pid", u.Id);
            cmd.Parameters.AddWithValue("sid", StoreId);
            cmd.Parameters.AddWithValue("un", u.Username);
            cmd.Parameters.AddWithValue("ph", (object?)u.PasswordHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("fn", u.FullName);
            cmd.Parameters.AddWithValue("r", u.Role);
            cmd.Parameters.AddWithValue("a", u.IsActive ? 1 : 0);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { }
    }

    public static List<User> GetAll()
    {
        var list = new List<User>();
        if (CloudDatabaseHelper.IsConfigured)
        {
            try
            {
                using var pgConn = CloudDatabaseHelper.GetConnection()!;
                pgConn.Open();
                using var cmd = new NpgsqlCommand("SELECT * FROM users ORDER BY username", pgConn);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read()) list.Add(MapPg(rdr));
                if (list.Count > 0) return list;
            }
            catch { }
        }
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd2 = new SQLiteCommand("SELECT * FROM Users ORDER BY Username", conn);
        using var rdr2 = cmd2.ExecuteReader();
        while (rdr2.Read()) list.Add(Map(rdr2));
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
        _ = TryWriteToPgAsync(u);
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
        if (CloudDatabaseHelper.IsConfigured)
        {
            try
            {
                using var pgConn = CloudDatabaseHelper.GetConnection()!;
                pgConn.Open();
                using var pgCmd = new NpgsqlCommand("DELETE FROM users WHERE pos_id = @id AND store_id = @sid", pgConn);
                pgCmd.Parameters.AddWithValue("id", id);
                pgCmd.Parameters.AddWithValue("sid", StoreId);
                pgCmd.ExecuteNonQuery();
            }
            catch { }
        }
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

    private static User MapPg(NpgsqlDataReader rdr)
    {
        return new User
        {
            Id = Convert.ToInt32(rdr["pos_id"]),
            Username = rdr["username"].ToString() ?? "",
            FullName = rdr["full_name"]?.ToString() ?? "",
            PasswordHash = rdr["password_hash"]?.ToString() ?? "",
            Role = rdr["role"]?.ToString() ?? "Cashier",
            IsActive = Convert.ToInt32(rdr["is_active"]) == 1
        };
    }

    public static void SyncFromCloud(List<System.Text.Json.JsonElement> cloudUsers)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        foreach (var cu in cloudUsers)
        {
            var posId = cu.GetProperty("posId").GetInt32();
            var username = cu.GetProperty("username").GetString() ?? "";
            var fullName = cu.TryGetProperty("fullName", out var fn) ? fn.GetString() ?? "" : "";
            var role = cu.TryGetProperty("role", out var rl) ? rl.GetString() ?? "Cashier" : "Cashier";
            var isActive = cu.TryGetProperty("isActive", out var ia) ? ia.GetBoolean() : true;
            var passwordHash = cu.TryGetProperty("passwordHash", out var ph) ? ph.GetString() ?? "12345" : "12345";

            // Check if user exists locally
            using var chk = new SQLiteCommand("SELECT Id, PasswordHash FROM Users WHERE Username = @u", conn);
            chk.Parameters.AddWithValue("@u", username);
            var existing = chk.ExecuteScalar();
            if (existing != null && existing != DBNull.Value)
            {
                // User exists — update fields but keep local password
                using var upd = new SQLiteCommand(
                    "UPDATE Users SET FullName=@f, Role=@r, IsActive=@a WHERE Username=@u", conn);
                upd.Parameters.AddWithValue("@u", username);
                upd.Parameters.AddWithValue("@f", fullName);
                upd.Parameters.AddWithValue("@r", role);
                upd.Parameters.AddWithValue("@a", isActive ? 1 : 0);
                upd.ExecuteNonQuery();
            }
            else
            {
                // New user — create with cloud password
                using var ins = new SQLiteCommand(
                    "INSERT INTO Users (Username, FullName, PasswordHash, Role, IsActive) VALUES (@u, @f, @p, @r, @a)", conn);
                ins.Parameters.AddWithValue("@u", username);
                ins.Parameters.AddWithValue("@f", fullName);
                ins.Parameters.AddWithValue("@p", passwordHash);
                ins.Parameters.AddWithValue("@r", role);
                ins.Parameters.AddWithValue("@a", isActive ? 1 : 0);
                ins.ExecuteNonQuery();
            }
        }
    }
}
