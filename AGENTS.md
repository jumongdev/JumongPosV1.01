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
│       ├── AppVersion.cs           # Current = "1.0.90"
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
    ├── v1.0.52/  (exe)
    ├── v1.0.53/  (exe)
    ├── v1.0.54/  (exe)
    ├── v1.0.73/  (exe)
    └── client/   (exe) — latest build at C:\JumongAPI\client\
```

## Tech Stack
| Layer | Technology |
|---|---|
| Desktop UI | **WinForms** (.NET 8.0-windows) |
| Local DB | **SQLite** via System.Data.SQLite.Core |
| Cloud API | **ASP.NET Core 8** (Web API) |
| Cloud DB | **PostgreSQL 18** via Npgsql |
| Hosting | ~~DigitalOcean App Platform~~ → **Local Windows 10 Pro Server PC** (NSSM service) |
| Packaging | Self-contained single-file publish (`win-x64`) |
| Updates | GitHub Releases (Settings > UPDATE button) |
| Remote Access | **Cloudflare Tunnel** → `admin.jumongdev.com` |

## Deploying Cloud API (for AI Agent)

**Use the batch file `C:\Users\ADMIN\Desktop\deploy_api.bat`** — double-click it and select **Run as administrator**. It will:
1. Stop the NSSM service `JumongCloudAPI`
2. Copy all publish files from `JumongCloudAPI\bin\Release\net8.0\win-x64\publish\*` to `C:\JumongAPI\`
3. Restart the service

The batch file lives on the Desktop so it's easy to find. You can only deploy via this batch file because the current PowerShell session is **non-admin** and cannot stop/start services. The batch must always be run **as administrator** (right-click → Run as administrator).

**Build command (run before deploying):**
```powershell
Set-Location -LiteralPath "C:\Users\ADMIN\Desktop\JumongPosV1.01\JumongCloudAPI"; dotnet publish -c Release -r win-x64 --self-contained true
```

## Local Server Infrastructure

### Machine Roles (IMPORTANT — WHERE IS WHAT)
| Machine | Role | What lives there |
|---|---|---|
| `DESKTOP-I097OO9` @ `192.168.1.24` | **DEV machine + CLOUD API HOST** (this PC) | Source at `C:\Users\ADMIN\Desktop\JumongPosV1.01`, API service at `C:\JumongAPI\`, test `C:\JumongAPI\client\`, Cloudflare tunnel, PostgreSQL |
| `DESKTOP-UU8E0D4` @ `192.168.1.37` | **HQ store (Andengs Superstore - HQ)** | POS client at **`C:\Users\ADMIN\Desktop\JumongPosHW\`** ← NOT in `C:\JumongAPI\client\` |
| `DESKTOP-TK63MO6` @ `192.168.1.4` | HVR store | (needs path verify) |
| `DESKTOP-NISQ3Q7` @ `192.168.1.152` | U Got Minimart - Naic | (needs path verify) |
| `DESKTOP-TK63MO6` @ `192.168.0.100` | ACGS - Naic Market | (needs path verify) |

> **GOTCHA:** `C:\JumongAPI\client\` is where the **newest client build gets published** on the dev/API host — it is NOT the running install on the HQ machine. The real HQ POS runs from `C:\Users\ADMIN\Desktop\JumongPosHW\` on the HQ machine. When diagnosing/fixing a store, always target the correct machine via the Agent (see Agent section), not the local `C:\JumongAPI\client\` folder.

| Component | Path / Detail |
|---|---|
| API executable | `C:\JumongAPI\JumongCloudAPI.exe` |
| API output folder | `C:\JumongAPI\` (bin, wwwroot, config files) |
| Client app output (DEV build target) | `C:\JumongAPI\client\JumongPosV1.01.exe` |
| HQ POS client (store machine) | `C:\Users\ADMIN\Desktop\JumongPosHW\JumongPosV1.01.exe` |
| API port | `http://localhost:5000` |
| LAN access | `http://192.168.1.39:5000` |
| Service name | `JumongCloudAPI` (NSSM, Automatic start) |
| Restart command | `Restart-Service JumongCloudAPI` |

### Agent (remote diagnostic) version gotcha
- The Agent reads `AppVersion` from the store DB **once at startup** (it caches the version). If the POS app updates while the Agent keeps running, heartbeats keep reporting the OLD version.
- To refresh: restart the Agent (or the whole POS app, which restarts the Agent). On the dashboard, an agent showing e.g. `v1.1.27` while the app is really `v1.1.33` just means its cached value is stale — restart it. Heartbeats are every 3s, so a `lastSeen` within a few seconds means the agent is alive (not asleep).
- Agent DB resolution: `baseDir\JumongPos.db` (Agent folder), else parent folder. Agent commands: `sql`, `ps`, `readfile`, `writefile`, `update`, `restart`.

### POS QR codes (v1.0.85+)
- POS app reads `StoreQrCodes` (JSON `[{header,file}]`) from local SQLite Settings, then loads `assets\<file>` **relative to the app's own exe folder** (`AppDomain.CurrentDomain.BaseDirectory`).
- If `assets\` folder or the image file is missing on the store machine → header (title) text still shows, but **no picture**. This is a common silent failure — picture "wala".
- The dashboard's **POS QR** panel (`posQrPanel` in wwwroot/components.js) is how you push a QR image to stores: it uploads to the API, then sends `update` (download image) + `sql` (write StoreQrCodes) commands per store via the agents.
- **QR push requires TWO things on the store:** (1) the DB updated, AND (2) the physical image file in `assets\` next to the app. `update` command only works if the assets dir already exists on the store.
- If Admin just sets `StoreQrCodes` but no file (`assets\ugot_qrcode.jpg` doesn't exist), the app shows only the header. Always create the folder and drop the file too.
- Uploaded images go to the API's `wwwroot\assets\` → served at `https://admin.jumongdev.com/assets/<file>`. If a debug push used a 404 URL, the file never lands on the store → same "no picture" symptom. Always `Invoke-WebRequest -Head` the URL first to confirm 200.

## Cloudflare Tunnel
| Item | Detail |
|---|---|
| Tunnel name | `jumong-pos` |
| Tunnel ID | `0b400db6-d379-464b-82d2-eb1149afeffc` |
| Public URL | `https://admin.jumongdev.com` → `localhost:5000` |
| Config file | `C:\Users\ADMIN\.cloudflared\config.yml` |
| Auto-start | `cloudflare_tunnel.vbs` in Windows Startup folder |
| Binary | `cloudflared.exe` (runs as background process, no window) |

## Cloud API
- **DigitalOcean URL:** https://jumong-pos-api-p285q.ondigitalocean.app/api — **NO LONGER IN USE. Verified 2026-08-06: all 4 POS clients already sync to the local server** (all stores show sales + agent heartbeats on `admin.jumongdev.com` that day). DO can be decommissioned.
- **Local URL:** https://admin.jumongdev.com/api (via Cloudflare Tunnel) — ALL 4 POS clients on this
- **DB connection:** `DATABASE_URL` env var (PostgreSQL, default `localhost:5432`), or check Helpers/CloudDatabaseHelper.cs
- **DO App ID:** `1bc1369e-6ece-4645-be57-1a7fcf7e90b8` (ready for cancellation)
- **DO DB ID:** `c6bababf-6a01-418a-9244-a830526f83b3` (ready for cancellation)
- **DO API Token:** `dop_v1_...` (decommissioned, no longer active)

## Stores (in Cloud / Local PG)
| Store ID | Name | Machine | IP |
|---|---|---|---|
| `STORE-20260602-7159` | Andengs Superstore - HQ | DESKTOP-UU8E0D4 | 192.168.1.37 |
| `STORE-20260602-AA36` | Andengs Superstore - HVR | DESKTOP-TK63MO6 | 192.168.1.4 |
| `STORE-20260622-E174` | U Got Minimart - Naic | DESKTOP-NISQ3Q7 | 192.168.1.152 |
| `STORE-20260626-A80C` | ACGS - Naic Market | DESKTOP-TK63MO6 | 192.168.0.100 |
| `STORE-DEV-0001` | DEV - Local Testing | — | — |
| `STORE-WAREHOUSE` | Warehouse (whapp) | — | — |

## Complete Change History

### v1.0.60 — Local Server Migration, Cloudflare Tunnel, NSSM Service, Connection Status

| File | Change |
|---|---|
| `Services/SyncService.cs:783` | Added `CheckConnectionAsync()` — pings `/dashboard/version` with 5s timeout, returns bool |
| `Forms/MainForm.cs` | Added `_lblConnStatus` label (green/red dot at sidebar y=780) + `_connTimer` (10s interval) + `CheckApiConnectionAsync()`; connection indicator refreshed every 10 seconds |
| `JumongCloudAPI/wwwroot/index.html:979,1023` | Fixed Alpine.js `x.status.toUpperCase()` crash on null status — added `(x.status \|\| '').toUpperCase()` fallback |
| `JumongCloudAPI/wwwroot/order.html:161` | Same Alpine.js null fix in `loadOrders()` |
| `Data/DatabaseHelper.cs` | Added migration: auto-fixes stale DigitalOcean `CloudApiUrl` → local `admin.jumongdev.com` on startup |
| — | **Infrastructure:** Migrated from DigitalOcean App Platform to local Windows 10 Pro server PC |
| — | **PostgreSQL:** Exported 21MB data from DO Managed PostgreSQL, imported to local PostgreSQL 18 (`jumongpos` DB). All row counts verified (sales: 17,799, sale_items: 81,915, products: 1,588, etc.) |
| — | **NSSM:** JumongCloudAPI installed as Windows service `JumongCloudAPI` (Running, Automatic start) at `C:\JumongAPI\JumongCloudAPI.exe` |
| — | **Cloudflare Tunnel:** `cloudflared` tunnel `jumong-pos` (ID: `0b400db6-d379-464b-82d2-eb1149afeffc`) → `admin.jumongdev.com` → `localhost:5000`. DNS route added. Auto-start via `cloudflare_tunnel.vbs` in Windows Startup folder |
| — | **Build fix:** Removed `temp_pg/obj` folder to resolve duplicate AssemblyInfo build conflict |

**Impact:** API now runs locally as a Windows service with auto-restart. Dashboard accessible remotely via Cloudflare Tunnel (`admin.jumongdev.com`). POS app shows API connection status (green/red dot). Alpine.js dashboard no longer crashes on null status values. After last POS client switches API URL from DigitalOcean to `admin.jumongdev.com`, DO can be decommissioned.

### v1.0.73 — SYNC ALL Only Pushes Today's Data (No More Full History)

| File | Change |
|---|---|
| `Forms/SettingsForm.cs:579-605` | **`btnSyncAll_Click` rewritten** — removed products, customers, users from sync loop (master data, one-time sync). Filters expenses/voids/stock trails/credit txns to today only. Sends ALL today's sales (regardless of synced status) so previously synced-but-missing records get re-sent. Matches SYNC TODAY pattern. |
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.73"` |

**Impact:** SYNC ALL now only processes today's data — sales, expenses, voids, stock trails, and credit transactions. No more uploading the entire product/customer/user catalog every time. This fixes the case where a sale was marked `Synced=1` but never reached the cloud DB (e.g., `INV-A80C-20260707-0001`): SYNC ALL re-sends all today's sales regardless of sync status.

### v1.0.74 — PublishReadyToRun for Faster Startup

| File | Change |
|---|---|
| `JumongPosV1.01.csproj` | Added `<PublishReadyToRun>true</PublishReadyToRun>` — pre-compiles IL to native code during publish, eliminates JIT delay on slow PCs |
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.74"` |

**Impact:** App startup on older/slower POS machines reduced from ~1 minute to a few seconds. File size increases ~20-30MB but startup is near-instant.

### v1.0.76 — End Shift Denomination Per-Row Totals

| File | Change |
|---|---|
| `Forms/EndShiftForm.cs:428-439` | Added `lblTotal1000`…`lblTotalCoins` field declarations for per-denomination total labels |
| `Forms/EndShiftForm.cs:467-482` | `Recalc()` now computes each denomination total separately and displays in `"= ₱X,XXX"` format per row. Old: just overall `lblCashOnHand`. New: shows `₱1,000 × [qty] = ₱2,000` etc. |
| `Forms/EndShiftForm.cs:486-488,490-498` | `AddDenomRow` signature changed — adds `x` label, `NumericUpDown`, and per-row total `Label` in a horizontal layout. `MakeTotalLabel()` helper creates cyan-colored total labels. |
| `Forms/EndShiftForm.cs:388-395` | `InitializeComponent` denomination section layout changed: `₱1,000 x [num] = ₱total` format. `x` label at x=78, numeric at x=100, total label at x=190. |
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.76"` |

**Impact:** End Shift cash denomination breakdown now shows per-row computed totals (e.g., `₱1,000 x 5 = ₱5,000`) instead of just a quantity input. Makes cash counting more transparent and easier to verify.

### v1.0.75 — Fix Daily Close Sync (CreatedAt Was Never Set)

| File | Change |
|---|---|
| `Forms/EndShiftForm.cs:84` | Added `CreatedAt = now.ToString(...)` to `DailyClose` object — was missing, causing empty string sent to PostgreSQL timestamp column → silent sync failure |
| `Services/DailyCloseService.cs:122-126` | INSERT now includes `CreatedAt` column and parameter |
| `Forms/SettingsForm.cs:579-603` | Added daily closes to `btnSyncAll_Click` loop (were missing from SYNC ALL) |
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.75"` |

**Impact:** End-shift daily closes now sync to cloud correctly. Short/Over card on dashboard now shows data. Historical daily closes will sync when client updates via UPDATE APP and runs SYNC ALL.

### v1.0.83 — Warehouse Walk-in Sell, Universal Customers, Customer Sync Banner

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.83"` |
| `Forms/WarehouseSellForm.cs` | **New file** — POS-style UI for wholesale walk-in sales (topbar, customer picker, product search + units, cart DGV, totals panel, SELL button, thermal receipt). Fixed `BeginInvoke` crash in constructor, `JsonDocument` disposal crash, missing `/dashboard/` in API URLs. |
| `Forms/MainForm.cs:289-304` | Added `btnWhSell` — opens WarehouseSellForm with try-catch + ErrorLogger. Visible on HQ + DEV store IDs. |
| `Services/PrinterService.cs` | Added `PrintWhReceipt()` for warehouse walk-in sale thermal receipt. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | Added `WhGetCustomers()` (with email/address/creditBalance fields), `WhSell()` (POST), `WhGetSales()`, `WhGetSaleItems()` endpoints. Added `GET /customers/count?since=` for pending customer banner. `WhGetProducts()` now supports `?search=` param for server-side filtering. |
| `JumongCloudAPI/wwwroot/index.html` | Added Sales subpage under Warehouse (date filter, items table, VIEW button, sale items modal with Pts column). |
| `JumongCloudAPI/wwwroot/components.js` | Added `salesData`, `saleFrom`, `saleTo`, `saleViewOpen`, `saleViewItems` state + `loadSales()`, `viewSaleItems()`, `closeSaleView()` methods. |
| `Forms/SettingsForm.cs` | Added **SYNC CUSTOMERS FROM CLOUD** button with progress popup. Removed temp upload/delete buttons. |
| `Services/SyncService.cs` | Added `DownloadCustomersAsync()`, `CountPendingCustomerUpdates()`, `SaveLastCustomerSync()`. Uses `LastCustomerSync` timestamp like `LastMasterSync`. Fixed `TryGetProperty` for optional fields. |
| `Forms/SalesForm.cs` | Added **CUSTOMERS: X NEW** purple banner in topbar (same behavior as MASTER banner — click to auto-sync). |
| `Data/DatabaseHelper.cs` | Added migration: deduplicates local customers by name before creating `idx_customers_name` unique index. `LastCustomerSync` setting used. |
| `JumongCloudAPI/Controllers/SyncController.cs` | Customers synced without `store_id` — universal. `ON CONFLICT (name)` for upsert. |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Schema: customers `UNIQUE(name)` instead of `UNIQUE(store_id, pos_id)`. Dropped old constraint. |
| `JumongCloudAPI/Database` | PostgreSQL: 21 duplicate customer records deleted. 310 customers set to `store_id=''`. Unique constraint on `name` added. |

