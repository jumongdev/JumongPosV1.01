# JumongPOS — Full Project Guide for AI Agents

## Project Structure
```
C:\Users\ADMIN\Desktop\JumongPosV1.01\
├── JumongPosV1.01.csproj      # WinForms client (.NET 8.0-windows)
├── JumongPos.db                # Local SQLite database (project root)
├── AGENTS.md                   # THIS FILE — agent guide
├── check_cost.csx              # Diagnostic script for zero-cost products
├── Dockerfile                  # Root Dockerfile (not used — cloud API has its own)
├── Data/
│   └── DatabaseHelper.cs       # SQLite schema init & migrations
├── Models/
│   ├── Product.cs              # Id, Name, Barcode, Category, Price, Cost, StockQty...
│   ├── ProductUnit.cs          # UnitName, Price, Cost, QtyPerUnit, IsDefault
│   ├── Sale.cs / SaleItem.cs   # InvoiceNo, GrandTotal, UnitCost, QtyPerUnit...
│   ├── Customer.cs / User.cs / Expense.cs / StockTrail.cs / etc.
├── Services/
│   ├── SaleService.cs          # SaveSale(), GenerateInvoiceNo(), void logic
│   ├── SyncService.cs          # All API calls to cloud (SyncProduct, SyncSale, etc.)
│   ├── ProductService.cs       # Product CRUD (local)
│   ├── ProductUnitService.cs   # ProductUnit CRUD (local)
│   ├── StockService.cs         # Stock receiving, stock trail
│   ├── UpdateService.cs        # GitHub release check & download
│   ├── DailyCloseService.cs    # End-of-shift
│   ├── ExpenseService.cs       # Expense CRUD
│   ├── DataExporter.cs         # Import/Export JSON
│   ├── MigrationService.cs     # Old DB migration tool
│       ├── AppVersion.cs           # Current = "1.0.48"
│   └── ... (PrinterService, EmailService, etc.)
├── Forms/
│   ├── MainForm.cs             # Sidebar navigation (POS, Products, Reports, Settings...)
│   ├── SalesForm.cs            # Point-of-sale cart UI
│   ├── ProductsForm.cs         # Product list + detail panel (now view-only)
│   ├── ProductUnitsForm.cs     # Unit manager (Name, Price, Qty only — Cost auto)
│   ├── SettingsForm.cs         # Organized sections with descriptions + progress popup
│   ├── ReportsForm.cs          # Sales reports
│   ├── StockMovementForm.cs    # Stock trail viewer (with TYPE column)
│   ├── StockReceivingForm.cs   # Stock receiving + history (maximized)
│   └── ... (PaymentForm, EndShiftForm, CustomersForm, etc.)
├── JumongCloudAPI/             # ASP.NET Core Web API
│   ├── Program.cs              # Entry point, CORS, DB init
│   ├── Controllers/
│   │   ├── DashboardController.cs  # Profit/margin queries, master catalog CRUD
│   │   └── SyncController.cs       # Receives sync from desktop app
│   ├── Data/
│   │   └── PgDatabaseHelper.cs     # PostgreSQL schema & migrations
│   ├── wwwroot/
│   │   └── index.html              # Cloud dashboard (admin.jumongdev.com)
│   └── Dockerfile
└── publish/
    ├── v1.0.19/  (exe)
    ├── v1.0.20/  (exe)
    ├── v1.0.21/  (exe)
    ├── v1.0.22/  (exe)
    ├── v1.0.23/  (exe)
    ├── v1.0.24/  (exe)
    ├── v1.0.26/  (exe)
    ├── v1.0.27/  (exe)
    ├── v1.0.28/  (exe)
    ├── v1.0.29/  (exe)
    ├── v1.0.30/  (exe)
    ├── v1.0.31/  (exe)
    ├── v1.0.32/  (exe)
    ├── v1.0.33/  (exe)
    ├── v1.0.34/  (exe)
    ├── v1.0.35/  (exe)
    ├── v1.0.36/  (exe)
    ├── v1.0.44/  (exe)
    ├── v1.0.45/  (exe)
    └── v1.0.48/  (exe) — current
```

## Tech Stack
| Layer | Technology |
|---|---|
| Desktop UI | **WinForms** (.NET 8.0-windows) |
| Local DB | **SQLite** via System.Data.SQLite.Core |
| Cloud API | **ASP.NET Core 8** (Web API) |
| Cloud DB | **PostgreSQL 18** via Npgsql |
| Hosting | **DigitalOcean App Platform** + Managed PostgreSQL |
| Packaging | Self-contained single-file publish (`win-x64`) |
| Updates | GitHub Releases (Settings > UPDATE button) |

