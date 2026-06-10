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
│   ├── AppVersion.cs           # Current = "1.0.21"
│   └── ... (PrinterService, EmailService, etc.)
├── Forms/
│   ├── MainForm.cs             # Sidebar navigation (POS, Products, Reports, Settings...)
│   ├── SalesForm.cs            # Point-of-sale cart UI
│   ├── ProductsForm.cs         # Product list + detail panel (now view-only)
│   ├── ProductUnitsForm.cs     # Unit manager (Name, Price, Qty only — Cost auto)
│   ├── SettingsForm.cs         # Organized sections with descriptions
│   ├── ReportsForm.cs          # Sales reports
│   ├── StockMovementForm.cs    # Stock trail viewer
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
    └── v1.0.21/  (exe) — current
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

### Profit/Margin Fix (v1.0.18 → v1.0.19)
| File | Change |
|---|---|
| `Forms/SalesForm.cs:456` | Sets `UnitCost = unit?.Cost ?? product.Cost` when adding item to cart |
| `JumongCloudAPI/DashboardController.cs` | 3 queries (`sale-profits`, `profit-summary`, debug) now use `COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0)` as fallback when unit_cost = 0 |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `ALTER TABLE sale_items ADD COLUMN IF NOT EXISTS unit_cost` |
| PostgreSQL data | Backfilled 36,002 historic sale_items with product costs |

### Sync From Cloud Feature (v1.0.19)
| File | Change |
|---|---|
| `Services/SyncService.cs:452` | `DownloadMasterCatalog()` — downloads master products + units from cloud, updates local Price/Cost/Category/Units, adds new products with StockQty=0, stock unchanged |
| `Forms/SettingsForm.cs` | Added **SYNC FROM CLOUD** button with description label |
| `JumongCloudAPI/DashboardController.cs` | `GET /products/master/download` endpoint returns all master_products with units as JSON |

### Expense Timezone Fix (v1.0.19 → v1.0.20)
| File | Change |
|---|---|
| `Services/SyncService.cs:286-302` | `SyncExpense()` now sends local time with `+08:00` offset instead of converting to UTC (was: `et.ToUniversalTime()`, now: `expense.Timestamp + " +08:00"`) |

### Settings Page Redesign (v1.0.20)
| File | Change |
|---|---|
| `Forms/SettingsForm.cs` | Complete rewrite: organized into 4 sections (RECEIPT SETUP, DISPLAY SETUP, CLOUD SYNC, DATA MANAGEMENT), each button has a gray description text explaining its purpose, fixed scrolling/overlapping |

### Unified Product Management (v1.0.21)
| File | Change |
|---|---|
| `Forms/ProductsForm.cs` | **New/Edit/Units/Delete/Save/Cancel buttons hidden for ALL users** — product creation only via cloud master catalog. Added SYNC TO CLOUD button (later removed). Only VIEW STOCK MOV'T, DOWNLOAD MASTER, CHECK COST remain. |
| `Forms/ProductUnitsForm.cs` | **Cost field removed** from input form and DataGridView. Cost auto-calculated as `baseCost × QtyPerUnit`. **ControlBox = false** (cannot close via X button, only Close button). Column headers added. |
| `Forms/SalesForm.cs:456` | `UnitCost` changed from `unit?.Cost ?? product.Cost` to `product.Cost * qtyPerUnit` (consistent with auto-calc approach) |
| `JumongCloudAPI/wwwroot/index.html` | Cloud dashboard unit form: **Cost input removed**, auto-calculates as `baseCost × QtyPerUnit` in `collectUnits()`. Column headers (Name, Price, Qty, Default) added. |

### Master Catalog Updates
| Action | Detail |
|---|---|
| Added 3 missing products | DEL MONTE TOMATO SAUCE, POTATO CRISPS BACON & CHEESE, SAN MIG LIGHT 330ML — copied from HQ store to master_products |
| Added default 'pc' units | For the 3 new master products |
| Master count | Now **621** products (matching HQ) |

## Current App Behavior by Role

### Products Page
| Feature | Any User |
|---|---|
| View product list | ✅ |
| View product details (right panel) | ✅ (read-only) |
| CHECK COST | ✅ |
| VIEW STOCK MOV'T | ✅ |
| DOWNLOAD MASTER | ✅ |
| + NEW / EDIT / UNITS | ❌ hidden for ALL |
| DELETE / SAVE / CANCEL | ❌ hidden for ALL |

### Settings Page
| Button | Description |
|---|---|
| SYNC ALL TO CLOUD | Upload all data (products, sales, expenses, etc.) to cloud |
| SYNC TODAY ONLY | Upload today's unsynced data |
| SYNC FROM CLOUD | Download master catalog prices & costs (stock unchanged) |
| VIEW SYNC LOG | History of sync operations |
| UPDATE APP | Check GitHub for new version |

## Build & Deploy

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
# Or force deploy via API:
# POST https://api.digitalocean.com/v2/apps/{app_id}/deployments
# Body: {"force_build": true}
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
5. **Expense timestamps** send local time with `+08:00` offset (Philippines timezone)
6. **Profit queries** in cloud API fallback to `p.cost` when `sale_items.unit_cost = 0`
