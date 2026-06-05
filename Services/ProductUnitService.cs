using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public class ProductUnitService
{
    public static List<ProductUnit> GetByProduct(int productId)
    {
        var list = new List<ProductUnit>();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM ProductUnits WHERE ProductId = @pid ORDER BY IsDefault DESC, UnitName";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pid", productId);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            list.Add(Map(rdr));
        return list;
    }

    public static ProductUnit? GetDefault(int productId)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT * FROM ProductUnits WHERE ProductId = @pid AND IsDefault = 1 LIMIT 1";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pid", productId);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read())
            return Map(rdr);
        return null;
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
    }

    public static void Delete(int id)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "DELETE FROM ProductUnits WHERE Id = @id";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
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
            IsDefault = Convert.ToBoolean(rdr["IsDefault"])
        };
    }
}
