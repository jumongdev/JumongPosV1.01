using System.Data;
using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;
using Npgsql;

namespace JumongPosV1._01.Services;

public class ProductUnitService
{
    private static string StoreId => SyncService.StoreId;

    private static async Task TryWriteToPgAsync(ProductUnit u)
    {
        if (!CloudDatabaseHelper.IsConfigured) return;
        try
        {
            using var pgConn = CloudDatabaseHelper.GetConnection()!;
            await pgConn.OpenAsync();
            await CloudDatabaseHelper.EnsureSchemaAsync(pgConn);
            var sql = @"INSERT INTO product_units (pos_id, store_id, product_pos_id, unit_name, price, cost, qty_per_unit, is_default)
                        VALUES (@pid, @sid, @ppid, @un, @pr, @co, @qpu, @def)
                        ON CONFLICT (store_id, product_pos_id, unit_name) DO UPDATE SET
                            price=@pr, cost=@co, qty_per_unit=@qpu, is_default=@def";
            using var cmd = new NpgsqlCommand(sql, pgConn);
            cmd.Parameters.AddWithValue("pid", u.Id);
            cmd.Parameters.AddWithValue("sid", StoreId);
            cmd.Parameters.AddWithValue("ppid", u.ProductId);
            cmd.Parameters.AddWithValue("un", u.UnitName);
            cmd.Parameters.AddWithValue("pr", u.Price);
            cmd.Parameters.AddWithValue("co", u.Cost);
            cmd.Parameters.AddWithValue("qpu", u.QtyPerUnit);
            cmd.Parameters.AddWithValue("def", u.IsDefault ? 1 : 0);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { }
    }

    public static List<ProductUnit> GetByProduct(int productId)
    {
        var list = new List<ProductUnit>();
        if (CloudDatabaseHelper.IsConfigured)
        {
            try
            {
                using var pgConn = CloudDatabaseHelper.GetConnection()!;
                pgConn.Open();
                using var cmd = new NpgsqlCommand("SELECT * FROM product_units WHERE product_pos_id = @pid ORDER BY is_default DESC, unit_name", pgConn);
                cmd.Parameters.AddWithValue("pid", productId);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read()) list.Add(MapPg(rdr));
                if (list.Count > 0) return list;
            }
            catch { }
        }
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd2 = new SQLiteCommand("SELECT * FROM ProductUnits WHERE ProductId = @pid ORDER BY IsDefault DESC, UnitName", conn);
        cmd2.Parameters.AddWithValue("@pid", productId);
        using var rdr2 = cmd2.ExecuteReader();
        while (rdr2.Read()) list.Add(Map(rdr2));
        return list;
    }

    public static ProductUnit? GetDefault(int productId)
    {
        if (CloudDatabaseHelper.IsConfigured)
        {
            try
            {
                using var pgConn = CloudDatabaseHelper.GetConnection()!;
                pgConn.Open();
                using var cmd = new NpgsqlCommand("SELECT * FROM product_units WHERE product_pos_id = @pid AND is_default = 1 LIMIT 1", pgConn);
                cmd.Parameters.AddWithValue("pid", productId);
                using var rdr = cmd.ExecuteReader();
                if (rdr.Read()) return MapPg(rdr);
            }
            catch { }
        }
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd2 = new SQLiteCommand("SELECT * FROM ProductUnits WHERE ProductId = @pid AND IsDefault = 1 LIMIT 1", conn);
        cmd2.Parameters.AddWithValue("@pid", productId);
        using var rdr2 = cmd2.ExecuteReader();
        if (rdr2.Read()) return Map(rdr2);
        return null;
    }