**Impact:** HQ can now do warehouse walk-in sales with POS-style UI. Customers are universal across all stores (no store tag). POS shows purple **CUSTOMERS: X NEW** banner when new customers are available on cloud — click to auto-download. Product search in warehouse sell now uses server-side filtering (faster). `JsonDocument` lifecycle fixed (prevents crashes). Customer sync uses `LastCustomerSync` timestamp tracking (like master catalog).

### v1.0.7 (Cloud API) — Short/Over Summary Card on Dashboard

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:101-118` | `/summary` endpoint now returns `todayVariance` — sum of `difference` from today's `daily_closes` |
| `JumongCloudAPI/wwwroot/index.html:168` | Added **Short / Over** summary card showing OVER (green) or SHORT (red) with amount at a glance. Grid changed from `xl:grid-cols-7` to `xl:grid-cols-8`. |

**Impact:** Admin can see today's total short/over amount right on the dashboard's top summary cards. SHIFT HISTORY panel (same page) shows per-shift breakdown with cashier name and variance.

### v1.0.6 (Cloud API) — Warehouse Import Fixes, Units Display, Edit Form Redesign

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:1255-1298` | **WhAddFromMaster** now reads the master product's default unit's `qty_per_unit` as `box_qty` and its `price` as `box_price` instead of multiplying base price by `boxQty`. Falls back to `boxQty` param if no default unit exists. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1255-1298` | **Duplicate prevention** — WhAddFromMaster now checks if `master_product_id` already exists in `wh_products`. If yes: UPDATE (reactivates `is_active=true`, refreshes prices) and cleans up extra duplicate rows. If no: INSERT. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1284-1326` | **WhBulkImportFromMaster** — same default unit logic via `LEFT JOIN default_units` |
| `JumongCloudAPI/Controllers/DashboardController.cs:1129-1152` | **WhGetProducts** — now returns `units` array from `master_product_units` (JSON aggregated) alongside `boxPrice`/`boxQty`/`piecePrice` |
| `JumongCloudAPI/Controllers/DashboardController.cs:1091-1110` | **UpdateMasterProduct auto-sync** — fixed `wh.box_qty` → `wh_products.box_qty` alias bug (was causing `missing FROM-clause entry for table "wh"`) |
| `JumongCloudAPI/Controllers/DashboardController.cs:1315-1339` | **WhSyncFromMaster** — same `wh` alias fix |
| `JumongCloudAPI/Controllers/DashboardController.cs:738` | **Version** bumped to `"1.0.6"` |
| `JumongCloudAPI/wwwroot/components.js:465-473,476-493,499-508` | **openAdd/openEdit** — uses new form fields (`price`, `cost`, `units[]`) instead of `boxPrice`/`boxQty`/`piecePrice`; `_computeBody()` converts units back to box format on save |
| `JumongCloudAPI/wwwroot/index.html:1036-1065` | **Warehouse edit form** — replaced BOX PRICE / BOX QTY / PIECE PRICE fields with PRICE / COST / UNITS section matching master catalog editor format |
| `JumongCloudAPI/wwwroot/index.html:831-858,866-895` | **Product & Inventory tables** — replaced Box Price / Box Qty / Piece Price columns with Price + Units columns (same inline format as master catalog) |
| `JumongCloudAPI/wwwroot/app.js:56` | **CSV export** — updated headers to `ID,Name,Barcode,Category,Price,Units,Stock` |

**Impact:** Importing from master now uses the product's default unit's qty and price instead of multiplying by an arbitrary box qty. Duplicate imports update the existing warehouse product instead of creating copies. Warehouse table shows units inline (same format as master catalog). Edit form matches the master catalog editor layout. Wh alias bug fixed (was causing 500 on SYNC FROM MASTER and auto-sync).

### v1.0.54 — POS Search Popup, Cashier Display, Stock Receiving Fix, Print Checklist Fix

| File | Change |
|---|---|
| `Forms/ProductSearchForm.cs` | **New file** — popup form with DataGridView, replaces old inline dropdown for product search in POS |
| `Forms/SalesForm.cs` | Removed search textbox + dropdown panel; added **Search (F2)** button that opens `ProductSearchForm` popup; cashier name changed to bold cyan label beside brand |
| `Forms/SalesForm.cs:914` | Master catalog banner click now shows non-modal **progress popup** via `SettingsForm.ShowSyncProgress()` |
| `Forms/SettingsForm.cs:691` | `ShowSyncProgress()` changed from `private` to `internal` so SalesForm can reuse it |
| `Forms/StockReceivingForm.cs:33` | Added `KeyDown` handler for Enter key in product picker — fixes arrow+Enter selecting next row instead of current product |
| `Forms/ProductsForm.cs:603` | **CHECKLIST** button no longer hidden (was accidentally set `Visible=false` with edit buttons) |
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.54"` |

**Impact:** POS search now shows full product names in a wide popup instead of truncated dropdown. Cashier name prominently displayed. Master update shows progress. Stock receiving product picker works correctly with Enter key. Print checklist button restored.

### v1.0.55 — Cloud Dashboard Rewrite (Tailwind+Alpine), Product Analytics, Store Selector Fix

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/index.html` | **Full rewrite** — Tailwind CSS utility classes, Alpine.js reactive binding, collapsed sidebar, dark/light toggle, all panels modernized |
| `JumongCloudAPI/wwwroot/app.js` | **New file** — Alpine store, components (summaryCards, profitCards, trendsChart, etc.), CSV export, toast notifications |
| `JumongCloudAPI/wwwroot/style.css` | **New file** — custom scrollbar, animations, print, modal, x-cloak styles |
| `JumongCloudAPI/Controllers/DashboardController.cs:129-175` | `GetTopProducts` enhanced: added `sort=profit` param, returns barcode, category, totalQty, revenue, cost, profit, marginPct |
| `JumongCloudAPI/Controllers/DashboardController.cs:583-633` | `GetSaleItems` returns `paymentMethod`, `referenceNo`, `ewPaid`, `grandTotal` alongside items |
| `JumongCloudAPI/wwwroot/app.js:43-127` | Fixed Add Product modal always visible on page load — `editorOpen` moved to Alpine store |
| `JumongCloudAPI/wwwroot/index.html` | Fixed store name badges — `text-cyan-300` → `text-cyan-700 dark:text-cyan-300` with `bg-cyan-100 dark:bg-cyan-900/20` |
| `JumongCloudAPI/wwwroot/index.html` | Fixed invoice links showing `...` — added `x-text="x.invoiceNo"` to all 3 invoice `<a>` tags |
| `JumongCloudAPI/wwwroot/index.html` | Fixed invoice click not showing sale details — sale modal state moved to Alpine store, `saleItemsModal` component removed |
| `JumongCloudAPI/wwwroot/index.html` | **Product Analytics panel** added with Top Selling / Most Profitable tabs, sidebar nav item, CSV export |
| `JumongCloudAPI/wwwroot/app.js` | Fixed product search — split `search` and `catFilter` into separate properties |
| `JumongCloudAPI/wwwroot/app.js` | Fixed EDIT button — added `$watch('$store.app.editorOpen')`, moved `editingId` to store |
| `JumongCloudAPI/wwwroot/app.js` | **Fixed store selector empty** — moved store loading inside `alpine:init` callback; populate `<select>` via `innerHTML` instead of `x-for` |
| `JumongCloudAPI/wwwroot/app.js` | Alpine.js loading order fixed: deferred in head, app.js sync at end of body (defer broke component registration) |
| `JumongCloudAPI/Controllers/DashboardController.cs:129-175` | Fixed Most Profitable query — added `total_profit` to SELECT list (ORDER BY referenced non-existent alias) |

**Impact:** Cloud dashboard modernized with Tailwind CSS and Alpine.js. Product Analytics shows top-selling and most profitable products. Store selector works reliably. Search, edit, and sale detail modals work correctly. Most Profitable tab shows data instead of empty.

### v1.0.56 — Light/Dark Theme System & Theme Toggle

| File | Change |
|---|---|
| `Helpers/ThemeManager.cs` | **New file** — `Theme` class with ~50 named color properties; `Dark` and `Light` static themes; `Current`, `LoadTheme()`, `SwitchTheme()` for runtime theme switching |
| `Data/DatabaseHelper.cs` | Added `AppTheme`, `'Dark'` to Settings seed migration |
| `Program.cs` | Calls `ThemeManager.LoadTheme()` on startup after DB init |
| `Forms/MainForm.cs` | Colors use `ThemeManager.Current`; added `ApplyTheme()` (sidebar) and `static ApplyThemeToChildren()` which iterates all open forms and calls `ApplyTheme()` on each |
| `Forms/SalesForm.cs` | All 22 `private static readonly Color` → expression-bodied `=> ThemeManager.Current.XXX`; added `ApplyTheme()` |
| `Forms/PaymentForm.cs` | All static colors → expression-bodied; added `ApplyTheme()` |
| `Forms/ProductsForm.cs` | `InitializeComponent` local vars → `ThemeManager.Current`; DGV column/header colors use `ThemeManager`; CellFormatting/eMetricForeColor use accent properties; added `ApplyTheme()` |
| `Forms/ReportsForm.cs` | All colors themed (`ShowItemPicker`, `LoadReport`, `InitializeComponent`); added `ApplyTheme()` |
| `Forms/SettingsForm.cs` | Colors themed; added **App Theme** dropdown (Dark/Light) in DISPLAY SETUP section (Admin) with `SelectedIndexChanged` that calls `ThemeManager.SwitchTheme()` + `ApplyThemeToChildren()` |
| `Forms/StockReceivingForm.cs` | All colors themed; added `ApplyTheme()` |
| `Forms/StockMovementForm.cs` | All colors themed; added `ApplyTheme()` |
| `Forms/EndShiftForm.cs` | All colors themed across `InitializeComponent`, `btnHistory_Click`, helpers; added `ApplyTheme()` |
| `Forms/CreditManagementForm.cs` | 3 variable blocks + inline DGV/accent colors all themed; added `ApplyTheme()` |
| `Forms/CustomersForm.cs` | 3 variable blocks + `AddField` colors themed; added `ApplyTheme()` |
| `Forms/UsersForm.cs` | Variable block + inline colors + `AddField` themed; added `ApplyTheme()` |
| `Forms/ExpensesForm.cs` | 2 variable blocks + inline DGV colors themed; added `ApplyTheme()` |
| `Forms/VoidLogForm.cs` | Variable block + inline DGV colors themed; added `ApplyTheme()` |
| `Forms/ProductUnitsForm.cs` | Colors themed; fixed `t.InputBg` param name in `AddField`; added `ApplyTheme()` |
| `Forms/LoginForm.cs` | 2 variable blocks themed; added `ApplyTheme()` |
| `Forms/PendingOrdersForm.cs` | 7 static colors → expression-bodied; added `ApplyTheme()` |
| `Forms/ProductSearchForm.cs` | `CBorderLight` → expression-bodied; added `ApplyTheme()` |
| `Forms/CustomerDisplayForm.cs` | 11 inline `Color.FromArgb` → `ThemeManager.Current`; added `ApplyTheme()` |
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.56"` |

**Impact:** All forms now support Dark (default) and Light themes. Theme can be toggled in Settings → DISPLAY SETUP → App Theme dropdown. Switch applies immediately to all open forms via `ApplyThemeToChildren()`. Setting persists across app restarts. Dark theme preserves original look; Light theme converts management forms to white/light-gray backgrounds with dark text.

### v1.0.57 — Stock Movement TYPE & CASHIER Fix + Sale Stock Trail Sync to Cloud

| File | Change |
|---|---|
| `Forms/StockMovementForm.cs:129-141` | **TYPE column fix** — new priority logic: Adjustment → Void/Return → Stock Receiving → Sale → `—`. Previously void restocks showed "Stock Receiving" and negative adjustments showed "Walk-in". |
| `Services/SaleService.cs:62,121-137` | Sale flow now collects stock trail IDs after each `INSERT` and syncs them to cloud API after commit. Previously only sync'd product + sale, missing the stock trail. |
| `Services/SaleService.cs:143-144` | Added `foreach (var st in trailList) SyncStockTrail(st)` after commit and before sync product + sale. |

**Impact:** StockMovementForm TYPE column now correctly shows "Void/Return" for void restocks and "Adjustment" for stock adjustments instead of "Stock Receiving"/"Walk-in". Sale stock trails are now pushed to cloud dashboard (both TYPE and CASHIER columns populated). Previously only receiving, adjustment, and void trails synced; sales were local-only.

### v1.0.58 — Warehouse Auto-Sync, Box Qty Config, Bulk Import, POS Receiving Fix

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:1214-1247` | **WhAddFromMaster** now accepts `?boxQty=` param (was hardcoded to 12). New **bulk import** endpoint `POST /warehouse/products/from-master/category/{cat}`. New **sync** endpoint `POST /warehouse/sync-from-master` updates all linked warehouse products from master catalog. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1091-1104` | **Auto-sync** — when a master product is saved (name/barcode/price/cost change), linked warehouse products are automatically updated (preserves existing `box_qty`). |
| `JumongCloudAPI/wwwroot/components.js:574` | **Bug fix** — `importFromMaster()` now fetches from `/products/master` instead of `/warehouse/products` (was showing warehouse products, not master catalog). |
| `JumongCloudAPI/wwwroot/components.js:572-593` | Added `importBoxQty`, `doBulkImport(category)`, `syncFromMaster()` handlers. |
| `JumongCloudAPI/wwwroot/index.html:789` | **ADD button** hidden for Products tab (manual product creation not advised — use FROM MASTER). |
| `JumongCloudAPI/wwwroot/index.html:789` | Added **SYNC FROM MASTER** button beside FROM MASTER for bulk price/name sync. |
| `JumongCloudAPI/wwwroot/index.html:1384-1407` | Import modal now shows **Box Qty** input (default 12) and **ALL IN CAT** button per category for bulk import. |
| `Forms/PendingOrdersForm.cs:60-113` | **Process Order** now adds stock via `StockService.ConfirmReceiving()` instead of opening a sale cart (was double-deducting stock). |
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.58"` |