## Cloud API
- **URL:** https://jumong-pos-api-p285q.ondigitalocean.app/api
- **Dashboard:** https://admin.jumongdev.com (wwwroot/index.html served by API)
- **Deploy:** git push → auto-build or manual via DigitalOcean API
- **DB connection:** DATABASE_URL env var (PostgreSQL)
- **App ID:** `1bc1369e-6ece-4645-be57-1a7fcf7e90b8`
- **DB ID:** `c6bababf-6a01-418a-9244-a830526f83b3`
- **API Token:** (was shared — should be revoked — see user for current token)

## Stores (in Cloud)
| Store ID | Name |
|---|---|
| `STORE-20260602-7159` | Andengs Superstore - HQ |
| `STORE-20260602-AA36` | Andengs Superstore - HVR |

## Complete Change History

### v1.0.44 — Various Fixes (Email, Dashboard Limits, Category Filter)

| File | Change |
|---|---|
| `Services/EmailService.cs:49-63` | `IsConfigured` now checks instance fields (`_smtpHost`, `_smtpUser`) instead of DB Settings table — fixes End Shift email not sending |
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.44"` |
| `JumongCloudAPI/wwwroot/index.html` | All dashboard list limits increased from 100 to 5000 (Recent Sales, Sale Profits, Void Logs, Expenses) |
| `JumongCloudAPI/wwwroot/index.html` | Void Logs panel moved to Dashboard section only (was visible on all sections) |
| `JumongCloudAPI/wwwroot/index.html` | Added category filter dropdown to Product List page |

**Impact:** End Shift auto-email now works. Dashboard shows all transactions instead of latest 100. Product list can be filtered by category.

### v1.0.43 — Update Master Catalog Only Downloads Changed Products

| File | Change |
|---|---|
| `Services/SyncService.cs:561-598` | `DownloadUpdatedMasterCatalog()` rewritten — no longer calls `DownloadMasterCatalog()` (which downloaded ALL). Instead processes only products from `?since=` filtered API response directly |
| `Services/SyncService.cs:600-680` | Extracted `ProcessProducts()` helper for shared insert/update logic |
| `Forms/SalesForm.cs:610-611` | Search results now show default unit's price (`ProductUnitService.GetDefault`) instead of base product price |
| `Forms/SettingsForm.cs:809-829` | `btnSyncFromCloud_Click` now uses `ShowSyncProgress` progress popup (was blocking button state) |
| `Forms/SettingsForm.cs:833-854` | `btnUpdateMaster_Click` now uses `ShowSyncProgress` progress popup (was blocking button state) |

**Impact:** UPDATE MASTER CATALOG only downloads products that actually changed since last sync. SYNC FROM CLOUD and UPDATE MASTER CATALOG both show non-modal progress popups. POS search shows correct default unit price.

### Profit/Margin Fix (v1.0.18 → v1.0.19)
| File | Change |
|---|---|
| `Forms/SalesForm.cs:456` | Sets `UnitCost = product.Cost * qtyPerUnit` when adding item to cart |
| `JumongCloudAPI/DashboardController.cs` | 3 queries (`sale-profits`, `profit-summary`, debug) now use `COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0)` as fallback when unit_cost = 0 |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `ALTER TABLE sale_items ADD COLUMN IF NOT EXISTS unit_cost` |
| PostgreSQL data | Backfilled 36,002 historic sale_items with product costs |

### Sync From Cloud Feature (v1.0.19)
| File | Change |
|---|---|
| `Services/SyncService.cs:452` | `DownloadMasterCatalog()` — downloads master products + units from cloud, updates local Price/Cost/Category/Units, adds new products with StockQty=0, stock unchanged |
| `Forms/SettingsForm.cs` | Added **SYNC FROM CLOUD** button with description label |
| `JumongCloudAPI/DashboardController.cs` | `GET /products/master/download` endpoint returns all master_products with units as JSON |

### Settings Page Redesign (v1.0.20)
| File | Change |
|---|---|
| `Forms/SettingsForm.cs` | Complete rewrite: organized into 4 sections (RECEIPT SETUP, DISPLAY SETUP, CLOUD SYNC, DATA MANAGEMENT), each button has a gray description text explaining its purpose, fixed scrolling/overlapping |

### Timezone Consistency Fix (v1.0.21 → v1.0.22)
| File | Change |
|---|---|
| `Services/SyncService.cs` | **`ToUtcString()`** renamed behavior: appends local offset `+08:00` instead of converting to UTC. Affects: StockTrail, VoidLog, CreditTransaction, DailyClose |
| `Services/SyncService.cs` | **`SyncExpense()`**: sends local time with `+08:00` offset (removed `.ToUniversalTime()`) |
| `Services/SyncService.cs` | **`SyncCustomer()`**: sends `CreatedAt` with `+08:00` offset (was missing timezone) |
| `Services/SyncService.cs` | **`SyncDailyClose()`**: `CloseDate` now sent with `+08:00` offset |
| PostgreSQL data | Backfilled 21,590 historical records (stock_trails, void_logs, credit_txns, expenses) to Philippine time (+8 hours) |

