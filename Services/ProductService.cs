using System.Data;
using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;
using Npgsql;

namespace JumongPosV1._01.Services;

public class ProductService
{
    private static string StoreId => SyncService.StoreId;

    public static async Task TryWriteToPgAsync(Product p)
    {
        if (!CloudDatabaseHelper.IsConfigured) return;
        try
        {
            using var pgConn = CloudDatabaseHelper.GetConnection()!;
            await pgConn.OpenAsync();
            await CloudDatabaseHelper.EnsureSchemaAsync(pgConn);
            var sql = @"INSERT INTO products (pos_id, store_id, name, barcode, category, price, cost, stock_qty, is_active, image_data)
                        VALUES (@pid, @sid, @n, @b, @c, @p, @co, @s, @a, @img)
                        ON CONFLICT (store_id, pos_id) DO UPDATE SET
                            name=@n, barcode=@b, category=@c, price=@p, cost=@co, stock_qty=@s, is_active=@a, image_data=@img";
            using var cmd = new NpgsqlCommand(sql, pgConn);
            cmd.Parameters.AddWithValue("pid", p.Id);
            cmd.Parameters.AddWithValue("sid", StoreId);
            cmd.Parameters.AddWithValue("n", p.Name);
            cmd.Parameters.AddWithValue("b", (object?)p.Barcode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("c", p.Category);
            cmd.Parameters.AddWithValue("p", p.Price);
            cmd.Parameters.AddWithValue("co", p.Cost);
            cmd.Parameters.AddWithValue("s", p.StockQty);
            cmd.Parameters.AddWithValue("a", p.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("img", (object?)p.ImageData ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { }
    }

    public static List<Product> GetAll()
    {
        var list = new List<Product>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT * FROM Products WHERE IsActive = 1 ORDER BY Name", conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(Map(rdr));
        return list;
    }

    public static Product? GetById(int id)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT * FROM Products WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read()) return Map(rdr);
        return null;
    }

    public static Product? GetByBarcode(string barcode)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT * FROM Products WHERE Barcode = @barcode AND IsActive = 1", conn);
        cmd.Parameters.AddWithValue("@barcode", barcode);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read()) return Map(rdr);
        return null;
    }

    public static void Save(Product p, string modifiedBy = "")
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();

        if (!string.IsNullOrEmpty(p.Barcode))
        {
            var checkSql = p.Id == 0
                ? "SELECT COUNT(*) FROM Products WHERE Barcode = @b"
                : "SELECT COUNT(*) FROM Products WHERE Barcode = @b AND Id != @id";
            using var checkCmd = new SQLiteCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("@b", p.Barcode);
            if (p.Id != 0) checkCmd.Parameters.AddWithValue("@id", p.Id);
            var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
            if (exists)
                throw new InvalidOperationException($"Barcode '{p.Barcode}' is already in use by another product.");
        }

        if (p.Id == 0)
        {
            var sql = "INSERT INTO Products (Name, Barcode, Category, Price, Cost, StockQty, ModifiedBy) VALUES (@n, @b, @c, @p, @co, @s, @mb)";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@n", p.Name);
            cmd.Parameters.AddWithValue("@b", p.Barcode);
            cmd.Parameters.AddWithValue("@c", p.Category);
            cmd.Parameters.AddWithValue("@p", p.Price);
            cmd.Parameters.AddWithValue("@co", p.Cost);
            cmd.Parameters.AddWithValue("@s", p.StockQty);
            cmd.Parameters.AddWithValue("@mb", modifiedBy);
            cmd.ExecuteNonQuery();
            using var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", conn);
            p.Id = Convert.ToInt32(idCmd.ExecuteScalar());
        }
        else
        {
            var sql = "UPDATE Products SET Name=@n, Barcode=@b, Category=@c, Price=@p, Cost=@co, StockQty=@s, IsActive=@a, ModifiedBy=@mb WHERE Id=@id";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@n", p.Name);
            cmd.Parameters.AddWithValue("@b", p.Barcode);
            cmd.Parameters.AddWithValue("@c", p.Category);
            cmd.Parameters.AddWithValue("@p", p.Price);
            cmd.Parameters.AddWithValue("@co", p.Cost);
            cmd.Parameters.AddWithValue("@s", p.StockQty);
            cmd.Parameters.AddWithValue("@a", p.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", p.Id);
            cmd.Parameters.AddWithValue("@mb", modifiedBy);
            cmd.ExecuteNonQuery();
        }
        _ = TryWriteToPgAsync(p);
        _ = SyncService.SyncProduct(p);
    }

    public static List<string> GetCategories()
    {
        var list = new List<string>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT DISTINCT Category FROM Products WHERE IsActive = 1 AND Category != '' ORDER BY Category", conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(rdr.GetString(0));
        return list;
    }