**Impact:** Warehouse products stay in sync with master catalog automatically. Box quantity per product is configurable during import. Bulk import by category available. POS "Process Order" correctly adds received stock instead of opening a sale. Manual product creation in warehouse deprecated — use FROM MASTER.

### v1.0.53 — Email Report ₱ Encoding Fix

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.53"` |
| `Services/EmailService.cs:120-127` | Replaced `₱` (PHP symbol) with `Php` in Cash Denomination table — fixes `?` character display in email clients |

**Impact:** End shift email now shows `Php 77,000.00` instead of `?77,000.00` in the denomination breakdown section.

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

### v1.0.50 — Product List Unit Display + Stock Trail Timestamp Backfill

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/index.html:788-801` | Product list "Units" column now shows each unit's name, price, and default marker (`*`) inline instead of just the unit count. Products without units show `—`. |
| `JumongCloudAPI/Controllers/DashboardController.cs:675-717` | Added `GET /fix-stock-trails-after-jun14` endpoint — backfills stock_trails, void_logs, and credit_transactions where Manila hour < 8 (wrong UTC-based timestamps from pre-v1.0.49 data) by adding 8 hours. |

**Impact:** Dashboard product list now shows unit prices at a glance for price verification. Old stock trail/void/credit records with wrong timestamps (off by 8 hours) can be fixed by hitting the fix endpoint.

### v1.0.51 — Reports Payment Method Filter

| File | Change |
|---|---|
| `Services/SaleService.cs:195` | Added `paymentMethod` optional param to `GetSales()` — filters via `WHERE s.PaymentMethod = @pm` |
| `Forms/ReportsForm.cs:27-29` | Reads combobox selection, passes payment method filter to `GetSales` |
| `Forms/ReportsForm.cs:217-222` | Added **Method** combobox (All / Cash / E-Wallet / Credit / Split) to toolbar, triggers reload on change |

**Impact:** Reports page now has a payment method dropdown to filter sales by Cash, E-Wallet, Credit, or Split. Selecting a method instantly filters the grid.

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

### v1.0.53 — Email Report ₱ Encoding Fix

| File | Change |
|---|---|
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.53"` |
| `Services/EmailService.cs:120-127` | Replaced `₱` (PHP symbol) with `Php` in Cash Denomination table — fixes `?` character display in email clients |

**Impact:** End shift email now shows `Php 77,000.00` instead of `?77,000.00` in the denomination breakdown section.

### v1.0.59 — Warehouse Transfer Partial Receive (Checklist on POS, Shortage Restock)

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:937-1000` | `WhGetOrderItems` — new `GET /warehouse/orders/{id}/items` endpoint returning items with productId, name, barcode, baseQty, receivedQty |
| `JumongCloudAPI/Controllers/DashboardController.cs:905-935` | `WhReceiveOrder` rewritten — accepts `[FromBody] WhReceiveRequest? body` for partial receive; shortages restock `wh_products.stock_qty`; status set to `"received"` or `"partial"` |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs:428` | Added migration: `received_qty INTEGER NOT NULL DEFAULT 0` to `wh_order_items` |
| `JumongCloudAPI/Controllers/DashboardController.cs` | `WhGetOrders` now includes `hasShortage` (checks `received_qty < base_qty`) |
| `JumongCloudAPI/wwwroot/components.js` | `receiveOrder()` sends `{}` body, shows shortage warning toast |
| `JumongCloudAPI/wwwroot/index.html` | Orders status cell shows PARTIAL badge when `hasShortage=true` |
| `Services/SyncService.cs:396-443` | Added `GetTransferItemsAsync(orderId)` — fetches items for checklist; `MarkTransferReceivedAsync` changed from `PUT` no-body to `PUT` with JSON body, returns `ReceiveResult` with `Shortages` list |
| `Models/PendingTransfer.cs:13` | Added `ProductId` field to `TransferItem` |
| `Forms/PendingOrdersForm.cs` | **Rewritten** — `btnProcess_Click` fetches items, shows modal checklist with checkboxes per item, unmatched items (not in local POS) highlighted in red; `ShowItemPicker()` returns checked items; calls `ConfirmReceiving()` for each, reports shortages |
| `Forms/SalesForm.cs` | Removed `LoadFromTransfer()` method, `_onlineOrderId`, `_skipOrderTypePrompt` fields (dead code from old online ordering flow) |
| `Forms/StockReceivingForm.cs:108-133` | Updated to use new `ReceiveResult` return type from `MarkTransferReceivedAsync`; shows shortage count |
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.59"` |

**Impact:** POS can now partially receive warehouse transfers — uncheck missing items, only accepted items get added to stock. Cloud API restocks warehouse inventory for unreceived items (shortages). Order status shows "partial" on cloud dashboard. Old dead code cleaned up. StockReceivingForm's "CHECK PENDING TRANSFERS" also correctly handles the new API.

### v1.0.6 — Fix Alpine x-for Duplicate Keys (Index-Based Keys for All Templates)

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/index.html:623,827,857,884,961,999,1405` | Changed 7 `x-for` templates from `:key="x.id"` to `:key="i"` (index-based) — API returns records with missing/duplicate `id` values, causing Alpine Warning + cascade crash |

**Impact:** Eliminates `Duplicate key on x-for` Alpine warnings and `Cannot read properties of undefined (reading 'after')` cascade errors. All dashboard sections (master products, warehouse products/clients/orders, customers, users, import modal) now render without console errors. Index-based keys guarantee uniqueness regardless of data quality.

### v1.0.7 — Warehouse Split into 4 Subpages (Product, Inventory, Online Order, Transfer)

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/index.html` | Sidebar: replaced single Warehouse nav item with WAREHOUSE header + 4 indented sub-items (Product, Inventory, Online Order, Transfer) |
| `JumongCloudAPI/wwwroot/index.html:772-936` | Warehouse section restructured: subpage nav replaces tab bar; each subpage has dedicated table/toolbar |
| `JumongCloudAPI/wwwroot/components.js:14,66-75` | Alpine store gains `whSubpage`, `switchWhSubpage()`, `isActive()`; `switchSection()` maps `wh-*` IDs to section+subpage |
| `JumongCloudAPI/wwwroot/components.js:442-633` | `warehousePanel` rewritten: `products`,`clientsData`,`orders`,`transfers` arrays replace single `data`; all methods use `sp` getter instead of `tab` |
| `JumongCloudAPI/wwwroot/app.js:52-65` | `exportCSV` updated for new subpage names (`wh-product`,`wh-inventory`,`wh-onlineorder`,`wh-transfer`) |

**Product subpage** — product list (ID, Name, Barcode, Category, Box Price, Box Qty, Piece Price) with EDIT/DEL actions + FROM MASTER import. **Inventory subpage** — stock-focused view (Stock column, ADJUST button to set qty). **Online Order subpage** — client management (+ ADD/EDIT/DEL) + order tracking (VIEW/PROCESS/SHIP/RECEIVE/CANCEL). **Transfer subpage** — pending transfers with RECEIVE. Category filter pills show on Product & Inventory subpages.

### v1.0.8 — Warehouse Transfer Rework (Dedicated Transfer System + Section Name Header)

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs:404-427` | Added `wh_transfers` and `wh_transfer_items` tables with indexes — dedicated warehouse-to-POS stock transfer tables (separate from customer orders) |
| `JumongCloudAPI/Controllers/DashboardController.cs:1629-1784` | Added 5 new endpoints: `GET /warehouse/transfers`, `POST /warehouse/transfers`, `GET /warehouse/transfers/{id}/items`, `PUT /warehouse/transfers/{id}/receive` (partial support), `GET /warehouse/transfers/pending-count` |
| `JumongCloudAPI/wwwroot/components.js:552-617` | Added transfer CRUD methods to `warehousePanel`: `openNewTransfer/saveTransfer/receiveTransfer/viewTransfer/cancelTransfer` + item management |
| `JumongCloudAPI/wwwroot/components.js:648-651` | `updateBadge()` changed from `/transfers/pending` to `/transfers/pending-count` (returns `{pending: N}`) |
| `JumongCloudAPI/wwwroot/index.html:798` | Added **"+ NEW TRANSFER"** button to Transfer subpage toolbar |
| `JumongCloudAPI/wwwroot/index.html:1017-1052` | **Transfer subpage** redesigned: shows all transfers with ID/POS Client/Status/Notes/Date/Actions (VIEW, RECEIVE for pending); status badges with color coding (PENDING yellow, COMPLETED green, PARTIAL orange) |
| `JumongCloudAPI/wwwroot/index.html:1280-1345` | **New Transfer modal** — select POS client (filtered to `storeType='pos'`), add products with qty from warehouse product list, create transfer |
| `JumongCloudAPI/wwwroot/index.html:1348-1378` | **Transfer View modal** — shows items with product/barcode/qty/received/current stock columns |
| `JumongCloudAPI/wwwroot/index.html:789-793` | Added **WAREHOUSE** section header with dynamic subpage name (Products/Inventory/Online Orders/Transfers) |
| `JumongCloudAPI/wwwroot/app.js:58` | CSV export updated for new transfer format (`ID,Client,Status,Notes,Date`) |
| `Services/AppVersion.cs` | `Current` bumped to `"1.0.8"` |

**Impact:** Transfer subpage is now a standalone warehouse-to-POS stock transfer system, separate from customer orders (Online Order). Transfers have their own lifecycle (pending → completed/partial). Create transfers directly by selecting a POS client and adding products from warehouse inventory. Old Online Order → shipped → receive flow still intact for customer ordering. Warehouse section now displays a title header. Build 0 errors.

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
| SYNC ALL TO CLOUD | Upload today's sales + expenses + voids + stock trails + credit txns only (no master data) | ✅ Non-modal popup |
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

**IMPORTANT: After EVERY git push, build and deploy the cloud API to the local server** (unless the push only touched publish/ or client-only files like Forms/*.cs, Models/*.cs).

### Client App
```powershell
# Build
dotnet publish -c Release -r win-x64 --self-contained true

# Publish new release to C:\JumongAPI\client\
dotnet publish -c Release -r win-x64 --self-contained true -o C:\JumongAPI\client
```

### API URL Change
**When distributing the new client to POS machines, also change the API URL:**
1. Open Settings → CLOUD SYNC
2. Change `CloudApiUrl` from `https://jumong-pos-api-p285q.ondigitalocean.app/api` to `https://admin.jumongdev.com/api`
3. Click **Save** at the bottom of Settings
4. Click **SYNC ALL TO CLOUD** to upload all data to the new local server

### Cloud API
```powershell
# Build
dotnet publish JumongCloudAPI\JumongCloudAPI.csproj -c Release -r win-x64 --self-contained true

# Deploy to local server
Copy-Item -Recurse "bin\Release\net8.0\win-x64\publish\*" "C:\JumongAPI\"
Restart-Service JumongCloudAPI
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
9. **Warehouse products must come from master catalog** — manual ADD is hidden for Products tab. Use FROM MASTER. Import uses the product's default unit `qty_per_unit` as `box_qty` and its `price` as `box_price`. If no default unit, falls back to the `boxQty` parameter (default 1). Auto-sync on master save keeps warehouse in sync.
10. **Warehouse product import is idempotent** — if `master_product_id` already exists in `wh_products`, importing again UPDATES the existing product (reactivates, refreshes prices) instead of creating a duplicate. Extra duplicate rows are unlinked.
11. **Warehouse edit form matches master catalog** — PRICE, COST, and UNITS section instead of BOX PRICE / BOX QTY / PIECE PRICE. On save, `_computeBody()` converts back to box format for backend compatibility.
12. **WhSyncFromMaster and auto-sync** reference `wh_products` table directly (not alias `wh`) — the `wh` alias was never defined, causing `missing FROM-clause entry for table "wh"` errors.
13. **Local server deployment** — API is a Windows service (NSSM) at `C:\JumongAPI\JumongCloudAPI.exe`. Cloudflare Tunnel `jumong-pos` routes `admin.jumongdev.com` → `localhost:5000`. Deploy by copying publish output and restarting service.
14. **Connection status** — POS sidebar shows green/red dot refreshed every 10s via `CheckConnectionAsync()` pinging `/dashboard/version`. No blocking — just visual indicator.
15. **DO decommission order** — ~~Keep DO running until last POS client switches API URL~~ **DONE (verified 2026-08-06): all 4 POS clients on `admin.jumongdev.com`.** DO App Platform + Managed PostgreSQL can now be cancelled. App ID `1bc1369e-6ece-4645-be57-1a7fcf7e90b8`, DB ID `c6bababf-6a01-418a-9244-a830526f83b3`.
16. **DB protection** — Set NTFS permissions on `JumongPos.db` to deny `Write`/`Delete` for `Users` group to prevent accidental deletion by employees. Cloud restore is the fallback (SYNC FROM CLOUD for master data, cloud PG has all sales/expenses).
17. **Tailscale uninstalled** — Was only needed for remote SMB access to Naic client, but UAC blocked admin shares. No longer needed since updates are via UPDATE APP over internet.
18. **Install PG on client? No** — POS clients keep SQLite + REST API sync to `admin.jumongdev.com/api`. Installing PG on each PC adds complexity with no benefit.

## System Areas (Pointers for Updates)

| Pointer Name | Files Covered | Build/Deploy Command |
|---|---|---|
| **CLOUD API** | `JumongCloudAPI/Controllers/DashboardController.cs` | `dotnet publish JumongCloudAPI\JumongCloudAPI.csproj -c Release -r win-x64 --self-contained true` → deploy via `deploy_api.bat` (Run as admin) |
| **DASHBOARD HTML** | `JumongCloudAPI/wwwroot/index.html` + `order.html` | No build needed — refresh browser after edit |
| **DASHBOARD JS** | `JumongCloudAPI/wwwroot/components.js` + `app.js` + `style.css` | No build needed — refresh browser after edit |
| **CLOUD DB** | `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Build + Restart-Service JumongCloudAPI |
| **POS CLIENT** | `Forms/`, `Services/`, `Models/`, `Data/DatabaseHelper.cs` | `dotnet publish -c Release -r win-x64 --self-contained true -o C:\JumongAPI\client` |
| **MOBILE INV** | `Services/InventoryService.cs`, `Services/InventoryWebServer.cs` (port 5002) | Part of POS Client — publish kasama |

### Common CMDs

```
[AREA] <CLOUD API | DASHBOARD HTML | DASHBOARD JS | CLOUD DB | POS CLIENT | MOBILE INV>
[WHAT] <isang linya lang kung ano gagawin>
[DETAILS] <mga specifics, opsiyonal>
```