### Unified Product Management (v1.0.21)
| File | Change |
|---|---|
| `Forms/ProductsForm.cs` | **New/Edit/Units/Delete/Save/Cancel buttons hidden for ALL users** — product creation only via cloud master catalog. Only VIEW STOCK MOV'T, DOWNLOAD MASTER, CHECK COST remain. Grid widened to 78%, name column auto-fills. |
| `Forms/ProductUnitsForm.cs` | **Cost field removed** from input form and DataGridView. Cost auto-calculated as `baseCost × QtyPerUnit`. **ControlBox = false** (cannot close via X button, only Close button). Column headers added. |
| `Forms/SalesForm.cs:456` | `UnitCost` changed from `unit?.Cost ?? product.Cost` to `product.Cost * qtyPerUnit` |
| `JumongCloudAPI/wwwroot/index.html` | Cloud dashboard unit form: **Cost input removed**, auto-calculates as `baseCost × QtyPerUnit` in `collectUnits()`. Column headers (Name, Price, Qty, Default) added. |

### Timezone Simplification (v1.0.25)
| File | Change |
|---|---|
| `Services/SyncService.cs` | **Simplified timezone**: removed ALL `ToUniversalTime()` and `+08:00` offset logic. Sends raw local time string. Cloud has `SET TIMEZONE TO 'Asia/Manila'` so PostgreSQL handles conversion automatically. |
| `Services/SyncService.cs` | `ToUtcString()` now returns local time as-is without offset append |
| `Forms/StockReceivingForm.cs` | Fixed toolbar overlap: `BringToFront()` on toolbar |
| `JumongCloudAPI/wwwroot/index.html` | Cloud dashboard: limit dropdown (50/100/200/500) for stock receiving, removed pagination |
| `JumongCloudAPI/wwwroot/index.html` | Added **Image** column to master products table with upload support |
| `JumongCloudAPI/DashboardController.cs` | Added `GET /products/categories` endpoint, `imageData` to product CRUD |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `image_data TEXT` added to `master_products` |
| `JumongCloudAPI/Controllers/DashboardController.cs` | Version endpoint updated to 1.0.25 |
| PostgreSQL data | Additional backfill: **22,226** total records fixed to PH time |

### Progress Popups & Stock Movement Improvements (v1.0.23 → v1.0.24)
| File | Change |
|---|---|
| `Forms/SettingsForm.cs` | Added `ShowSyncProgress()` — non-modal progress popup. Wired to ALL sync buttons (SYNC ALL, SYNC TODAY, SYNC FROM CLOUD). |
| `Forms/ProductsForm.cs` | DOWNLOAD MASTER now shows progress popup |
| `Forms/StockMovementForm.cs` | TYPE column now shows meaningful values: **Stock Receiving**, **Sale**, **Void/Return**, **Adjustment** |
| `Forms/SaleService.cs` | All StockTrail INSERTs now include **UserName** (cashier name) for sales and voids |
| `Forms/StockReceivingForm.cs` | Stock Receiving History opens **maximized**, proper column headers with names, dock order fixed |

### v1.0.27 Changes
| File | Change |
|---|---|
| `Forms/MainForm.cs` | Added `StartTransferPoll()` — 60-second background timer polls `GetPendingTransfersAsync()`, shows NotifyIcon balloon tip on new transfers, redirects to Inventory on click |
| `JumongCloudAPI/wwwroot/index.html` | Added PRINT buttons to Sale Profits and Warehouse panels |
| `Services/ProductService.cs` | Added `GetLowStockThreshold()` — reads from Settings table (key: `LowStockThreshold`), used by `GetStockStats()` and `Search()` |
| `Forms/ProductsForm.cs` | Cell formatting uses configurable threshold instead of hardcoded 10 |
| `Forms/SalesForm.cs` | `RebuildSearchPanel()` shows 3 states: green (in stock), orange (low stock ≤ threshold), red (out of stock); `btnPay_Click()` prompts to email receipt to customer |
| `Forms/SettingsForm.cs` | Added Low Stock Threshold NumericUpDown in DISPLAY SETUP section, persists to Settings table |
| `Services/EmailService.cs` | Added `SendReceipt(Sale, Customer, List<SaleItem>)` — generates HTML receipt and sends to customer email |

### v1.0.28 Changes
| File | Change |
|---|---|
| `Forms/SalesForm.cs:1117` | Wired up `btnRemove` — dead handler connected (`btnRemove.Click += btnRemove_Click`) |
| `Forms/SalesForm.cs:793` | Replaced print prompt with `PrintReceipt` setting — auto-prints if `"True"`, skips if `"False"`, no dialog |
| `Models/PendingTransfer.cs` | Created — extracted `PendingTransfer` and `TransferItem` classes from SyncService.cs |
| `Services/SyncService.cs` | Removed duplicate `PendingTransfer` / `TransferItem` class definitions |