    public static Dictionary<int, ProductUnit> GetDefaultsByProductIds(List<int> productIds)
    {
        var result = new Dictionary<int, ProductUnit>();
        if (productIds.Count == 0) return result;

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var placeholders = string.Join(",", productIds.Select((_, i) => $"@p{i}"));
        using var cmd = new SQLiteCommand($"SELECT * FROM ProductUnits WHERE IsDefault = 1 AND ProductId IN ({placeholders})", conn);
        for (var i = 0; i < productIds.Count; i++)
            cmd.Parameters.AddWithValue($"@p{i}", productIds[i]);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            var unit = Map(rdr);
            result[unit.ProductId] = unit;
        }
        return result;
    }

    public static void Save(ProductUnit u)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        if (u.Id == 0)
        {
            if (u.IsDefault)
                ClearDefault(u.ProductId, conn);
            var sql = "INSERT INTO ProductUnits (ProductId, UnitName, Price, Cost, QtyPerUnit, IsDefault) VALUES (@pid, @un, @pr, @co, @qpu, @def)";
            using var cmd = new SQLiteCommand(sql, conn);
            SetParams(cmd, u);
            cmd.ExecuteNonQuery();
            using var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", conn);
            u.Id = Convert.ToInt32(idCmd.ExecuteScalar());
        }
        else
        {
            if (u.IsDefault)
                ClearDefault(u.ProductId, conn);
            var sql = "UPDATE ProductUnits SET UnitName=@un, Price=@pr, Cost=@co, QtyPerUnit=@qpu, IsDefault=@def WHERE Id=@id";
            using var cmd = new SQLiteCommand(sql, conn);
            SetParams(cmd, u);
            cmd.Parameters.AddWithValue("@id", u.Id);
            cmd.ExecuteNonQuery();
        }
        _ = TryWriteToPgAsync(u);
    }

    public static void Delete(int id)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "DELETE FROM ProductUnits WHERE Id = @id";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        if (CloudDatabaseHelper.IsConfigured)
        {
            try
            {
                using var pgConn = CloudDatabaseHelper.GetConnection()!;
                pgConn.Open();
                using var pgCmd = new NpgsqlCommand("DELETE FROM product_units WHERE pos_id = @id", pgConn);
                pgCmd.Parameters.AddWithValue("id", id);
                pgCmd.ExecuteNonQuery();
            }
            catch { }
        }
    }

    private static void ClearDefault(int productId, SQLiteConnection conn)
    {
        var sql = "UPDATE ProductUnits SET IsDefault = 0 WHERE ProductId = @pid";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pid", productId);
        cmd.ExecuteNonQuery();
    }

    private static void SetParams(SQLiteCommand cmd, ProductUnit u)
    {
        cmd.Parameters.AddWithValue("@pid", u.ProductId);
        cmd.Parameters.AddWithValue("@un", u.UnitName);
        cmd.Parameters.AddWithValue("@pr", u.Price);
        cmd.Parameters.AddWithValue("@co", u.Cost);
        cmd.Parameters.AddWithValue("@qpu", u.QtyPerUnit);
        cmd.Parameters.AddWithValue("@def", u.IsDefault ? 1 : 0);
    }

    private static ProductUnit Map(SQLiteDataReader rdr)
    {
        return new ProductUnit
        {
            Id = Convert.ToInt32(rdr["Id"]),
            ProductId = Convert.ToInt32(rdr["ProductId"]),
            UnitName = rdr["UnitName"].ToString() ?? "",
            Price = Convert.ToDecimal(rdr["Price"]),
            Cost = Convert.ToDecimal(rdr["Cost"]),
            QtyPerUnit = Convert.ToInt32(rdr["QtyPerUnit"]),
            IsDefault = Convert.ToBoolean(rdr["IsDefault"]),
            PointsPerUnit = rdr["PointsPerUnit"] != DBNull.Value ? Convert.ToInt32(rdr["PointsPerUnit"]) : 0
        };
    }

    private static ProductUnit MapPg(NpgsqlDataReader rdr)
    {
        return new ProductUnit
        {
            Id = Convert.ToInt32(rdr["pos_id"]),
            ProductId = Convert.ToInt32(rdr["product_pos_id"]),
            UnitName = rdr["unit_name"].ToString() ?? "",
            Price = Convert.ToDecimal(rdr["price"]),
            Cost = Convert.ToDecimal(rdr["cost"]),
            QtyPerUnit = Convert.ToInt32(rdr["qty_per_unit"]),
            IsDefault = Convert.ToInt32(rdr["is_default"]) == 1
        };
    }
}