**Examples:**
```
[AREA] CLOUD API + DASHBOARD JS
[WHAT] Add inventory activity endpoint using wh_stock_trails
[DETAILS] Query from wh_stock_trails instead of stock_trails, frontend refresh lang

[AREA] POS CLIENT
[WHAT] Fix sync service timeout
[DETAILS] Increase timeout from 5s to 15s in SyncService.cs:783
```

## Complete Change History (cont.)

### v1.1.37 (POS) + v1.1.11 (Cloud API) — Void Integrity, End Shift Flow Alignment, Wholesale Method Badges

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **WhVoidSale fixes**: (1) partial void now decrements `wh_walkin_sales.total_amount` so voided amounts are removed from ALL sales computations (summary, end shift, reports) — previously partial-voided amounts stayed in totals; (2) **credit reversal** — voiding a Credit sale now inserts a reversal `credit_transactions` row (type `Void`, credit=voidedAmt) and decrements `customers.credit_balance`; (3) **loyalty points reversal** — voids now deduct `points_earned` from customer. Check query now reads `payment_method` + `customer_id`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | `WhGetSales` now returns `paymentMethod` (COALESCE 'Cash') — feeds the method badges. API version bumped to `"1.1.11"`; agent `latestVer` = `"1.1.37"`. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **Sales report method badges** — CASH (green) / E-WALLET (blue) / CREDIT (purple) chip per sale card. **VOID** — added ✖ VOID SALE + per-item ✖ VOID buttons in sale items modal (confirm + reason prompt, POST `/warehouse/sales/{id}/void`, reloads). **End Shift gap banner** — amber warning when preview `since` is ≥1 day ago ("kasama ang lahat ng benta mula noon"). |
| `Forms/WarehouseSellForm.cs` | Wholesale REPORT popup now shows **METHOD column** (parsed from `paymentMethod`) and the printout includes `inv | customer | METHOD | date`. |
| `Forms/EndShiftForm.cs` | **PRINT button REMOVED** — printing now only via auto-print after SAVE, or 📋 HISTORY reprint (prevents HVR-style "printed but never saved" 2-day-gap closes). EMAIL moved to x=115. Added `WarnIfShiftGap()` on load — amber date warning + MessageBox when last close is ≥1 day ago (2-day window visibility). |
| `JumongCloudAPI/wwwroot/components.js`, `index.html` | Inventory Cost Report rows now show **⚠ 2-DAY WINDOW badge** when consecutive closes for a store are ≥2 days apart (per-store prev-date tracking). ("NO CLOSE TODAY" alert already existed via `missingShifts`.) |
| `Services/AppVersion.cs` | Bumped to `"1.1.37"`. |
| — | Voided wholesale sales remain EXCLUDED from gross/total sales computations per store policy (user-confirmed: "dapat hindi"). Verified stock restore on void is correct (`stock_deduction` pc-sqty, `void_return` trail, item/header `is_voided` flags, transactional). |

**Impact:** Voiding a wholesale sale (full or partial) now fully reverses stock, money (credit balance for credit sales), and loyalty points; partially voided sales no longer inflate shift/sales totals. POS end shift can no longer be "printed but never saved" — eliminates the HVR Aug 8 missing-close scenario. Method badges visible on mobile + POS wholesale report + dashboard. Deploy: `deploy_api.bat` (admin) for API; web copy `whmobile.html`/`components.js`/`index.html` → `C:\JumongAPI\wwwroot\` + `publish\wwwroot\`; client publish → `C:\JumongAPI\client\`; stores via UPDATE APP.

### v1.0.85 QR Code Carousel on POS Sales Screen

| File | Change |
|---|---|
| Data/DatabaseHelper.cs | Added migration: seeds default QR codes (GCash) in StoreQrCodes setting; adds ssets/ folder creation on startup |
| Forms/SettingsForm.cs:469-513 | Added **QR CODES** section (Admin only) � DataGridView with HEADER/FILE columns, +ADD/REMOVE buttons, loaded/saved via StoreQrCodes JSON setting |
| Forms/SalesForm.cs:1168-1195,1208,1226,1244,1356-1379,1409-1447,1498-1505 | Added QR carousel in right panel: _pbQr PictureBox, _lblQrHeader, _btnQrPrev/_btnQrNext nav buttons, _qrEntries list, LoadQrCodes(), ShowQrIndex(), Recalc() layout below Pay button |
| Services/AppVersion.cs | Current bumped to "1.0.85" |

**Impact:** POS sales screen shows QR code images (GCash, Maya, etc.) in right panel below totals. Admin configures in Settings.

### v1.0.86 � Inventory Reconciliation in End Shift

| File | Change |
|---|---|
| Models/DailyClose.cs | Added TotalInventoryCost, TotalCostSold, TotalStockReceivedCost |
| Data/DatabaseHelper.cs | Migration: adds TotalInventoryCost, TotalCostSold, TotalStockReceivedCost columns |
| Services/DailyCloseService.cs:64 | GetShiftTotals() returns 10-element tuple. Added GetLastInventoryCost(). |
| Forms/EndShiftForm.cs:98-107 | Computes 	otalInvCost = SUM(StockQty � Cost) before save |
| Services/PrinterService.cs | Prints **Inventory Reconciliation** section with variance |
| Services/EmailService.cs | Inventory reconciliation table in end-shift email |
| Services/SyncService.cs | SyncDailyClose() includes new fields |
| JumongCloudAPI/Data/PgDatabaseHelper.cs | Migration: adds inventory cost columns |
| JumongCloudAPI/Controllers/SyncController.cs | daily closes includes new columns |
| Services/AppVersion.cs | Current bumped to "1.0.86" |

**Impact:** End shift captures total inventory cost, COGS, stock received cost. Prints/emails reconciliation with variance.

### v1.0.87 � QR Click-to-Enlarge, Browse Button, Crash Fixes

| File | Change |
|---|---|
| Forms/SalesForm.cs:1175-1190 | Click handler on QR PictureBox � opens full-size maximized Form |
| Forms/SalesForm.cs:1191 | ToolTip "Click to enlarge" on QR |
| Forms/SettingsForm.cs:501-513 | ADD QR now opens file picker, auto-copies to assets/ |
| Forms/SettingsForm.cs:519 | QR section height 235?275 (buttons were clipped) |
| Services/SyncService.cs:914,932 | Fixed InvalidCastException � Convert.ToInt32() for SQLite long |
| Services/AppVersion.cs | Current bumped to "1.0.87" |

### v1.0.88 � Auto-Cleanup on Startup (Slow HDD Fix)

| File | Change |
|---|---|
| Helpers/ErrorLogger.cs | Added TrimLog() � keeps last 500 lines if error.log > 1MB |
| Services/EmailService.cs:407-410 | FlushQueue() discards entries older than 7 days |
| Program.cs:66-71 | Startup: ErrorLogger.TrimLog(), delete SyncLog > 30 days |
| Services/AppVersion.cs | Current bumped to "1.0.88" |

**Impact:** Fixes 5-10 min startup on HDD. Logs auto-trim. Old failed emails cleared.

### v1.0.89 � POS Promo Message (Local Settings)

| File | Change |
|---|---|
| Forms/SettingsForm.cs:521-540 | Added POS PROMO section with multiline textbox |
| Forms/SettingsForm.cs:110-111 | Loads PosPromoMessage in LoadSettings |
| Forms/SettingsForm.cs:176-185 | Saves PosPromoMessage on Save |
| Forms/SalesForm.cs:1228-1237,1246 | Added _lblPromo Label below QR, _promoText field |
| Forms/SalesForm.cs:1406-1412 | Recalc shows/hides promo label |
| Forms/SalesForm.cs:1435-1436 | Loads PosPromoMessage from local Settings |
| Services/AppVersion.cs | Current bumped to "1.0.89" |

### v1.0.90 � Cloud-Managed POS Promo (Dashboard + Auto-Fetch)

| File | Change |
|---|---|
| JumongCloudAPI/Data/PgDatabaseHelper.cs:551-557 | Added pos_promo table (id, message, updated_at) with seed |
| JumongCloudAPI/Controllers/DashboardController.cs:2586-2610 | GET/POST /dashboard/pos-promo endpoints |
| JumongCloudAPI/wwwroot/components.js:953-973 | posPromoPanel Alpine component |
| JumongCloudAPI/wwwroot/index.html:62 | POS Promo nav item in sidebar |
| JumongCloudAPI/wwwroot/index.html:1988-2012 | POS Promo section panel with textarea + SAVE |
| Services/SyncService.cs:976-994 | FetchPromoMessageAsync() � cloud API with local fallback |
| Forms/SalesForm.cs:1441-1456 | FetchCloudPromoAsync() after LoadQrCodes |
| Forms/SettingsForm.cs:504-516 | Fixed IOEception in ADD QR � delete + retry on locked file |
| Services/AppVersion.cs | Current bumped to "1.0.90" |

**Impact:** Admin sets promo message once on dashboard, all POS clients auto-fetch. Falls back to local setting if cloud unreachable.

### v1.1.00–v1.1.07 — Local-First Reads, Wholesale Invoice, INV CHECK, Transfer Stock Trail, Update Fixes

| File | Change |
|---|---|
| `Services/ProductService.cs` | Removed all 6 PG-read fallback blocks (GetAll, GetById, GetByBarcode, GetCategories, GetStockStats, GetStockValues). Reads now SQLite-only, instant, zero timeout. Kept TryWriteToPgAsync, Save PG write, Delete PG write. Removed MapPg helper. |
| `Services/CustomerService.cs` | Removed all 4 PG-read fallback blocks (GetAll, GetById, GetByPhone, Search). SQLite-only reads. Kept TryWriteToPgAsync, Save, Delete, UpdateLoyaltyPoints, UpdateCreditBalance PG writes. Removed MapPg helper. |
| `Services/UserService.cs` | Removed 1 PG-read fallback (GetAll). SQLite-only. Kept TryWriteToPgAsync, Save, Delete PG writes. Removed MapPg helper. |
| `Services/ProductUnitService.cs` | Removed 2 PG-read fallbacks (GetByProduct, GetDefault). SQLite-only. Kept TryWriteToPgAsync, Save, Delete PG write. Removed MapPg helper. |
| `Services/StockService.cs` | Removed 2 PG-read fallbacks (GetByBarcode, Search). SQLite-only. Kept ConfirmReceiving PG write. Removed MapPg helper. |
| `JumongPosV1.01.csproj` | Added `tools\**` exclusion (prevent duplicate AssemblyInfo build errors). |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Added `invoice_no` column to `wh_walkin_sales`. Added `wh_invoice_counter` table for sequence generation. |
| `JumongCloudAPI/Controllers/DashboardController.cs:2424-2435` | WhSell: generates invoice number `WH-YYYYMMDD-NNNN` via `wh_invoice_counter`, stores in `invoice_no` column. WhGetSales returns `invoiceNo`. |
| `JumongCloudAPI/Controllers/DashboardController.cs:2060-2075` | WhCreateTransfer: logs stock trail (`transfer_out`) in `wh_stock_trails` on stock deduction. |
| `JumongCloudAPI/Controllers/DashboardController.cs:2232-2245` | WhCancelTransfer: logs stock trail (`transfer_cancel`) in `wh_stock_trails` on stock restoration. Backfilled 515 existing transfer items. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1387-1421` | WhGetProducts: added `LEFT JOIN master_products mp` to return `imageData` column. |
| `JumongCloudAPI/wwwroot/index.html:1061-1097` | Warehouse Product table: added Image column (from master catalog base64). |
| `JumongCloudAPI/wwwroot/index.html:1162-1189` | Warehouse Inventory table: added Image column. |
| `JumongCloudAPI/wwwroot/index.html:1065-1095` | Wholesale Sales table: added Invoice # column (`x.invoiceNo`). |
| `JumongCloudAPI/wwwroot/index.html:2142-2148` | Master product editor: "+" button next to Category dropdown — prompts for new category, validates duplicates. |
| `JumongCloudAPI/wwwroot/components.js:421-430` | productEditor: added `addCategory()` method with duplicate check. |
| `JumongCloudAPI/wwwroot/components.js:982-1009` | inventoryCostReport: changed from single `prevInvCost` variable to per-store dictionary `prevInvByStore`. Fixes Previous column showing ₱0 for interleaved stores. Added `x.prevInvCost` property assignment. |
| `JumongCloudAPI/wwwroot/components.js:1000` | Added `x.prevInvCost = prevInvCost;` line (was missing — every row showed ₱0). |
| `Forms/WarehouseSellForm.cs:1129-1205` | ShowVoidPopupAsync: added Invoice column to DGV. btnReprint was missing from Controls.AddRange — now included. Fixed column indices after adding Invoice column. |
| `Forms/ProductsForm.cs:700-890` | Added "INV CHECK" button to toolbar. Opens popup showing inventory reconciliation (Previous/Received/COGS/Expected/Actual/Variance), cost mismatch details, receiving recompute, per-item cost change detection via last sale comparison. |
| `Forms/SalesForm.cs:98` | Changed update banner text from "UPDATE AVAILABLE" to "APP UPDATE". |
| `Services/UpdateService.cs:33-83` | Rewrote DownloadAndUpdate: downloads to `%TEMP%\JumongPosV1.01_update.exe`, batch copies over old exe after process kill. No more File.Move on running exe. Process.Kill() instead of Environment.Exit. Ping instead of timeout. |
| `Services/AppVersion.cs` | Current bumped to `"1.1.07"`. |
| `tools/InvVarianceCheck/` | New standalone diagnostic tool (Console app). Checks inventory reconciliation, cost mismatches, receiving recompute, zero-cost products. Self-contained publish to `invcheck.exe`. |

**Impact:** POS reads now instant from SQLite — zero PG timeout, fully offline. Wholesale sales now have invoice numbers (visible on dashboard and receipts). Inventory Cost Report Previous column fixed for multi-store scenarios. INV CHECK button on Products page provides in-POS inventory reconciliation. Transfer stock movements now logged to warehouse stock trails (backfilled 515 records). Warehouse products/inventory show master catalog images. Master editor category field has "+" button for adding new categories with duplicate validation. Update system now uses batch copy instead of File.Move — no more update loops. btnReprint was missing from void popup — now visible.

### Remote Diagnostic Agent (`tools/Agent/`)

A console app that runs on each POS client machine, connecting outbound to the cloud API. Enables the AI agent to remotely query the local SQLite database, run diagnostic commands, and update files — no port forwarding, no remote desktop needed.

#### Agent Files
| File | Purpose |
|---|---|
| `tools/Agent/Agent.csproj` | Project — self-contained, win-x64, PublishReadyToRun |
| `tools/Agent/Program.cs` | Agent logic — heartbeat, command polling, file ops |