### v1.0.28 Bug Fixes
| # | File | Fix |
|---|---|---|
| 1 | `JumongCloudAPI/Controllers/SyncController.cs` | **CRITICAL** Parameterized `store_id` in all 9 sync endpoints to prevent SQL injection |
| 2 | `Forms/PaymentForm.cs` | **CRITICAL** Changed `FlashMethodButton()` from `async void` to `async Task` — crash on exception |
| 3 | `Services/ProductService.cs` | **HIGH** Parameterized `@thresh` in `GetStockStats()` and `Search()` |
| 4 | `Forms/CustomerDisplayForm.cs` | **HIGH** Added `FormClosing` handler that hides instead of closing (prevents disposed object access) |
| 5 | `Forms/MainForm.cs` | **MEDIUM** Subscribed NotifyIcon events before `ShowBalloonTip()`; added `Dispose()` on click |
| 6 | `Forms/ReportsForm.cs` | **MEDIUM** Added null check on `e.CellStyle` before accessing |
| 7 | `Forms/EndShiftForm.cs` | **MEDIUM** Replaced empty `catch { }` with user-visible error message |
| 8 | `Forms/SettingsForm.cs` | **MEDIUM** Wrapped `btnSyncFromCloud_Click` in try-catch to prevent `async void` crash |
| 9 | `Forms/SalesForm.cs` | **LOW** Removed unused `_lastBarcodeKeystroke` field |
| 10 | `Forms/SettingsForm.cs` | **LOW** Changed `.Wait()` → `await` and `Thread.Sleep` → `Task.Delay` in sync methods; `ShowSyncProgress` now accepts `Func<..., Task<int>>` |

### v1.0.29 — Tax, Discount, SMTP Config & Loyalty Points

#### Tax Support
| File | Change |
|---|---|
| `Services/DatabaseHelper.cs` | Added `TaxRate` setting seed to Settings table on DB init |
| `Services/SaleService.cs` | `SaveSale()` now reads `TaxRate` from Settings, stores `Tax` on Sale |
| `Forms/SalesForm.cs` | `UpdateTotals()` computes tax from `_taxRate` setting; shows tax line in cart footer UI |
| `Forms/SalesForm.cs` | `btnPay_Click()` computes and passes `taxAmt` to the Sale object |
| `Models/Sale.cs` | Added `Tax` property |
| `Models/SaleItem.cs` | Added `Tax` property (per-item) |
| `Services/PrinterService.cs` | Receipt prints tax line |
| `Services/SyncService.cs` | `SyncSale()` includes Tax in synced JSON |

#### Discount Engine
| File | Change |
|---|---|
| `Forms/SalesForm.cs` | Added `_discountPercent` field; `lblDiscountVal` click opens InputBox for discount % |
| `Forms/SalesForm.cs` | `UpdateTotals()` applies discount before tax |
| `Forms/SalesForm.cs` | `btnPay_Click()` passes `discountAmt` to Sale |
| `Models/Sale.cs` | Added `Discount` property |
| `Services/PrinterService.cs` | Receipt prints discount line (when > 0) |
| `Services/SyncService.cs` | `SyncSale()` includes discount in synced JSON |
| `Data/DatabaseHelper.cs` | Added `DiscountPercent` setting seed |

#### Configurable SMTP
| File | Change |
|---|---|
| `Forms/SettingsForm.cs` | Added EMAIL SETUP section with SMTP Host, Port, User, Pass, Recipient fields; saved to Settings table |
| `Services/EmailService.cs` | Constructor reads SMTP settings from Settings table (falls back to hardcoded defaults) |
| `Services/EmailService.cs` | `IsConfigured` now checks for configured SMTP host + user |

#### Loyalty Points
| File | Change |
|---|---|
| `Services/CustomerService.cs` | Added `UpdateLoyaltyPoints(id, points)` method |
| `Forms/PaymentForm.cs` | Added points redemption UI: shows available points, click to redeem, deducts from grand total |
| `Forms/PaymentForm.cs` | Added `PointsUsed` public property, uses `_effectiveTotal` for all payment calculations |
| `Forms/SalesForm.cs` | `btnPay_Click()` awards 1 point per ₱100 spent and deducts redeemed points after payment |

### v1.0.30 — PostgreSQL Multi-PC (Dual Database)

#### Npgsql Dependency
| File | Change |
|---|---|
| `JumongPosV1.01.csproj` | Added `Npgsql 10.0.3` NuGet package for direct PostgreSQL connectivity |

#### CloudDatabaseHelper (new)
| File | Change |
|---|---|
| `Data/CloudDatabaseHelper.cs` | New class: reads PG connection string from SQLite Settings, provides `GetConnection()`, `TestConnection()`, `EnsureSchemaAsync()`, `IsConfigured` |

#### Dual-Database Services
All shared entities (Products, Customers, Users, ProductUnits, Stock) now read from PostgreSQL first, fall back to SQLite. Writes go to both databases.

