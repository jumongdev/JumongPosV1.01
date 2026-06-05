using System.Text.Json;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public static class DataExporter
{
    public static void ExportAll(string path)
    {
        var data = new ExportData
        {
            Products = ProductService.GetAll(),
            Customers = CustomerService.GetAll(),
            Users = UserService.GetAll()
        };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        MessageBox.Show($"Exported {data.Products.Count} products, {data.Customers.Count} customers, {data.Users.Count} users to:\n{path}",
            "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static void ImportAll(string path)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show("File not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<ExportData>(json);
        if (data == null)
        {
            MessageBox.Show("Invalid file format.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();

        using var trans = conn.BeginTransaction();

        var delProducts = new System.Data.SQLite.SQLiteCommand("DELETE FROM ProductUnits", conn);
        delProducts.ExecuteNonQuery();
        delProducts.CommandText = "DELETE FROM Products";
        delProducts.ExecuteNonQuery();

        var delCustomers = new System.Data.SQLite.SQLiteCommand("DELETE FROM Customers", conn);
        delCustomers.ExecuteNonQuery();

        var delUsers = new System.Data.SQLite.SQLiteCommand("DELETE FROM Users", conn);
        delUsers.ExecuteNonQuery();

        foreach (var p in data.Products)
        {
            var cmd = new System.Data.SQLite.SQLiteCommand(
                "INSERT INTO Products (Name, Barcode, Category, Price, Cost, StockQty, IsActive) VALUES (@n, @b, @c, @p, @co, @s, @a)", conn);
            cmd.Parameters.AddWithValue("@n", p.Name);
            cmd.Parameters.AddWithValue("@b", p.Barcode ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@c", p.Category ?? "");
            cmd.Parameters.AddWithValue("@p", p.Price);
            cmd.Parameters.AddWithValue("@co", p.Cost);
            cmd.Parameters.AddWithValue("@s", p.StockQty);
            cmd.Parameters.AddWithValue("@a", p.IsActive ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        foreach (var c in data.Customers)
        {
            var cmd = new System.Data.SQLite.SQLiteCommand(
                "INSERT INTO Customers (Name, Phone, Email) VALUES (@n, @p, @e)", conn);
            cmd.Parameters.AddWithValue("@n", c.Name);
            cmd.Parameters.AddWithValue("@p", c.Phone ?? "");
            cmd.Parameters.AddWithValue("@e", c.Email ?? "");
            cmd.ExecuteNonQuery();
        }

        foreach (var u in data.Users)
        {
            var cmd = new System.Data.SQLite.SQLiteCommand(
                "INSERT INTO Users (Username, FullName, PasswordHash, Role, IsActive) VALUES (@u, @f, @p, @r, @a)", conn);
            cmd.Parameters.AddWithValue("@u", u.Username);
            cmd.Parameters.AddWithValue("@f", u.FullName);
            cmd.Parameters.AddWithValue("@p", u.PasswordHash);
            cmd.Parameters.AddWithValue("@r", u.Role);
            cmd.Parameters.AddWithValue("@a", u.IsActive ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        trans.Commit();

        MessageBox.Show($"Imported {data.Products.Count} products, {data.Customers.Count} customers, {data.Users.Count} users.\nPlease restart the app.",
            "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static void ExportProducts(string path)
    {
        var products = ProductService.GetAll();
        var json = JsonSerializer.Serialize(products, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        MessageBox.Show($"Exported {products.Count} products to:\n{path}",
            "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static void ImportAndSyncProducts(string path)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show("File not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var json = File.ReadAllText(path);
        var products = JsonSerializer.Deserialize<List<Product>>(json);
        if (products == null || products.Count == 0)
        {
            MessageBox.Show("No products found in file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var rdCmd = new System.Data.SQLite.SQLiteCommand("SELECT Barcode, Name FROM Products", conn))
        using (var rdr = rdCmd.ExecuteReader())
        {
            while (rdr.Read())
            {
                var bc = rdr["Barcode"]?.ToString() ?? "";
                var nm = rdr["Name"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(bc)) existing.Add("BC:" + bc);
                existing.Add("NM:" + nm.ToLowerInvariant());
            }
        }

        int updated = 0, added = 0, skipped = 0;
        foreach (var p in products)
        {
            var bcKey = string.IsNullOrEmpty(p.Barcode) ? null : "BC:" + p.Barcode;
            var nmKey = "NM:" + (p.Name ?? "").ToLowerInvariant();

            bool found = (bcKey != null && existing.Contains(bcKey)) || existing.Contains(nmKey);

            if (found)
            {
                System.Data.SQLite.SQLiteCommand upd;
                if (!string.IsNullOrEmpty(p.Barcode))
                {
                    upd = new System.Data.SQLite.SQLiteCommand(
                        "UPDATE Products SET Price=@p, Cost=@c, Category=@cat WHERE Barcode=@b AND Barcode != ''", conn);
                    upd.Parameters.AddWithValue("@b", p.Barcode);
                }
                else
                {
                    upd = new System.Data.SQLite.SQLiteCommand(
                        "UPDATE Products SET Price=@p, Cost=@c, Category=@cat WHERE Name=@n", conn);
                    upd.Parameters.AddWithValue("@n", p.Name ?? "");
                }
                upd.Parameters.AddWithValue("@p", p.Price);
                upd.Parameters.AddWithValue("@c", p.Cost);
                upd.Parameters.AddWithValue("@cat", p.Category ?? "");
                var rows = upd.ExecuteNonQuery();
                if (rows > 0) updated++; else skipped++;
            }
            else
            {
                var ins = new System.Data.SQLite.SQLiteCommand(
                    "INSERT INTO Products (Name, Barcode, Category, Price, Cost, StockQty, IsActive) VALUES (@n, @b, @cat, @p, @c, 0, @a)", conn);
                ins.Parameters.AddWithValue("@n", p.Name);
                ins.Parameters.AddWithValue("@b", p.Barcode ?? (object)DBNull.Value);
                ins.Parameters.AddWithValue("@cat", p.Category ?? "");
                ins.Parameters.AddWithValue("@p", p.Price);
                ins.Parameters.AddWithValue("@c", p.Cost);
                ins.Parameters.AddWithValue("@a", p.IsActive ? 1 : 0);
                ins.ExecuteNonQuery();

                if (bcKey != null) existing.Add(bcKey);
                existing.Add(nmKey);
                added++;
            }
        }

        trans.Commit();

        MessageBox.Show($"Import complete: {added} added, {updated} updated, {skipped} skipped.\nPrice, Cost, Category synced. Stock not affected.\nPlease restart the app.",
            "Import & Sync Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private class ExportData
    {
        public List<Product> Products { get; set; } = new();
        public List<Customer> Customers { get; set; } = new();
        public List<User> Users { get; set; } = new();
    }
}