#### Agent Commands
| Type | Payload | What it does |
|---|---|---|
| `sql` | SQL query text | Runs against local `JumongPos.db`, returns tab-separated output (max 500 rows) |
| `invcheck` | DB path (or blank) | Runs invcheck.exe on the local DB |
| `ps` | PowerShell script | Runs `powershell.exe -NoProfile -Command "..."` |
| `readfile` | File path | Returns file contents as text |
| `writefile` | `PATH|CONTENT` | Writes content to a file (relative to agent folder or absolute) |
| `update` | `URL|TARGET_PATH` | Downloads a file from URL to target path |
| `restart` | (none) | Restarts `JumongPosV1.01.exe` in parent folder |

#### How to build and deploy
```powershell
Set-Location tools\Agent
dotnet publish -c Release -r win-x64 --self-contained true
Compress-Archive -Force -Path "bin\Release\net8.0-windows\win-x64\publish\*" -DestinationPath "C:\JumongAPI\wwwroot\agent.zip"
```

#### How to install on POS machine
1. Download `https://admin.jumongdev.com/agent.zip`
2. Extract ALL files to `Agent\` subfolder inside POS folder
3. Double-click `Agent\Agent.exe`
4. It auto-finds `..\JumongPos.db` (parent folder) and `CloudApiUrl` from Settings

#### Cloud API Endpoints (in DashboardController)
| Endpoint | Method | Purpose |
|---|---|---|
| `/api/dashboard/agent/heartbeat` | POST | Agent sends heartbeat every 3 seconds |
| `/api/dashboard/agent/status` | GET | Returns all connected agents |
| `/api/dashboard/agent/poll/{storeId}` | GET | Agent polls for pending commands |
| `/api/dashboard/agent/send/{storeId}` | POST | Dashboard sends command to agent |
| `/api/dashboard/agent/result` | POST | Agent posts command result |
| `/api/dashboard/agent/results/{storeId}` | GET | Dashboard fetches results |

#### Dashboard UI
- Sidebar: **AGENTS** tab
- Shows all connected agents (store, machine, IP, last seen)
- SQL Query / Inventory Check / Read File commands
- Execute button → polls result (up to 15 tries × 2s)

#### Important Notes
- Agent is a **console app** (runs in window, shows status)
- Self-contained publish extracts to temp folder — **NOT single-file** (needs native SQLite DLL)
- Agent checks **parent folder** for `JumongPos.db` if not found in current folder
- All commands execute locally on the POS machine
- Agent timeout: 15 minutes (for large file downloads)
- No GUI required — works fully over CLI/API
- **Auto-start is at Windows LOGON level, NOT POS-app-open** (v1.1.35+): both `Program.cs StartAgent()` and the Agent itself write the `HKCU\...\CurrentVersion\Run` → `JumongPosAgent` Run key (pointing at `Agent\Agent.exe`), and `StopAgent()` is an empty no-op. So the agent starts at every Windows logon even if the POS app is never opened, and survives POS app close/lock screen. **Caveat:** HKCU Run fires only when a Windows user logs in — if the PC sits at the login screen (e.g., overnight reboot, nobody logged in), the agent does NOT run until someone logs in. A Windows-service/SYSTEM scheduled-task install would be needed for boot-time-without-login coverage — decided NOT worth it (store is closed anyway).

### v1.1.08–v1.1.15 — Bidirectional Sync, Inventory Fixes, Transfer Rework, Agent Dashboard, Warehouse Viewer

| File | Change |
|---|---|
| `Services/SyncService.cs` | Added `PushAllUnsyncedAsync()` — background push for Sales, StockTrail, VoidLog, CreditTxn, DailyClose, Expenses. Added `ScheduleSnapshotPush()` with 5s debounce (event-driven, not timer). Added `PushStockSnapshotAsync()` with delta comparison — only pushes changed products. Changed sync order to `ORDER BY Id DESC` (newest first). Added debounce mechanism for batched snapshot pushes. |
| `Data/DatabaseHelper.cs` | Added `PRAGMA journal_mode=WAL` for concurrent reads+writes. Added `busy_timeout=5000` to secondary connection string. Added Synced column migrations for VoidLog, CreditTransaction, DailyClose, Expenses. |
| `Forms/MainForm.cs` | Added 30s auto-push timer (`PushAllUnsyncedAsync`). Added 5-min auto-pull timer (master catalog + customers + settings). Removed snapshot from timer — moved to event-driven `ScheduleSnapshotPush()`. |
| `Forms/SalesForm.cs` | Fixed `IsHandleCreated` check before `BeginInvoke` in `CheckForUpdatesAsync` and `FetchCloudPromoAsync`. Changed banner text to "APP UPDATE". |
| `Forms/EndShiftForm.cs` | Stock snapshot pushed on end shift with barcode matching. |
| `Forms/ProductsForm.cs` | Added "INV CHECK" button — shows inventory reconciliation with cost mismatch detection. |
| `Forms/WarehouseSellForm.cs` | Fixed `btnVoid` null ref (local variable → field). Added `btnReprint` to void popup controls. Added **📊 WH-INVENTORY** button (HQ only) — opens warehouse inventory viewer popup with DataGridView, category filter, search, low stock toggle, total value display, **PRINT** and **PRINT BY CATEGORY** buttons. |
| `Forms/PaymentForm.cs` | Fixed `EwPaid` not set for pure E-Wallet payments (was always 0). |
| `Forms/CreditManagementForm.cs` | Credit payment flow — "total exceeds balance" check uses `_selectedCustomer.CreditBalance`. |
| `Services/CreditService.cs` | Fixed `AddTransaction` not getting `Id` from `last_insert_rowid()` (was syncing with Id=0). Fixed `CreditBalance` recalculation from `SUM(Debit - Credit)`. |
| `Services/CustomerService.cs` | Removed `store_id` filter from PG queries (`UpdateCreditBalance`, `UpdateLoyaltyPoints`, `Delete`) — customers are universal. |
| `Services/PrinterService.cs` | Unified all receipts to **Courier New 9pt Bold** (was mixed regular/bold/8pt/9pt/11pt). Added `PrintWarehouseInventory()` method for printing warehouse stock lists. |
| `Services/UpdateService.cs` | Rewrote `DownloadAndUpdate` — downloads to `%TEMP%\JumongPosV1.01_update.exe`, batch copies over old exe after process kill. `Process.Kill()` instead of `Environment.Exit`. |
| `Services/AppVersion.cs` | Bumped to `"1.1.15"`. |
| `Program.cs` | POS saves `AppVersion` to Settings on startup. `StartAgent()` auto-starts `Agent\Agent.exe` on POS launch. `StopAgent()` kills agent on POS exit. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **Transfer rework:** removed stock deduction on `WhCreateTransfer` (stock held pending). Added stock deduction on `WhReceiveTransfer` (deducts when POS accepts). Added agent endpoints: `agent/heartbeat`, `agent/status`, `agent/send/{storeId}`, `agent/poll/{storeId}`, `agent/result`, `agent/results/{storeId}`. Added `AgentHeartbeat`, `AgentCommand`, `AgentResult` model classes. **Snapshot fix:** removed `UPDATE wh_products SET stock_qty` from `WhStockSnapshot` — snapshot now informational only, no longer corrupts warehouse stock. Added delta=0 check to skip creating snapshot trails when no stock change. Added `cost` column to `WhGetProducts` response (from `master_products.cost`). |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Added `wh_invoice_counter` table for wholesale invoice sequence. Added `invoice_no` column to `wh_walkin_sales`. |
| `JumongCloudAPI/wwwroot/index.html` | Added **AGENTS** sidebar item with health cards (🟢/🔴 status, version, outdated badge, error summary). Moved agents panel inside main content div. |
| `JumongCloudAPI/wwwroot/components.js` | Added `agentsPanel` Alpine component with SQL query, inventory check, file read, store selector, execute button, result viewer, poll results (15s timeout). |
| `tools/Agent/` | Remote diagnostic agent — heartbeat, command polling, SQL execution, PowerShell, invcheck, file operations. Auto-starts with POS. Added `writefile`, `update` (download), `restart` commands. Reads POS version from Settings table. Self-update capability via batch file. |
| `tools/InvVarianceCheck/` | Standalone inventory variance diagnostic tool. |
| `JumongPosV1.01.csproj` | Added `tools\**` exclusion. Added `CopyAgentAfterPublish` target — includes Agent folder in POS publish output. |
| **PostgreSQL data** | Deleted 46,069+46,649+19,631+3,512 = ~115,000 useless 0-change snapshot trails. Deleted 1,152 duplicate transfer_out trails. Recalculated all `wh_products.stock_qty` from legitimate trails (46,073 units, 0 negative). Reset 5 negative stock products to 0. Fixed Yosi Me Green (320→1000), Yosi Me Red (0→200), Marlboro Ice Blast Blue (0→30), Mighty Green Menthol (0→600). |

**Impact:** Bidirectional sync auto-pushes every 30s (newest first), auto-pulls master data every 5 min. Transfer stock held pending until POS accepts (no more double deduction). Snapshot no longer corrupts `wh_products` stock — informational trails only. Event-driven snapshot with 5s debounce replaces timer flooding (~3,000 trails/hour → maybe 50). WAL mode prevents "database is locked" errors. Agent auto-starts/auto-closes with POS — dashboard shows health, version, outdated flag. Warehouse inventory viewer in HQ Wholesale page with print by category. All receipts Courier New 9pt Bold. E-Wallet EwPaid correctly recorded. Credit sync sends correct Id. Version bumps required for every change (no re-uploading to same tag).

### v1.1.22 — Credit Balance Sync Fix

| File | Change |
|---|---|
| `Services/CreditService.cs:47` | After `AddTransaction` (payment/credit-sale), calls `SyncService.SyncCustomer()` to push updated CreditBalance to cloud via REST API. |
| `Services/SaleService.cs:538` | After `VoidSale`, calls `SyncCustomer()` to push credit balance after void reversal. |
| `Services/SaleService.cs:675` | After `VoidItem`, calls `SyncCustomer()` to push credit balance after void reversal. |
| `Services/SyncService.cs:1179-1232` | `DownloadCustomersAsync` — removed unused `creditBalance` variable read and all 3 `@cb` parameter bindings (name match UPDATE, phone match UPDATE, INSERT). Download never touches `CreditBalance` column. |
| `Forms/SalesForm.cs:830-845` | APP UPDATE banner click now shows error message if check fails instead of silent return. |
| `Forms/MainForm.cs:541` | Restored `DownloadCustomersAsync` in 5-min auto-pull timer (was accidentally removed then restored). |
| `Services/AppVersion.cs` | Current bumped to `"1.1.22"`. |
| `Services/SaleService.cs:670` | Fixed `GetItems(saleId)` call — removed (was missing `conn` parameter causing build failure). Uses `updatedSale.Items` instead. |

**Impact:** Credit balance changes (payment, credit sale, void) now sync to cloud via REST API. The 5-min customer download from cloud no longer has any path to touch local `CreditBalance`. Cloud PG `credit_balance` zeroed for all 334 customers. Only EMZ ABAYON (₱1,278) has active debt. Build cache issue fixed: always run `dotnet clean` before `dotnet publish` to prevent stale exe.

### 2026-08-06 — DigitalOcean Migration Verified Complete (DO Ready for Decommission)

| Item | Detail |
|---|---|
| Check | Queried local PostgreSQL (`sales`, `stock_trails` per store) + `/api/dashboard/agent/status` endpoint |
| Result | **All 4 POS clients already on `admin.jumongdev.com`** — every store has sales + stock trails on 2026-08-06, all agents heartbeat to the local server (last seen within the minute) |
| Stores confirmed | HQ `7159` (DESKTOP-UU8E0D4 / 192.168.1.37 / app 1.1.27), HVR `AA36` (DESKTOP-TK63MO6 / 192.168.1.4 / app 1.1.25), U Got Minimart `E174` (DESKTOP-NISQ3Q7 / 192.168.1.152 / app 1.1.28), ACGS `A80C` (DESKTOP-TK63MO6 / 192.168.0.100 / app 1.1.30) |
| Action | None taken — DO App Platform + Managed PostgreSQL now safe to cancel. App ID `1bc1369e-6ece-4645-be57-1a7fcf7e90b8`, DB ID `c6bababf-6a01-418a-9244-a830526f83b3` |

**Impact:** No client is pointed at DigitalOcean anymore — their data lands on the local server. The old "4 POS clients still pointing here" note is removed; Stores table now lists all 4 with machine/IP; Key Decision #15 marked DONE.

### 2026-08-07 — HQ POS QR "No Picture" Investigation + Machine Roles Documentation

| Item | Detail |
|---|---|
| Check | Agent `sql` + `ps` commands against HQ (STORE-20260602-7159) via `/dashboard/agent/send` |
| Result | HQ DB is correct: `StoreQrCodes=[{"header":"ito muna ang Gamitin","file":"ugot_qrcode.jpg"}]`, `AppVersion=1.1.33`. BUT `C:\Users\ADMIN\Desktop\JumongPosHW\assets\` **does not exist** → app shows header only, no image |
| Root cause | (1) The HQ POS runs from `C:\Users\ADMIN\Desktop\JumongPosHW\` (NOT `C:\JumongAPI\client\`); (2) the assets dir was never created there, so the agent `update` (image download) can't write to `..\assets\`; (3) any push URL must first return HTTP 200 from `https://admin.jumongdev.com/assets/<file>` |
| Fix applied | Wrote StoreQrCodes directly into `C:\JumongAPI\client\JumongPos.db` + `publish\JumongPos.db` (dev/test DBs); copied `ugot_qrcode.jpg` into `publish\assets\` — for the real HQ machine the fix must be done via Agent (create `assets\` dir first, then `update` the image, then `restart`) |
| AGENTS.md | Added Machine Roles table (dev/API host vs store machines), HQ POS path gotcha, Agent version-cache note, POS QR push procedure |

**Impact:** Documented where each machine's POS actually lives and how QR pushes actually reach a store (DB value + physical file). Agent `lastSeen` within a few seconds means alive — version number on the dashboard may be stale until agent restart.


## Warehouse Mobile App (Android, WarehouseApp/)

WebView wrapper app that loads https://admin.jumongdev.com/whmobile.html (login via whapp API, SELL/INVENTORY/SETUP tabs, Bluetooth thermal printing, in-app update). Source: Kotlin, no gradle wrapper — use system Gradle 8.14.3 from C:\Users\ADMIN\.gradle\wrapper\dists\gradle-8.14.3-bin\ with Android Studio JBR 21.

### Build & Sign
`powershell
Set-Location "C:\Users\ADMIN\Desktop\JumongPosV1.01\WarehouseApp"
# local.properties must use FORWARD SLASHES (backslashes = invalid path in Java properties):
#   sdk.dir=C:/Users/ADMIN/AppData/Local/Android/Sdk
\C:\Program Files\Eclipse Adoptium\jdk-21.0.11.10-hotspot = "C:\Program Files\Android\Android Studio\jbr"
& "C:\Users\ADMIN\.gradle\wrapper\dists\gradle-8.14.3-bin\cv11ve7ro1n3o1j4so8xd9n66\gradle-8.14.3\bin\gradle.bat" :app:assembleRelease --no-daemon
# Sign (keystore: jumong_sign.keystore, alias jumong, pass jumong2026)
& "C:\Users\ADMIN\AppData\Local\Android\Sdk\build-tools\37.0.0\apksigner.bat" sign --ks jumong_sign.keystore --ks-key-alias jumong --ks-pass "pass:jumong2026" --key-pass "pass:jumong2026" --out JumongWarehouse.apk app\build\outputs\apk\release\app-release-unsigned.apk
`
Copy JumongWarehouse.apk to JumongCloudAPI\wwwroot\updates\ AND JumongCloudAPI\bin\Release\net8.0\win-x64\publish\wwwroot\updates\. Bump warehouse-version.json (version + changelog). Old warehouse.keystore password lost — v1.0.4 uses the NEW jumong_sign.keystore cert, so existing installs MUST uninstall first (changelog says so). Gradle OOM risk on this PC: only ~2.8GB free RAM; heap capped at 1536m in gradle.properties.

### v1.1.0 (Cloud API) — Warehouse Mobile App (Android WebView) + whmobile.html

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | Added `PostWhappLogin`, `PostWhappValidate`, `PostWhappLogout` endpoints. `GET /stores` response now includes `STORE-WAREHOUSE`. `Login` app checks `IsActive` + `mobileAccess`. Dashboard version bumped to `"1.1.0"`. |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Added `mobile_access BOOLEAN NOT NULL DEFAULT false` to `users`. Added `whapp_tokens` table (id, user_id, token, created_at, expires_at). |
| `JumongCloudAPI/Controllers/DashboardController.cs:803-805` | User CRUD reads/writes `mobileAccess`; persisting user keeps token-session rows (whapp_tokens) valid if mobileAccess stays true. |
| `JumongCloudAPI/Program.cs` | Added static-file endpoint `/updates/*` serving `wwwroot/updates/*.apk` + version JSON. |
| `JumongCloudAPI/Controllers/DashboardController.cs:391-430` | New `/api/dashboard/whapp/login` (username+password → token, session window 5 tokens/user), `/whapp/validate`, `/whapp/logout`. |
| `JumongCloudAPI/wwwroot/index.html` | User Manager → **MOBILE ACCESS** checkbox on Add/Edit User modal; Mobile column on users table. Added **MOBILE APP** sidebar button linking `whmobile.html`. |
| `JumongCloudAPI/wwwroot/components.js` | User editor reads/writes `mobileAccess` to/from table; `@click` on MOBILE APP shows alert. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **New file** — Warehouse mobile web app (login via whapp API, SELL/INVENTORY/SETUP tabs, Bluetooth thermal printing via Android bridge, in-app update via updates/warehouse-version.json). Hosted at `admin.jumongdev.com/whmobile.html`. |
| `WarehouseApp/` | **New folder** — Kotlin Android WebView wrapper app (MainActivity, BluetoothPrinter.kt, JavaScript bridge). `build.gradle` versionCode 4, versionName `"1.0.3"`. |
| `README` / `AGENTS.md` | Documented build & sign steps and local.properties gotcha (must use forward slashes). |

### v1.0.4 (APK rebuild) — New signing keystore, memory fix, OOM crash resolved

| File | Change |
|---|---|
| `WarehouseApp/build.gradle` | versionCode bumped to `5`, versionName `"1.0.4"`. |
| `WarehouseApp/gradle.properties` | `org.gradle.jvmargs=-Xmx1536m -XX:MaxMetaspaceSize=512m` (capped heap — PC only ~2.8GB free RAM). |
| `JumongCloudAPI/wwwroot/updates/JumongWarehouse.apk` | Rebuilt APK (was stale v1.0.3 with versionCode 4). Signed with NEW keystore. |
| `JumongCloudAPI/wwwroot/updates/warehouse-version.json` | version `"1.0.4"` — changelog: new signing cert, must uninstall old first. |
| `.gitignore` | Added rules for `WarehouseApp/build/`, `.gradle/`, `*.apk` artifacts, keystores, `local.properties`. |
| `AGENTS.md` | Documented build/sign steps and the "must uninstall first" upgrade note. |

**Impact:** Warehouse APK now installs and updates correctly. Gradle is capped at 1536MB to avoid OOM on this PC. Existing v1.0.3 installs MUST uninstall before downloading v1.0.4 (different signing key). Signed keystore `jumong_sign.keystore` (alias `jumong`, pass `jumong2026`) — keep safe, future updates need it.

### v1.0.5 (APK) — Paper Width Selector (50mm/80mm) + Width-Aware Receipts

| File | Change |
|---|---|
| `WarehouseApp/MainActivity.kt` | Added `@JavascriptInterface getPaperWidth()` / `setPaperWidth()` using SharedPreferences `wh_prefs` key `paper_width`, default 80. |
| `WarehouseApp/app/src/main/java/com/jumong/warehouse/MainActivity.kt` | Paper width bridge implemented + forward-slashes fix. |
| `WarehouseApp/build.gradle` | versionCode bumped to 6, versionName `"1.0.5"`. |
| `JumongCloudAPI/wwwroot/whmobile.html` | Added PAPER WIDTH card + 50mm/80mm buttons, paperWidth state + loadPaperWidth/setPaperWidth; `buildReceiptText()` width-aware (32ch vs 48ch); both called on load/tab switch. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **Hardened `loadPaperWidth`/`setPaperWidth`** — feature-detects `AndroidApp.setPaperWidth` before calling (prevents crash on old APKs), always persists to localStorage + toast + highlight even without native bridge. |
| `JumongCloudAPI/wwwroot/updates/JumongWarehouse.apk` | **v1.0.5** — signed with `jumong_sign.keystore` (same cert as v1.0.4). |
| `JumongCloudAPI/wwwroot/updates/warehouse-version.json` | version `"1.0.5"` — changelog: adds Paper Width selector (50mm/80mm) in SETUP, same signing cert so updates install without uninstalling. |
| `AGENTS.md` | Updated with new build/sign/kill commands. |

**Impact:** Warehouse app now lets you pick 50mm or 80mm paper. Receipts auto-fit the selected width. The hardening means even a phone still on v1.0.4 can tap 50mm/80mm and get visual feedback + localStorage persistence (native save only works after updating to v1.0.5). The web-only parts (whmobile.html) deploy instantly with `Copy-Item` to `C:\JumongAPI\wwwroot\` — no APK rebuild needed for web-only fixes.

### Mobile app nav redesign — burger menu, sales report, inventory modes

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/whmobile.html` | Top bar simplified to brand + main store chip only; removed user chip/chevron/store-switcher/printer badge/LOGOUT from top bar. Bottom nav = SELL, INVENTORY, **MENU ☰** (burger) — SETUP removed from bottom nav. |
| `JumongCloudAPI/wwwroot/whmobile.html` | Added overlay **MENU drawer** (`#burgerMenu`) with: user name + role, version ("Jumong Pos v1.1.0"), logout button, store-switcher chip (opens store modal), INVENTORY sections (Stock / Out of Stock / Low Stock), REPORT section (Sales), SETTINGS section (Setup → opens `switchTab('setup')`). |
| `JumongCloudAPI/wwwroot/whmobile.html` | Added **SALES REPORT** page (`tabSales`) with backend list, click → sale items modal (items shown; voided struck through). |
| `JumongCloudAPI/wwwroot/whmobile.html` | Inventory modes (`invMode`) — Stock/Out of Stock/Low Stock filters in drawer; searchInventory() applies modal filter (stockQty===0 / ≤10 / all) and limits. |
| `JumongCloudAPI/wwwroot/whmobile.html` | Printer status moved to SETUP card instead of top-bar badge; `updatePrinterStatus` now targets ap. |
| `JumongCloudAPI/wwwroot/whmobile.html` | `savedTab` still supports legacy 'setup' — drawer Menu routes to it. |

**Impact:** Warehouse app is now a full-featured mobile POS with a drawer menu, per-mode inventory views, and sales reports — all in `whmobile.html` (no APK rebuild needed). The nav redesign shipped as a web-only deployment to `admin.jumongdev.com`.

### v1.1.1 (Cloud API) — Mobile Warehouse Tabs + Source Marker in Stock Trails

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | Version bumped to `"1.1.1"`. `WhSell` now writes `source` (`'mobile'` vs `'desktop'`) to `wh_stock_trails` from request body. |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs:713-715` | Migration: `ALTER TABLE wh_stock_trails ADD COLUMN IF NOT EXISTS source TEXT NOT NULL DEFAULT ''`. |
| `JumongCloudAPI/wwwroot/whmobile.html` | Warehouse section in burger menu: **📦 Product**, **📊 Inventory**, **🧾 Sales**, **🛒 Online Order**, **🚚 Transfer**. `goProducts()/goOrders()/goTransfers()` swap to warehouse tabs; SELL/INVENTORY stay bottom-nav. |

**Impact:** Phone trails can be distinguished from desktop-synced ones on the cloud (`source` column). Warehouse Product/Orders/Transfers screens reachable from the mobile menu.

### v1.1.2 — Mobile SELL fix, Receiving tab, Sales summary, Transfer picker exclusion

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **WhStockMoveDto.Source** — `WhStockMove` (PUT stock-move) now writes `source` to `wh_stock_trails` + appends `" | Mobile"` to reference (fixed `mvSource`→`mvSrc` typo). |
| `JumongCloudAPI/Controllers/DashboardController.cs:2831-2854` | **New `GET /warehouse/sales/summary?from=&to=`** → `{totalSales, transactionCount, grossInventoryCost}`. COGS = `si.stock_deduction × COALESCE(mp.cost, wp.box_cost/NULLIF(wp.box_qty,0), 0)`, voided excluded. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1165` | Version bumped to `"1.1.2"`. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **SELL fix** — duplicate `searchProducts()` (PRODUCT tab at :998 overrode SELL at :774) renamed to `searchProdList()` (includes its SEARCH button + refreshCurrentTab products case). |
| `JumongCloudAPI/wwwroot/whmobile.html` | `openUnitPicker()` rewritten — was fetching `?search=<productId>` (which searches name/barcode, never matches an ID). Now `ensureSellProdCache()` loads the active product list once and looks up by ID client-side. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **Sales report cards** — Total Sales 💵 / Transactions 🧾 / Gross Inventory Cost 📦 + TODAY / ALL / single-day date picker; `setSalesDay()`, `salesFromTo()`, `loadSalesSummary()`. |
| `JumongCloudAPI/wwwroot/whmobile.html` + `index.html:1815` | Transfer client pickers (mobile `loadTxClients()` + web dropdown) now exclude `STORE-WAREHOUSE` (`c.storeId !== 'STORE-WAREHOUSE'`) so the warehouse can't transfer to itself. |
| `Services/…` | No POS client changes. |

**Impact:** SELL tab works correctly (unit picker resolved by ID, no stale search). Warehouse mobile sales report shows today's/all-time totals with gross inventory cost (per-day via date picker). Warehouse excluded as a transfer destination. Requires v1.1.2 API deploy for the summary endpoint + stock-move source.

### Warehouse mobile UI fixes — keyboard, inventory totals, header

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/whmobile.html` | **Receiving modal hidden behind keypad** — was bottom-anchored (`items-end`); now `items-start pt-[8vh]` (float above the soft keyboard) + `enterkeyhint="done"` on qty input. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **INVENTORY totals cards** — Cost Value + Gross Value (SRP) + items/units line via existing `GET /warehouse/inventory-summary` (DashboardController.cs:1678); `loadInventoryTotals()` on tab open, pull-to-refresh, and search. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **Top header removed** — deleted JUMONG POS + store name bar (`#mainStore`) to free ~50px; user/version/logout + store switcher already live in the burger drawer (`menuUserName`/`menuStoreName`). |

**Impact:** Receiving works with the keypad up; Inventory shows the total warehouse worth (cost + SRP); more vertical space on every screen. Web-only deploys.

### v1.1.3 — Batch Stock Receiving with Source + Print History (mobile)

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **New `POST /warehouse/receivings`** — body `{source, items:[{productId, qty}]}` → atomic tx: validates source non-empty + qty>0, builds ref `RECV-yyyyMMdd-HHmmss | Supplier`, UPDATEs each product stock (guards negatives), inserts one `wh_stock_trails` row per item (type `manual_receive`, `source='mobile'`, shared ref). Returns `{success, reference}`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **New `GET /warehouse/receivings`** — `SELECT reference, MIN(created_at), COUNT(*), SUM(qty_change) … WHERE reference_type='manual_receive' AND reference LIKE 'RECV-%' GROUP BY reference ORDER BY created_at DESC LIMIT 100`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **New `GET /warehouse/receivings/{ref}/items`** — product rows for a reference (used for reprint). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | Added `WhReceivingDto` / `WhReceivingItemDto`; version bumped to `"1.1.3"`. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **Receiving tab rebuild** — required 🏷️ **Supplier/Source** input; **batch cart** (search/scan → + ADD → qty prompt AN→ ADD TO BATCH; rows with +/− qty, ✕ remove, CLEAR); one **SAVE RECEIVING (N pcs)** button; atomic cloud save. Success modal shows `+N pcs received`, ref, **🖨️ PRINT RECEIVING** (multi-item voucher: ref, source, cashier, item/qty list, totals) + **DONE**. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **🖨️ HISTORY sub-view** — `toggleRecvHistory()`/`loadRecvHistory()` lists past receivings (ref, supplier parsed from ` | `, date, item count); tap → item detail; **REPRINT** fetches the ref's items and re-sends to printer. History lives in cloud `wh_stock_trails`, so it survives app re-installs and works across devices. |

**Impact:** Receiving is now a true multi-item workflow — enter every item from a delivery first, save once (atomic), print one voucher, and reprint anytime from stored history. No schema change (reuses `wh_stock_trails.reference` as the batch/print-history ID). Requires v1.1.3 API deploy.

**Impact:** Warehouse app is now a full-featured mobile POS with a drawer menu, per-mode inventory views, and sales reports — all in `whmobile.html` (no APK rebuild needed). The nav redesign shipped as a web-only deployment to `admin.jumongdev.com`.

### Warehouse mobile — bulk qty input + cleaner receipts (web-only)

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/whmobile.html` | **Cart qty is now an editable input** (`qty-input` class, `type=number inputmode=numeric`) — tap the number and type `50` instead of tapping + 50 times. New `setQty(i, val)` validates stock (clamps to max available including other cart rows of the same product), removes the row if qty ≤ 0. `+`/`−` buttons unchanged. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **Receipt rewrite** (`buildReceiptText`) — items now two-line (product name, then `Unit x qty` left + amount right-aligned via `pad()`), TOTAL/CASH/CHANGE right-aligned with `₱` comma formatting, CASH+CHANGE lines shown for Cash payments, `THANK YOU / Please come again` footer. `items` passed from `confirmPay` now includes `price` + `unitName` (was missing → receipt would've printed NaN). |
| — | **Web-only deploy** — `Copy-Item` to `C:\JumongAPI\wwwroot\whmobile.html` (live, no restart needed) + `bin\Release\net8.0\win-x64\publish\wwwroot\` (next full deploy). Verified live: `setQty` + `qty-input` present on `admin.jumongdev.com/whmobile.html`. |

**Impact:** Warehouse walk-in sellers can type bulk quantities directly (no more tapping + 50×). Receipts print with right-aligned ₱ amounts, cash/change breakdown, and proper item layout. No APK rebuild — web-only fix.

### Warehouse Mobile Sales Report 500 Fix (string → DateTime date params)

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:2894-2895` | **WhGetSales date filter fixed** — was passing raw `from`/`to` strings → PG error `42883: operator does not exist: timestamp with time zone >= text` → 500 on ANY date-filtered query (mobile sales report TODAY/date picker always sends from/to). Now `DateTime.TryParse` + `AddWithValue("from", DateTime)` (to = `toDate.Date.AddDays(1)`), same pattern as `WhGetInventoryActivity`. |
| `JumongCloudAPI/Controllers/DashboardController.cs:2922-2923` | **WhGetSalesSummary** — same string→DateTime fix (mobile summary cards were also 500ing). |
| — | Deployed via `deploy_api.bat` (elevated UAC). Verified live: `GET /warehouse/sales?from=2026-08-07&to=2026-08-07` → 200, 13 sales; summary OK. |

**Impact:** Mobile warehouse SALES report showed "No sales recorded"/failed silently because every date-filtered call returned 500. Sales themselves were always saved fine (`wh_walkin_sales` rows exist) — only the REPORT query was broken. The "request queue is full" 503 seen in-app was a downstream symptom of repeated failing requests. Fix deployed + verified live.

### v1.0.6 (APK) — Stable Bluetooth Printing (chunked writes + keep-alive + auto-reconnect)

| File | Change |
|---|---|
| `WarehouseApp/app/src/main/java/com/jumong/warehouse/BluetoothPrinter.kt` | **Chunked writes** — `printBytes` now writes in 96-byte chunks with 8ms delay between each (cheap thermal printer RX buffer overflow was killing the socket mid-print: "read failed, socket might closed, read ret: -1"). |
| `WarehouseApp/.../BluetoothPrinter.kt` | **Keep-alive polling** — background daemon thread sends a 3-byte ESC/POS DLE EOT status request every 8s while connected, so the printer never sleeps/shuts its BT radio. Idle sleep was the real cause of "connected but instantly dropped, nothing even printed". Auto-reconnects silently if the keep-alive write fails. |
| `WarehouseApp/.../BluetoothPrinter.kt` | **Auto-reconnect + retry** — if a print's write fails, reconnect once and retry the whole job before giving up (invisible-retry pattern used by retail POS apps). |
| `WarehouseApp/.../BluetoothPrinter.kt` | **Insecure RFCOMM fallback** — if the standard SPP socket is refused, falls back to `createInsecureRfcommSocketToServiceRecord` (cheap printers often reject the secure channel). |
| `WarehouseApp/.../BluetoothPrinter.kt` | **Connect timeout** — 8s connect timeout via reflection so the UI never hangs forever on a dead printer; `cancelDiscovery()` before connect. |
| `WarehouseApp/.../BluetoothPrinter.kt` | **₱ (U+20B1) → 'P'** — peso sign is outside 7-bit ASCII; replaced so US_ASCII encoding never drops the stream mid-receipt. |
| `WarehouseApp/app/build.gradle` | versionCode bumped to 7, versionName `"1.0.6"`. |
| `JumongCloudAPI/wwwroot/updates/JumongWarehouse.apk` | Rebuilt + signed with `jumong_sign.keystore` (same cert — existing installs can update without uninstalling). |
| `JumongCloudAPI/wwwroot/updates/warehouse-version.json` | version `"1.0.6"` — changelog documents the stability fixes. |

**Impact:** Bluetooth thermal printing is now stable — no more "read failed, socket might closed / read ret -1" errors. Root causes: (a) one giant `write()` overflows the printer RX buffer; (b) the printer enters sleep mode after ~1-2 min idle and silently drops the RFCOMM link even though the app thinks it's connected. Fixed the same way Loyverse/Zobaze-style POS apps win: chunked writes + persistent socket + 8s keep-alive poll + invisible reconnect, plus an insecure-RFCOMM fallback and a connect timeout. Same signing cert, existing installs can use SETUP → UPDATE APP. NOTE: build requires `JAVA_HOME=C:\Program Files\Android\Android Studio\jbr` — the Adoptium path in AGENTS.md no longer exists.

### v1.0.6 (APK) — Update Check BOM Fix + Silent Error Handling

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/updates/warehouse-version.json` | **BOM removed** — was saved UTF-8-with-BOM (`EF BB BF`); Android `checkForUpdate` returned the raw bytes → `JSON.parse` threw "Unexpected token". Rewritten as UTF-8 no-BOM. Deployed to source + live `C:\JumongAPI\wwwroot\updates\`. |
| `JumongCloudAPI/wwwroot/whmobile.html` | `checkAppUpdate()` hardened — strips `\uFEFF` BOM + `.trim()` before parse; **all failures now fail silently** ("Update check not available right now" on manual check, nothing on auto) instead of showing raw exception messages like "unexpected token" to end users. |
| `WarehouseApp/.../MainActivity.kt` | `checkForUpdate()` reads as **UTF-8 explicitly** and strips BOM + trims in Kotlin before returning to JS (defense in depth). |
| — | Deployed whmobile.html to all 3 locations + rebuilt/signed APK (same cert, still v1.0.6). Verified live JSON parses OK (first byte `7B`, no BOM). |

**Impact:** End users never see cryptic errors. The update prompt appears on app open if a newer version exists; any network/parse hiccup is silently skipped. BOM source was PowerShell `Set-Content -Encoding UTF8` (writes BOM) — always use `[System.IO.File]::WriteAllText(path, content, UTF8Encoding($false))` for JSON files.

### v1.0.7 (APK) + v1.1.4 (Cloud API) — Dashboard-Controlled Branding (Mobile App)

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs:607-618` | New `branding` table (id PK, app_title, logo_url, splash_bg, login_bg, primary_color, icon_key, updated_at) with id=1 seed. |
| `JumongCloudAPI/Controllers/DashboardController.cs:3327-3372` | New `GET /dashboard/branding` (returns BrandingConfig) + `POST /dashboard/branding` (upsert via ON CONFLICT id=1) + `POST /dashboard/branding/logo` (multipart upload -> wwwroot/assets/brand_logo.ext, validated png/jpg/webp/svg). Version bumped to `"1.1.4"`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | Added `BrandingConfig` DTO class. |
| `JumongCloudAPI/wwwroot/index.html` | Sidebar `Branding` nav item + **MOBILE APP BRANDING** panel (App Title input, Launcher Icon dropdown default/xmas/gold/blue, Splash bg + Accent color pickers, Logo file upload with preview, SAVE). |
| `JumongCloudAPI/wwwroot/components.js` | Added `brandingPanel` Alpine component (load/save/handleLogoUpload). |
| `JumongCloudAPI/wwwroot/whmobile.html` | CSS: `:root` vars `--jbg`/`--jacc`/`--jacc-dark` replace hardcoded colors across body, btn-primary, focus, chips, nav, qty-input. Loading/login screens got IDs for logo/title swap. Added `applyBrandCached(b)` (instant cached branding: bg color, accent, title, logo img, theme-color meta) + `applyBranding()` (fetches `/dashboard/branding` with 6s timeout, caches to localStorage, calls `AndroidApp.setAppIcon(iconKey)` when in app). Called at startup in `init()`. |
| `WarehouseApp/app/src/main/AndroidManifest.xml` | Launcher moved to 4 `activity-alias` entries (AliasDefault enabled=true, AliasXmas/AliasGold/AliasBlue enabled=false), each with own `android:icon` + MAIN/LAUNCHER filter. MainActivity no longer carries the launcher intent-filter. |
| `WarehouseApp/app/src/main/res/drawable/icon_{xmas,gold,blue}.xml` | 3 new pre-shipped vector launcher icons (Christmas tree, gold bars, blue bars) alongside existing icon_fg. |
| `WarehouseApp/.../MainActivity.kt` | Added `setAppIcon(key)` @JavascriptInterface -> `setAppIconFor(key)` toggles PackageManager.setComponentEnabledSetting across the 4 aliases (enable target, disable rest, DONT_KILL_APP). |
| `WarehouseApp/app/build.gradle` | versionCode 8, versionName `"1.0.7"`. |
| `JumongCloudAPI/wwwroot/updates/warehouse-version.json` | `"1.0.7"` changelog; rebuilt + signed APK (same jumong_sign keystore) deployed to all 3 update folders, no BOM. |

**Impact:** Warehouse app branding (splash + login + accent + title) is now controlled from the web dashboard's Branding panel; logo uploads land in wwwroot/assets. Launcher icon switches among pre-shipped variants at runtime via PackageManager (bank-app trick) — the icon may take a moment to refresh on the home screen. Limits: brand-new icon colors still need an APK rebuild; splash/login changes are instant because they live in whmobile.html. Verified live: GET/POST branding round-trip, logo upload serves at /assets/, version json 1.0.7, API version 1.1.4.

### v1.0.8 (APK) — Update Install Fix (REQUEST_INSTALL_PACKAGES permission missing)

| File | Change |
|---|---|
| `WarehouseApp/app/src/main/AndroidManifest.xml` | Added `android.permission.REQUEST_INSTALL_PACKAGES` — **critical**: without it on Android 8+, the APK download "completed" but the OS silently blocked the INSTALL because the app couldn't self-install, even after the user enabled "Install unknown apps" in Settings. |
| `WarehouseApp/.../MainActivity.kt` | Added `pendingApkFile` field + `onResume()` listener — after the user grants "Install unknown apps" in Settings and returns to the app, the pending APK install now auto-resumes (no need to tap UPDATE NOW again). Extracted `launchApkInstall(file)` helper. |
| `WarehouseApp/app/build.gradle` | versionCode 9, versionName `"1.0.8"`. |
| `JumongCloudAPI/wwwroot/updates/warehouse-version.json` | `"1.0.8"` changelog; rebuilt + signed APK (same jumong_sign keystore) deployed to all 3 update folders, no BOM. |

**Impact:** Warehouse app updates now actually install. Root cause: `REQUEST_INSTALL_PACKAGES` was never declared in the manifest, so on Android 8+ the system silently refused the install even though the APK downloaded and the user enabled unknown-app sources. Fixed by (1) declaring the permission and (2) auto-resuming the install when the user returns from the Settings grant screen. Diagnosed from the user report "Nag-download, di nag-install".

### v1.0.9 (APK) — Auto-Reopen After Update + Startup ANR Fix

| File | Change |
|---|---|
| `WarehouseApp/.../ReopenReceiver.kt` | **New file** — BroadcastReceiver for `ACTION_MY_PACKAGE_REPLACED`: relaunches the app 500ms after an in-app update installs, so the user doesn't drop back to the home screen. |
| `WarehouseApp/app/src/main/AndroidManifest.xml` | Registered `ReopenReceiver` (exported=false, MY_PACKAGE_REPLACED intent-filter). |
| `WarehouseApp/.../MainActivity.kt` | **Removed `webView.clearCache(true)` + `clearHistory()`** — they run synchronously on the main thread at startup and cause "app not responding" (ANR) on slow phones. The page URL is already version-busted (`?v=timestamp`) and `cacheMode=LOAD_NO_CACHE`, so web updates still go live without clearing. |
| `WarehouseApp/app/build.gradle` | versionCode 10, versionName `"1.0.9"`. |
| `JumongCloudAPI/wwwroot/updates/warehouse-version.json` | `"1.0.9"` changelog; rebuilt + signed APK (same jumong_sign keystore) deployed to all 3 update folders, no BOM. |

**Impact:** After tapping UPDATE NOW and the new version installs, the app now reopens itself automatically. Startup is faster and no longer ANRs on slow devices. Diagnosed from user reports "close program after update" + "app not responding".

### v1.0.10 (APK) + v1.1.5 (Cloud API) — Built-In Crash Logger

| File | Change |
|---|---|
| `WarehouseApp/.../MainActivity.kt` | **`Thread.setDefaultUncaughtExceptionHandler`** — writes every uncaught crash (time, app version, device model/SDK, thread, exception class+message+full stacktrace) to `getExternalFilesDir/crash.log`. Safe handler (double try/catch — must never crash itself). Added `getCrashLog()` / `clearCrashLog()` JS bridge so the web page can read + clear the log. |
| `JumongCloudAPI/wwwroot/whmobile.html` | `window.onerror` captures uncaught JS errors (last 5, stack-incl). `reportToCloud()` on `init()` — if a native `crash.log` exists or JS errors are queued, POSTs `{app, version, device, type ('native-crash'|'web-error'), log}` to `/api/dashboard/crash-report`, then clears the log. Silent failure (never blocks startup). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | New `POST /dashboard/crash-report` (creates `crash_reports` table if missing; inserts app/version/device/type/log capped 20000 chars) + `GET /dashboard/crash-reports?limit=` (id, app, version, device, type, log, createdAt, newest first). Version bumped to `"1.1.5"`. |
| `WarehouseApp/app/build.gradle` | versionCode 11, versionName `"1.0.10"`. |
| `JumongCloudAPI/wwwroot/updates/warehouse-version.json` | `"1.0.10"` changelog; rebuilt + signed APK (same jumong_sign keystore) deployed to all 3 update folders, ASCII-only changelog (no em dash — rendered as mojibake). |

**Impact:** Every crash on a real phone now reports itself to the cloud. Next time the app "keeps stopping" or the user reports a hang, the exact exception + device + version is already in the `crash_reports` PG table — no more guessing from user descriptions. Requires v1.1.5 API deploy (the `.exe`/`.dll` were rebuilt; run `deploy_api.bat` as admin to take effect).

### v1.1.6 (Cloud API) + v1.1.34 (POS) — Wholesale Sales Report: Summary Fix + Dashboard Cards + POS Report

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:2951` | **`WhGetSalesSummary` bug fix** — Total Sales + Transaction Count now computed via scalar subqueries against `wh_walkin_sales` header only, while the `wh_walkin_sale_items` join stays exclusively for Gross Inventory Cost. Previously `COUNT(*)`/`SUM(s.total_amount)` ran over JOINED rows where each sale with N items was counted N times (Aug 7: 16 sales → 28 rows → inflated `465703.00` instead of correct `193087.00`). Verified via psql: now returns `193087.00 | 16 | 191303.00`. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1165` | Version bumped to `"1.1.6"`. |
| `JumongCloudAPI/wwwroot/components.js:466,565` | Added `saleSummary` state; `loadSales()` now also fetches `/warehouse/sales/summary` with the same from/to filter. |
| `JumongCloudAPI/wwwroot/index.html:1527-1543` | **Warehouse → Sales subpage**: 3 summary cards (Total Sales ₱cyan / Transactions white / Gross Cost amber) shown above the table, updated on TODAY/FILTER. |
| `Forms/WarehouseSellForm.cs` | Added **📄 REPORT** button (below WH-INVENTORY) — opens a sales report popup with date range (From→To), FILTER, PRINT, summary labels (Total Sales / Transactions / Gross Cost / Gross Profit) + per-row invoice list (Invoice #, Customer, Items, Total, Date, Voided). Loads `/dashboard/warehouse/sales?from&to` and `/summary`. |
| `Services/PrinterService.cs:1071` | Added `PrintRawText(string)` — generic Courier New 9pt Bold print of pre-formatted text (used by the wholesale report PRINT). |
| `Services/AppVersion.cs` | Bumped to `"1.1.34"`. |
| — | Built + deployed web files (`index.html`/`components.js`) to live `C:\JumongAPI\wwwroot\` + `publish\wwwroot\`. POS client published to `C:\JumongAPI\client`. |

**Impact:** Mobile wholesale sales summary now matches reality (was inflated by item-join row multiplication). The dashboard Warehouse Sales page shows the same progressive cards as mobile. The POS client (HQ) gets a dedicated wholesale REPORT with date filter, summary labels and per-sale list — consistent reporting across all 3 surfaces from the same cloud endpoints. Requires v1.1.6 API deploy (`deploy_api.bat` as admin) for the fixed summary + the already-built `warehouse/sales/{id}/receipt` endpoint.

### v1.1.7 (web-only) — Mobile Bottom Nav Clearance Fix (Sales Report 0000 Overlap)

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/whmobile.html:101` | **Bottom nav overlap fix** — `main-content` used `p-4` (padding: 1rem) PLUS the inline `.main-content{padding-bottom:120px}` rule. Tailwind CDN injects its generated CSS *after* the inline `<style>` block, so equal-specificity `.p-4` overrode the 120px bottom clearance → actual padding-bottom was only **16px**. The fixed bottom nav (SELL/INVENTORY/MENU ≈58px + safe-area) therefore covered the last row of every page — on the Sales report the old invoice `WH-20260807-0000` was hidden. Fixed by replacing `p-4` with explicit utilities: `px-4 pt-4 pb-36 space-y-4` (`pb-36` = 144px, wins the cascade). |

**Impact:** All mobile pages now reserve the nav height — the last row on every tab is fully visible/scrollable. Web-only change; deployed to live `C:\JumongAPI\wwwroot\` + publish. Verify with `?v=` cache-bust on the WebView.

### v1.1.8 (Cloud API) + v1.1.7 (web) — Mobile Receiving Unit Conversion + History Reprint Fix

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/whmobile.html` | **Receiving modal now unit-aware** — instead of the old fixed "box + extra pcs" (which used `wh_products.box_qty`, the *default* unit qty e.g. 10 for Kopiko "by 10"), it renders **unit chips** (PC / BY 5 / BY 10 / BOX ×120) from `master_product_units` via the existing `units[]` API field, plus qty input. Conversion = `unit.qtyPerUnit × qty` → total pcs (e.g. 1 BOX × 120 = 120 pcs). Batch cart stores pcs; row shows unit breakdown (`3 box + 5 pc`); ± steppers step by the unit's qty. Falls back to old box+pcs split if product has no units but `boxQty > 1`, plain pcs otherwise. |
| `JumongCloudAPI/wwwroot/whmobile.html` | `unitBoxQty(p)`/`unitBoxLabel(p)` helpers — largest qty unit from `units[]` replaces `boxQty` in stock display (`boxesText`, `stockText`, inventory list). |
| `JumongCloudAPI/wwwroot/whmobile.html` | **History REPRINT 0-items bug fixed** — `loadRecvHistory` was stripping ` \| supplier` from the reference, so `reprintReceiving`/`showRecvHistoryItems` queried `/receivings/{stripped-ref}/items` which matched nothing → "0 item 0 qty" receipt. Now passes the FULL reference to both handlers. |
| `JumongCloudAPI/wwwroot/whmobile.html` | `lastReceiving` now set immediately after `saveReceivingBatch` success, so the instant PRINT RECEIVING button after saving works (was "Nothing to print"). |
| `JumongCloudAPI/Controllers/DashboardController.cs:1862` | `WhGetReceivingItems` hardened — `WHERE reference = @ref OR reference LIKE @ref \|\| ' \|%'` so both full and stripped references resolve items. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1165` | Version bumped to `"1.1.8"`. |

**Impact:** Receiving a box of Kopiko Black (real box = 120 pcs) now adds 120 pcs instead of 10 — the conversion rate comes from the item's own unit attributes instead of the misnamed `box_qty` column. Stock remains pcs-based for transfers + POS selling (unchanged). History reprint now prints the actual items. Requires v1.1.8 API deploy (`deploy_api.bat` as admin) for the backend fix + version; web files are live already (verified 200 on `admin.jumongdev.com/whmobile.html`).

### v1.1.36 — Wholesale Reprint Shows Void-Adjusted Total (POS HQ)

| File | Change |
|---|---|
| Forms/WarehouseSellForm.cs:1398-1425 | btnReprint_Click now reads ''isVoided'' from the sale items JSON and skips voided items when summing gTotal. All items voided -> "Nothing to reprint" message. Passes title="REPRINT (VOID ADJUSTED)" when some items were voided. |
| Forms/WarehouseSellForm.cs:1360 | (existing) Void item picker already knew isVoided - reprint now uses the same flag. |
| Services/PrinterService.cs:913 | PrintWhReceipt() gained optional ''title'' param - prints bold title line under "─── WALK-IN SALE ───" (default empty, no change to normal receipts). |
| Services/AppVersion.cs | Current bumped to "1.1.36". |

**Impact:** Reprint after a partial void on the POS HQ wholesale report now shows the ADJUSTED total (voided item subtotals excluded) with a "REPRINT (VOID ADJUSTED)" banner, matching the POS ReportsForm behavior. Root cause: WhVoidSale only sets is_voided=TRUE (subtotal kept in DB), and the old reprint summed every item's subtotal without the isVoided filter. Client-only change - no cloud API deploy needed.

### v1.1.9 (Cloud API) + mobile web — Inventory Summary PC-Basis Cost + Void-Adjusted Reprint (mobile)

| File | Change |
|---|---|
| JumongCloudAPI/Controllers/DashboardController.cs:1726-1733 | WhInventorySummary cost now uses per-PC basis: COALESCE(mp.cost, w.box_cost/NULLIF(w.box_qty,0), 0) * w.stock_qty via LEFT JOIN master_products (was ox_cost/box_qty * stock_qty which understated cost when box_qty was corrected to real carton qty, e.g. Kopiko 125/120=1.04). zero_cost_items now based on COALESCE(mp.cost, w.box_cost). |
| JumongCloudAPI/wwwroot/whmobile.html | buildReceiptTextReprint(): filters out isVoided items, computes TOTAL from active subtotals (no longer h.total), prints "VOID ADJUSTED (n items voided)" note, returns null when all voided -> reprintSale() shows "Nothing to reprint" toast. Matches POS WholesaleSendForm/PrintWhReceipt behavior. |
| DashboardController.cs:1165 | Version bumped to "1.1.9" |

**Impact:** Mobile inventory Cost Value now reflects true per-PC catalog cost (~1.78M instead of 0.83M vs gross 1.82M ~3% margin). Mobile reprint of partially-voided wholesale sales prints only active items with VOID ADJUSTED marker. Requires v1.1.9 API deploy; whmobile.html already deployed live.

### v1.1.10 (Cloud API) + mobile web — Warehouse End Shift (Mobile Only)

| File | Change |
|---|---|
| JumongCloudAPI/Controllers/DashboardController.cs:3301-3393 | WhEndShift rewritten — `since` is computed server-side as `MAX(close_date)` from `wh_daily_closes` (no client `Since`); `preview=true` returns totals without INSERT; single shared shift for mobile + POS wholesale (same cash drawer). Response now returns id, totals, saleCount, cashOnHand, difference, expenses, and all denom fields (for instant thermal print). |
| JumongCloudAPI/Controllers/DashboardController.cs:3690-3715 | WhEndShiftRequest changed: removed `Since`/`CashOnHand`; now `Preview`, `Expenses`, `CashierName`, `Denom1000`, `Denom500`, `Denom200`, `Denom100`, `Denom50`, `Denom20`, `DenomCoins` (cash on hand computed server-side from denominations). |
| JumongCloudAPI/Controllers/DashboardController.cs:3395-3416 | WhGetShifts now returns denom1000..denom_coins columns for history detail view. |
| JumongCloudAPI/Data/PgDatabaseHelper.cs:700-709 | Migration: `ALTER TABLE wh_daily_closes ADD COLUMN IF NOT EXISTS denom1000/500/200/100/50/20/denom_coins NUMERIC`. |
| JumongCloudAPI/wwwroot/whmobile.html | **END SHIFT** menu item added (WAREHOUSE section in burger menu). Modal with shift totals card (Total Sales / Sale Count / Cash / E-Wallet / Credit / Voided + Expected Cash), denomination inputs ₱1000/₱500/₱200/₱100/₱50/₱20 counts + Coins with live per-row totals (recalcEndShift), Expenses input, Cash on Hand vs Expected + diff (green OVER / red SHORT), SAVE END SHIFT → confirm dialog → POST → thermal print (printEndShift, denial check) → SHIFT HISTORY (loadShiftHistory) with per-shift detail view. JS verified with node --check. |
| DashboardController.cs:1165 | Version bumped to "1.1.10" |
| DashboardController.cs:3309-3319 | Fallback: when no prior close exists (table empty), `since` defaults to today's PH midnight (Asia/Manila) instead of `DateTime.MinValue` — first close only counts today's sales |
| PostgreSQL data (2026-08-09) | Baseline record inserted into `wh_daily_closes` (id=2, close=2026-08-09 00:00 PH, totalSales=1,650,013 covering ALL pre-existing wh_walkin_sales, settled diff=0, cashier `SYSTEM - baseline`) so the first REAL mobile end shift counts only 2026-08-09 sales (₱186,772 / 38 sales) and today stays open |

**Impact:** Warehouse mobile sellers can close the shift like the POS EndShiftForm — count denominations from the shared cash drawer, see expected cash vs on-hand (short/over), expenses, and print the end-of-shift report. The cloud dashboard does NOT have a UI yet (only API + mobile). Requires v1.1.10 API deploy (run deploy_api.bat as admin; web files are already live).

### v1.1.13 (Cloud API) + mobile web — Cashier Name Fix, Mobile End-Shift UI Fixes, Credit Billing

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:214,845,915` | **"Cashier #N" display fix** — 3 queries (recent-sales, cashier-performance, sale-profits) now fall back to the **last known cashier_name of the same (store_id, user_id)** before rendering "Cashier #N". Fixes orphan users: user_id 7 (ACGS Bella Porch), 26 (HQ angelique), 13 (neslene de vera) — same person no longer appears twice ("neslene de vera" + "Cashier #13"). Display-only; end shift computation never used cashier_name. |
| `JumongCloudAPI/Controllers/DashboardController.cs:3351-3421` | **WhEndShift** — tracks `creditCollected` (all Payment-typed credit txn credits since last close) + `creditCollectedCash` (Cash-method only). `expectedCash = totalCash + creditCollectedCash` (e-wallet credit collections don't go to the drawer). Stored `sale_count` + `credit_collected` in wh_daily_closes. |
| `JumongCloudAPI/Controllers/DashboardController.cs:3423-3435` | **WhGetShifts** — returns `saleCount` + `creditCollected` (history detail + reprint). |
| `JumongCloudAPI/Controllers/DashboardController.cs:2779-2821` | **New `POST /warehouse/credit-pay`** — body `{customerId, amount, method, cashierName}`: validates amount ≤ balance, decrements `customers.credit_balance`, inserts `credit_transactions` row (type `Payment`, credit=amount, pos_id = negative NEXTVAL to avoid the `UNIQUE(store_id, pos_id)` collision with sale rows). Returns `{id, amount, method, balance}`. |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs:710-712` | Migration: `ALTER TABLE wh_daily_closes ADD COLUMN IF NOT EXISTS sale_count INTEGER NOT NULL DEFAULT 0; ALTER TABLE wh_daily_closes ADD COLUMN IF NOT EXISTS credit_collected NUMERIC NOT NULL DEFAULT 0;` |
| `JumongCloudAPI/wwwroot/whmobile.html` | **End-shift modal scroll fix** — `items-end` bottom sheet → `items-start pt-[8vh]` (soft keyboard no longer hides SAVE, matches recvModal pattern). **txItemsModal z-50 → z-[60]** — SHIFT HISTORY no longer renders behind the end-shift modal. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **🖨️ PRINT REPORT in shift history** — `showShiftDetail()` stores `lastShiftDetail`; new `printShiftFromHistory()` reprints any closed shift using stored denoms/totals/date/cashier (printEndShift now accepts dateLabel+cashierLabel). Print shows Credit Collected line when > 0. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **💳 CREDIT BILLING** — burger menu row → `creditModal` (z-60) lists customers with wholesale balance > 0 (computed: WH credit sales billed − wholesale payments), searchable; tap/VIEW → **`creditBreakdownModal` (z-65)** — shows TOTAL BALANCE card + per-receipt FIFO breakdown (invoice #, date, amount, remaining/PAID, oldest-first allocation); COLLECT opens `creditPayModal` (z-70) with amount + method (Cash/E-Wallet) → POST credit-pay → toast + list refresh + optional thermal payment voucher (padDL helper). New `wh_credit_pay` JS section. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **New `GET /warehouse/credit-billing`** — customers with wholesale balance > 0 (`billed` = non-voided WH credit sales, `paid` = `store_id=''` Payment credits, balance = billed−paid, clamped ≥0). **New `GET /warehouse/credit-breakdown?customerId=N`** — FIFO per-receipt amounts (oldest-first, payments applied in order), returns `{customerId, name, totalBalance, paidTotal, receipts:[{invoiceNo, date, amount, remaining}]}`. |
| `Services/AppVersion.cs` | (unchanged, still 1.1.38 — client release v1.1.38 has the POS promo full-display fix) |
| `JumongCloudAPI/Controllers/DashboardController.cs:1173` | Version bumped to `"1.1.13"`. |

**Impact:** Dashboard CASHIER PERFORMANCE no longer shows mystery "Cashier #N" rows (resolves to last known name). Mobile end shift works with the soft keyboard open, history is reachable, and any old shift can be reprinted. Warehouse sellers can now collect customer credit payments on the phone — balance decremented, Payment trail in credit_transactions (feeds `creditCollected` in end shift), thermal voucher printed, and per-receipt FIFO breakdown shows exactly which invoices still owe (wholesale only — retail POS receipts excluded by `store_id=''` filter on payments). Requires v1.1.13 API deploy (`deploy_api.bat` as admin). Web files already live.
