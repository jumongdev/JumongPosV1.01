# Jumong POS V1.01 - Progress Tracker

## Completed Tasks [x]

### Theme & Layout
- [x] Applied "dark cyber" theme to all management forms (Products, Customers, Reports, Users, StockReceiving, EndShift, Expenses, CreditManagement, Settings, VoidLog, StockMovement)
- [x] Fixed control render order (`pnlMain` → `pnlToolbar`) across all forms to prevent header overlay
- [x] Added `Shown` event initialization for responsive layout on all forms
- [x] Implemented `ResizeLayout` methods for dynamic panel resizing on window resize
- [x] SettingsForm: fixed save button anchor, z-order, and panel width stretching

### Grid Optimization
- [x] `AutoSizeColumnsMode = AllCells` with key columns (Name/Product) set to `Fill`
- [x] StockReceivingForm search picker and history trail optimized
- [x] All DataGridViews use compact auto-sizing with text-heavy columns set to Fill

### Expense Management System
- [x] Added `Expenses` table to database schema
- [x] Created `ExpenseService` (CRUD, shift total calculations, category totals)
- [x] Built `ExpensesForm` with dark theme, list view, and entry modal
- [x] Integrated expenses into `EndShiftForm` shift math
- [x] Expense category presets (7 categories in dropdown)

### End Shift - Blind Close Workflow
- [x] Removed all revealing financial metrics from cashier screen
- [x] Summary card shows only: Shift Date/Time, Active Cashier, Opening Balance, Shift Expenses
- [x] Confirmation dialog stripped to compliance alert only
- [x] Cash denomination validation blocks close if not entered
- [x] Backend silently calculates variance and saves to `DailyClose`
- [x] `PrintAuditEndShiftReport()` — thermal printer outputs 4 itemized sections
- [x] `EmailService.SendEndShiftReport()` rewritten as styled HTML email
- [x] Added `GetCreditPaymentsSinceLastClose()` to `DailyCloseService`

### Shift Session Lifecycle
- [x] `ShiftSessionService` manages active shift state via Settings table
- [x] Login prompts cashier for opening balance when no active shift
- [x] Subsequent logins skip prompt until shift is closed
- [x] Admin users bypass opening balance prompt entirely
- [x] Opening balance locked read-only in EndShiftForm, auto-populated from session
- [x] `EndSession()` called on shift close, clearing the flag

