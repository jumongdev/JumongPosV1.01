using System.Data.SQLite;
using Npgsql;

namespace JumongPosV1._01.Data;

public static class CloudDatabaseHelper
{
    private static string? ReadSetting(string key)
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = @k", conn);
            cmd.Parameters.AddWithValue("@k", key);
            return cmd.ExecuteScalar()?.ToString();
        }
        catch { return null; }
    }

    public static string? BuildConnectionString()
    {
        var host = ReadSetting("PgHost");
        var port = ReadSetting("PgPort");
        var db   = ReadSetting("PgDatabase");
        var user = ReadSetting("PgUser");
        var pass = ReadSetting("PgPass");
        var ssl  = ReadSetting("PgSsl");

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(db) ||
            string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            return null;

        var cs = $"Host={host};Port={port ?? "5432"};Database={db};Username={user};Password={pass};";
        if (ssl == "True" || ssl == "true")
            cs += "SSL Mode=Require;Trust Server Certificate=true;";
        return cs;
    }

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(BuildConnectionString());

    public static NpgsqlConnection? GetConnection()
    {
        var cs = BuildConnectionString();
        if (cs == null) return null;
        return new NpgsqlConnection(cs);
    }

    public static bool TestConnection()
    {
        try
        {
            using var conn = GetConnection();
            if (conn == null) return false;
            conn.Open();
            return conn.State == System.Data.ConnectionState.Open;
        }
        catch { return false; }
    }

    public static async Task EnsureSchemaAsync(NpgsqlConnection conn)
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS products (
                id SERIAL PRIMARY KEY,
                pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL DEFAULT '',
                name TEXT NOT NULL,
                barcode TEXT,
                category TEXT DEFAULT '',
                price NUMERIC NOT NULL DEFAULT 0,
                cost NUMERIC NOT NULL DEFAULT 0,
                stock_qty INTEGER NOT NULL DEFAULT 0,
                is_active INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL DEFAULT (to_char(now(), 'YYYY-MM-DD HH24:MI:SS')),
                image_data TEXT,
                UNIQUE(store_id, pos_id)
            );
            CREATE TABLE IF NOT EXISTS product_units (
                id SERIAL PRIMARY KEY,
                pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL DEFAULT '',
                product_pos_id INTEGER NOT NULL,
                unit_name TEXT NOT NULL,
                price NUMERIC NOT NULL DEFAULT 0,
                cost NUMERIC NOT NULL DEFAULT 0,
                qty_per_unit INTEGER NOT NULL DEFAULT 1,
                is_default INTEGER NOT NULL DEFAULT 0,
                UNIQUE(store_id, product_pos_id, unit_name)
            );
            CREATE TABLE IF NOT EXISTS customers (
                id SERIAL PRIMARY KEY,
                pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL DEFAULT '',
                name TEXT NOT NULL,
                phone TEXT,
                email TEXT,
                address TEXT DEFAULT '',
                loyalty_points INTEGER NOT NULL DEFAULT 0,
                credit_balance NUMERIC NOT NULL DEFAULT 0,
                credit_limit NUMERIC NOT NULL DEFAULT 0,
                is_active INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL DEFAULT (to_char(now(), 'YYYY-MM-DD HH24:MI:SS')),
                UNIQUE(store_id, pos_id)
            );
            CREATE TABLE IF NOT EXISTS users (
                id SERIAL PRIMARY KEY,
                pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL DEFAULT '',
                username TEXT NOT NULL,
                password_hash TEXT,
                full_name TEXT DEFAULT '',
                role TEXT DEFAULT 'Cashier',
                is_active INTEGER NOT NULL DEFAULT 1,
                UNIQUE(store_id, pos_id)
            );";
        using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
