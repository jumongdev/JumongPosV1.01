using Npgsql;

namespace JumongCloudAPI.Data;

public static class PgDatabaseHelper
{
    public static string ConnectionString { get; set; } = "";

    public static NpgsqlConnection GetConnection()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        using var tz = conn.CreateCommand();
        tz.CommandText = "SET TIMEZONE TO 'Asia/Manila'";
        try { tz.ExecuteNonQuery(); } catch { }
        return conn;
    }

    public static void Initialize()
    {
        using var conn = GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS stores (
                store_id TEXT PRIMARY KEY,
                store_name TEXT NOT NULL DEFAULT '',
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

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
                is_active BOOLEAN NOT NULL DEFAULT TRUE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                modified_by TEXT DEFAULT '',
                synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                UNIQUE(store_id, pos_id)
            );

            CREATE TABLE IF NOT EXISTS customers (
                id SERIAL PRIMARY KEY,
                pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL DEFAULT '',
                name TEXT NOT NULL,
                phone TEXT,
                email TEXT,
                loyalty_points INTEGER NOT NULL DEFAULT 0,
                is_active BOOLEAN NOT NULL DEFAULT TRUE,
                credit_balance NUMERIC NOT NULL DEFAULT 0,
                credit_limit NUMERIC NOT NULL DEFAULT 0,
                address TEXT DEFAULT '',
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                modified_by TEXT DEFAULT '',
                synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                UNIQUE(store_id, pos_id)
            );

            CREATE TABLE IF NOT EXISTS users (
                id SERIAL PRIMARY KEY,
                pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL DEFAULT '',
                username TEXT NOT NULL,
                role TEXT NOT NULL DEFAULT 'Cashier',
                full_name TEXT DEFAULT '',
                is_active BOOLEAN NOT NULL DEFAULT TRUE,
                synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                UNIQUE(store_id, pos_id)
            );

            CREATE TABLE IF NOT EXISTS sales (
                id SERIAL PRIMARY KEY,
                pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL DEFAULT '',
                invoice_no TEXT NOT NULL,
                sale_date TIMESTAMPTZ NOT NULL,
                sub_total NUMERIC NOT NULL DEFAULT 0,
                discount NUMERIC NOT NULL DEFAULT 0,
                tax NUMERIC NOT NULL DEFAULT 0,
                grand_total NUMERIC NOT NULL DEFAULT 0,
                amount_paid NUMERIC NOT NULL DEFAULT 0,
                change NUMERIC NOT NULL DEFAULT 0,
                payment_method TEXT NOT NULL DEFAULT 'Cash',
                customer_id INTEGER,
                user_id INTEGER,
                is_voided BOOLEAN NOT NULL DEFAULT FALSE,
                reference_no TEXT DEFAULT '',
                order_type TEXT NOT NULL DEFAULT 'Walk-in',
                cash_paid NUMERIC NOT NULL DEFAULT 0,
                ew_paid NUMERIC NOT NULL DEFAULT 0,
                cashier_name TEXT DEFAULT '',
                synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                UNIQUE(store_id, pos_id)
            );

            CREATE TABLE IF NOT EXISTS sale_items (
                id SERIAL PRIMARY KEY,
                pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL DEFAULT '',
                sale_id INTEGER NOT NULL,
                product_id INTEGER NOT NULL,
                product_name TEXT NOT NULL,
                barcode TEXT,
                price NUMERIC NOT NULL DEFAULT 0,
                quantity INTEGER NOT NULL DEFAULT 1,
                total_price NUMERIC NOT NULL DEFAULT 0,
                is_voided BOOLEAN NOT NULL DEFAULT FALSE,
                unit_name TEXT DEFAULT '',
                qty_per_unit INTEGER NOT NULL DEFAULT 1,
                synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                UNIQUE(store_id, pos_id)
            );

            CREATE TABLE IF NOT EXISTS void_logs (
                id SERIAL PRIMARY KEY,
                pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL DEFAULT '',
                sale_id INTEGER NOT NULL,
                sale_item_id INTEGER,
                action TEXT NOT NULL,
                reason TEXT DEFAULT '',
                invoice_no TEXT DEFAULT '',
                product_name TEXT DEFAULT '',
                quantity INTEGER NOT NULL DEFAULT 0,
                amount NUMERIC NOT NULL DEFAULT 0,
                user_id INTEGER NOT NULL DEFAULT 0,
                user_name TEXT DEFAULT '',
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                UNIQUE(store_id, pos_id)
            );

            CREATE TABLE IF NOT EXISTS stock_trails (
                id SERIAL PRIMARY KEY,
                pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL DEFAULT '',
                product_id INTEGER NOT NULL,
                product_name TEXT NOT NULL,
                barcode TEXT DEFAULT '',
                quantity_added NUMERIC NOT NULL DEFAULT 0,
                stock_before INTEGER NOT NULL DEFAULT 0,
                stock_after INTEGER NOT NULL DEFAULT 0,
                reference TEXT DEFAULT '',
                user_id INTEGER NOT NULL DEFAULT 0,
                user_name TEXT DEFAULT '',
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                UNIQUE(store_id, pos_id)
            );

            CREATE TABLE IF NOT EXISTS credit_transactions (
                id SERIAL PRIMARY KEY,
                pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL DEFAULT '',
                customer_id INTEGER NOT NULL,
                sale_id INTEGER,
                type TEXT NOT NULL,
                description TEXT NOT NULL,
                debit NUMERIC NOT NULL DEFAULT 0,
                credit NUMERIC NOT NULL DEFAULT 0,
                balance NUMERIC NOT NULL DEFAULT 0,
                payment_method TEXT DEFAULT '',
                reference_no TEXT DEFAULT '',
                user_id INTEGER NOT NULL DEFAULT 0,
                user_name TEXT DEFAULT '',
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                UNIQUE(store_id, pos_id)
            );

            CREATE TABLE IF NOT EXISTS daily_closes (
                id SERIAL PRIMARY KEY,
                pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL DEFAULT '',
                close_date TIMESTAMPTZ NOT NULL,
                total_sales NUMERIC NOT NULL DEFAULT 0,
                total_cash NUMERIC NOT NULL DEFAULT 0,
                total_ewallet NUMERIC NOT NULL DEFAULT 0,
                total_credit NUMERIC NOT NULL DEFAULT 0,
                total_voided NUMERIC NOT NULL DEFAULT 0,
                cash_on_hand NUMERIC NOT NULL DEFAULT 0,
                difference NUMERIC NOT NULL DEFAULT 0,
                opening_cash NUMERIC NOT NULL DEFAULT 0,
                total_expenses NUMERIC NOT NULL DEFAULT 0,
                notes TEXT DEFAULT '',
                user_id INTEGER NOT NULL DEFAULT 0,
                user_name TEXT DEFAULT '',
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                UNIQUE(store_id, pos_id)
            );

            CREATE TABLE IF NOT EXISTS expenses (
                id SERIAL PRIMARY KEY,
                pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL DEFAULT '',
                amount NUMERIC NOT NULL,
                category TEXT NOT NULL,
                description TEXT NOT NULL,
                reference_no TEXT,
                cashier_username TEXT NOT NULL,
                timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                receipt_image TEXT DEFAULT '',
                synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                UNIQUE(store_id, pos_id)
            );
        ";
        cmd.ExecuteNonQuery();

        // Migration: add store_id column to existing tables
        using var migCmd = conn.CreateCommand();
        migCmd.CommandText = @"
            DO $$ BEGIN
                ALTER TABLE products ADD COLUMN IF NOT EXISTS store_id TEXT NOT NULL DEFAULT '';
                ALTER TABLE customers ADD COLUMN IF NOT EXISTS store_id TEXT NOT NULL DEFAULT '';
                ALTER TABLE users ADD COLUMN IF NOT EXISTS store_id TEXT NOT NULL DEFAULT '';
                ALTER TABLE sales ADD COLUMN IF NOT EXISTS store_id TEXT NOT NULL DEFAULT '';
                ALTER TABLE sale_items ADD COLUMN IF NOT EXISTS store_id TEXT NOT NULL DEFAULT '';
                ALTER TABLE void_logs ADD COLUMN IF NOT EXISTS store_id TEXT NOT NULL DEFAULT '';
                ALTER TABLE stock_trails ADD COLUMN IF NOT EXISTS store_id TEXT NOT NULL DEFAULT '';
                ALTER TABLE credit_transactions ADD COLUMN IF NOT EXISTS store_id TEXT NOT NULL DEFAULT '';
                ALTER TABLE daily_closes ADD COLUMN IF NOT EXISTS store_id TEXT NOT NULL DEFAULT '';
                ALTER TABLE expenses ADD COLUMN IF NOT EXISTS store_id TEXT NOT NULL DEFAULT '';
            END $$;
        ";
        migCmd.ExecuteNonQuery();

        // Migration: add cashier_name to sales
        using var mig2 = conn.CreateCommand();
        mig2.CommandText = "ALTER TABLE sales ADD COLUMN IF NOT EXISTS cashier_name TEXT DEFAULT ''";
        mig2.ExecuteNonQuery();

        // Migration: backfill empty cashier_name from users table
        using var backfill = conn.CreateCommand();
        backfill.CommandText = @"
            UPDATE sales s
            SET cashier_name = COALESCE(NULLIF(u.full_name,''), NULLIF(u.username,''), '')
            FROM users u
            WHERE s.user_id = u.pos_id AND s.store_id = u.store_id
            AND (s.cashier_name IS NULL OR s.cashier_name = '')
        ";
        try { backfill.ExecuteNonQuery(); } catch { }

        // Recreate unique constraints with store_id
        using var dropCmd = conn.CreateCommand();
        dropCmd.CommandText = @"
            DO $$ BEGIN
                ALTER TABLE products DROP CONSTRAINT IF EXISTS products_pos_id_key;
                ALTER TABLE products DROP CONSTRAINT IF EXISTS products_store_id_pos_id_key;
                ALTER TABLE products ADD CONSTRAINT products_store_id_pos_id_key UNIQUE (store_id, pos_id);
                
                ALTER TABLE customers DROP CONSTRAINT IF EXISTS customers_pos_id_key;
                ALTER TABLE customers DROP CONSTRAINT IF EXISTS customers_store_id_pos_id_key;
                ALTER TABLE customers ADD CONSTRAINT customers_store_id_pos_id_key UNIQUE (store_id, pos_id);
                
                ALTER TABLE users DROP CONSTRAINT IF EXISTS users_pos_id_key;
                ALTER TABLE users DROP CONSTRAINT IF EXISTS users_store_id_pos_id_key;
                ALTER TABLE users ADD CONSTRAINT users_store_id_pos_id_key UNIQUE (store_id, pos_id);
                
                ALTER TABLE sales DROP CONSTRAINT IF EXISTS sales_pos_id_key;
                ALTER TABLE sales DROP CONSTRAINT IF EXISTS sales_store_id_pos_id_key;
                ALTER TABLE sales ADD CONSTRAINT sales_store_id_pos_id_key UNIQUE (store_id, pos_id);
                
                ALTER TABLE sale_items DROP CONSTRAINT IF EXISTS sale_items_pos_id_key;
                ALTER TABLE sale_items DROP CONSTRAINT IF EXISTS sale_items_store_id_pos_id_key;
                ALTER TABLE sale_items ADD CONSTRAINT sale_items_store_id_pos_id_key UNIQUE (store_id, pos_id);
                
                ALTER TABLE void_logs DROP CONSTRAINT IF EXISTS void_logs_pos_id_key;
                ALTER TABLE void_logs DROP CONSTRAINT IF EXISTS void_logs_store_id_pos_id_key;
                ALTER TABLE void_logs ADD CONSTRAINT void_logs_store_id_pos_id_key UNIQUE (store_id, pos_id);
                
                ALTER TABLE stock_trails DROP CONSTRAINT IF EXISTS stock_trails_pos_id_key;
                ALTER TABLE stock_trails DROP CONSTRAINT IF EXISTS stock_trails_store_id_pos_id_key;
                ALTER TABLE stock_trails ADD CONSTRAINT stock_trails_store_id_pos_id_key UNIQUE (store_id, pos_id);
                
                ALTER TABLE credit_transactions DROP CONSTRAINT IF EXISTS credit_transactions_pos_id_key;
                ALTER TABLE credit_transactions DROP CONSTRAINT IF EXISTS credit_transactions_store_id_pos_id_key;
                ALTER TABLE credit_transactions ADD CONSTRAINT credit_transactions_store_id_pos_id_key UNIQUE (store_id, pos_id);
                
                ALTER TABLE daily_closes DROP CONSTRAINT IF EXISTS daily_closes_pos_id_key;
                ALTER TABLE daily_closes DROP CONSTRAINT IF EXISTS daily_closes_store_id_pos_id_key;
                ALTER TABLE daily_closes ADD CONSTRAINT daily_closes_store_id_pos_id_key UNIQUE (store_id, pos_id);
                
                ALTER TABLE expenses DROP CONSTRAINT IF EXISTS expenses_pos_id_key;
                ALTER TABLE expenses DROP CONSTRAINT IF EXISTS expenses_store_id_pos_id_key;
                ALTER TABLE expenses ADD CONSTRAINT expenses_store_id_pos_id_key UNIQUE (store_id, pos_id);
            END $$;
        ";
        dropCmd.ExecuteNonQuery();

        using var idxCmd = conn.CreateCommand();
        idxCmd.CommandText = @"
            CREATE INDEX IF NOT EXISTS idx_sales_sale_date ON sales(sale_date);
            CREATE INDEX IF NOT EXISTS idx_sale_items_sale_id ON sale_items(sale_id);
            CREATE INDEX IF NOT EXISTS idx_daily_closes_close_date ON daily_closes(close_date);
            CREATE INDEX IF NOT EXISTS idx_expenses_timestamp ON expenses(timestamp);
            CREATE INDEX IF NOT EXISTS idx_stock_trails_created_at ON stock_trails(created_at);
            CREATE INDEX IF NOT EXISTS idx_credit_transactions_customer_id ON credit_transactions(customer_id);
        ";
        idxCmd.ExecuteNonQuery();

        // Master product catalog (shared across stores)
        using var masterCmd = conn.CreateCommand();
        masterCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS master_products (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                barcode TEXT,
                category TEXT DEFAULT '',
                price NUMERIC NOT NULL DEFAULT 0,
                cost NUMERIC NOT NULL DEFAULT 0,
                stock_qty INTEGER NOT NULL DEFAULT 0,
                is_active BOOLEAN NOT NULL DEFAULT TRUE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                modified_by TEXT DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS master_product_units (
                id SERIAL PRIMARY KEY,
                product_id INTEGER NOT NULL REFERENCES master_products(id) ON DELETE CASCADE,
                unit_name TEXT NOT NULL DEFAULT 'Piece',
                price NUMERIC NOT NULL DEFAULT 0,
                cost NUMERIC NOT NULL DEFAULT 0,
                qty_per_unit INTEGER NOT NULL DEFAULT 1,
                is_default BOOLEAN NOT NULL DEFAULT FALSE
            );
            CREATE INDEX IF NOT EXISTS idx_master_product_units_product_id ON master_product_units(product_id);
        ";
        masterCmd.ExecuteNonQuery();

        // Warehouse / ordering system
        using var whCmd = conn.CreateCommand();
        whCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS wh_products (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                barcode TEXT,
                category TEXT DEFAULT '',
                box_price NUMERIC NOT NULL DEFAULT 0,
                box_cost NUMERIC NOT NULL DEFAULT 0,
                box_qty INTEGER NOT NULL DEFAULT 1,
                piece_price NUMERIC NOT NULL DEFAULT 0,
                stock_qty INTEGER NOT NULL DEFAULT 0,
                is_active BOOLEAN NOT NULL DEFAULT TRUE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            CREATE TABLE IF NOT EXISTS wh_clients (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                contact TEXT DEFAULT '',
                address TEXT DEFAULT '',
                store_type TEXT NOT NULL DEFAULT 'pos',
                is_active BOOLEAN NOT NULL DEFAULT TRUE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            CREATE TABLE IF NOT EXISTS wh_orders (
                id SERIAL PRIMARY KEY,
                client_id INTEGER NOT NULL REFERENCES wh_clients(id),
                client_name TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT 'pending',
                notes TEXT DEFAULT '',
                total_amount NUMERIC NOT NULL DEFAULT 0,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            CREATE TABLE IF NOT EXISTS wh_order_items (
                id SERIAL PRIMARY KEY,
                order_id INTEGER NOT NULL REFERENCES wh_orders(id) ON DELETE CASCADE,
                product_id INTEGER NOT NULL REFERENCES wh_products(id),
                product_name TEXT NOT NULL,
                unit_type TEXT NOT NULL DEFAULT 'box',
                qty INTEGER NOT NULL DEFAULT 1,
                price NUMERIC NOT NULL DEFAULT 0,
                total_price NUMERIC NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_wh_orders_client ON wh_orders(client_id);
            CREATE INDEX IF NOT EXISTS idx_wh_orders_status ON wh_orders(status);
            CREATE INDEX IF NOT EXISTS idx_wh_order_items_order ON wh_order_items(order_id);
        ";
        whCmd.ExecuteNonQuery();
    }
}
