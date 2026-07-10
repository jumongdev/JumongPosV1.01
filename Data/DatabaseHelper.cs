using System.Data.SQLite;

namespace JumongPosV1._01.Data;

public class DatabaseHelper
{
    private static string _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JumongPos.db");
    private static string _connectionString = $"Data Source={_dbPath};Version=3;";

    public static string DbPath => _dbPath;
    public static string ConnectionString => _connectionString;

    public static void SetDbPath(string path)
    {
        _dbPath = path;
        _connectionString = $"Data Source={path};Version=3;";
    }

    public static void Initialize()
    {
        if (!File.Exists(_dbPath))
            SQLiteConnection.CreateFile(_dbPath);

        using var conn = GetConnection();
        conn.Open();

        var sql = @"
            CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Barcode TEXT UNIQUE,
                Category TEXT DEFAULT '',
                Price REAL NOT NULL DEFAULT 0,
                Cost REAL NOT NULL DEFAULT 0,
                StockQty INTEGER NOT NULL DEFAULT 0,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS Customers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Phone TEXT,
                Email TEXT,
                LoyaltyPoints INTEGER NOT NULL DEFAULT 0,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                Role TEXT NOT NULL DEFAULT 'Cashier',
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS Sales (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                InvoiceNo TEXT NOT NULL UNIQUE,
                SaleDate TEXT NOT NULL,
                SubTotal REAL NOT NULL DEFAULT 0,
                Discount REAL NOT NULL DEFAULT 0,
                Tax REAL NOT NULL DEFAULT 0,
                GrandTotal REAL NOT NULL DEFAULT 0,
                AmountPaid REAL NOT NULL DEFAULT 0,
                Change REAL NOT NULL DEFAULT 0,
                PaymentMethod TEXT NOT NULL DEFAULT 'Cash',
                CustomerId INTEGER,
                UserId INTEGER,
                IsVoided INTEGER NOT NULL DEFAULT 0,
                VoidedAt TEXT,
                Synced INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            );

            CREATE TABLE IF NOT EXISTS SaleItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SaleId INTEGER NOT NULL,
                ProductId INTEGER NOT NULL,
                ProductName TEXT NOT NULL,
                Barcode TEXT,
                Price REAL NOT NULL DEFAULT 0,
                Quantity INTEGER NOT NULL DEFAULT 1,
                TotalPrice REAL NOT NULL DEFAULT 0,
                IsVoided INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE,
                FOREIGN KEY (ProductId) REFERENCES Products(Id)
            );

            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS VoidLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SaleId INTEGER NOT NULL,
                SaleItemId INTEGER,
                Action TEXT NOT NULL,
                Reason TEXT NOT NULL DEFAULT '',
                InvoiceNo TEXT NOT NULL DEFAULT '',
                ProductName TEXT NOT NULL DEFAULT '',
                Quantity INTEGER NOT NULL DEFAULT 0,
                Amount REAL NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS ProductUnits (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductId INTEGER NOT NULL,
                UnitName TEXT NOT NULL,
                Price REAL NOT NULL DEFAULT 0,
                Cost REAL NOT NULL DEFAULT 0,
                QtyPerUnit INTEGER NOT NULL DEFAULT 1,
                IsDefault INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
            );
        ";

        using var cmd = new SQLiteCommand(sql, conn);
        cmd.ExecuteNonQuery();

        // StockTrail table
        var stockTrailTable = "CREATE TABLE IF NOT EXISTS StockTrail (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT," +
            "ProductId INTEGER NOT NULL," +
            "ProductName TEXT NOT NULL," +
            "Barcode TEXT NOT NULL DEFAULT ''," +
            "QuantityAdded REAL NOT NULL DEFAULT 0," +
            "StockBefore INTEGER NOT NULL DEFAULT 0," +
            "StockAfter INTEGER NOT NULL DEFAULT 0," +
            "Reference TEXT NOT NULL DEFAULT ''," +
            "UserId INTEGER NOT NULL DEFAULT 0," +
            "UserName TEXT NOT NULL DEFAULT ''," +
            "InvoiceNo TEXT NOT NULL DEFAULT ''," +
            "CustomerName TEXT NOT NULL DEFAULT ''," +
            "CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')))";
        using var stCmd = new SQLiteCommand(stockTrailTable, conn);
        stCmd.ExecuteNonQuery();
        using var checkTrailSynced = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('StockTrail') WHERE name = 'Synced'", conn);
        if (Convert.ToInt32(checkTrailSynced.ExecuteScalar()) == 0)
        {
            using var alterTrail = new SQLiteCommand("ALTER TABLE StockTrail ADD COLUMN Synced INTEGER NOT NULL DEFAULT 0", conn);
            alterTrail.ExecuteNonQuery();
        }

