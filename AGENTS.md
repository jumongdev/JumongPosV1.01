# JumongPOS - Project Guide for AI Agents

> CONDENSED 2026-09-05: core operational reference lang ito (connections, machines, deploy, structure, rules).
> Lahat ng detailed change history ay nasa CHANGELOG.md (rolling session logs + v1.0.x/v1.1.x archive).
> Huwag na magdagdag ng session logs dito - ilagay sa CHANGELOG.md. May format ito:
> "## Latest Change (YYYY-MM-DD) - one-line summary" + detail, lagi sa ITAAS ng CHANGELOG.md.

## Quick Status (2026-09-14)
- POS client: **1.1.83** (Services/AppVersion.cs) | Cloud API constant: 1.1.84 | latestVer: **1.1.83** (DashboardController.cs) — **🔍 CUSTOMERS search + WALANG LIMIT (2026-09-21, API 1.1.84):** dashboard Customers panel may search box (name/phone/email/QR, hindi kasama ang inactive); ang `/dashboard/customers` ay **walang LIMIT na** (dating LIMIT 500 → 33 active R-Z customers ang nakatago; ngayon 581 lahat) + optional `search` param. **🚨 CRITICAL POINTS FIX (2026-09-21, API 1.1.83):** ang `SyncCustomers`/`SyncUsers` (v1.1.82 update-only code) ay snake_case lang ang binabasa pero **camelCase** ang JSON ng POS (`posId`/`loyaltyPoints`/`isActive`) → **na-wipe ang points (0) sa bawat push mula ~Sep 16-17**. Na-fix via `TryGetProp()` dual lookup; na-repair ang **52 customers (+300 pts)** sa cloud PG via `GREATEST(current, earned from sales by customer_qr + ecom)`. POINTS LIABILITY ngayon = **1,207 pts (₱1,207)**. **FOLLOW-UP (POS release):** i-re-read ang per-item PointsEarned bago mag-SyncSale (SaleService.cs:223) at huwag i-clear ang PointsDirty kapag `updated=0` ang cloud. v1.1.82 released 2026-09-14: **ONE RULE — customers ay sa E-COMMERCE (Google) registration lang**: POS CustomersForm view-only, CustomerService.Save update-only, cloud SyncCustomers never-insert (google_sub lang ang update), Google login **linking** (email→name) para walang "Name 2"; manual accounts (350) lahat **inactive + 0 points** (walang delete — safe references); dashboard Customers panel may **⭐ POINTS LIABILITY (LIVE)** strip (`/customers/points-summary`, 1 pt = ₱1) para ma-set aside sa bank; CustomerChatForm fix (AutoSize+MaximumSize name wrap + debug label `#if DEBUG` — tanggal ang "CustomerChatForm" overlay sa SEND).
- **v1.1.82 rollout (checked 2026-09-16 via agent status):** HQ ✅ 1.1.82, HVR ✅ 1.1.82; **Naic + ACGS nasa 1.1.80 pa** (outdated) — i-tap ang UPDATE APP sa dalawang store.
- **🧹 Phantom store cleanup + BARCODE-SYNC (2026-09-16):** na-delete na ang `STORE-20260912-88AF` (737 rows) + DEV leaked products (3 rows) sa cloud `products`; `/stock-status` at `/summary` = **registered stores lang** (`IN (SELECT store_id FROM stores)`); DEV products push disabled sa SyncController. **Barcode change sa master → agad na naka-sync sa lahat ng stores** (`PUT /products/master/{id}` propagation) at ang stock-snapshot ay **master-preferring** name/barcode — kaya HINDI na lumalabas na 0 ang stock sa dashboard pagpalit ng barcode (transient window dati: ≤5 min master pull + 30s push).
- **🚫 Master-INACTIVE sa TRANSFER/SELL pickers (2026-09-16, live):** ang **53 products** na inactive sa master (may stock pa — 555 SARDINES 74, TATTOOS CHEDDAR 25, Absolute 8L 24, MALING CLM 12…) ay HINDI na lumalabas sa whmobile **TRANSFER** + **SELL** search at dashboard **STORE TRANSFER** dropdown. Bagong optional `excludeMasterInactive` param (default OFF) sa `/dashboard/warehouse/products?source=hq` at `/dashboard/stock-status` — predicate `bool_or(mp.is_active)` by barcode: walang master (legacy) = visible, may active = visible, **puro inactive = hidden**. HINDI ginalaw: mobile INVENTORY/PRODUCTS/RECEIVING, dashboard VIEW STOCK/TOTAL, POS, data/reports. API **1.1.76** · whmobile `WEB_VER 20260916-1`.
- **📱 STORE-AWARE MOBILE POS (2026-09-19, API 1.1.78 · POS v1.1.83):** isang "Jumong Pos" app — store/user ang difference (login store picker). **DEFAULT STORE (`WEB_VER 20260919-6`):** ang huling piniling store (`wh_last_store` + `wh_session`) ang default sa login/reopen — hindi na bumabalik sa WAREHOUSE/HQ; palitan sa burger → Store switcher. whmobile: SELL/INVENTORY/TRANSFER search per selected store (`source=store&storeId=`), TRANSFER destinations kasama ang **HQ** kapag store ang source, END SHIFT per store (may denominations/preview/print), per-store sales/shift lists; HQ-only menu (Online Orders/Receiving/Credit Billing/**Chat**) nakatago sa store mode. **Points:** QR customers lang ang kumikita (picker = ⭐ QR default, may "Lahat" toggle para sa Credit); POS rule (ppu×qty o subtotal/PointsRate ₱200). **Store isolation:** TRANSFER list may `?storeId=` filter + ⬅IN/➡OUT; VOID/EDIT sale store-source fix (tamang store stock + delta, hindi na wh_products). **Dashboard:** **📱 Mobile POS** panel (Reports) — tabs **SALES | END SHIFT** per store (HQ/U Got) + item detail + CSV. **Compact UI (`WEB_VER 20260919-2/4`):** standard mobile sizes (13px root, rem-based — Display Size selector scales lahat) + **SETUP → 📱 Display Size** (Maliit/Standard/Malaki) + 560px centered container; App Info may **Web: <ver>**. **50mm print fix (`WEB_VER 20260919-3`):** lahat ng print builders W-aware (`PW()/fitW/wrapW/padLR/ctrW/asciiSafe`) — walang overflow sa 32/48 chars, walang `?` chars. **Standard 57mm NON-BOLD (`WEB_VER 20260919-7`):** `escWrap`/`ptPrint` — `ESC E 0` (bold off) per line via ESC/POS bytes (web-only, walang APK update); Font A 32 chars standard. **Cart buttons isang row (`WEB_VER 20260919-8`):** `.cart-actions` — SELL NOW | CLEAR | HOLD | HELD sa isang flex row. **DEVICE STORE LOCK (`WEB_VER 20260919-9`):** SETUP → 🔒 Store Lock — `wh_lock_store` (localStorage) — naka-lock ang device sa isang store (walang picker, hindi bumubukas ang switcher; UNLOCK sa SETUP). Soft lock (mawawala kapag na-clear ang app data). **STOCK RECEIVING store-aware (`WEB_VER 20260919-10`, API 1.1.79):** RECEIVING available sa lahat ng stores — Source dropdown = **suppliers** (`/dashboard/suppliers` + agent) + "Iba pa" (free text); `POST /warehouse/receivings` may `stockSource=store`+`storeId` (dagdag sa napiling store + delta); history per store (`?storeId=`). **Reporting tags + scope (`WEB_VER 20260919-12`, API 1.1.80):** `wh_walkin_sales.cashier_name` + `wh_transfers.created_by` — naka-tag lahat sa login user (sell/transfer/receiving) + dashboard Mobile POS SALES **Cashier** column; mobile TRANSFER list = **outgoing lang** (`?direction=out`); **WAREHOUSE wala na sa store choices**; **STORE-DEV-0001 (TEST) = admin lang**. **Transfer timestamps (`WEB_VER 20260919-13`, API 1.1.81):** `wh_transfers.received_at` (accept time) + `created_via` (mobile/dashboard) — dashboard STORE TRANSFER may **Accepted** + **Via** columns; **date filter fix** (`@date::date` cast — dating 500 kapag may date filter!) + **created OR received** match (lumalabas kahapon-created/ngayon-accepted). **U Got POS update pending:** kailangan nakasara ang app; exe ready sa `C:\JumongAPI\client\` + `C:\JumongAPI\wwwroot\updates\JumongPosV1.01_1183.exe` (agent `update` + `restart`).
- **📅 MOBILE POS daily view + 🛒 E-COMMERCE card (2026-09-21, API 1.1.82):** dashboard → Reports → **Mobile POS** — SALES at END SHIFT tabs may **TODAY | ALL | 📅 date picker** (default TODAY, flat list; ang shifts ay sumusunod na rin sa araw via bagong `from`/`to` sa `/warehouse/shifts` → `close_date`). Bagong **E-COMMERCE (HQ ONLINE)** card sa SALES tab — delivered online orders lang (`delivered_at` basehan, tulad ng HQ Outflow) per araw, may total/CSV at clickable item detail. **HQ-only channel ito** (walang `store_id` ang `online_orders`; HQ stock + HQ driver) kaya hiwalay sa U Got/HQ mobile POS cards — hindi maa-attribute sa U Got. Endpoint: `GET /dashboard/ecom-sales?from=&to=&limit=500` (window totals = buong range). Dashboard cache-buster `v=20260921a`.
- **📤 HQ OUTFLOW panel (2026-09-16, live):** dashboard → POS CLIENT → Reports → **HQ Outflow** — per-day e-com delivered sales/holds + transfers out (per-destination chips) para sa HQ lang; may range (Today/7D/30D/Month) + CSV. Endpoint: `GET /dashboard/hq-outflow?from=&to=`. **INVENTORY COST REPORT** may bagong **Transfer Out / E-com Hold / True Var** columns (True Var = variance + transfers + e-com = tunay na shrink lang). **📜 Driver end-shift history:** driver app **v2.0.9** (native, build sa server) may HISTORY screen — per-day remittance; dashboard REMITTANCE panel may **DRIVER END-SHIFT HISTORY** table (`/remittances.shiftHistory`). POS `EndShiftForm` may on-screen STOCK OUTFLOW card na (out\client pa lang — hindi pa naka-release). **GOTCHA:** `JumongCloudAPI/Controllers/DashboardController.cs` ay **66MB** (corrupted old comment lines) — i-edit gamit ang edit tool, **HUWAG** `Get-Content -Raw` + `WriteAllText` (nag-mo-mojibake → 765MB → `CS8103` build error).
- **E-COMMERCE IN-APP CHAT (live, WALANG AI — real person):** customer ↔ shop app `💬` bubble (member only) ↔ **dashboard sidebar 💬 Customer Chat** (admin) ↔ **HQ POS `💬 CHAT` button** (cashier on duty, HQ store lang; cashier name naka-save sa `reply_by`, HINDI ipinapakita sa customer). Tables: `shop_chat_conversations` + `shop_chat_messages` (seen_by_admin/customer, reply_by). Conv card: 📞 phone + 🏘️ default subdivision (block/lot) mula sa customers + customer_addresses.
- **Dashboard ➖ DEDUCT STOCK tool** (sidebar POS CLIENT group): per-store stock deduction via agents na may remark (`Adjustment: <remark>` trail, UserName 'Dashboard'); 📦 live stock chips per store; 🕘 HISTORY panel (GET /adjust-log — cloud stock_trails `Adjustment:%`).
- **🏦 CHECKS app (EastWest + RCBC):** dashboard POS CLIENT → Checks — record (bank dropdown, check no, DUE date, payee=Suppliers, amount, auto words) + print sa **HP Smart Tank** via server (`/api/checks`, `CheckPrintService`); **per-bank** `check_template` calibration (⚙️ Print Position Settings + TEST PRINT) + **record block sa ibaba ng check** (date created/bank/check no/payee/amount/words/due date/agent/contact + signature) sa visible bond paper ng carrier; tables `checks` + `check_template`; GOTCHA: maliit na check (6.25×2.75") = PaperOut sa HP → **A4 carrier sheet** (check taped flush top-left, paper size A4).
- **Dashboard 🛰 MONITOR = HINDI PA TAPOS** (plan lang: app_events table + e-commerce health alerts).
- Home ng shop = FEED POSTS lang (feed_posts table; dashboard: Feed Posts panel). Wala nang promo banners/FB posts/promo groups UI.
- STOCK LINK model: linked child product = stock/cost nasa PARENT (x link_ratio); child stock laging 0; cost lock = parent cost x ratio; price = libre. **GOTCHA:** kapag nag-link ng BOX matapos ang pack conversion, ang ratio ay nasa PACK units (hal. Birch 730 = 20 by-8 sleeves/box, HINDI 160 sachets).
- Loyalty points: QR/online-registered customers (may qr_code) lang ang kumikita; POS awards push agad sa cloud (PointsDirty flag); resibo may Previous/+Earned/New.
- E-commerce: kailangan Google login para makita ang presyo at mag-order; COD lang; stock HELD sa HQ simula SUBMIT ng order; cancel = release.
- Sari-sari tier = REMOVED. Messenger bot = backup lang.
- Pack conversions done (single pack = 1 punch): MILO CHOCO 24G by-12 @115, BEAR BRAND 33G by-8 @87, Kopiko twins by-10/by-5 units, **BIRCH TREE MILK FORTIFIED 33G by-8 @75/71.5** (id 51).
- Warehouse (wh_products) RETIRED - mobile app (whmobile/whapp) at e-commerce ay sa HQ server products na lahat.
- HQ POS local stock = mirror ng server via 10s stock-pull; lahat ng mobile/ecom deltas naa-apply sa local.
- HQ CloudApiUrl = LAN http://DESKTOP-I097OO9:5000/api | ibang stores = https://admin.jumongdev.com/api
- **Transfer receive STOCK-SAFETY (v1.1.78):** POS client ay HINDI na nag-local-receive ng server `Shortages` items (kulang ang HQ stock) — PHANTOM stock na dati (Nescafe #981, Redhorse #1007). Kapag may shortage: mananatiling 'partial' ang transfer, malinaw na mensahe.
- **Shop print flyer:** `https://admin.jumongdev.com/shop-qr-flyer.html` (Legal 8.5x13, 6 cards: QR `assets/shop_qr_1000.png` → shop.jumongdev.com).

---
## Reference

## Project Structure
```
C:\dev\JumongPosV1.01\          # DEV PC repo (primary). Server keeps a read-only clone at C:\Users\ADMIN\Desktop\JumongPosV1.01
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
│   ├── AppVersion.cs           # Current = "1.1.82"
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
└── (walang publish/ folder sa dev repo — latest client build ay C:\dev\out\client\, store drop ay C:\JumongAPI\client\ sa server)
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

**PRIMARY (from the DEV PC via WinRM — the dev PC is the deploy driver):**
```powershell
Set-Location C:\dev\JumongPosV1.01
dotnet publish JumongCloudAPI\JumongCloudAPI.csproj -c Release -r win-x64 --self-contained true
$s = New-PSSession -ComputerName DESKTOP-I097OO9 -Credential DESKTOP-I097OO9\remotedev
Copy-Item -ToSession $s -Path 'JumongCloudAPI\bin\Release\net8.0\win-x64\publish\*' -Destination 'C:\JumongAPI\' -Recurse
Invoke-Command -Session $s -ScriptBlock { net stop JumongCloudAPI; net start JumongCloudAPI }
Remove-PSSession $s
```

**FALLBACK (on the server itself):** `C:\Users\ADMIN\Desktop\deploy_api.bat` — double-click and select **Run as administrator**. It will:
1. Stop the NSSM service `JumongCloudAPI`
2. Copy all publish files from `C:\Users\ADMIN\Desktop\JumongPosV1.01\JumongCloudAPI\bin\Release\net8.0\win-x64\publish\*` to `C:\JumongAPI\`
3. Restart the service

The batch file lives on the Desktop so it's easy to find. It must always be run **as administrator** (right-click → Run as administrator). Also available: `deploy_web.bat` (wwwroot) and `deploy_client.bat` (client drop) on the server Desktop.

## Local Server Infrastructure

### Machine Roles (IMPORTANT — WHERE IS WHAT)
| Machine | Role | What lives there |
|---|---|---|
| `DESKTOP-I097OO9` @ `192.168.1.21` (Ethernet, 1 Gbps) + `192.168.1.41` (Wi-Fi) | **SERVER ONLY** (Cloud API host, no dev) | API service at `C:\JumongAPI\` (+ client drop `C:\JumongAPI\client\`), Cloudflare tunnel, PostgreSQL, Cloudflare config. Repo clone kept at `C:\Users\ADMIN\Desktop\JumongPosV1.01` (read-only reference — NO dev work here anymore) |
| `DESKTOP-Q36S34R` (DHCP — was `192.168.1.55`, now `192.168.1.35` as of 2026-08-12) | **DEV PC (all development happens here)** | Cloned repo at **`C:\dev\JumongPosV1.01`**, non-git assets at `C:\dev\extras\`, client publish output `C:\dev\out\client`, Gradle at `C:\dev\gradle\gradle-8.14.3`, dev DB `C:\dev\JumongPosV1.01\JumongPos.db` (STORE-DEV-0001) |
| `DESKTOP-UU8E0D4` @ `192.168.1.25` (verified 2026-08-15; was .26) | **HQ store (Andengs Superstore - HQ)** | POS client at **`C:\Users\ADMIN\Desktop\JumongPosHW\`** ← NOT in `C:\JumongAPI\client\` |
| `DESKTOP-U5BO3Q0` @ `192.168.1.100` | HVR store (moved to new PC 2026-08-19; was DESKTOP-TK63MO6 @ 192.168.1.15) | POS client at **`C:\Users\ADMIN\Desktop\HVR_POS\`** (verified 2026-09-05), agent on `DESKTOP-U5BO3Q0`; inbound RDP/WinRM/ICMP BLOCKED (lanfix not yet done); sleep = never |
| `DESKTOP-NISQ3Q7` @ `192.168.1.152` | U Got Minimart - Naic | POS client at **`C:\JumongPos\`** (verified 2026-09-05), agent at `C:\JumongPos\agent\` (LUMANG agent build — walang per-heartbeat version refresh; i-restart para mag-update ang appVersion) |
| `DESKTOP-TK63MO6` @ `192.168.0.103` | ACGS - Naic Market | POS client at `C:\JumongPos\` (verified 2026-08-12) |

> **GOTCHA:** `C:\JumongAPI\client\` is where the **newest client build gets published** on the dev/API host — it is NOT the running install on the HQ machine. The real HQ POS runs from `C:\Users\ADMIN\Desktop\JumongPosHW\` on the HQ machine. When diagnosing/fixing a store, always target the correct machine via the Agent (see Agent section), not the local `C:\JumongAPI\client\` folder.
>
> **IPs are DHCP-assigned and change** (dev PC went .55→.35, store IPs moved too). ALWAYS use **computer names** (`DESKTOP-Q36S34R`, `DESKTOP-I097OO9`, etc.) for WinRM/network targets — DNS resolves names to current IPs automatically, so name-based commands survive DHCP changes. IPs in the tables above are informational snapshots only.

| Component | Path / Detail |
|---|---|
| API executable | `C:\JumongAPI\JumongCloudAPI.exe` |
| API output folder | `C:\JumongAPI\` (bin, wwwroot, config files) |
| Client build output (DEV PC) | `C:\dev\out\client\JumongPosV1.01.exe` |
| Client publish DROP (server, for stores' UPDATE APP) | `C:\JumongAPI\client\JumongPosV1.01.exe` |
| HQ POS client (store machine) | `C:\Users\ADMIN\Desktop\JumongPosHW\JumongPosV1.01.exe` |
| API port | `http://localhost:5000` |
| LAN access | `http://DESKTOP-I097OO9:5000` (use name, not IP — DHCP may change it) |
| Service name | `JumongCloudAPI` (NSSM, Automatic start) |
| Restart command | `Restart-Service JumongCloudAPI` |

### WinRM Remote Access (server ⇄ dev PC + HQ, added 2026-08-11 / HQ 2026-08-15)
Both machines can remote into each other over WinRM (LAN only). **The dev PC is the deploy driver** — it pushes builds to the server and restarts the service. **HQ is now also WinRM-reachable from the dev PC** (2026-08-15, agent-assisted setup — see note below).

| Item | Detail |
|---|---|
| Dev PC → Server account | `DESKTOP-I097OO9\remotedev` / `Jum0ng!Dev55` (admin) |
| Server → Dev PC account | `DESKTOP-Q36S34R\serverdev` / `Jum0ng!Dev55` (admin) |
| Dev PC → **HQ** account | `DESKTOP-UU8E0D4\remotedev` / `Jum0ng!Dev55` (admin, created 2026-08-15) |
| Server TrustedHosts (as client) | `DESKTOP-Q36S34R` (names only — no IPs; DHCP changes don't break TrustedHosts) |
| Dev PC TrustedHosts (as client) | `DESKTOP-I097OO9, DESKTOP-UU8E0D4` (names only) |
| Ports | WinRM 5985 both machines + HQ, ICMP enabled |
| Server Ethernet | **1 Gbps full duplex** (cable fixed 2026-08-11; was 10 Mbps) |

> **HQ WinRM setup (2026-08-15, via agent + one UAC click):** HQ's firewall blocked ALL inbound (no WinRM/SMB/RDP; UAC enabled → the agent's PowerShell runs with a FILTERED token, so even `netsh`/`net user`/`schtasks /rl highest` fail silently with "Access is denied"). Fix applied by (1) writing `lanfix.ps1` to HQ via agent `writefile`, (2) agent `ps`: `Start-Process powershell -Verb RunAs` → staff clicked Yes on the UAC dialog once → script ran elevated: `winrm quickconfig` + `Enable-PSRemoting -Force -SkipNetworkProfileCheck`, firewall rules `WinRM HTTP LAN` (TCP 5985, any profile) + `ICMPv4 Ping LAN`, created `remotedev` admin user, `LocalAccountTokenFilterPolicy=1`. Temp files deleted after. **Verified from dev PC:** `New-PSSession -ComputerName DESKTOP-UU8E0D4 -Credential DESKTOP-UU8E0D4\remotedev` → OK (host/agent/POS exe confirmed). ALSO verified: **HQ → server LAN `DESKTOP-I097OO9:5000` = reachable** (the API is on the LAN; only HQ's own inbound was blocked). Note: on THIS dev PC the WSMan client `TrustedHosts` edit must be done via the dev PC agent (runs as SYSTEM) — a plain non-elevated shell gets "Access is denied".
>
> **HQ → server LAN API switch (2026-08-15, COMPLETE):** HQ POS `CloudApiUrl` = **`http://DESKTOP-I097OO9:5000/api`** (LAN, no internet/Cloudflare hop) — owner's plan: HQ hosts ALL stock (retail + wholesale + e-commerce) in one DB; warehouse to be retired. The POS reads the setting per sync call, so the flip is zero-downtime (verified: SyncLog all-OK after flip). **Phase 2 done 2026-08-15 20:31** — HQ updated to v1.1.42 via GitHub release (pos-status now posts → dashboard sync chip live) and the agent was restarted so it also uses the LAN URL (it caches at startup). **GitHub release v1.1.42 created 2026-08-15** with the exe asset (the release had been created WITHOUT the asset earlier → stores got "DOWNLOAD FAILED" 404s until the 211 MB upload finished via curl). Rollback anytime: `UPDATE Settings SET Value='https://admin.jumongdev.com/api' WHERE Key='CloudApiUrl'` + restart. The startup URL-fix migrations only rewrite `%railway%`/`%digitalocean%` values → the LAN URL survives restarts. No auto-failover: if the LAN drops, HQ sync pauses (POS keeps selling offline, auto-drains on reconnect) — same as an internet outage. Store rollout 2026-08-15: HQ 1.1.42 ✅, ACGS 1.1.42 ✅ (tapped UPDATE APP), HVR 1.1.42 ✅ (remote exe swap; version/chip show after next cashier login), Naic still 1.1.38 (PC off — UPDATE APP when back online). *(Historical — current store versions nasa Quick Status sa itaas.)*

```powershell
# From the DEV PC -> server (standard pattern for all deploys)
$s = New-PSSession -ComputerName DESKTOP-I097OO9 -Credential DESKTOP-I097OO9\remotedev
Invoke-Command -Session $s -ScriptBlock { "OK on $env:COMPUTERNAME as $(whoami)" }
Copy-Item -ToSession $s -Path '...\publish\*' -Destination 'C:\JumongAPI\' -Recurse
Remove-PSSession $s

# From the SERVER -> dev PC (diagnostics on the dev PC)
$s = New-PSSession -ComputerName DESKTOP-Q36S34R -Credential DESKTOP-Q36S34R\serverdev
Invoke-Command -Session $s -ScriptBlock { "OK on $env:COMPUTERNAME" }
```

### Agent (remote diagnostic) version gotcha — FIXED 2026-08-15
- **Old behavior:** the Agent read `AppVersion` (and `CloudApiUrl`) **once at startup** and cached them — after a POS app update the heartbeat kept reporting the OLD version until the agent was restarted.
- **Fix (agent.zip rebuilt 2026-08-15):** the agent now re-reads `AppVersion` + `CloudApiUrl` from the DB on **every heartbeat (3s)** — version and API URL changes self-correct within seconds, no restarts needed anywhere. Deploy = update `agent.zip` on the server + per-store download→kill→expand→start (already done for HQ/HVR/ACGS; Naic pending — PC off; dev PC agent still old build until next reboot — it runs as a SYSTEM task that can't be replaced from a non-elevated shell).
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
- **Local URL:** https://admin.jumongdev.com/api (via Cloudflare Tunnel) — HVR, Naic, ACGS on this; **HQ uses `http://DESKTOP-I097OO9:5000/api` (LAN, since 2026-08-15)**
- **DB connection:** `DATABASE_URL` env var (PostgreSQL, default `localhost:5432`), or check `Data/CloudDatabaseHelper.cs` (client-side helper)

## Stores (in Cloud / Local PG)
| Store ID | Name | Machine | IP |
|---|---|---|---|
| `STORE-20260602-7159` | Andengs Superstore - HQ | DESKTOP-UU8E0D4 | 192.168.1.25 |
| `STORE-20260602-AA36` | Andengs Superstore - HVR | DESKTOP-U5BO3Q0 | 192.168.1.100 |
| `STORE-20260622-E174` | U Got Minimart - Naic | DESKTOP-NISQ3Q7 | 192.168.1.152 |
| `STORE-20260626-A80C` | ACGS - Naic Market | DESKTOP-TK63MO6 | 192.168.0.103 |
| `STORE-DEV-0001` | DEV - Local Testing | — | — |
| `STORE-WAREHOUSE` | Warehouse (whapp) | — | — |


## Current App Behavior

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

### Client App (run on the DEV PC, `C:\dev\JumongPosV1.01`)
```powershell
# Build
dotnet publish -c Release -r win-x64 --self-contained true

# Publish new release to C:\dev\out\client
dotnet publish -c Release -r win-x64 --self-contained true -o C:\dev\out\client

# Deploy the client drop to the server (then stores update via UPDATE APP)
$s = New-PSSession -ComputerName DESKTOP-I097OO9 -Credential DESKTOP-I097OO9\remotedev
Copy-Item -ToSession $s -Path 'C:\dev\out\client\*' -Destination 'C:\JumongAPI\client\' -Recurse
Remove-PSSession $s
```

### API URL Change
`CloudApiUrl` lives in each machine's SQLite `Settings` (Settings → CLOUD SYNC). All stores: `https://admin.jumongdev.com/api` (internet) — **except HQ: `http://DESKTOP-I097OO9:5000/api` (LAN, since 2026-08-15)**. The startup migrations only rewrite stale `railway`/`digitalocean` values — LAN URLs survive restarts. Rollback = change the value back + restart the POS (the agent caches the URL at startup).

### Cloud API
```powershell
# Build (on the DEV PC)
dotnet publish JumongCloudAPI\JumongCloudAPI.csproj -c Release -r win-x64 --self-contained true

# Deploy to local server (via WinRM)
$s = New-PSSession -ComputerName DESKTOP-I097OO9 -Credential DESKTOP-I097OO9\remotedev
Copy-Item -ToSession $s -Path 'JumongCloudAPI\bin\Release\net8.0\win-x64\publish\*' -Destination 'C:\JumongAPI\' -Recurse
Invoke-Command -Session $s -ScriptBlock { net stop JumongCloudAPI; net start JumongCloudAPI }
Remove-PSSession $s
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
| **POS CLIENT** | `Forms/`, `Services/`, `Models/`, `Data/DatabaseHelper.cs` | `dotnet publish -c Release -r win-x64 --self-contained true -o C:\dev\out\client` → WinRM push to `C:\JumongAPI\client\` |
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


## Remote Diagnostic Agent (`tools/Agent/`)

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
Compress-Archive -Force -Path "bin\Release\net8.0-windows\win-x64\publish\*" -DestinationPath "$env:TEMP\agent.zip"
# push to the server via WinRM (from the dev PC)
$s = New-PSSession -ComputerName DESKTOP-I097OO9 -Credential DESKTOP-I097OO9\remotedev
Copy-Item -ToSession $s -Path "$env:TEMP\agent.zip" -Destination 'C:\JumongAPI\wwwroot\agent.zip'
Remove-PSSession $s
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
- **Dev PC agent = SYSTEM scheduled task (NOT HKCU Run):** the dev PC (`DESKTOP-Q36S34R`, `STORE-DEV-0001`) agent runs as the `JumongPosAgent` scheduled task created with `schtasks /create /tn JumongPosAgent /tr "C:\dev\JumongPosV1.01\Agent\Agent.exe" /sc ONSTART /ru SYSTEM /rl HIGHEST /f` — boots without logon and has full admin power (needed because the server's WinRM token into the dev PC is filtered/non-admin). Only reachable when the dev PC is on. On the dashboard it shows `v?` OUTDATED — cosmetic only (no POS exe next to the agent to read the version from).
- **Agent error badge gotcha (fixed v1.1.40 agent):** `hasError` used to compare `SyncLog.CreatedAt` (local time) against `datetime('now','-1 hour')` (UTC) — PH is UTC+8, so old failures kept the red ERROR badge lit for up to ~9 hours. Fixed to `datetime('now','localtime','-1 hour')`; `errorSummary` now shows only entries from the last 2 hours (timestamped headers). Stores need the new agent.zip deployed to clear stale badges.


## Warehouse Mobile App (Android, WarehouseApp/)

WebView wrapper app that loads https://admin.jumongdev.com/whmobile.html (login via whapp API, SELL/INVENTORY/SETUP tabs, Bluetooth thermal printing, in-app update). Source: Kotlin, no gradle wrapper — use system Gradle 8.14.3 from C:\dev\gradle\gradle-8.14.3\ with Android Studio JBR 21.

> **NOTE (2026-08-11):** APK builds still run on the **SERVER** (`C:\Users\ADMIN\Desktop\JumongPosV1.01\WarehouseApp`) until Android Studio + Android SDK are installed on the dev PC. The Gradle zip + keystores are already on the dev PC (`C:\dev\gradle\`, `C:\dev\JumongPosV1.01\WarehouseApp\*.keystore`). When the dev PC is APK-ready: `sdk.dir=C:/Users/<you>/AppData/Local/Android/Sdk` (forward slashes) in `local.properties`, `JAVA_HOME=C:\Program Files\Android\Android Studio\jbr`, and use `C:\dev\gradle\gradle-8.14.3\bin\gradle.bat`.

### Build & Sign
```powershell
Set-Location "C:\Users\ADMIN\Desktop\JumongPosV1.01\WarehouseApp"   # SERVER (until dev PC has Android Studio)
# local.properties must use FORWARD SLASHES (backslashes = invalid path in Java properties):
#   sdk.dir=C:/Users/ADMIN/AppData/Local/Android/Sdk
$env:JAVA_HOME = "C:\Program Files\Android\Android Studio\jbr"
& "C:\Users\ADMIN\.gradle\wrapper\dists\gradle-8.14.3-bin\cv11ve7ro1n3o1j4so8xd9n66\gradle-8.14.3\bin\gradle.bat" :app:assembleRelease --no-daemon
# Sign (keystore: jumong_sign.keystore, alias jumong, pass jumong2026)
& "C:\Users\ADMIN\AppData\Local\Android\Sdk\build-tools\37.0.0\apksigner.bat" sign --ks jumong_sign.keystore --ks-key-alias jumong --ks-pass "pass:jumong2026" --key-pass "pass:jumong2026" --out JumongWarehouse.apk app\build\outputs\apk\release\app-release-unsigned.apk
```

Copy JumongWarehouse.apk to JumongCloudAPI\wwwroot\updates\ AND JumongCloudAPI\bin\Release\net8.0\win-x64\publish\wwwroot\updates\. Bump warehouse-version.json (version + changelog). Old warehouse.keystore password lost — v1.0.4 uses the NEW jumong_sign.keystore cert, so existing installs MUST uninstall first (changelog says so). Gradle OOM risk on this PC: only ~2.8GB free RAM; heap capped at 1536m in gradle.properties.


> Detailed change history -> CHANGELOG.md
