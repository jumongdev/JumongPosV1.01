using System.Data;
using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;
using Npgsql;

namespace JumongPosV1._01.Services;

public class CustomerService
{
    private static string StoreId => SyncService.StoreId;

    public static async Task TryWriteToPgAsync(Customer c)
    {
        if (!CloudDatabaseHelper.IsConfigured) return;
        try
        {
            using var pgConn = CloudDatabaseHelper.GetConnection()!;
            await pgConn.OpenAsync();
            await CloudDatabaseHelper.EnsureSchemaAsync(pgConn);
            var sql = @"INSERT INTO customers (pos_id, store_id, name, phone, email, address, loyalty_points, credit_balance, credit_limit, is_active)
                        VALUES (@pid, @sid, @n, @ph, @e, @a, @lp, @cb, @cl, @act)
                        ON CONFLICT (store_id, pos_id) DO UPDATE SET
                            name=@n, phone=@ph, email=@e, address=@a, loyalty_points=@lp,
                            credit_balance=@cb, credit_limit=@cl, is_active=@act";
            using var cmd = new NpgsqlCommand(sql, pgConn);
            cmd.Parameters.AddWithValue("pid", c.Id);
            cmd.Parameters.AddWithValue("sid", StoreId);
            cmd.Parameters.AddWithValue("n", c.Name);
            cmd.Parameters.AddWithValue("ph", (object?)c.Phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("e", (object?)c.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("a", c.Address);
            cmd.Parameters.AddWithValue("lp", c.LoyaltyPoints);
            cmd.Parameters.AddWithValue("cb", c.CreditBalance);
            cmd.Parameters.AddWithValue("cl", c.CreditLimit);
            cmd.Parameters.AddWithValue("act", c.IsActive ? 1 : 0);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { }
    }

    public static List<Customer> GetAll()
    {
        var list = new List<Customer>();
        if (CloudDatabaseHelper.IsConfigured)
        {
            try
            {
                using var pgConn = CloudDatabaseHelper.GetConnection()!;
                pgConn.Open();
                using var cmd = new NpgsqlCommand("SELECT * FROM customers ORDER BY name", pgConn);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read()) list.Add(MapPg(rdr));
                if (list.Count > 0) return list;
            }
            catch { }
        }
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd2 = new SQLiteCommand("SELECT * FROM Customers ORDER BY Name", conn);
        using var rdr2 = cmd2.ExecuteReader();
        while (rdr2.Read()) list.Add(Map(rdr2));
        return list;
    }

    public static Customer? GetById(int id)
    {
        if (CloudDatabaseHelper.IsConfigured)
        {
            try
            {
                using var pgConn = CloudDatabaseHelper.GetConnection()!;
                pgConn.Open();
                using var cmd = new NpgsqlCommand("SELECT * FROM customers WHERE pos_id = @id AND store_id = @sid", pgConn);
                cmd.Parameters.AddWithValue("id", id);
                cmd.Parameters.AddWithValue("sid", StoreId);
                using var rdr = cmd.ExecuteReader();
                if (rdr.Read()) return MapPg(rdr);
            }
            catch { }
        }
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd2 = new SQLiteCommand("SELECT * FROM Customers WHERE Id = @id", conn);
        cmd2.Parameters.AddWithValue("@id", id);
        using var rdr2 = cmd2.ExecuteReader();
        if (rdr2.Read()) return Map(rdr2);
        return null;
    }

    public static Customer? GetByPhone(string phone)
    {
        if (CloudDatabaseHelper.IsConfigured)
        {
            try
            {
                using var pgConn = CloudDatabaseHelper.GetConnection()!;
                pgConn.Open();
                using var cmd = new NpgsqlCommand("SELECT * FROM customers WHERE phone = @p", pgConn);
                cmd.Parameters.AddWithValue("p", phone);
                using var rdr = cmd.ExecuteReader();
                if (rdr.Read()) return MapPg(rdr);
            }
            catch { }
        }
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd2 = new SQLiteCommand("SELECT * FROM Customers WHERE Phone = @p", conn);
        cmd2.Parameters.AddWithValue("@p", phone);
        using var rdr2 = cmd2.ExecuteReader();
        if (rdr2.Read()) return Map(rdr2);
        return null;
    }

    public static bool IsPhoneTaken(string phone, int excludeId = 0)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT COUNT(*) FROM Customers WHERE Phone = @p AND Id != @eid";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@p", phone);
        cmd.Parameters.AddWithValue("@eid", excludeId);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public static string? Save(Customer c, string modifiedBy = "")
    {
        if (!string.IsNullOrEmpty(c.Phone) && IsPhoneTaken(c.Phone, c.Id))
            return $"Phone number '{c.Phone}' already exists for another customer.";

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        if (c.Id == 0)
        {
            var sql = "INSERT INTO Customers (Name, Phone, Email, Address, CreditLimit, IsActive, ModifiedBy) VALUES (@n, @p, @e, @a, @cl, @act, @mb)";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@n", c.Name);
            cmd.Parameters.AddWithValue("@p", c.Phone);
            cmd.Parameters.AddWithValue("@e", c.Email);
            cmd.Parameters.AddWithValue("@a", c.Address);
            cmd.Parameters.AddWithValue("@cl", c.CreditLimit);
            cmd.Parameters.AddWithValue("@act", c.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@mb", modifiedBy);
            cmd.ExecuteNonQuery();
            using var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", conn);
            c.Id = Convert.ToInt32(idCmd.ExecuteScalar());
        }
        else
        {
            var sql = "UPDATE Customers SET Name=@n, Phone=@p, Email=@e, Address=@a, CreditLimit=@cl, IsActive=@act, ModifiedBy=@mb WHERE Id=@id";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@n", c.Name);
            cmd.Parameters.AddWithValue("@p", c.Phone);
            cmd.Parameters.AddWithValue("@e", c.Email);
            cmd.Parameters.AddWithValue("@a", c.Address);
            cmd.Parameters.AddWithValue("@cl", c.CreditLimit);
            cmd.Parameters.AddWithValue("@act", c.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", c.Id);
            cmd.Parameters.AddWithValue("@mb", modifiedBy);
            cmd.ExecuteNonQuery();
        }
        _ = TryWriteToPgAsync(c);
        _ = SyncService.SyncCustomer(c);
        return null;
    }

    public static int RemoveDuplicates()
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var deleted = 0;

        var sql = @"SELECT MIN(Id) AS KeepId, Name, Phone, COUNT(*) AS Cnt
                     FROM Customers
                     GROUP BY Name, Phone
                     HAVING COUNT(*) > 1";
        using var cmd = new SQLiteCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        var groups = new List<(int KeepId, string Name, string Phone)>();
        while (rdr.Read())
            groups.Add((Convert.ToInt32(rdr["KeepId"]), rdr["Name"].ToString()!, rdr["Phone"].ToString()!));

        foreach (var g in groups)
        {
            var del = new SQLiteCommand(
                "DELETE FROM Customers WHERE Name = @n AND Phone = @p AND Id != @keep", conn);
            del.Parameters.AddWithValue("@n", g.Name);
            del.Parameters.AddWithValue("@p", g.Phone);
            del.Parameters.AddWithValue("@keep", g.KeepId);
            deleted += del.ExecuteNonQuery();
        }
        return deleted;
    }

    public static void Delete(int id)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("DELETE FROM Customers WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        if (CloudDatabaseHelper.IsConfigured)
        {
            try
            {
                using var pgConn = CloudDatabaseHelper.GetConnection()!;
                pgConn.Open();
                using var pgCmd = new NpgsqlCommand("DELETE FROM customers WHERE pos_id = @id AND store_id = @sid", pgConn);
                pgCmd.Parameters.AddWithValue("id", id);
                pgCmd.Parameters.AddWithValue("sid", StoreId);
                pgCmd.ExecuteNonQuery();
            }
            catch { }
        }
    }

    public static int RemoveDuplicatesNoPoints()
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var deleted = 0;

        var sql = @"SELECT MIN(Id) AS KeepId, Name, COUNT(*) AS Cnt
                     FROM Customers
                     WHERE LoyaltyPoints = 0
                     GROUP BY Name
                     HAVING COUNT(*) > 1";
        using var cmd = new SQLiteCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        var groups = new List<(int KeepId, string Name)>();
        while (rdr.Read())
            groups.Add((Convert.ToInt32(rdr["KeepId"]), rdr["Name"].ToString()!));

        foreach (var g in groups)
        {
            var del = new SQLiteCommand(
                "DELETE FROM Customers WHERE Name = @n AND Id != @keep AND LoyaltyPoints = 0", conn);
            del.Parameters.AddWithValue("@n", g.Name);
            del.Parameters.AddWithValue("@keep", g.KeepId);
            deleted += del.ExecuteNonQuery();
        }
        return deleted;
    }

    public static void UpdateLoyaltyPoints(int customerId, int points)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("UPDATE Customers SET LoyaltyPoints = @pts WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@pts", points);
        cmd.Parameters.AddWithValue("@id", customerId);
        cmd.ExecuteNonQuery();
        if (CloudDatabaseHelper.IsConfigured)
        {
            try
            {
                using var pgConn = CloudDatabaseHelper.GetConnection()!;
                pgConn.Open();
                using var pgCmd = new NpgsqlCommand("UPDATE customers SET loyalty_points = @pts WHERE pos_id = @id AND store_id = @sid", pgConn);
                pgCmd.Parameters.AddWithValue("pts", points);
                pgCmd.Parameters.AddWithValue("id", customerId);
                pgCmd.Parameters.AddWithValue("sid", StoreId);
                pgCmd.ExecuteNonQuery();
            }
            catch { }
        }
    }