        using var checkCol = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('SaleItems') WHERE name = 'UnitName'", conn);
        if (Convert.ToInt32(checkCol.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE SaleItems ADD COLUMN UnitName TEXT NOT NULL DEFAULT ''", conn);
            alter.ExecuteNonQuery();
        }
        using var checkUc = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('SaleItems') WHERE name = 'UnitCost'", conn);
        if (Convert.ToInt32(checkUc.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE SaleItems ADD COLUMN UnitCost REAL NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
        }
        using var checkQty = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('SaleItems') WHERE name = 'QtyPerUnit'", conn);
        if (Convert.ToInt32(checkQty.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE SaleItems ADD COLUMN QtyPerUnit INTEGER NOT NULL DEFAULT 1", conn);
            alter.ExecuteNonQuery();
        }
        using var checkSaleVoid = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Sales') WHERE name = 'IsVoided'", conn);
        if (Convert.ToInt32(checkSaleVoid.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Sales ADD COLUMN IsVoided INTEGER NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
            alter.CommandText = "ALTER TABLE Sales ADD COLUMN VoidedAt TEXT";
            alter.ExecuteNonQuery();
        }
        using var checkItemVoid = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('SaleItems') WHERE name = 'IsVoided'", conn);
        if (Convert.ToInt32(checkItemVoid.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE SaleItems ADD COLUMN IsVoided INTEGER NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
        }

        var voidLogTable = "CREATE TABLE IF NOT EXISTS VoidLog (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT," +
            "SaleId INTEGER NOT NULL," +
            "SaleItemId INTEGER," +
            "Action TEXT NOT NULL," +
            "Reason TEXT NOT NULL DEFAULT ''," +
            "InvoiceNo TEXT NOT NULL DEFAULT ''," +
            "ProductName TEXT NOT NULL DEFAULT ''," +
            "Quantity INTEGER NOT NULL DEFAULT 0," +
            "Amount REAL NOT NULL DEFAULT 0," +
            "CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')))";
        using var voidCmd = new SQLiteCommand(voidLogTable, conn);
        voidCmd.ExecuteNonQuery();

        using var checkFullName = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name = 'FullName'", conn);
        if (Convert.ToInt32(checkFullName.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Users ADD COLUMN FullName TEXT NOT NULL DEFAULT ''", conn);
            alter.ExecuteNonQuery();
        }

        using var checkSrc = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Products') WHERE name = 'SourceId'", conn);
        if (Convert.ToInt32(checkSrc.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Products ADD COLUMN SourceId TEXT", conn);
            alter.ExecuteNonQuery();
        }

        using var checkAddr = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Customers') WHERE name = 'Address'", conn);
        if (Convert.ToInt32(checkAddr.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Customers ADD COLUMN Address TEXT NOT NULL DEFAULT ''", conn);
            alter.ExecuteNonQuery();
        }

        using var checkRefNo = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Sales') WHERE name = 'ReferenceNo'", conn);
        if (Convert.ToInt32(checkRefNo.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Sales ADD COLUMN ReferenceNo TEXT NOT NULL DEFAULT ''", conn);
            alter.ExecuteNonQuery();
        }

        using var checkOType = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Sales') WHERE name = 'OrderType'", conn);
        if (Convert.ToInt32(checkOType.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Sales ADD COLUMN OrderType TEXT NOT NULL DEFAULT 'Walk-in'", conn);
            alter.ExecuteNonQuery();
        }

        using var checkCashPaid = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Sales') WHERE name = 'CashPaid'", conn);
        if (Convert.ToInt32(checkCashPaid.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Sales ADD COLUMN CashPaid REAL NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
            alter.CommandText = "ALTER TABLE Sales ADD COLUMN EwPaid REAL NOT NULL DEFAULT 0";
            alter.ExecuteNonQuery();
        }

        using var checkSynced = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Sales') WHERE name = 'Synced'", conn);
        if (Convert.ToInt32(checkSynced.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Sales ADD COLUMN Synced INTEGER NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
        }

        using var checkCredBal = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Customers') WHERE name = 'CreditBalance'", conn);
        if (Convert.ToInt32(checkCredBal.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Customers ADD COLUMN CreditBalance REAL NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
        }

        using var checkCredLim = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Customers') WHERE name = 'CreditLimit'", conn);
        if (Convert.ToInt32(checkCredLim.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Customers ADD COLUMN CreditLimit REAL NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
        }

        using var checkCustActive = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Customers') WHERE name = 'IsActive'", conn);
        if (Convert.ToInt32(checkCustActive.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Customers ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1", conn);
            alter.ExecuteNonQuery();
        }

        var creditTransTable = "CREATE TABLE IF NOT EXISTS CreditTransactions (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT," +
            "CustomerId INTEGER NOT NULL," +
            "SaleId INTEGER," +
            "Type TEXT NOT NULL," +
            "Description TEXT NOT NULL," +
            "Debit REAL NOT NULL DEFAULT 0," +
            "Credit REAL NOT NULL DEFAULT 0," +
            "Balance REAL NOT NULL DEFAULT 0," +
            "PaymentMethod TEXT NOT NULL DEFAULT ''," +
            "ReferenceNo TEXT NOT NULL DEFAULT ''," +
            "CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime'))," +
            "FOREIGN KEY (CustomerId) REFERENCES Customers(Id)," +
            "FOREIGN KEY (SaleId) REFERENCES Sales(Id))";
        using var ctCmd = new SQLiteCommand(creditTransTable, conn);
        ctCmd.ExecuteNonQuery();

        using var checkPayMethod = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('CreditTransactions') WHERE name = 'PaymentMethod'", conn);
        if (Convert.ToInt32(checkPayMethod.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE CreditTransactions ADD COLUMN PaymentMethod TEXT NOT NULL DEFAULT ''", conn);
            alter.ExecuteNonQuery();
            alter.CommandText = "ALTER TABLE CreditTransactions ADD COLUMN ReferenceNo TEXT NOT NULL DEFAULT ''";
            alter.ExecuteNonQuery();
        }

        var heldCartsTable = "CREATE TABLE IF NOT EXISTS HeldCarts (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT," +
            "OrderType TEXT NOT NULL DEFAULT 'Walk-in'," +
            "CustomerId INTEGER," +
            "CustomerName TEXT NOT NULL DEFAULT ''," +
            "ItemsJson TEXT NOT NULL DEFAULT '[]'," +
            "CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')))";
        using var hcCmd = new SQLiteCommand(heldCartsTable, conn);
        hcCmd.ExecuteNonQuery();

        var dailyCloseTable = "CREATE TABLE IF NOT EXISTS DailyClose (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT," +
            "CloseDate TEXT NOT NULL," +
            "TotalSales REAL NOT NULL DEFAULT 0," +
            "TotalCash REAL NOT NULL DEFAULT 0," +
            "TotalEWallet REAL NOT NULL DEFAULT 0," +
            "TotalCredit REAL NOT NULL DEFAULT 0," +
            "TotalVoided REAL NOT NULL DEFAULT 0," +
            "Denom1000 INTEGER NOT NULL DEFAULT 0," +
            "Denom500 INTEGER NOT NULL DEFAULT 0," +
            "Denom200 INTEGER NOT NULL DEFAULT 0," +
            "Denom100 INTEGER NOT NULL DEFAULT 0," +
            "Denom50 INTEGER NOT NULL DEFAULT 0," +
            "Denom20 INTEGER NOT NULL DEFAULT 0," +
            "DenomCoins REAL NOT NULL DEFAULT 0," +
            "CashOnHand REAL NOT NULL DEFAULT 0," +
            "Difference REAL NOT NULL DEFAULT 0," +
            "Notes TEXT NOT NULL DEFAULT ''," +
            "CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')))";
        using var dcCmd = new SQLiteCommand(dailyCloseTable, conn);
        dcCmd.ExecuteNonQuery();

        var expensesTable = "CREATE TABLE IF NOT EXISTS Expenses (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT," +
            "Amount REAL NOT NULL," +
            "Category TEXT NOT NULL," +
            "Description TEXT NOT NULL," +
            "ReferenceNo TEXT," +
            "CashierUsername TEXT NOT NULL," +
            "Timestamp TEXT NOT NULL DEFAULT (datetime('now','localtime')))";
        using var expCmd = new SQLiteCommand(expensesTable, conn);
        expCmd.ExecuteNonQuery();

        // Migrate: add UserName to DailyClose
        using var checkDcUser = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('DailyClose') WHERE name = 'UserName'", conn);
        if (Convert.ToInt32(checkDcUser.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE DailyClose ADD COLUMN UserId INTEGER NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
            alter.CommandText = "ALTER TABLE DailyClose ADD COLUMN UserName TEXT NOT NULL DEFAULT ''";
            alter.ExecuteNonQuery();
        }

        // Migrate: add TotalExpenses to DailyClose
        using var checkDcExp = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('DailyClose') WHERE name = 'TotalExpenses'", conn);
        if (Convert.ToInt32(checkDcExp.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE DailyClose ADD COLUMN TotalExpenses REAL NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
        }

        // Migrate: add Denom20 to DailyClose
        using var checkDcDenom20 = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('DailyClose') WHERE name = 'Denom20'", conn);
        if (Convert.ToInt32(checkDcDenom20.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE DailyClose ADD COLUMN Denom20 INTEGER NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
        }

        // Migrate: add OpeningCash to DailyClose
        using var checkDcOpen = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('DailyClose') WHERE name = 'OpeningCash'", conn);
        if (Convert.ToInt32(checkDcOpen.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE DailyClose ADD COLUMN OpeningCash REAL NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
        }

        // AuditLog table
        var auditLogTable = "CREATE TABLE IF NOT EXISTS AuditLog (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT," +
            "Action TEXT NOT NULL," +
            "SettingKey TEXT NOT NULL," +
            "OldValue TEXT NOT NULL DEFAULT ''," +
            "NewValue TEXT NOT NULL DEFAULT ''," +
            "UserName TEXT NOT NULL DEFAULT ''," +
            "CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')))";
        using var alCmd = new SQLiteCommand(auditLogTable, conn);
        alCmd.ExecuteNonQuery();

        // Migrate: add UserId, UserName to VoidLog
        using var checkVlUser = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('VoidLog') WHERE name = 'UserId'", conn);
        if (Convert.ToInt32(checkVlUser.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE VoidLog ADD COLUMN UserId INTEGER NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
            alter.CommandText = "ALTER TABLE VoidLog ADD COLUMN UserName TEXT NOT NULL DEFAULT ''";
            alter.ExecuteNonQuery();
        }

        // Migrate: add UserId, UserName to CreditTransactions
        using var checkCtUser = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('CreditTransactions') WHERE name = 'UserId'", conn);
        if (Convert.ToInt32(checkCtUser.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE CreditTransactions ADD COLUMN UserId INTEGER NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
            alter.CommandText = "ALTER TABLE CreditTransactions ADD COLUMN UserName TEXT NOT NULL DEFAULT ''";
            alter.ExecuteNonQuery();
        }

        // Migrate: add ModifiedBy to Products
        using var checkPMod = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Products') WHERE name = 'ModifiedBy'", conn);
        if (Convert.ToInt32(checkPMod.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Products ADD COLUMN ModifiedBy TEXT NOT NULL DEFAULT ''", conn);
            alter.ExecuteNonQuery();
        }

        // Migrate: add image_data to Products
        using var checkImg = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Products') WHERE name = 'image_data'", conn);
        if (Convert.ToInt32(checkImg.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Products ADD COLUMN image_data TEXT", conn);
            alter.ExecuteNonQuery();
        }

        // Migrate: add PointsExempt, PointsPerUnit to Products
        using var checkPEx = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Products') WHERE name = 'PointsExempt'", conn);
        if (Convert.ToInt32(checkPEx.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Products ADD COLUMN PointsExempt INTEGER NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
            alter.CommandText = "ALTER TABLE Products ADD COLUMN PointsPerUnit INTEGER NOT NULL DEFAULT 0";
            alter.ExecuteNonQuery();
        }

        // Migrate: add PointsPerUnit to ProductUnits
        using var checkPuP = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('ProductUnits') WHERE name = 'PointsPerUnit'", conn);
        if (Convert.ToInt32(checkPuP.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE ProductUnits ADD COLUMN PointsPerUnit INTEGER NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
        }

        // Migrate: add ModifiedBy to Customers
        using var checkCMod = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Customers') WHERE name = 'ModifiedBy'", conn);
        if (Convert.ToInt32(checkCMod.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Customers ADD COLUMN ModifiedBy TEXT NOT NULL DEFAULT ''", conn);
            alter.ExecuteNonQuery();
        }

        // Migrate: add ModifiedBy to Users
        using var checkUMod = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name = 'ModifiedBy'", conn);
        if (Convert.ToInt32(checkUMod.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Users ADD COLUMN ModifiedBy TEXT NOT NULL DEFAULT ''", conn);
            alter.ExecuteNonQuery();
        }

        // Migrate: add ReceiptImage to Expenses
        using var checkExpImg = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('Expenses') WHERE name = 'ReceiptImage'", conn);
        if (Convert.ToInt32(checkExpImg.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE Expenses ADD COLUMN ReceiptImage TEXT NOT NULL DEFAULT ''", conn);
            alter.ExecuteNonQuery();
        }

        // Migrate: add InvoiceNo, CustomerName to StockTrail
        using var checkStInv = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('StockTrail') WHERE name = 'InvoiceNo'", conn);
        if (Convert.ToInt32(checkStInv.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE StockTrail ADD COLUMN InvoiceNo TEXT NOT NULL DEFAULT ''", conn);
            alter.ExecuteNonQuery();
            alter.CommandText = "ALTER TABLE StockTrail ADD COLUMN CustomerName TEXT NOT NULL DEFAULT ''";
            alter.ExecuteNonQuery();
        }

        // Migrate DailyClose: remove UNIQUE on CloseDate (allow multiple shifts per day)
        var checkDcUnique = new SQLiteCommand(@"
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'table' AND name = 'DailyClose'
            AND sql LIKE '%UNIQUE%'", conn);
        if (Convert.ToInt32(checkDcUnique.ExecuteScalar()) > 0)
        {
            using var trans2 = conn.BeginTransaction();
            try
            {
                using var cr = new SQLiteCommand(
                    "CREATE TABLE IF NOT EXISTS DailyClose_new (" +
                    "Id INTEGER PRIMARY KEY AUTOINCREMENT," +
                    "CloseDate TEXT NOT NULL," +
                    "TotalSales REAL NOT NULL DEFAULT 0," +
                    "TotalCash REAL NOT NULL DEFAULT 0," +
                    "TotalEWallet REAL NOT NULL DEFAULT 0," +
                    "TotalCredit REAL NOT NULL DEFAULT 0," +
                    "TotalVoided REAL NOT NULL DEFAULT 0," +
                    "Denom1000 INTEGER NOT NULL DEFAULT 0," +
                    "Denom500 INTEGER NOT NULL DEFAULT 0," +
                    "Denom200 INTEGER NOT NULL DEFAULT 0," +
                    "Denom100 INTEGER NOT NULL DEFAULT 0," +
                    "Denom50 INTEGER NOT NULL DEFAULT 0," +
                    "Denom20 INTEGER NOT NULL DEFAULT 0," +
                    "DenomCoins REAL NOT NULL DEFAULT 0," +
                    "CashOnHand REAL NOT NULL DEFAULT 0," +
                    "Difference REAL NOT NULL DEFAULT 0," +
                    "Notes TEXT NOT NULL DEFAULT ''," +
                    "CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')))", conn);
                cr.ExecuteNonQuery();
                using var cp = new SQLiteCommand("INSERT INTO DailyClose_new SELECT * FROM DailyClose", conn);
                cp.ExecuteNonQuery();
                using var dr = new SQLiteCommand("DROP TABLE DailyClose", conn);
                dr.ExecuteNonQuery();
                using var rn = new SQLiteCommand("ALTER TABLE DailyClose_new RENAME TO DailyClose", conn);
                rn.ExecuteNonQuery();
                trans2.Commit();
            }
            catch { trans2.Rollback(); }
        }

        // Deduplicate customers by phone (keep lowest ID), then enforce unique phone
        // Only deduplicates customers WITH a phone — empty-phone customers are left alone
        var cleanDupes = conn.CreateCommand();
        cleanDupes.CommandText = @"
            DELETE FROM Customers WHERE Phone != '' AND Id NOT IN (
                SELECT MIN(Id) FROM Customers WHERE Phone != '' GROUP BY Phone
            )";
        cleanDupes.ExecuteNonQuery();
        // Drop old index (which blocked empty phones) and recreate as partial unique index
        var dropIdx = conn.CreateCommand();
        dropIdx.CommandText = "DROP INDEX IF EXISTS idx_customers_phone";
        dropIdx.ExecuteNonQuery();
        var uniqueIdx = conn.CreateCommand();
        uniqueIdx.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS idx_customers_phone ON Customers(Phone) WHERE Phone != ''";
        uniqueIdx.ExecuteNonQuery();
        // Deduplicate customers by name before creating unique index
        var dedupeName = conn.CreateCommand();
        dedupeName.CommandText = "DELETE FROM Customers WHERE Id NOT IN (SELECT MIN(Id) FROM Customers WHERE Name != '' GROUP BY Name) AND Name != ''";
        dedupeName.ExecuteNonQuery();
        var nameIdx = conn.CreateCommand();
        nameIdx.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS idx_customers_name ON Customers(Name)";
        nameIdx.ExecuteNonQuery();

        // Fix any negative stock values
        using var fixStock = new SQLiteCommand("UPDATE Products SET StockQty = 0 WHERE StockQty < 0", conn);
        fixStock.ExecuteNonQuery();

        // SyncQueue for cloud sync retries
        using var syncQ = new SQLiteCommand(
            "CREATE TABLE IF NOT EXISTS SyncQueue (Id INTEGER PRIMARY KEY AUTOINCREMENT, Endpoint TEXT NOT NULL, Payload TEXT NOT NULL, CreatedAt TEXT NOT NULL)",
            conn);
        syncQ.ExecuteNonQuery();

        using var syncLog = new SQLiteCommand(
            "CREATE TABLE IF NOT EXISTS SyncLog (Id INTEGER PRIMARY KEY AUTOINCREMENT, Endpoint TEXT NOT NULL, Status TEXT NOT NULL, Error TEXT DEFAULT '', CreatedAt TEXT NOT NULL)",
            conn);
        syncLog.ExecuteNonQuery();

        // Seed SMTP and AppTimezone settings if missing
        using var seedMissing = new SQLiteCommand(
            "INSERT OR IGNORE INTO Settings (Key, Value) VALUES " +
            "('SmtpHost', ''), ('SmtpPort', '587'), ('SmtpUser', ''), ('SmtpPass', ''), ('SmtpTo', ''), " +
            "('AppTimezone', '480'), ('LastMasterSync', ''), ('AppTheme', 'Dark'), ('PointsRate', '200')",
            conn);
        seedMissing.ExecuteNonQuery();

        // Fix stale Railway cloud API URL → DigitalOcean
        using var fixUrl = new SQLiteCommand(
            "UPDATE Settings SET Value = 'https://jumong-pos-api-p285q.ondigitalocean.app/api' WHERE Key = 'CloudApiUrl' AND Value LIKE '%railway%'",
            conn);
        if (fixUrl.ExecuteNonQuery() > 0)
        {
            using var logUrl = new SQLiteCommand(
                "INSERT OR IGNORE INTO SyncLog (Endpoint, Status, Error, CreatedAt) VALUES ('/migration', 'OK', 'Fixed stale CloudApiUrl (Railway→DO)', datetime('now','localtime'))",
                conn);
            logUrl.ExecuteNonQuery();
        }

        // Fix stale DigitalOcean cloud API URL → local server
        using var fixUrl2 = new SQLiteCommand(
            "UPDATE Settings SET Value = 'https://admin.jumongdev.com/api' WHERE Key = 'CloudApiUrl' AND Value LIKE '%digitalocean%'",
            conn);
        if (fixUrl2.ExecuteNonQuery() > 0)
        {
            using var logUrl2 = new SQLiteCommand(
                "INSERT OR IGNORE INTO SyncLog (Endpoint, Status, Error, CreatedAt) VALUES ('/migration', 'OK', 'Fixed stale CloudApiUrl (DO→local)', datetime('now','localtime'))",
                conn);
            logUrl2.ExecuteNonQuery();
        }

        // Clear stale SyncQueue entries that failed against old DO URL
        using var clearQueue = new SQLiteCommand("DELETE FROM SyncQueue", conn);
        clearQueue.ExecuteNonQuery();

        // Migrate: add PointsEarned to SaleItems
        using var checkPe = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('SaleItems') WHERE name = 'PointsEarned'", conn);
        if (Convert.ToInt32(checkPe.ExecuteScalar()) == 0)
        {
            using var alter = new SQLiteCommand("ALTER TABLE SaleItems ADD COLUMN PointsEarned INTEGER NOT NULL DEFAULT 0", conn);
            alter.ExecuteNonQuery();
        }

        SeedDefaults(conn);
    }

    private static void SeedDefaults(SQLiteConnection conn)
    {
        var checkUser = "SELECT COUNT(*) FROM Users";
        using var cmd = new SQLiteCommand(checkUser, conn);
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        if (count == 0)
        {
            var insert = "INSERT INTO Users (Username, PasswordHash, Role, FullName) VALUES ('admin', 'admin', 'Admin', 'Administrator')";
            using var insCmd = new SQLiteCommand(insert, conn);
            insCmd.ExecuteNonQuery();
        }

        var checkSettings = "SELECT COUNT(*) FROM Settings";
        using var cmdS = new SQLiteCommand(checkSettings, conn);
        var sCount = Convert.ToInt32(cmdS.ExecuteScalar());
        if (sCount == 0)
        {
            using var trans = conn.BeginTransaction();
            var insertS = "INSERT INTO Settings (Key, Value) VALUES (@key, @val)";
            using var insCmd = new SQLiteCommand(insertS, conn);
            insCmd.Parameters.AddWithValue("@key", "PrinterName");
            insCmd.Parameters.AddWithValue("@val", "");
            insCmd.ExecuteNonQuery();

            insCmd.Parameters.Clear();
            insCmd.Parameters.AddWithValue("@key", "PrintReceipt");
            insCmd.Parameters.AddWithValue("@val", "True");
            insCmd.ExecuteNonQuery();

            insCmd.Parameters.Clear();
            insCmd.Parameters.AddWithValue("@key", "TaxRate");
            insCmd.Parameters.AddWithValue("@val", "0");
            insCmd.ExecuteNonQuery();

            trans.Commit();
        }
    }

    public static SQLiteConnection GetConnection()
    {
        return new SQLiteConnection(_connectionString);
    }

    public static string GetSetting(string key, string defaultValue = "")
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = @key", conn);
        cmd.Parameters.AddWithValue("@key", key);
        var val = cmd.ExecuteScalar();
        return val?.ToString() ?? defaultValue;
    }

    public static void SaveSetting(string key, string value)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@key, @val)", conn);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@val", value);
        cmd.ExecuteNonQuery();
    }
}
