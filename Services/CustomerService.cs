using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public class CustomerService
{
    public static List<Customer> GetAll()
    {
        var list = new List<Customer>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM Customers ORDER BY Name";
        using var cmd = new SQLiteCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(Map(rdr));
        }
        return list;
    }

    public static Customer? GetById(int id)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM Customers WHERE Id = @id";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read()) return Map(rdr);
        return null;
    }

    public static Customer? GetByPhone(string phone)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM Customers WHERE Phone = @p";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@p", phone);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read()) return Map(rdr);
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

    public static void UpdateCreditBalance(int customerId, decimal newBalance)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("UPDATE Customers SET CreditBalance = @bal WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@bal", newBalance);
        cmd.Parameters.AddWithValue("@id", customerId);
        cmd.ExecuteNonQuery();
    }

    public static List<Customer> Search(string keyword)
    {
        var list = new List<Customer>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM Customers WHERE Name LIKE @kw OR Phone LIKE @kw ORDER BY Name LIMIT 30";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@kw", $"%{keyword}%");
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            list.Add(Map(rdr));
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
            CreatedAt = DateTime.Parse(rdr["CreatedAt"].ToString()!)
        };
    }
}