| File | Change |
|---|---|
| `Services/ProductService.cs` | All CRUD methods try PG first, fall back to SQLite; `TryWriteToPgAsync()` writes upsert to PG; `MapPg()` for PG reader |
| `Services/CustomerService.cs` | Same dual pattern: GetAll, GetById, GetByPhone, Search, Save, Delete, UpdateLoyaltyPoints, UpdateCreditBalance |
| `Services/UserService.cs` | Same dual pattern: GetAll, Save, Delete + `TryWriteToPgAsync()` |
| `Services/ProductUnitService.cs` | Same dual pattern: GetByProduct, GetDefault, Save, Delete |
| `Services/StockService.cs` | `ConfirmReceiving()` updates PG stock; GetByBarcode/Search try PG first |

#### Settings UI
| File | Change |
|---|---|
| `Forms/SettingsForm.cs` | Added **CLOUD DATABASE** section with PG Host/Port/Database/User/Pass/SSL fields, **TEST CONNECTION** button, **MIGRATE TO CLOUD DB** button (progress popup) |

#### What Stays SQLite-Only
Sales, SaleItems, Expenses, DailyClose, StockTrails, Settings (per-PC operational data)

#### Online Ordering Pipeline (completed)
| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/order.html` | Fixed API URL from Railway to relative path |
| `JumongCloudAPI/wwwroot/manifest.json` | Created for PWA support |
| `Forms/PendingOrdersForm.cs` | New form: lists pending warehouse transfers, **Process Order** button auto-matches items to local products, opens SalesForm with cart pre-populated |
| `Forms/SalesForm.cs` | Added `LoadFromTransfer(orderId, customerName, items)` — skips order-type prompt, populates cart from transfer items; `btnPay_Click` auto-marks transfer received on sale complete |
| `Forms/MainForm.cs` | Added **Online Orders** sidebar button; transfer poll interval reduced 60s→15s; button text shows pending count badge; balloon tip links to Online Orders |
| `Services/SyncService.cs` | (no change) existing `GetPendingTransfersAsync()` and `MarkTransferReceivedAsync()` used |

### v1.0.36 — Sale Date Timezone Fix

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.36"` |
| `Services/SyncService.cs:160` | Wraps `sale.SaleDate` with `DateTime.SpecifyKind(..., Local)` so System.Text.Json serializes with `+08:00` offset — cloud API receives PH timezone correctly |

**Impact:** Fixes synced sales showing wrong date/time on cloud dashboard (was off by 8 hours because `Unspecified` DateTime was treated as UTC).

### v1.0.37 — Comprehensive Timezone Fix (ToUtcString + SpecifyKind)

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.37"` |
| `Services/SyncService.cs:355-362` | `ToUtcString()` now appends `+08:00` offset (was a no-op returning bare string). Affects: VoidLog, StockTrail, CreditTxn, DailyClose |
| `Services/SyncService.cs:267` | `DailyClose.CloseDate` now uses `ToUtcString()` for timezone offset (was bare string) |
| `Services/SyncService.cs:288-301` | `SyncExpense` simplified: uses `ToUtcString()` instead of custom offset logic (behavior unchanged) |
| `Services/SaleService.cs:153` | `GetByInvoiceNo()`: `DateTime.Parse` → `DateTime.SpecifyKind(..., Local)` |
| `Services/SaleService.cs:304` | `MapSale()`: `DateTime.Parse` → `DateTime.SpecifyKind(..., Local)` |
| `Services/ProductService.cs:360` | `Product.Map()`: `DateTime.Parse` → `DateTime.SpecifyKind(..., Local)` |
| `Services/StockService.cs:193` | `Product.Map()`: `DateTime.Parse` → `DateTime.SpecifyKind(..., Local)` |
| `Services/CustomerService.cs:332` | `Customer.Map()`: `DateTime.Parse` → `DateTime.SpecifyKind(..., Local)` |
| `Services/CreditService.cs:352` | `Customer.MapCustomer()`: `DateTime.Parse` → `DateTime.SpecifyKind(..., Local)` |
| `Forms/RetrieveHeldCartForm.cs:37` | `HeldCart.CreatedAt`: `DateTime.Parse` → `DateTime.SpecifyKind(..., Local)` |

**Impact:** Eliminates root cause of recurring timezone bugs: `DateTime.Parse` from SQLite always produces `Kind = Unspecified`, which `System.Text.Json` serializes without timezone offset. Now ALL `DateTime` properties carry `Kind.Local` at the point of SQLite read, so they serialize with `+08:00`. String-based timestamp sync methods now also send explicit offset via `ToUtcString()`.