    public static List<Product> Search(string keyword, string? category = null, string? stockFilter = null)
    {
        var list = new List<Product>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM Products WHERE IsActive = 1";
        if (!string.IsNullOrEmpty(keyword))
            sql += " AND (Name LIKE @q OR Barcode LIKE @q)";
        if (!string.IsNullOrEmpty(category))
            sql += " AND Category = @cat";
        if (stockFilter == "low")
            sql += " AND StockQty > 0 AND StockQty <= @thresh";
        else if (stockFilter == "out")
            sql += " AND StockQty = 0";
        sql += " ORDER BY Name LIMIT 200";
        using var cmd = new SQLiteCommand(sql, conn);
        if (!string.IsNullOrEmpty(keyword))
            cmd.Parameters.AddWithValue("@q", $"%{keyword}%");
        if (!string.IsNullOrEmpty(category))
            cmd.Parameters.AddWithValue("@cat", category);
        if (stockFilter == "low")
            cmd.Parameters.AddWithValue("@thresh", GetLowStockThreshold());
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(Map(rdr));
        return list;
    }

    public static int GetLowStockThreshold()
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'LowStockThreshold'", conn);
            var val = cmd.ExecuteScalar()?.ToString();
            if (int.TryParse(val, out var t) && t > 0) return t;
        }
        catch { }
        return 10;
    }

    public static (int total, int lowStock, int outOfStock) GetStockStats()
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var threshold = GetLowStockThreshold();
        var cmd = new SQLiteCommand("SELECT COUNT(*), SUM(CASE WHEN StockQty = 0 THEN 1 ELSE 0 END), SUM(CASE WHEN StockQty > 0 AND StockQty <= @thresh THEN 1 ELSE 0 END) FROM Products WHERE IsActive = 1", conn);
        cmd.Parameters.AddWithValue("@thresh", threshold);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read())
            return (Convert.ToInt32(rdr[0]), rdr[2] != DBNull.Value ? Convert.ToInt32(rdr[2]) : 0, rdr[1] != DBNull.Value ? Convert.ToInt32(rdr[1]) : 0);
        return (0, 0, 0);
    }

    public static (decimal retailValue, decimal costValue) GetStockValues()
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var cmd = new SQLiteCommand("SELECT COALESCE(SUM(StockQty * Price), 0), COALESCE(SUM(StockQty * Cost), 0) FROM Products WHERE IsActive = 1", conn);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read())
            return (Convert.ToDecimal(rdr[0]), Convert.ToDecimal(rdr[1]));
        return (0, 0);
    }

    public static void Delete(int id)
    {
        // Read product before deletion for sync
        Product? product = null;
        using (var conn = DatabaseHelper.GetConnection())
        {
            conn.Open();
            using var rdr = new SQLiteCommand("SELECT * FROM Products WHERE Id = @id", conn);
            rdr.Parameters.AddWithValue("@id", id);
            using var reader = rdr.ExecuteReader();
            if (reader.Read()) product = Map(reader);
        }

        if (product == null) return;

        using var conn2 = DatabaseHelper.GetConnection();
        conn2.Open();
        var sql = "UPDATE Products SET IsActive = 0 WHERE Id = @id";
        using var cmd2 = new SQLiteCommand(sql, conn2);
        cmd2.Parameters.AddWithValue("@id", id);
        cmd2.ExecuteNonQuery();

        if (CloudDatabaseHelper.IsConfigured)
        {
            try
            {
                using var pgConn = CloudDatabaseHelper.GetConnection()!;
                pgConn.Open();
                using var pgCmd = new NpgsqlCommand("UPDATE products SET is_active = 0 WHERE pos_id = @id AND store_id = @sid", pgConn);
                pgCmd.Parameters.AddWithValue("id", id);
                pgCmd.Parameters.AddWithValue("sid", StoreId);
                pgCmd.ExecuteNonQuery();
            }
            catch { }
        }

        // Sync deletion to cloud API
        product.IsActive = false;
        _ = SyncService.SyncProduct(product);
    }

    private static Product Map(SQLiteDataReader rdr)
    {
        return new Product
        {
            Id = Convert.ToInt32(rdr["Id"]),
            Name = rdr["Name"].ToString() ?? "",
            Barcode = rdr["Barcode"].ToString() ?? "",
            Category = rdr["Category"].ToString() ?? "",
            Price = Convert.ToDecimal(rdr["Price"]),
            Cost = Convert.ToDecimal(rdr["Cost"]),
            StockQty = Convert.ToInt32(rdr["StockQty"]),
            IsActive = Convert.ToBoolean(rdr["IsActive"]),
            CreatedAt = DateTime.SpecifyKind(DateTime.Parse(rdr["CreatedAt"].ToString()!), DateTimeKind.Local),
            ImageData = rdr["image_data"]?.ToString() ?? "",
            PointsExempt = rdr["PointsExempt"] != DBNull.Value && Convert.ToBoolean(rdr["PointsExempt"]),
            PointsPerUnit = rdr["PointsPerUnit"] != DBNull.Value ? Convert.ToInt32(rdr["PointsPerUnit"]) : 0
        };
    }
}