### Transaction Safety
- [x] SettingsForm `btnSave_Click` wrapped in SQLite transaction (all-or-nothing + audit)
- [x] EndShiftForm close: save first, print/email in try-catch (printer failure won't block close)

### Full Transaction Tagging (Every record stamped with who did it)
- [x] Sales: `sale.UserId` set from logged-in user
- [x] Voids: `VoidLog.UserId` + `VoidLog.UserName` populated on all void operations
- [x] Credit transactions: `CreditTransactions.UserId` + `UserName` on all credit ops
- [x] Products: `Products.ModifiedBy` tracked on create/edit
- [x] Customers: `Customers.ModifiedBy` tracked on create/edit
- [x] Users: `Users.ModifiedBy` tracked on create/edit
- [x] Expenses: `Expenses.CashierUsername` (already existed)
- [x] Stock receiving: `StockTrail.UserId` + `UserName` (already existed)
- [x] End shift: `DailyClose.UserId` + `UserName` (already existed)
- [x] Settings changes: `AuditLog` table tracks old/new values + username

### Anti-Theft / Security
- [x] Role-based shift history: Admin sees all columns; Cashier sees only CloseDate, UserName, Notes
- [x] VoidLog form shows per-cashier void summary with alert colors (>5 orange, >10 red)
- [x] Opening balance locked read-only for ALL roles in EndShiftForm
- [x] Mandatory cash count: shift cannot close without denomination entry
- [x] Cash on hand zero check: blocks close if transactions occurred but no cash counted
- [x] End Shift access audit: every form open logged to AuditLog
- [x] Audit log viewer in SettingsForm (all settings changes with before/after)

### Database Backup/Restore
- [x] "BACKUP DATABASE" button in SettingsForm (save to chosen location)
- [x] "RESTORE DATABASE" button (creates pre-restore safety backup, restarts app)
- [x] Both actions logged to AuditLog
- [x] Daily auto-backup (7-day retention) in Program.cs
- [x] `DatabaseHelper.SetDbPath()` for testing

### Email Report Scheduling
- [x] Background timer in MainForm checks every 60s
- [x] Configurable `EmailScheduleHour` (default 20 = 8PM)
- [x] Sends daily auto-report once per day
- [x] Configurable in SettingsForm via "Email Report Hour" field

### Shift Comparison Dashboard
- [x] `DailyCloseService.GetShiftComparison()` — daily aggregates
- [x] "TRENDS" button in shift history — 60-day grid: date, shifts, sales, expenses, AVG OVER/SHORT
- [x] Green (over) / Red (short) color coding on variance column

### Expense Receipt Photo
- [x] `ReceiptImage` column in Expenses table
- [x] "ATTACH RECEIPT PHOTO" button in ExpenseEntryForm
- [x] File picker (jpg/png/bmp), copies to `ExpenseReceipts\` folder
- [x] Path saved to DB, preview shown after attachment

### Barcode Scanner Improvements
- [x] 180ms debounce timer — auto-detects scanner input (no Enter required)
- [x] Tab key also triggers barcode processing
- [x] Fast scanner input triggers on pause; Enter/Tab triggers immediately

### Customer Display Optimization
- [x] `DoubleBuffered = true` + optimized paint flags
- [x] `SuspendLayout()/ResumeLayout()` around cart updates (no flicker)

### UI Fixes
- [x] Opening balance dialog (`PromptOpeningBalance`) — proper sizing, no overlap, Cancel button
- [x] PaymentForm layout — expanded to 400x580, all controls positioned with proper Y offsets
- [x] E-Wallet payment: amount locked to exact total, no change shown
- [x] Product checklist print: long names wrap to second line instead of truncating
- [x] Checklist format optimized: `|___` fits within paper width

### Bug Fixes
- [x] Removed duplicate `EndShiftForm` from `DailyCloseForm.cs`
- [x] Fixed `btnNew` null ref in `ProductsForm`
- [x] Fixed `VoidLogForm` missing model using
- [x] Fixed VoidLogForm action color check ("VoidItem" instead of "VOID_ITEM")
- [x] Void re-sync: voided items now trigger sale re-sync to cloud (v1.0.16)
- [x] Cloud revenue: changed from `s.grand_total` to item-level sums excluding voided items
- [x] PH timezone: `SET TIMEZONE TO 'Asia/Manila'` for all cloud queries
- [x] Update source: migrated from Railway to GitHub Releases to avoid dependency

### Debug Site Map
- [x] `Helpers/DebugHelper.cs` — static helper adds form-name label to bottom-right of any form
- [x] All 21 forms call `DebugHelper.AddFormLabel(this)` in constructor
- [x] Label shows class name (e.g. `ProductsForm`, `SalesForm`) in dim text for easy debugging

### Online Ordering Toggle
- [x] "Enable Online Ordering" checkbox in SettingsForm (admin-only DISPLAY SETUP section)
- [x] Saved as `EnableOnlineOrders` in Settings table (default: true)
- [x] `OrderTypeForm` reads setting; hides "ONLINE ORDER" button and shrinks form when disabled

### Stock Receiving Print
- [x] `PrinterService.PrintStockReceiving()` — thermal receipt for received items (company header, date, cashier, ref#, item list with prev/new stock, totals)
- [x] `PrinterService.PrintStockReceivingHistory()` — printable history report for trail entries
- [x] After `ConfirmReceiving()`, dialog asks "Print receiving receipt?"
- [x] HISTORY dialog has green PRINT button to print currently filtered trail entries

### Cloud Dashboard (v4)
- [x] Dashboard v4: loading spinners, error toasts, connection indicator, last-refresh timestamp
- [x] Date range picker, search/filter, CSV export, collapsible panels, pagination
- [x] Sale Profit Breakdown panel (per-invoice revenue, cost, profit, margin)
- [x] Cashier Performance table with payment method breakdown
- [x] Peak Hours 24-hour bar chart
- [x] Product Management panel (CRUD for master products with units)
- [x] Expense Details panel (individual entries with descriptions)
- [x] Voided Amount card in summary
- [x] Click invoice number to see per-item breakdown with cost/profit/margin
- [x] PH timezone: all date filters use Asia/Manila
- [x] Revenue uses item-level sums (excludes voided items)
- [x] RENAME button removed

### Local App Features (v1.0.16)
- [x] CHECK COST tool — grid view with filter buttons (Cost=0, Cost=Price, Cost>Price)
- [x] DOWNLOAD MASTER button — pulls 618 products with 717 units from cloud
- [x] Incremental download — matches by barcode, updates existing, adds new
- [x] SYNC ALL now includes sales (push historical sales to cloud)
- [x] Void re-sync — voiding items triggers sale re-sync to cloud
- [x] Update from GitHub Releases (no Railway dependency)

### Deployment
- [x] Target: `bin\Release\net8.0-windows\win-x64\JumongPosV1.01.exe`
- [x] Self-contained publish: single exe (168MB), no .NET install needed
- [x] Release mode builds cleanly with no warnings
- [x] Updates distributed via GitHub Releases (v1.0.16+)

### Warehouse Transfer System (v1.0.26)
- [x] Cloud DB: added `master_product_id` to `wh_products`, `store_id` to `wh_clients`, `base_qty` + `base_unit_name` to `wh_order_items`
- [x] Cloud API: `POST /warehouse/products/from-master/{masterId}` — create warehouse product from master catalog
- [x] Cloud API: `GET /warehouse/transfers/pending?storeId=X` — list shipped orders for a POS store
- [x] Cloud API: `PUT /warehouse/orders/{id}/receive` — mark order received + return items for stock adding
- [x] Cloud API: `WhUpdateOrderStatus` auto-deducts warehouse stock when status set to "shipped"
- [x] Cloud API: `WhGetClients` supports `storeId` filter; `WhCreateClient` includes `store_id`
- [x] Cloud API: `WhCreateOrder` auto-calculates `base_qty` from unit type × box_qty_per_unit
- [x] Dashboard UI: **FROM MASTER** button imports products from master catalog into warehouse
- [x] Dashboard UI: **NEW ORDER** flow — select client, add items (box/piece), auto-calculates base qty
- [x] Dashboard UI: **PROCESS** / **SHIP** action buttons on orders with status workflow
- [x] Desktop: `SyncService.GetPendingTransfersAsync()` — fetches shipped transfers from cloud
- [x] Desktop: `SyncService.MarkTransferReceivedAsync(orderId)` — marks received + returns items
- [x] Desktop: `StockReceivingForm` **CHECK PENDING TRANSFERS** button — fetch, select, receive, auto-populate
- [x] Desktop: `PendingTransfer` and `TransferItem` model classes
- [x] Stock always tracked in base units (pieces) — no conversion errors when receiving

### Master Product Catalog (Cloud PostgreSQL)
- [x] `master_products` table — 618 products seeded from HQ client DB
- [x] `master_product_units` table — 717 units (multi-pack pricing)
- [x] API: GET /api/dashboard/products/master/download (products + units in one call)
- [x] API: POST /api/dashboard/products/master (create product with units)
- [x] API: PUT /api/dashboard/products/master/{id} (update product with units)
- [x] API: DELETE /api/dashboard/products/master/{id} (soft delete)

## Pending / Upcoming Tasks

- [ ] Unit tests (blocked: MSTest/xUnit NuGet packages incompatible with net8.0-windows TFM)
- [ ] PostgreSQL migration for multi-PC setup (PG 18.4 installed, connection ready)

## Key Decisions
- **Shift Math:** `Expected Cash = Total Cash Sales - Total Shift Expenses`
- **Shared Drawer:** Single opening balance per session, multiple cashiers share
- **Blind Close:** Cashier sees zero financial metrics; owner receives full HTML report
- **Admin Bypass:** Admin login skips opening balance prompt
- **E-Wallet:** Always exact amount, no change, read-only tendered field
- **Role-Based History:** Admin sees all columns; Cashier sees only non-financial columns
- **DB Recovery:** Restore creates safety backup before overwriting
- **Update Distribution:** GitHub Releases (v1.0.16+), no Railway dependency
- **Cloud Revenue:** Item-level sums with `si.is_voided = false`, not `s.grand_total`
- **Time Zone:** All cloud queries use `SET TIMEZONE TO 'Asia/Manila'`
- **Master Catalog:** Cloud PostgreSQL as source of truth for products; stores pull via DOWNLOAD MASTER
- **Warehouse Transfers:** Products flow master → warehouse → order (shipped) → POS client receives. All stock tracked in base units (pieces) to prevent unit mismatch.
- **Transfer Status Flow:** pending → processing → shipped → received. Shipped deducts warehouse stock; received triggers POS stock receiving sync.

## Files Created
- `Models/AuditLog.cs`
- `Services/AuditLogService.cs`
- `Services/ShiftSessionService.cs`
- `Helpers/DebugHelper.cs`

## Critical Context
- `ShiftSessionService`: persists shift active state + opening balance to Settings table
- `DailyCloseService.GetLastCashOnHand()`: previous close's cash
- `DailyCloseService.GetShiftComparison()`: 60-day OVER/SHORT trends
- `AuditLogService.Log()`: standalone logging (own connection)
- `AuditLogService.LogTransaction()`: participates in caller's transaction
- `ExpenseService.SaveExpense()`: accepts optional `receiptImage` path
- `SaleService.VoidSale/VoidItem`: require `voidedByUserId`, `voidedByUserName` + re-sync sale to cloud
- All forms use `Shown` event for initial layout calculation
- `PrinterService.PrintAuditEndShiftReport` handles 4-section audit slip
- `PrinterService.PrintStockReceiving` prints receiving receipt for confirmed items
- `PrinterService.PrintStockReceivingHistory` prints trail history from filter results
- `EmailService.SendEndShiftReport` sends styled HTML with full variance breakdown
- `UpdateService.CheckUpdate()` queries GitHub Releases API directly (no cloud dependency)
- Cloud revenue queries use `SUM(si.total_price)` with `si.is_voided = false` (not `s.grand_total`)
- All cloud date comparisons use `SET TIMEZONE TO 'Asia/Manila'` at connection level
- Master catalog endpoints: `/api/dashboard/products/master/*`