### v1.0.37b — Cloud Dashboard Timezone Display Fix

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/index.html` | Added `timeZone:'Asia/Manila'` to all 10 `toLocaleDateString`/`toLocaleTimeString` calls — times now display in PH time regardless of browser timezone |
| `JumongCloudAPI/wwwroot/order.html` | Same fix for warehouse order list |

**Impact:** Cloud dashboard was displaying UTC times in the browser's local timezone because `toLocaleDateString('en-PH')` only controls date formatting, not timezone conversion. Now all dates explicitly use `timeZone: 'Asia/Manila'` so the dashboard shows correct PH time from any browser.

### v1.0.35 — Cloud API URL Auto-Fix + Retry MarkSynced

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.35"` |
| `Data/DatabaseHelper.cs` | Added migration: auto-fixes stale Railway `CloudApiUrl` → DigitalOcean on startup |
| `Services/SyncService.cs` | `RetryFailedAsync()` now calls `MarkSynced()` on successful `/sales` retry |

**Impact:** Fixes silent sync failure caused by old Railway API URL lingering in DB. Background retries now properly mark sales as synced on success.

### v1.0.34 — SMTP/PG Settings Seed Migration

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.34"` |
| `Data/DatabaseHelper.cs` | Added migration: `INSERT OR IGNORE` seeds missing SMTP (SmtpHost, SmtpPort, SmtpUser, SmtpPass, SmtpTo) and PG (PgHost, PgPort, PgDatabase, PgUser, PgPass, PgSsl) settings for existing DBs |

**Impact:** New and existing databases now get SMTP and PostgreSQL connection settings seeded automatically. Previously these were only created if the Settings table was empty. Fixes blank EMAIL SETUP and CLOUD DATABASE fields in Settings.

### v1.0.33 — Sale Date Sync Fix + Unsynced-Only Query

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.33"` |
| `Services/SyncService.cs:160` | Fixed `saleDate` format: pass raw `DateTime` instead of `ToString("yyyy-MM-dd HH:mm:ss")` — cloud now receives ISO 8601 format, fixing 400 validation error |
| `Services/SaleService.cs:195` | Added `bool? synced = null` param to `GetSales()` — SQL filters `WHERE Synced = @synced` |
| `Forms/SettingsForm.cs:706-708` | `btnSyncToday_Click` now uses `GetSales(..., synced: false)` — only loads unsynced sales, no in-memory filtering |

**Impact:** Sales now sync to cloud dashboard correctly. SYNC TODAY only queries unsynced sales from SQLite directly — faster, no wasted loops.

### v1.0.32 — Stock Receiving Form Layout Fix

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.32"` |
| `Forms/StockReceivingForm.cs:325` | `dgvPending` Y from 32→40 to fix overlap with "PENDING ITEMS" label, column headers, and first row |
| `Forms/StockReceivingForm.cs:331` | Remove column header changed from `""` to `"✕"` |
| `Forms/StockReceivingForm.cs:334` | Added `AutoSizeMode = None` to Remove column so Width=35 is respected (was overridden by Fill mode) |
| `Forms/StockReceivingForm.cs:382-383` | `ResizeLayout` updated to match new Y=40 and adjusted height (`availH - 48`) |
| `Forms/StockReceivingForm.cs:208` | `ShowTrail()` toolbar height 50→60, controls Y adjusted for vertical centering |
| `Forms/StockReceivingForm.cs:213` | `ShowTrail()` title size 300×28→350×30 to prevent DPI/font overflow |
| `Forms/StockReceivingForm.cs:216` | `ShowTrail()` `AutoSizeColumnsMode` `AllCells`→`Fill`, `ColumnHeadersHeight` 32→35 |
| `Forms/StockReceivingForm.cs:257` | `ShowTrail()` removed unnecessary `BringToFront()`, toolbar added before DataGridView |
| `JumongPos.db` | SMTP and PostgreSQL connection details seeded into Settings table |

### v1.0.31 — Void Sync Fix (VoidLog + StockTrail + CreditTxn)

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.31"` |
| `Services/SaleService.cs:418-479` | `VoidSale()` now syncs all void logs, stock trails, and credit transactions to cloud after committing (was only syncing sale state) |
| `Services/SaleService.cs:588-589` | `VoidItem()` now syncs stock trail to cloud after voiding (was only syncing void log + sale) |
| `Services/SaleService.cs:607-626` | `VoidItem()` now syncs credit transactions to cloud after voiding a credit sale item |
| `JumongCloudAPI/Controllers/DashboardController.cs` | Version endpoint updated to 1.0.31; added `GET /api/dashboard/void-logs` endpoint |
| `JumongCloudAPI/wwwroot/index.html` | Added **VOID LOGS** panel showing per-item action, reason, product, qty, amount, cashier, date/time |

**Impact:** Cloud dashboard now correctly reflects voided sales, stock trail records, and credit balance reversals in real-time. Existing voided sales corrected by running SYNC ALL after update. Void Logs panel lets you see exactly what item was voided and why.

### Change History

### v1.0.45 — Email Error Propagation + Product Deletion Sync + Master Cleanup