    public static void UpdateCreditBalance(int customerId, decimal newBalance)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("UPDATE Customers SET CreditBalance = @bal WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@bal", newBalance);
        cmd.Parameters.AddWithValue("@id", customerId);
        cmd.ExecuteNonQuery();
        if (CloudDatabaseHelper.IsConfigured)
        {
            try
            {
                using var pgConn = CloudDatabaseHelper.GetConnection()!;
                pgConn.Open();
                using var pgCmd = new NpgsqlCommand("UPDATE customers SET credit_balance = @bal WHERE pos_id = @id AND store_id = @sid", pgConn);
                pgCmd.Parameters.AddWithValue("bal", newBalance);
                pgCmd.Parameters.AddWithValue("id", customerId);
                pgCmd.Parameters.AddWithValue("sid", StoreId);
                pgCmd.ExecuteNonQuery();
            }
            catch { }
        }
    }

    public static List<Customer> Search(string keyword)
    {
        var list = new List<Customer>();
        if (CloudDatabaseHelper.IsConfigured)
        {
            try
            {
                using var pgConn = CloudDatabaseHelper.GetConnection()!;
                pgConn.Open();
                using var cmd = new NpgsqlCommand("SELECT * FROM customers WHERE name ILIKE @kw OR phone ILIKE @kw ORDER BY name LIMIT 30", pgConn);
                cmd.Parameters.AddWithValue("kw", $"%{keyword}%");
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read()) list.Add(MapPg(rdr));
                if (list.Count > 0) return list;
            }
            catch { }
        }
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd2 = new SQLiteCommand("SELECT * FROM Customers WHERE Name LIKE @kw OR Phone LIKE @kw ORDER BY Name LIMIT 30", conn);
        cmd2.Parameters.AddWithValue("@kw", $"%{keyword}%");
        using var rdr2 = cmd2.ExecuteReader();
        while (rdr2.Read()) list.Add(Map(rdr2));
        return list;
    }

    private static Customer Map(SQLiteDataReader rdr)
    {
        return new Customer
        {
            Id = Convert.ToInt32(rdr["Id"]),
            Name = rdr["Name"].ToString() ?? "",
            Phone = rdr["Phone"].ToString() ?? "",
            Email = rdr["Email"].ToString() ?? "",
            Address = rdr["Address"]?.ToString() ?? "",
            LoyaltyPoints = Convert.ToInt32(rdr["LoyaltyPoints"]),
            CreditBalance = rdr["CreditBalance"] != DBNull.Value ? Convert.ToDecimal(rdr["CreditBalance"]) : 0,
            CreditLimit = rdr["CreditLimit"] != DBNull.Value ? Convert.ToDecimal(rdr["CreditLimit"]) : 0,
            IsActive = rdr["IsActive"] != DBNull.Value ? Convert.ToBoolean(rdr["IsActive"]) : true,
            CreatedAt = DateTime.SpecifyKind(DateTime.Parse(rdr["CreatedAt"].ToString()!), DateTimeKind.Local)
        };
    }

    private static Customer MapPg(NpgsqlDataReader rdr)
    {
        return new Customer
        {
            Id = Convert.ToInt32(rdr["pos_id"]),
            Name = rdr["name"].ToString() ?? "",
            Phone = rdr["phone"]?.ToString() ?? "",
            Email = rdr["email"]?.ToString() ?? "",
            Address = rdr["address"]?.ToString() ?? "",
            LoyaltyPoints = Convert.ToInt32(rdr["loyalty_points"]),
            CreditBalance = rdr["credit_balance"] != DBNull.Value ? Convert.ToDecimal(rdr["credit_balance"]) : 0,
            CreditLimit = rdr["credit_limit"] != DBNull.Value ? Convert.ToDecimal(rdr["credit_limit"]) : 0,
            IsActive = rdr["is_active"] != DBNull.Value ? Convert.ToInt32(rdr["is_active"]) == 1 : true,
            CreatedAt = DateTime.TryParse(rdr["created_at"]?.ToString(), out var dt) ? dt : TimeHelper.Now
        };
    }
}
