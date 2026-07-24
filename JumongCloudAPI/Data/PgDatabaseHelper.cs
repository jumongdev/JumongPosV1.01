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
                UNIQUE(name)
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

        // Migration: add inventory cost columns to daily_closes
        using var invMig = conn.CreateCommand();
        invMig.CommandText = "ALTER TABLE daily_closes ADD COLUMN IF NOT EXISTS total_inventory_cost NUMERIC NOT NULL DEFAULT 0";
        invMig.ExecuteNonQuery();
        invMig.CommandText = "ALTER TABLE daily_closes ADD COLUMN IF NOT EXISTS total_cost_sold NUMERIC NOT NULL DEFAULT 0";
        invMig.ExecuteNonQuery();
        invMig.CommandText = "ALTER TABLE daily_closes ADD COLUMN IF NOT EXISTS total_stock_received_cost NUMERIC NOT NULL DEFAULT 0";
        invMig.ExecuteNonQuery();

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

        // Migration: add unit_cost to sale_items
        using var ucMig = conn.CreateCommand();
        ucMig.CommandText = "ALTER TABLE sale_items ADD COLUMN IF NOT EXISTS unit_cost NUMERIC NOT NULL DEFAULT 0";
        try { ucMig.ExecuteNonQuery(); } catch { }

        // Migration: add image_data to master_products
        using var imgMig = conn.CreateCommand();
        imgMig.CommandText = "ALTER TABLE master_products ADD COLUMN IF NOT EXISTS image_data TEXT DEFAULT ''";
        try { imgMig.ExecuteNonQuery(); } catch { }

        // Migration: add updated_at to master_products
        using var upMig = conn.CreateCommand();
        upMig.CommandText = "ALTER TABLE master_products ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()";
        try { upMig.ExecuteNonQuery(); } catch { }

        // Migration: add password_hash to users for centralized PIN auth
        using var pwMig = conn.CreateCommand();
        pwMig.CommandText = "ALTER TABLE users ADD COLUMN IF NOT EXISTS password_hash TEXT NOT NULL DEFAULT '12345'";
        try { pwMig.ExecuteNonQuery(); } catch { }

        // Migration: create user_stores junction table for multi-store user access
        using var usMig = conn.CreateCommand();
        usMig.CommandText = @"
            CREATE TABLE IF NOT EXISTS user_stores (
                id SERIAL PRIMARY KEY,
                user_pos_id INTEGER NOT NULL,
                store_id TEXT NOT NULL,
                UNIQUE(user_pos_id, store_id)
            );
            CREATE INDEX IF NOT EXISTS idx_user_stores_store ON user_stores(store_id);
            CREATE INDEX IF NOT EXISTS idx_user_stores_user ON user_stores(user_pos_id);
        ";
        try { usMig.ExecuteNonQuery(); } catch { }

        // Migration: backfill user_stores from existing users table
        using var backfillUs = conn.CreateCommand();
        backfillUs.CommandText = @"
            INSERT INTO user_stores (user_pos_id, store_id)
            SELECT DISTINCT u.pos_id, u.store_id FROM users u
            WHERE u.pos_id > 0 AND u.store_id != ''
            AND NOT EXISTS (
                SELECT 1 FROM user_stores us WHERE us.user_pos_id = u.pos_id AND us.store_id = u.store_id
            )
        ";
        try { backfillUs.ExecuteNonQuery(); } catch { }

        // Recreate unique constraints with store_id
        using var dropCmd = conn.CreateCommand();
        dropCmd.CommandText = @"
            DO $$ BEGIN
                ALTER TABLE products DROP CONSTRAINT IF EXISTS products_pos_id_key;
                ALTER TABLE products DROP CONSTRAINT IF EXISTS products_store_id_pos_id_key;
                ALTER TABLE products ADD CONSTRAINT products_store_id_pos_id_key UNIQUE (store_id, pos_id);
                
                ALTER TABLE customers DROP CONSTRAINT IF EXISTS customers_pos_id_key;
                ALTER TABLE customers DROP CONSTRAINT IF EXISTS customers_store_id_pos_id_key;
                ALTER TABLE customers DROP CONSTRAINT IF EXISTS customers_name_key;
                ALTER TABLE customers ADD CONSTRAINT customers_name_key UNIQUE (name);
                ALTER TABLE customers ALTER COLUMN store_id SET DEFAULT '';
                UPDATE customers SET store_id = '' WHERE store_id IS NOT NULL AND store_id != '';
                
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
                modified_by TEXT DEFAULT '',
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
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

        // Warehouse migration: link to master catalog and stores
        using var whMig = conn.CreateCommand();
        whMig.CommandText = @"
            ALTER TABLE wh_products ADD COLUMN IF NOT EXISTS master_product_id INTEGER REFERENCES master_products(id);
            ALTER TABLE wh_clients ADD COLUMN IF NOT EXISTS store_id TEXT NOT NULL DEFAULT '';
            ALTER TABLE wh_order_items ADD COLUMN IF NOT EXISTS base_qty INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE wh_order_items ADD COLUMN IF NOT EXISTS base_unit_name TEXT NOT NULL DEFAULT 'Piece';
            ALTER TABLE wh_order_items ADD COLUMN IF NOT EXISTS received_qty INTEGER NOT NULL DEFAULT 0;

            -- Transfer tables for warehouse-to-POS stock transfers
            CREATE TABLE IF NOT EXISTS wh_transfers (
                id SERIAL PRIMARY KEY,
                client_id INTEGER NOT NULL REFERENCES wh_clients(id),
                client_name TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT 'pending',
                notes TEXT DEFAULT '',
                store_id TEXT DEFAULT '',
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            CREATE TABLE IF NOT EXISTS wh_transfer_items (
                id SERIAL PRIMARY KEY,
                transfer_id INTEGER NOT NULL REFERENCES wh_transfers(id) ON DELETE CASCADE,
                product_id INTEGER NOT NULL REFERENCES wh_products(id),
                product_name TEXT NOT NULL,
                barcode TEXT DEFAULT '',
                qty INTEGER NOT NULL DEFAULT 0,
                received_qty INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_wh_transfers_client ON wh_transfers(client_id);
            CREATE INDEX IF NOT EXISTS idx_wh_transfers_status ON wh_transfers(status);
            CREATE INDEX IF NOT EXISTS idx_wh_transfer_items_transfer ON wh_transfer_items(transfer_id);
            CREATE TABLE IF NOT EXISTS wh_stock_trails (
                id SERIAL PRIMARY KEY,
                product_id INTEGER NOT NULL REFERENCES wh_products(id),
                product_name TEXT NOT NULL DEFAULT '',
                barcode TEXT DEFAULT '',
                qty_change INTEGER NOT NULL DEFAULT 0,
                reference TEXT NOT NULL DEFAULT '',
                reference_type TEXT NOT NULL DEFAULT '',
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            CREATE INDEX IF NOT EXISTS idx_wh_stock_trails_product ON wh_stock_trails(product_id);
        ";
        whMig.ExecuteNonQuery();

        // Seed wh_clients from existing stores (for warehouse-to-POS transfers)
        using var seedClients = conn.CreateCommand();
        seedClients.CommandText = @"
            INSERT INTO wh_clients (name, contact, address, store_type, store_id)
            SELECT s.store_name, '', '', 'pos', s.store_id
            FROM stores s
            WHERE NOT EXISTS (
                SELECT 1 FROM wh_clients wc WHERE wc.store_id = s.store_id
            )";
        seedClients.ExecuteNonQuery();

        // Migration: add points fields to master products
        using var ptMig = conn.CreateCommand();
        ptMig.CommandText = @"
            ALTER TABLE master_products ADD COLUMN IF NOT EXISTS points_exempt BOOLEAN NOT NULL DEFAULT FALSE;
            ALTER TABLE master_products ADD COLUMN IF NOT EXISTS points_per_unit INTEGER NOT NULL DEFAULT 0;
        ";
        try { ptMig.ExecuteNonQuery(); } catch { }

        // Migration: add points_per_unit to master product units
        using var puMig = conn.CreateCommand();
        puMig.CommandText = "ALTER TABLE master_product_units ADD COLUMN IF NOT EXISTS points_per_unit INTEGER NOT NULL DEFAULT 0";
        try { puMig.ExecuteNonQuery(); } catch { }

        // Migration: add points_earned to sale_items
        using var peMig = conn.CreateCommand();
        peMig.CommandText = "ALTER TABLE sale_items ADD COLUMN IF NOT EXISTS points_earned INTEGER NOT NULL DEFAULT 0";
        try { peMig.ExecuteNonQuery(); } catch { }

        // Migration: store_settings for configurable values like PointsRate
        using var ssMig = conn.CreateCommand();
        ssMig.CommandText = @"
            CREATE TABLE IF NOT EXISTS store_settings (
                id SERIAL PRIMARY KEY,
                store_id TEXT NOT NULL DEFAULT '',
                key TEXT NOT NULL,
                value TEXT NOT NULL DEFAULT '',
                UNIQUE(store_id, key)
            );
            INSERT INTO store_settings (store_id, key, value)
            SELECT DISTINCT store_id, 'PointsRate', '200' FROM stores
            WHERE NOT EXISTS (
                SELECT 1 FROM store_settings ss WHERE ss.store_id = stores.store_id AND ss.key = 'PointsRate'
            );
        ";
        try { ssMig.ExecuteNonQuery(); } catch { }

        // Migration: walk-in sales tables for Warehouse Sell
        using var wsMig = conn.CreateCommand();
        wsMig.CommandText = @"
            CREATE TABLE IF NOT EXISTS wh_walkin_sales (
                id SERIAL PRIMARY KEY,
                customer_id INTEGER,
                customer_name TEXT NOT NULL DEFAULT '',
                total_amount NUMERIC NOT NULL DEFAULT 0,
                item_count INTEGER NOT NULL DEFAULT 0,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            CREATE TABLE IF NOT EXISTS wh_walkin_sale_items (
                id SERIAL PRIMARY KEY,
                sale_id INTEGER NOT NULL REFERENCES wh_walkin_sales(id) ON DELETE CASCADE,
                product_id INTEGER NOT NULL,
                product_name TEXT NOT NULL DEFAULT '',
                barcode TEXT DEFAULT '',
                unit_name TEXT NOT NULL DEFAULT 'Piece',
                qty INTEGER NOT NULL DEFAULT 1,
                price NUMERIC NOT NULL DEFAULT 0,
                subtotal NUMERIC NOT NULL DEFAULT 0,
                stock_deduction INTEGER NOT NULL DEFAULT 0,
                points_earned INTEGER NOT NULL DEFAULT 0,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            CREATE INDEX IF NOT EXISTS idx_wh_walkin_sale_items_sale ON wh_walkin_sale_items(sale_id);
            CREATE INDEX IF NOT EXISTS idx_wh_walkin_sales_customer ON wh_walkin_sales(customer_id);
        ";
        try { wsMig.ExecuteNonQuery(); } catch { }

        // Migration: UNIQUE index on barcode (partial — multiple NULL/empty allowed)
        using var barcodeMig = conn.CreateCommand();
        barcodeMig.CommandText = @"
            -- First: nullify duplicate barcodes (keep first by id)
            UPDATE master_products SET barcode = NULL
            WHERE id NOT IN (
                SELECT MIN(id) FROM master_products
                WHERE barcode IS NOT NULL AND barcode != '' GROUP BY barcode
            )
            AND barcode IN (
                SELECT barcode FROM master_products
                WHERE barcode IS NOT NULL AND barcode != ''
                GROUP BY barcode HAVING COUNT(*) > 1
            );
            -- Then: add unique partial index
            CREATE UNIQUE INDEX IF NOT EXISTS idx_master_products_barcode
            ON master_products(barcode) WHERE barcode IS NOT NULL AND barcode != '';";
        try { barcodeMig.ExecuteNonQuery(); } catch { }

        // Migration: pos_promo table for cloud-managed promo message
        using var promoMig = conn.CreateCommand();
        promoMig.CommandText = @"
            CREATE TABLE IF NOT EXISTS pos_promo (
                id SERIAL PRIMARY KEY,
                message TEXT NOT NULL DEFAULT '',
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            INSERT INTO pos_promo (id, message)
            SELECT 1, '' WHERE NOT EXISTS (SELECT 1 FROM pos_promo);";
        try { promoMig.ExecuteNonQuery(); } catch { }

        // Migration: add is_voided to wh_walkin_sales
        using var voidMig = conn.CreateCommand();
        voidMig.CommandText = "ALTER TABLE wh_walkin_sales ADD COLUMN IF NOT EXISTS is_voided BOOLEAN NOT NULL DEFAULT FALSE";
        try { voidMig.ExecuteNonQuery(); } catch { }

        // Migration: add invoice_no to wh_walkin_sales
        using var invnoMig = conn.CreateCommand();
        invnoMig.CommandText = "ALTER TABLE wh_walkin_sales ADD COLUMN IF NOT EXISTS invoice_no TEXT DEFAULT ''";
        try { invnoMig.ExecuteNonQuery(); } catch { }

        // Migration: wh_invoice_counter for invoice number generation
        using var icMig = conn.CreateCommand();
        icMig.CommandText = @"
            CREATE TABLE IF NOT EXISTS wh_invoice_counter (
                date_key TEXT PRIMARY KEY,
                last_seq INTEGER NOT NULL DEFAULT 0
            )";
        try { icMig.ExecuteNonQuery(); } catch { }
    }
}