#### Email Fix
| File | Change |
|---|---|
| `Services/EmailService.cs:182-188` | `SendEndShiftReport()` now returns actual SMTP error instead of always `null` — UI now shows failure message. Falls back to queue on error. |
| `Forms/MainForm.cs:139-168` | `SendScheduledReport()` no longer silently catches all exceptions — logs failures to `scheduled_report_errors.log` |

#### Product Deletion Sync
| File | Change |
|---|---|
| `Services/ProductService.cs:325-362` | `Delete()` now calls `SyncService.SyncProduct()` with `IsActive=false` after soft-delete, so cloud API per-store `products` table is updated |
| `Services/SyncService.cs:466-563` | `DownloadMasterCatalog()` now deactivates local `SourceId='master'` products whose IDs weren't in cloud response — cleans up products deleted from master catalog |
| `Forms/ProductsForm.cs:720` | DELETE button visible for Admin users (`_currentUser?.Role == "Admin"`) |

**Impact:** Deleting a product locally now syncs to cloud. Running SYNC FROM CLOUD removes locally orphaned products deleted from master catalog. Delete button available for Admin. End shift email errors are now visible.

### v1.0.46 — End Shift Credit Payment Totals Fix

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.46"` |
| `Services/DailyCloseService.cs:86,96` | Fixed `GetShiftTotals()` credit payment queries: changed `Description LIKE 'CREDIT_PAY_CASH\|%'` → `PaymentMethod = 'Cash'` and `'CREDIT_PAY_EWALLET\|%'` → `PaymentMethod = 'E-Wallet'` — the old description patterns never matched the actual stored descriptions (`"Payment - Cash \| ..."`) |
| `Services/DailyCloseService.cs:231-248,309-328` | Fixed `GetCreditCustomersSinceLastClose()` and `GetCreditCustomersBetween()`: replaced `s.GrandTotal` with `COALESCE(SUM(si.TotalPrice), 0)` joining `SaleItems` with `si.IsVoided = 0` — the old query showed the original sale total even when items were voided. Now shows only non-voided item totals. |

**Impact:** End Shift now correctly includes cash and e-wallet credit payments in the difference calculation. Previously `_creditPayCash` and `_creditPayEWallet` were always 0, inflating the shift difference by the amount of credit payments received during the shift. Credit customer list now shows only non-voided items' totals instead of the full sale `GrandTotal` — customer debt reflects voided/refunded items correctly.

### v1.0.48 — Barcode Sync Fix for Master Catalog Update

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.48"` |
| `Services/SyncService.cs:511,663` | Added `Barcode=@b` to UPDATE SQL in both `DownloadMasterCatalog()` and `ProcessProducts()` — barcode changes from cloud master now sync to local client |

**Impact:** Changing a product's barcode in the cloud master catalog and running UPDATE MASTER or SYNC FROM CLOUD now correctly updates the barcode in the local database. Previously the barcode was parsed from cloud JSON but never written during updates (only on new product inserts).

### v1.0.49 — Consistent Timestamps for Stock Trail & Void Log Inserts

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.49"` |
| `Services/StockService.cs:72-73,80-93,96` | `ConfirmReceiving()` now explicitly sets `CreatedAt = TimeHelper.Now` in INSERT (was relying on SQLite `datetime('now','localtime')` which uses machine OS timezone). Sync reuses same `now` variable. |
| `Forms/StockMovementForm.cs:273-275,292` | Adjustment INSERT now explicitly sets `CreatedAt = TimeHelper.Now`. Sync reuses same `now` variable. |
| `Services/SaleService.cs:554-568,570-581,615-616` | VoidItem stock trail and void log INSERTs now explicitly set `CreatedAt = TimeHelper.Now`. Sync calls reuse same `now` variable. |

**Impact:** Fixes time discrepancy between local display and cloud dashboard for stock receiving, adjustments, and void stock trails. Previously these records used SQLite's `datetime('now','localtime')` (machine OS timezone) for local storage but `TimeHelper.Now` (UTC+8 configured offset) for cloud sync. If the machine's OS timezone differed from the configured AppTimezone (+08:00), local and cloud timestamps would differ. Sales were already consistent because SaleService explicitly set `SaleDate` from the same source.

### v1.0.47 — Reports Role Access, Settings Crash Fix, POS Banners, Online Orders Toggle

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.47"` |
| `Forms/ReportsForm.cs` | Redesigned: single date picker (not range), cashier grid empty until Enter pressed in invoice search, admin auto-loads with metrics bar (transaction count + total sales) |
| `Forms/SettingsForm.cs:53-94,123-166` | Admin-only controls (`cmbPosScreen`, `cmbCustomerScreen`) wrapped in `if Admin` in `LoadSettings()` and `btnSave_Click()` — fixes NullReferenceException for cashier |
| `Forms/SettingsForm.cs:682-692` | Added APP UPDATE section at bottom, visible to all users (was admin-only) |
| `Forms/SalesForm.cs:115-155,990-1027` | Added red "UPDATE AVAILABLE" and orange "MASTER: X NEW" banners in topbar with click handlers, checked async on load |
| `Services/SyncService.cs:610-630` | Added `CountPendingMasterUpdates()` lightweight HTTP check for banner |
| `Forms/MainForm.cs:57-66,342-370` | `btnOnlineOrders.Visible` controlled by `EnableOnlineOrders` setting; `LayoutMenuButtons()` stacks visible buttons sequentially removing gaps; called on constructor, after visibility changes, and on `Load` event |
| `Data/DatabaseHelper.cs` | Added `EnableOnlineOrders` setting seed (default `True`) |
| `Services/AppVersion.cs` | Changed `LatestVersion` GitHub URL to `raw.githubusercontent.com` |

**Impact:** Cashier can now open Settings without crash — only RECEIPT SETUP and APP UPDATE sections visible. Reports form simplified for both roles. POS now shows update/master catalog banner alerts. Online Orders button can be hidden via Settings → DISPLAY SETUP. Menu buttons no longer have gaps when some are hidden.

---

# Current App Behavior

### Products Page
| Feature | Any User | Admin |
|---|---|---|
| View product list | ✅ (78% width, name auto-fills) | ✅ |
| View product details (right panel) | ✅ (read-only, 22% width) | ✅ |
| CHECK COST | ✅ | ✅ |
| VIEW STOCK MOV'T | ✅ (TYPE column: Sale/Receiving/Void/Adjustment) | ✅ |
| UPDATE MASTER | ✅ (incremental, all users) | ✅ |
| DELETE | ❌ hidden | ✅ (Admin only) |
| NEW / EDIT / UNITS / SAVE / CANCEL | ❌ hidden for ALL | ❌ hidden for ALL |

### Settings Page
| Button | Description | Progress |
|---|---|---|
| SYNC ALL TO CLOUD | Upload all data (products, sales, expenses, etc.) | ✅ Non-modal popup |
| SYNC TODAY ONLY | Upload today's unsynced data (SQL-level filter, skips synced) | ✅ Non-modal popup |
| SYNC FROM CLOUD | Download master catalog (stock unchanged) | ✅ Non-modal popup |
| VIEW SYNC LOG | History of sync operations | — |
| UPDATE APP | Check GitHub for new version (all users) | — |

### Stock Movement / Receiving
| Feature | Detail |
|---|---|
| Stock Movement TYPE | Sale, Stock Receiving, Void/Return, Adjustment |
| Cashier recorded | ✅ UserName now saved for sales and voids |
| Receiving History | Opens maximized, column headers, docked properly |

## Build & Deploy

**IMPORTANT: After EVERY git push, ALWAYS force-deploy the cloud API to DigitalOcean** (unless the push only touched publish/ or client-only files like Forms/*.cs, Models/*.cs).

### Client App
```powershell
# Build
dotnet publish -c Release -r win-x64 --self-contained true

# Publish new release
dotnet publish -c Release -r win-x64 --self-contained true -o publish\v1.0.XX
gh release create v1.0.XX "publish\v1.0.XX\JumongPosV1.01.exe" `
  --title "v1.0.XX" --notes "Changes" --repo jumongdev/JumongPosV1.01
```

### Cloud API
```powershell
# Deploy to DigitalOcean
git push origin master
# Then ALWAYS force deploy via API (auto-build from push may not trigger):
$body = @{force_build = $true} | ConvertTo-Json
Invoke-RestMethod -Uri "https://api.digitalocean.com/v2/apps/{app_id}/deployments" -Method Post -ContentType "application/json" -Headers @{Authorization = "Bearer {token}"} -Body $body
```

### Database Queries (Cloud PostgreSQL)
Use a temp .NET project with Npgsql. Add machine IP to firewall first:
```powershell
# PUT https://api.digitalocean.com/v2/databases/{db_id}/firewall
# Body: {"rules": [{"type": "app", "value": "{app_id}"}, {"type": "ip_addr", "value": "{your_ip}"}]}
# REMEMBER to remove IP after done!
```

## Key Decisions / Rules
1. **Base product Cost must always be the smallest unit's cost** (per-piece), not a pack/box cost
2. **Unit Cost = baseCost × QtyPerUnit** — auto-calculated, no manual entry
3. **Product management only via cloud master catalog** — local creation/editing disabled
4. **SYNC FROM CLOUD** updates Price/Cost/Category/Units but NEVER changes StockQty
5. **All timestamps** send raw local time string (no offset) — cloud `SET TIMEZONE TO 'Asia/Manila'` handles conversion
6. **Profit queries** in cloud API fallback to `p.cost` when `sale_items.unit_cost = 0`
7. **Sync progress** shown via non-modal popup — user can continue working while syncing
8. **Local DB StoreId must be set to `STORE-DEV-0001` during development/testing** to prevent accidental cloud sync contamination of customer's production data
