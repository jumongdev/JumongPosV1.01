# JumongPOS — Full Project Guide for AI Agents

## Latest Change (2026-08-25) — v1.1.59 (API) + web: MODERN MOBILE SHOP — AI chat REMOVED, Home/Catalog split, bottom nav, RESTOCK REQUESTS + PRODUCT SUGGESTIONS

**Request sequence:** (1) "lahat tapos pa alis muna na ai messenger" — remove the shop AI chat widget + full catalog modernization (skeleton/sort/cart-bar/bottom-nav/etc.); (2) nav tabs → **Home / Catalog / Cart / History / Account**; (3) "sa home mga promo at banners lang dapat, sa catalog nalang nakikita mga items" — Home view = carousel/promos only, items ONLY in Catalog; (4) **restock trigger** for out-of-stock items (customer notifies warehouse → admin sees it) + **product suggestions** (customer asks to add a product to the catalog).

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/shop.html` | **AI CHAT WIDGET REMOVED** (bubble + panel + all chat JS incl. `sendChat`/`chatHistory`/typing; `escH()` was chat-local but used by 9 other places — RESTORED as general helper; Escape handler + visualViewport block cleaned). **HOME/CATALOG SPLIT**: new `#homeView` (carousel + landingExtras — promos/banners only) + `#catalogView` (`hidden lg:block` — guestShop lock + memberShop grid); `switchView()` toggles per bottom-nav tap (desktop shows both stacked); `applyMemberUI` no longer hides landingExtras for members; footer links → `scrollToShop()`. **BOTTOM NAV 5 tabs** (Home/Catalog/Cart/History/Account, `z-40` — BELOW all overlays; was z-75 covering the cart drawer/checkout). **FLOATING CART BAR** (`View Cart ₱total · CHECKOUT →`, above nav, `updateCartBar()` overlay-aware — hidden when cart/checkout/success open; called from closeCart/closeCheckout/closeSuccess/updateBadge/applyMemberUI). **ORDER HISTORY MODAL** `#historyModal` (History tab; `loadOrders()` dual-renders orderList + historyList; `trackLastOrder` → `openHistory`). **SORT SHEET** (Featured/Price↑↓/A-Z/In-stock, `applySort` in visibleProducts). **SEARCH recents** (localStorage chips + clear ✕). **CARD POLISH**: violet price, `SAVE ₱X` badge + strikethrough when `onlinePrice < base` (regular members only, default-unit basis), circular **+ button on image corner**, 🔔 button on sold-out cards, press effect. **PRICING FIX**: new `unitPrice(p,u)`/`onlineSavings(p,u)` — client now actually charges/displays `online_price` for regular members (was bypassed — only `u.price` used everywhere: grid/detail/suggestions/addToCart/cart rows). **SARI-SARI FRESHNESS**: `openDetail` now fetches the product FIRST and updates `isSariSari` from the response BEFORE computing usable units — a just-approved sari-sari store sees ALL units on the next detail open (no page refresh). **RESTOCK**: 🔔 NOTIFY ME WHEN BACK IN STOCK (grid corner button + detail full button) → `requestRestock(id)` → POST. **SUGGEST**: 💡 button (grid header) → `#suggestModal` (name/brand/note) → `submitSuggestion()`. |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: **`restock_requests`** (id, product_id, product_name, customer_id, customer_name, status pending/fulfilled/dismissed, created_at, resolved_at + status/product idx) + **`product_suggestions`** (id, customer_id, customer_name, name, brand, note, status pending/approved/dismissed, created_at, reviewed_at + status idx). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **8 new endpoints**: `POST /customer/restock-request` (member-only, dedupe: no pending request for same product+customer), `GET /restock-requests?status=`, `POST /restock-requests/{id}/resolve` (fulfilled/dismissed), `GET /restock-requests/pending-count`; `POST /customer/product-suggestion` (member, name 2-200 chars), `GET /product-suggestions?status=`, `POST /product-suggestions/{id}/review` (approve/dismiss), `GET /product-suggestions/pending-count`. DTOs: RestockRequestDto/RestockResolveDto/ProductSuggestionDto/SuggestionReviewDto. Version → `"1.1.59"` (2 places; latestVer stays 1.1.58 — no POS client release). |
| `JumongCloudAPI/wwwroot/index.html` + `components.js` | Sidebar grp-ecom: **🚨 Restock Requests** + **💡 Product Suggestions** (red `_restockBadge`/`_suggBadge`); new `restockPanel` (PENDING/DONE/SKIP/ALL pills + ✔ DONE / ✕ per row) + `productSuggestPanel` (PENDING/APPROVED/SKIP/ALL + ✔ APPROVE / ✕); badge spans in the nav template extended. |

**Verified live:** API v1.1.59 (tables `restock_requests`+`product_suggestions` exist, pending counts 0/0); shop live has homeView/catalogView/requestRestock/suggestModal; headless render = homeView visible (3 banner slides), catalogView hidden on mobile initial, chat bubble GONE, 5-tab nav; dashboard has both panels. All JS `node --check` OK, div balance 658/658 (index) + 166/166 (shop). **NOTES:** guests land on Home (banners); Catalog shows the login lock until member; the `online_price` pricing alignment means regular members may now see different (lower) prices than before where online_price is set — checkout total matches the displayed price. Git commit pending.

### Same-session follow-up: PULL-TO-REFRESH 1px BUG (auto-refreshing while scrolling) + SEARCH KEYBOARD TOGGLE

User: "why the app keep on refreshing even i am not refreshing" + "i just scrolling in the catalog all keep on refreshing" — **BUG:** the PTR `touchend` refresh condition was `ind.style.transform !== 'translate(-50%,-70px)'` — ANY positive dy (even 1-3px micro-movement at touchstart on scrollY=0 — happens on nearly every phone tap/scroll-start) moved the indicator → refreshShop() → skeleton flash + "Shop refreshed ✓" toast = constant fake refreshes. **FIX:** track `ptrLastY`; refresh ONLY when `pull >= PTR_TRIGGER` (80px, matching the green indicator threshold) + `touchcancel` resets state (shop.html). **SEARCH UX:** magnifier is now a clickable `#searchBtn` in `#searchWrap` — tap toggles keyboard (focused → blur + results stay; not focused → focus + recents show); tap OUTSIDE the search (document touchstart, `!wrap.contains`) blurs the input; Enter saves the recent + closes keyboard. Deployed live (verified searchBtnToggle/PTR_TRIGGER on live shop.html; headless render OK). Git commit pending.

### Same-session follow-up 2: SARI-SARI units in GRID (clickable, stock-checked) + SUGGESTION photos + 45s sari heartbeat

User: "sarisari store approve does not see those other price under unit" + "dapat sa grid view nkikita at pwede iclick tapos check kung sapat ang stock tapos sa you may also like wala photo" — (1) **PROVEN WORKING via real-browser CDP test** (session cookie `jshop` for customer 1481 Pretchie: `member=true, isSariSari=true`, GINEBRA 211 detail shows all 4 unit chips w/ prices) — the earlier report was a page OPEN before approval (stale in-memory flag). Added **45s sari-sari heartbeat**: re-checks `/customer/me`, re-renders grid/detail ONLY when the flag flips — approval now takes effect without reload. (2) **GRID unit chips**: sari-sari members see ALL units on each card (`pickGridUnit`/`gridUnitSel`, horizontal scroll, active chip highlighted); card price/soldOut/avail + `addToCart` now use the SELECTED unit (need = qtyPerUnit → stock sufficiency check per unit: box unit w/ insufficient stock → OUT OF STOCK overlay + 🔔). (3) **"You may also like" PHOTOS**: suggestion tiles now lazy-load real images (`img[data-lazy]` + mono behind, `applyImg` now querySelectorAll = updates ALL matches); eager-load on modal open (`#detailBody img[data-lazy]` forEach loadImg) because IO below-the-fold never fired inside the modal scroll; imageless products keep the letter tile. Test session/artifacts cleaned. Git commit pending.

### Same-session follow-up 3: GRID unit chips NO SCROLL (flex-wrap) + DETAIL unit click BUG FIXED (selectUnit never stuck)

User: "can you make pc 1 box 5 Box 10 Box dont scroll and also if click the product detail with you may also like the unit there cannot be click" — (1) **grid chips scroll removed**: `overflow-x-auto scrollbar-hide` → `flex-wrap` (all units visible, wrap to next line). (2) **REAL BUG FOUND via CDP interaction test**: clicking a DETAIL unit chip called `selectUnit` → set `detailUnit` → then `openDetail(openDetailId)` RE-RENDER **RESET `detailUnit` back to the default unit** (`detailUnit = usable.find(isDefault)...`) → the click visually did nothing (price/chip stayed on default) — this bug existed since the unit chips were introduced. **FIX**: openDetail preserves the current selection — `const keepU = detailUnit && usable.some(x => x.unitName === detailUnit.unitName) ? detailUnit : null; detailUnit = keepU || (usable.find(isDefault) || usable[0] || null);` (falls back to default only when opening a DIFFERENT product). **Verified via CDP clicks**: grid chip → `gridUnitSel="1 box"` + card price ₱1,620; detail chip "10 BOX" → `detailUnit="10 BOX 16100"` + price ₱16,100; suggestion tile navigation → new detail + chip click sticks (`box 3140`). Test sessions/artifacts cleaned. Git commit pending.



### Same-session follow-up 4: MOBILE BACK BUTTON (app-style overlay close) + STAY-ON-VIEW after refresh + SARI-SARI apply w/o DTI

User: (1) "can you make mobile back button usable like if click profile and use mobile back it closes the profile" — **History-API overlay stack** in shop.html: every overlay open (`openCart/openCheckout/openDetail/openProfile/openHistory/openSuggest/openSort/openAddrForm` + success modal) → `pushOverlay()` (`history.pushState` + `overlayStack`); every close fn → `popOverlay()` (pops stack + `history.back()` when `history.state`); `popstate` handler closes the TOPMOST open overlay (`overlayPriority` list) — Android/iOS back now closes overlays one at a time, exits only when nothing is open. (2) "if swipe refresh use stay on what page he is dont go home" — the NATIVE browser pull-to-refresh (our PTR can't preventDefault, passive) reloaded the page → init always ran `switchView('home')` → back to Home. **FIX**: `switchView` saves `shop_view` to sessionStorage; init restores `catalog` if saved — swipe-refresh now lands back on the SAME view. (3) **SARI-SARI APPLY w/o DTI**: apply box text + validation toast now say "DTI (kung meron) o larawan ng harap ng iyong tindahan" (shop.html); dashboard panel header → "DTI o store front photo", column → "DTI / PHOTO", link → "VIEW ↗" (index.html). Server unchanged (dti_file column + jpg/png/webp/pdf/5MB validation already accepts photos). Deployed live; node --check OK, div balance 168/168. Git commit pending.

### Same-session follow-up 5: DESKTOP SPLIT CATALOG (fixed Favorites + scrollable categories) + LAST-ORDER in Favorites + search recents REMOVED

User: "gawin yung catalog asa left side yung favorite naka fix tapos catalog scrollable up and down tapos pag pick ng catalog lalabas sa right side yung selected catalog items in single grid pero maximize ang space tapos paki alis yung recent search eating space tapos sa favorite naman yung content nyan yung last order ng customer asa taas lang yung mga favorite na click nya" — (1) **desktop sidebar split**: `#favBtn` (❤️ Favorites) FIXED at top (`shrink-0`) + `#sideCats` now `flex-1 overflow-y-auto` (categories scroll up/down independently); catalog container `max-w-6xl` → full-width (`px-4 lg:px-6`), grid columns → `lg:grid-cols-5 xl:grid-cols-6 2xl:grid-cols-7` (maximize space). (2) **Favorites view**: `#favHeader` renders the customer's LAST ORDER as a violet gradient card (orderNo/status/date/total + View all → openHistory) ABOVE the favorites grid; `setFavOnly` loads orders on first toggle + updates favBtn active classes; `loadOrders` re-renders grid when favOnly. (3) **search recents REMOVED** (`#searchRecents` + RECENT_KEY/showRecents/hideRecents/useRecent/saveRecent deleted — clear ✕ + keyboard toggle + outside-blur kept). Verified live + headless desktop render; div balance 171/171, node --check OK. Committed with the rest of the session.

### Same-session follow-up 6: RECEIPT QR NOW SCANNABLE (graphics, not text) + PAANO MAG-REGISTER instructions — client v1.1.59

User: "kaso yung barcode qrcode nilagayt mno d nila pa scan" + "termal printer gamit" + "tsaka lagyan mo instruction paano sila mag register" — the v1.1.54 ASCII half-block QR (`ShopQrAscii`, `█▀▀▀` text lines at Spacing 10) was NOT scannable: (a) half-block rendering via Courier New DrawString squished modules vertically (line pitch 2.65mm vs char width 1.9mm → ~0.7 aspect → broken QR), (b) font-rendered blocks blur on thermal. **FIX (PrinterService.cs):** replaced text-QR with a **GRAPHICS QR** — `ShopQrMatrix` (25x25 EC-M for `https://shop.jumongdev.com`, generated + **verified decoding via jsQR pixel render** = exact URL) + `DrawShopQr(g, x0, y0, printW)` draws black `FillRectangle`s per module (module = printW*0.85/29, quiet zone 2) inside the existing PrintDocument PrintPage loop (`LineEntry.IsQr` flag; `ExtendPaperIfNeeded` +30 lines when QR present). Crisp at printer DPI — phone-scannable. **REGISTER INSTRUCTIONS** added after the QR: "PAANO MAG-REGISTER: 1. I-scan ang QR o i-type ang shop.jumongdev.com 2. I-click ang SIGN IN WITH GOOGLE 3. Piliin ang iyong Gmail account 4. Punuin ang pangalan at mobile number 5. Mag-order na - Cash on Delivery ang bayad!" (live retail prints only, reprints unchanged). Client AppVersion → `"1.1.59"`; API `latestVer` → `"1.1.59"` (API version stays 1.1.59). Deployed: client publish (exe 211,424,700 B, sha256 D0BE422B…) → drop `C:\JumongAPI\client\`; **GitHub release v1.1.59** (id 376953339, asset id 530456774, raw `--data-binary`, MZ verified, size exact); API redeployed (1.1.59, latestVer 1.1.59). HQ/HVR get it via **UPDATE APP** — next retail receipt prints the scannable QR + instructions. Git commit pending.

### Same-session follow-up 7: WHMOBILE CREDIT PAY 400 FIX — validate against the LEDGER, not the stale column (EDITO LOBO 693)

User: "pa check ako ng mobile app yung credit ng customer d nag paid nag eerror" — crash_reports showed `credit-pay http 400: Amount exceeds balance (0.00)` for customer 693 (EDITO LOBO, ₱132,615 on WH-20260824-0000). **Root cause:** `WhCreditPay` validated `req.Amount > currentBalance` where `currentBalance` = the **`customers.credit_balance` COLUMN** (0.00) — but the mobile breakdown/billing show the **wholesale LEDGER** (billed wh_walkin_sales Credit − paid credit_transactions store_id='') = **135,745** → every wholesale payment 400'd once the column drifted (the column is owned by the POS retail credit sync and is routinely clobbered — DownloadCustomers never touches it, but SyncCustomer pushes local retail values over it). **FIX (DashboardController.cs `WhCreditPay`):** reads `ledgerBalance` via the same billed−paid subqueries as WhCreditBilling/WhCreditBreakdown, validates against it, and **stops writing the column** (removed the `UPDATE customers SET credit_balance` — wholesale ledger is the truth; column stays retail-owned). Response `balance` = ledger-based (voucher print unaffected). **Verified live:** POST credit-pay ₱1.00 → 200 `{id:7819, balance:135744.00}` (was 400 pre-fix); test txn 7819 DELETEd, ledger back to 135,745. API redeployed (1.1.59). Git commit pending.

### Same-session follow-up 8: WHMOBILE credit-pay "fit is not defined" — payment recorded but print crashed (Dave Terrenal 53)

User: "in credit i pay dave and there is error failed something is not defined" — crash_reports: `credit-pay exception: fit is not defined` for customer 53 (Dave Terrenal, WH-20260811-0009 ₱2,580 + WH-20260818-0010 ₱5,130, 15:53). **Root cause:** the payment SUCCEEDED server-side (txns 7821/7823 recorded), but the voucher PRINT inside `submitCreditPay` called `fit(...)` — `fit` was only declared as a `const` LOCAL to other print functions (lines ~2206/2383/2942/3232), NOT in the credit section scope → ReferenceError → the whole try-block catch showed "Failed: fit is not defined" → user thought the payment failed (money was fine). **FIX (whmobile.html):** (1) **global `const fit`** declared next to `paperWidth` (uses `paperWidth<=50?32:48` width — same formula as the local copies, which now shadow it harmlessly); (2) the print block wrapped in its OWN try/catch — a print failure now shows "Payment OK, pero hindi na-print ang voucher: ..." and NEVER breaks the payment flow/success toast. Deployed live (verified global fit + print try/catch); node --check OK. Git commit pending.

### Same-session follow-up 9: PRODUCT IMAGE PROTECTION — updates never wipe the picture unless explicitly removed

User: "dapat hindi nabubura mga yan kung d naman inaalis" (after my diagnostic test PUT overwrote product 1+2 images with test payloads — user's upload looked "not saving" because the dashboard table showed only IMG placeholders + shop images were broken by the IO bug, both since fixed). **FIX (DashboardController.cs `UpdateMasterProduct` + components.js `productEditor` + index.html editor):** an update with **empty/missing `imageData` now KEEPS the existing image** (the SET clause omits image_data unless a real image or `RemoveImage=true` is sent — same dynamic-SET pattern as PatchMasterProductFlags); `SeedProductDto` gains `bool? RemoveImage`; editor gains a **REMOVE IMAGE** button (`clearImage()` → `imageData=''` + `removeImage:true`; picking a new file resets `removeImage:false`). CREATE path unchanged. **Verified live:** PUT empty imageData on a product with image → image retained (len 118→118); PUT `removeImage:true` → cleared (len 0); scratch product deleted. API + web redeployed (1.1.59). Git commit pending.

### Same-session follow-up 10: MESSENGER BOT REVIVED — webhook dot-param FIX + fresh page token + app re-subscribed (Andeng Superstore page 203372639529959)

User: "problema d mo naman ma buhay" (Messenger bot dead). **Found 3 issues:** (1) **WEBHOOK BUG (root cause of "never worked")** — Meta sends DOT-named query params (`hub.mode`/`hub.verify_token`/`hub.challenge`) but the endpoint bound underscore-named action params (`hub_mode`) → verification ALWAYS failed in Meta UI → Meta never delivered messages. Fixed: read `Request.Query` directly with dot+underscore fallback (`MessengerWebhookVerify`); verified live `CHALLENGE_OK_123` echo (log: mode=subscribe tokenLen=21 ok=True). (2) **messenger_convos table** was missing (created via CREATE TABLE IF NOT EXISTS + unique psid index). (3) **page token DEAD** (Aug 21 Graph Explorer token expired; Graph `/me` errored no-pages-permission). **Revival:** user supplied a new token (user token) → derived the PAGE token via `/me/accounts` (Andeng Superstore, id 203372639529959, 264 chars) → saved to `messenger_bot` (enabled=true) → `POST /{page}/subscribed_apps?subscribed_fields=messages,messaging_postbacks` → `{"success":true}` (also `me/subscribed_apps` success) → dashboard `/messenger/test` → `{"ok":true,"detail":"Page token valid - Messenger API OK"}`. Bot now live: keywords shop/store/qr/menu/order → shop.jumongdev.com link + QR attachment; else AI reply via GetBotReplyAsync (llama + KB). **COMPLETED via API (no Meta UI needed):** user supplied App Secret → app token (client_credentials) → `POST /{app_id}/subscriptions?object=page&callback_url=...&verify_token=jumongbot_verify_2026&fields=messages,messaging_postbacks` → `{"success":true}` → GET subscriptions confirms **`active:true`** with both fields; `messenger_bot` now also stores `app_id`/`app_secret` columns (for future token refresh via fb_exchange_token). Page subscription (subscribed_apps) already active. **Bot FULLY LIVE — Meta will deliver page messages to the webhook.**

**Delivery VERIFIED END-TO-END (2026-08-26):** webhook log shows real `POST entry messaging=1` (22:19:45, tester Andrea De PSID 27205704152410631) → `ProcessMessengerMessage` ran → AI reply saved to `messenger_convos` + sent back. Bot now: receives ✓ replies ✓ sends link+QR ✓ saves history ✓. Also proven SEND via `POST /me/messages` (message_ids returned for Andrea + Caim Grocer PSIDs from `/conversations`). Dev-mode tester acceptance (App Roles → Testers, confirm URL `https://developers.facebook.com/apps/1041403252013127/roles/testers/confirm`) was the final gate for webhook delivery; roles API shows only admins (pending testers don't appear via API). **CATALOG CAROUSEL added** (user: "ang chatgenie may ecommerce ang messenger tayo ba?"): keywords `catalog/products/browse/listahan/produkto` → `SendCatalogCarousel` (Generic Template, up to 10 in-stock sell_online products w/ images, subtitle price+category, ORDER NOW web_url → shop.jumongdev.com + quick replies order/delivery/promo); new public `GET /product-image/{id}` serves master_products.image_data as image/jpeg (Cache-Control 1h) — the template image_url. Verified: `catalog carousel send=OK elements=10`. Git commit pending.

## Previous Change (2026-08-25) — v1.1.58 (POS client): VOID NOW REVERSES LOYALTY POINTS (Abigail +10 stayed after void)

**Request:** "may problema yan abigail para nag purchase sya sa HQ ng bearbrand box good nag points 10 kaso bumalik [pina void]. nag void sya bumalik item pero ang points nya nanatili andoon" — a voided HQ retail sale left the awarded points on the customer. Investigation: INV-7159-20260825-0084 (₱2,016 BEAR BRAND box, customer 1119/cloud 1454, voided 15:34) — customer got +10 (2016/200, int) but **`SaleService.VoidSale`/`VoidItem` reversed stock + credit only, NEVER points** (the credit-reversal block existed; points reversal did not). SECONDARY bug: `GetById`'s item mapper never read `PointsEarned` (default 0) → ang VOID re-sync (`SyncSale(updatedSale...)`) ay nag-clobber ng cloud `sale_items.points_earned` 10 → 0 (the original value WAS stored at sale time — `SaveSale` computes it per item, lines 77-87).

| File | Change |
|---|---|
| `Services/SaleService.cs` | **`GetById` item mapper** — now reads `PointsEarned` from SaleItems. **New `ReverseSalePoints(conn, trans, customerId, ptsAwarded)` helper** — QR-guarded (walang QR = hindi nag-earn), `LoyaltyPoints = MAX(0, cur - pts)`, same transaction. **`VoidSale`**: reverses `SUM(PointsEarned)` of the non-voided items before commit. **`VoidItem`**: reads the item's `PointsEarned` (`si.*` reader) + reverses it. Both rely on the existing post-void `SyncCustomer` (lines ~550/686) to push the corrected balance to the cloud. |
| `Services/AppVersion.cs` | `"1.1.57"` → `"1.1.58"`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | `latestVer` → `"1.1.58"`. |
| PostgreSQL (live) | `UPDATE customers SET loyalty_points=0 WHERE id=1454` (Abigail's 10 from the voided sale reversed); `UPDATE sale_items SET points_earned=10 WHERE sale_id=16866` (audit trail restored — the void re-sync had clobbered it to 0). HQ local self-corrects via the cloud customer download (LoyaltyPoints is synced from cloud) + a redundant agent `sql` UPDATE was sent (cmd 7). |

**Deployed:** client v1.1.58 (exe 211,420,604 B, sha256 BB749EBA…) → drop pushed to `C:\JumongAPI\client\`; API redeployed (1.1.51, latestVer 1.1.58); GitHub release v1.1.58 (id 376311727) created, exe asset uploading (raw `--data-binary`, NOT multipart). Stores fix via UPDATE APP. **Root-cause recap:** per-item `PointsEarned` was stored correctly at sale time; the void simply never deducted it (and the mapper bug hid the value from the re-sync). Git commit pending.

### Same-session follow-up: DRIVER-REQUIRED GUARD for SHIPPED/DELIVERED + the 5 orphaned unpaid orders assigned to ken

User: "bakit my mga unpaid pa sa ecommerce pero d nakikita sa driver app?" — the 5 delivered-unpaid orders (4, 7, 10, 11, 12) had `driver_id = NULL` (the driver app filters `WHERE driver_id = @d`). Answer: MARK DELIVERED sa dashboard ay walang driver requirement — staff can deliver without a driver. **Fixes:** (1) assigned ken (pos 16) to those 5 orders → now visible in the driver app (verified 10 orders: 5 TO COLLECT + 5 paid); (2) **`ShopUpdateOrderStatus` GUARD** — `shipped`/`delivered` now require `driver_id` else 400 `"Hindi maaaring i-ship/i-deliver ang order - mag-assign muna ng driver (🚚 DRIVER card)"` (verified live: ship attempt without driver → 400, order 12 temp-modified then restored); (3) **dashboard UI** — MARK SHIPPED/MARK DELIVERED buttons hidden when `!detail.driverId`, replaced with amber ⚠ "I-assign muna ang driver" hint (div balance 638/638). Also noted: 5 orders (1, 2, 3, 5, 13) became PAID — ang v2.0.2 multipart payment fix ay gumagana na end-to-end (points awarded on payment). API redeployed with the guard; web live.

### Same-session follow-up: DRIVER APP v2.0.3 — TO-COLLECT-only list + order DATE + auto-camera for GCASH

User: "pwede ba ang nakikita unpaid lang at lagyan sana ng date tapos pag qrcode ba ginamit na payment meron camera nalabas to picture the reference from customer?" — **DriverApp v2.0.3 (versionCode 5):** (1) deliveries list now shows **TO COLLECT (unpaid) only** by default with an amber **TO COLLECT** toggle chip in the header (tap → "ALL" to also see paid orders; `visOrders` filtered list + `rebindOrders()`); (2) each order row gained a **date line** (`rowDate`, UTC→Asia/Manila via SimpleDateFormat — the API returns UTC `createdAt`); (3) **GCASH/SPLIT auto-camera** — selecting GCash/Split in the pay screen auto-launches the camera (`postDelayed 600ms`, once, only when no proof picture yet) to photograph the customer's GCash reference. Built/signed on server (BUILD SUCCESSFUL 48s; first build failed — `setText` on `<View>` ref, fixed with `findViewById<TextView>`), deployed live + repo; driver-version.json → 2.0.3. Update via in-app UPDATE dialog (centered).

## Previous Change (2026-08-25) — DASHBOARD CUSTOMERS PANEL: points/QR column + per-customer ORDER HISTORY with points

**Request:** "web dashboard paki update ng customer panel kasi wala pa doon sino customer ang my start at viewing ng history ng order nya with points also" — ang Customers panel ay walang points indicator at walang view ng order history ng bawat customer.

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`GetCustomers`** — SELECT/return gains `COALESCE(c.qr_code,'') AS qr_code` (idx 10) + `c.id AS cid` (idx 11, cloud PK — needed because online_orders.customer_id = cloud id, NOT pos_id). **NEW `GET /customers/{id}/orders`** — admin per-customer order history: id, orderNo, status, total, paymentMethod, createdAt, paidStatus + `totalPoints` (same SUM CASE exempt/ppu/rate formula as CustomerOrders) + `awardPoints` (int floor), LIMIT 50, newest first. |
| `JumongCloudAPI/wwwroot/index.html` | Customers table: **Loyalty → ⭐ Points column** (amber bold when >0 + **⭐EARN** badge when `qrCode` present / ⛔ when none — points eligibility) + **📋 ORDERS** button per row (violet) + **ORDER HISTORY MODAL** (fixed overlay): order no/date/total/status badge/✅ PAID/⭐ `X.XX pts → +N` per order. GOTCHA hit: ang unang edit ng modal ay na-insert sa MALING section (isang generic `</div></div></div>` match sa bandang line 295) — na-fix sa pamamagitan ng pag-reseat sa loob ng customers section (anchor: `<!-- USERS SECTION -->`), div balance 637/637. |
| `JumongCloudAPI/wwwroot/components.js` | `customersList` — state `orders/ordersOpen/ordersName/ordersLoading` + `viewOrders(x)` (fetch `/customers/{id}/orders`) + `closeOrders()` + `statusCls()` (status badge colors). |

**Verified live:** API redeployed; `/customers` → 366 rows with `qrCode`+`id`; `/customers/1454/orders` (Abigail) → 2 orders each with `totalPoints` (e.g. 0.74 pts → +0); live index.html has ⭐EARN badge + ORDERS button + modal; components.js has viewOrders/statusCls. NOTE: `pos_id` ≠ cloud `id` — ang order history ay dapat i-fetch gamit ang cloud id (x.id), hindi pos_id. Git commit pending.

### Same-session follow-up: STAR CUSTOMER FILTER + MODAL SCOPE BUG FIX (the "order history not working" root cause)

User: "can you separate the viewing or put selector for star customer to without" + pasted the ⭐ WITH POINTS list (22 customers — verified EXACTLY all 22 Google-registered: 366 total, 22 qr_code, 22 google_sub, 0 google-without-qr). **Filter pills** (ALL / ⭐ WITH POINTS / ⛔ WITHOUT with counts) in the customers header via `ptsFilter` + `filtered`/`withStar`/`withoutStar` getters; **ECOM badge** replaces "Unknown" store for online customers (store_id=''). **CRITICAL BUG (user console: `Alpine Expression Error: ordersOpen is not defined`):** ang ORDER HISTORY MODAL ay na-insert sa tabi ng customersList card — SA LABAS ng `<div x-data="customersList">` scope → ang x-show/orders* ay niresolve sa global scope → modal NEVER showed = "order history is not working". **Fix:** `x-data="customersList"` moved from the card div to the SECTION wrapper (`x-show="section === 'customers'"`) — table + modal now share ONE instance (div balance 638/638). GOTCHA re-learned: ang pag-verify ng modal placement ay HINDI sapat na "x-data appears before modal sa file" — dapat nasa LOOB ng x-data element ang modal (x-for/x-show scope boundary = the x-data div itself).

## Previous Change (2026-08-25) — v1.1.51 (Cloud API) + web: DRIVER APP LOGIN FIX (invisible errors + silent bounce)

**Request:** "d naman maka login sa driver app... ni walang error, basta kahit click login 1million times walang mangyayari". Investigation: server 100% OK (ken/12345 login 200 + `/driver/orders` 200 with 5 orders via localhost AND public URL); live driver.html/driver-app.html identical; 56 crash_report logins all `status:200` with ZERO `err:` entries; driver tokens created repeatedly (93 rows — never trimmed) yet diag always showed `loginVisible:true, mainVisible:false` → silent bounce to login. Found bug cluster in the app's own error handling that MASKS the real cause.

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/driver.html` + `driver-app.html` (identical edits) | **BUG 1 — lgErr INVISIBLE FOREVER:** `<p id="lgErr" style="display:none;...">` — inline display:none beat every `classList.remove('hidden')` error path (doLogin/cancel/pay ×12 sites) → error messages NEVER rendered → "walang error na sinasabi". Fixed: inline display removed, `class="hidden"` instead. **BUG 2 — login screen PIN-LOCK:** `window.onerror` handler did `login.style.display='flex'` (inline) on ANY JS error → login screen stuck visible over everything (show() uses classes only); removed the forced display; error handler now just shows the message. **BUG 3 — show() hardening:** now sets inline `el.style.display = (target ? map[id] : 'none')` explicitly per screen (loginScreen=flex, others=block) — immune to class/inline conflicts. **BUG 4 — `.at()` polyfill** added in `<head>` BEFORE the Tailwind CDN (Android 11 WebViews throw `t.entries.at is not a function` from Tailwind internals — seen on OnePlus 8 Pro in crash_reports; pin-locked login via BUG 2 + invisible via BUG 1 = the "defective" experience). **401 → visible:** loadOrders/openDetail 401 now `setErr('⚠ Hindi na-validate ang session — mag-login ulit')` before logout (was silent bounce). **TELEMETRY:** `report()` extracted to global (was a local const in doLogin — loadOrders couldn't use it); loadOrders now reports `orders:<status>` per call → the NEXT attempt will prove whether `/driver/orders` 401s on the phone. **`unhandledrejection` capture** added to `_diag`. Version strings → `1.1.49`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **DriverLogin token trim** — driver login never trimmed `whapp_tokens` (93 rows for ken/pos 16 vs 5 for whapp/web users); now same keep-last-5 DELETE as WhappLogin. Version → `"1.1.51"` (2 places); latestVer stays 1.1.57 (no POS client release). |
| — | **Verified live:** API 1.1.51; admin.jumongdev.com/driver-app.html has v1.1.49 + lgErr class fix + polyfill + orders telemetry; login+orders still 200 post-deploy. **Next:** user reopens the app (APK reloads fresh via LOAD_NO_CACHE + `?v=` bust) — if still failing, the error is now VISIBLE on screen AND the `orders:` telemetry row identifies the exact status. |

**Root-cause recap:** the app's logins genuinely succeeded server-side the whole time (200s + tokens); the UI failed to reflect it because (a) any real error was invisible (lgErr inline-display bug) and (b) any error pinned the login screen via inline flex (error-handler bug) — with the silent 401→logout bounce as the only remaining invisible path, which the new telemetry now exposes.

### Same-session follow-up (v1.1.50 web + APK v2.0.0 NATIVE REWRITE): user chose "pure APK, hindi WebView" (payment-focused)

User (after still seeing "hindi ma-load ang app" banner + white screen on the phone despite server-verified fixes): **"gawa ka nalang ng pur apk not webview? total more on payment purposes lang ito"** — and asked about Tauri (answered: Tauri on Android still uses the system WebView + needs Rust toolchain; NOT the fix). Decision: **full native Kotlin rewrite of the driver app** — no WebView at all. Same package `com.jumong.driver` + same `jumong_sign.keystore` cert → installs as an in-place UPDATE via the existing update flow.

| File | Change |
|---|---|
| `DriverApp/app/src/main/java/com/jumong/driver/MainActivity.kt` | **FULL REWRITE — native (zero WebView):** `HttpURLConnection` + `org.json` (no new deps) + Thread/runOnUiThread. Login (`POST /driver/login`, SharedPreferences token), Deliveries list (ListView+BaseAdapter, TO COLLECT/PAID badges), Order detail (customer/address/items/total, PAID badge), 💵 COLLECT PAYMENT (CASH w/ change calc / GCASH w/ ref + camera proof via FileProvider cache + store GCash QRs from `/payment-qrs` / SPLIT) → multipart `POST /driver/orders/{id}/pay` (payments JSON + pic0/pic1), 📍 ARRIVED, ✖ CANCEL (reason modal), 🏠 RETURN TO HQ, logout, self-update (driver-version.json → download APK → REQUEST_INSTALL_PACKAGES install → ReopenReceiver auto-reopen). |
| `DriverApp/app/src/main/res/layout/activity_main.xml` (+`item_order.xml`, 11 drawables) | Dark #10102a + violet brand UI, 4 screens toggled by visibility + cancel overlay + update bar. |
| `DriverApp/app/build.gradle` | versionCode 2, versionName `"2.0.0"`; dropped `androidx.webkit` (no WebView); kept appcompat/core-ktx/swiperefreshlayout. |
| `DriverApp/app/src/main/res/xml/file_paths.xml` | Added `<cache-path name="pics" path="pics/" />` (camera proof pictures live in cacheDir). |
| `JumongCloudAPI/wwwroot/updates/JumongDriver.apk` + `driver-version.json` | **APK v2.0.0** (2,608,213 B, versionCode 2, label "Andengs Driver", signed jumong_sign.keystore — same cert = updates in place). Built on SERVER (`JAVA_HOME` = Android Studio jbr, gradle 8.14.3 dist, apksigner 37.0.0 — first build failed: `textStyle` isn't a TextView property + `setTextColor` on `<View>` refs; fixed with `setTypeface(null,BOLD)` + `findViewById<TextView>`; BUILD SUCCESSFUL 49s). Deployed live both hosts + repo copies. |
| `JumongCloudAPI/wwwroot/driver.html` + `driver-app.html` | **v1.1.50 web hardening (still live for browser use):** watchdog now AUTO-RELOADS once (`location.href = pathname + '?v=' + Date.now() + '&r=1'`) when both screens invisible (banner only on the retry pass); errBanner RELOAD button uses cache-busted `?v=` URL (was `location.reload()` — same-URL reload could return a stale instance); diag/footer version → 1.1.50. |

**Verified live:** APK 2.0.0 HTTP 200 (2,608,213 B) on driver.jumongdev.com + admin.jumongdev.com; driver-version.json → 2.0.0 (no BOM); web v1.1.50 with autoReload + busted reload live. **User action:** open the app → update bar 📲 UPDATE → installs native v2.0.0 in place → login ken/12345 → deliveries + payment collection. NOTE: the old WebView page (v1.1.50) remains live as a browser fallback (admin.jumongdev.com/driver.html); the WebView APK is fully replaced by the native one (same package → update overwrites).

### Same-session follow-up: driver.jumongdev.com ROOT → LANDING PAGE (no more web login)

User: "dapat pag iopen ang driver.jumongdev.com ang nakikita lang doon tungkol driver app at download app no more login para san" + "latest app version at ano update kada sa app yun nalang sana ang nakikita dyan walan ng login para d nakaka lito". **Program.cs rewrite** for `driver.jumongdev.com/` → `/driver-landing.html` (was `/driver.html` — the web login). New `wwwroot/driver-landing.html`: brand card + big 📲 DOWNLOAD APP button (`/updates/JumongDriver.apk`) + **LATEST APP VERSION + ANO ANG BAGO** (fetches `driver-version.json`, renders `changelog` split by `\n` into bullets; graceful fallback text on fetch fail). No login anywhere. `driver-version.json` changelog rewritten to newline bullet list (ASCII). `/driver-app.html` + `/driver.html` remain as hidden fallbacks (old WebView APK still loads driver-app.html). Deployed: web-only copy (no API rebuild needed after the earlier publish deploy); verified live 200, no SIGN IN, changelog bullets present.

### Same-session follow-up: DRIVER APP v2.0.1 — native UI fixes (insets, back nav, pull-to-refresh)

User: "can login now" ✅ (native v2.0.0 login works!) — then reported: (1) top content under the status bar (time/camera/signal), (2) bottom buttons under the nav bar (back/home/minimize), (3) phone back button should navigate inside the app, (4) swipe-down to reload. **Fixes (DriverApp, v2.0.1, versionCode 3, 2,608,213 B):** `activity_main.xml` root gained `android:fitsSystemWindows="true"` (status-bar top + nav-bar bottom insets) and the order list was wrapped in `SwipeRefreshLayout` (swipe down → `loadOrders(true)` + `loadPaymentQrs()`); `MainActivity.kt` added an `onBackPressedDispatcher` callback (cancel modal → close; pay → detail; detail → list; else exit) + `loadOrders(pull)` stops the refresh spinner. Built/signed on server (BUILD SUCCESSFUL 49s), deployed live + repo; driver-version.json → 2.0.1 (changelog covers the 3 fixes). Update via in-app 📲 UPDATE bar.

### Same-session follow-up: DRIVER APP v2.0.2 — PAYMENT 400 FIX + UNLI-UPDATE FIX + centered update dialog

User: "nag try ako mag bayad payment failed 400" + "d ata na babago version ng mobile pag ka update kasi naging unli update" + "pwede ba yung prompt ng update sa center of the app pag ka open wag sa taas". **BUG 1 (payment 400):** Kotlin multipart wrote `"--$boundary$crlf"` where `crlf` was a `ByteArray` → Kotlin interpolated `[B@hash` instead of `\r\n` → malformed multipart → ASP.NET 400 with empty body (app showed "Payment failed (HTTP 400)"). Server-side verified OK with a byte-identical correct multipart (test payments on orders 2+5 **reverted**: order_payments/timeline deleted, paid_status→unpaid, Fei Drio points -7 restored). **BUG 2 (unli update):** `checkUpdate()` compared driver-version.json against the HARDCODED `APP_VERSION = "2.0.0"` constant (never bumped) → update bar shown forever even after installing newer builds. **Fix:** removed the constant; `currentVersion()` reads the ACTUAL installed `versionName` from PackageManager. **UX:** update prompt moved from top bar → **centered dialog** (`updateOverlay` with LATER/UPDATE + changelog bullets; old `updateBar`/`btnUpdate` removed from layout + code). DriverApp v2.0.2 (versionCode 4, 2,608,213 B) built/signed on server, deployed live + repo; driver-version.json → 2.0.2 (changelog covers all 3). GOTCHA re-learned: huwag mag-iwan ng hardcoded version constant sa self-update check — lagi mong basahin ang installed versionName.

### Same-session follow-up (v1.1.49b): WHITE/EMPTY SCREEN after login — show() regression FIXED

User: "naka login na ako pero white background lang empty" — the v1.1.49 login fix WORKED (telemetry: 3× `orders:200` on the Infinix at 15:39), but after `show('main')` the screen was blank (diag `loginVisible:false, mainVisible:false`). **Root cause (introduced by my own v1.1.49 show() rewrite):** the new show() set inline `el.style.display` ONLY — but all 3 screens (mainScreen/detailScreen/payScreen) carry `class="hidden"` whose critical-css rule is `.hidden { display: none !important }` — **!important class beats inline style** → target screen stayed hidden → both screens invisible → white/empty. **Fix:** show() now toggles BOTH `el.classList.remove/add('hidden')` AND inline display (target: remove hidden + set map display; others: add hidden + inline none). Deployed web-only (no API/APK change); verified live (`el.classList.remove('hidden'); el.style.display` present). **LESSON: `.hidden` is `!important` in this page — any inline-display visibility control MUST also toggle the class.**

## Previous Change (2026-08-22) — v1.1.47 (Cloud API) + web: THREE-TIER PRICING (online_price + Sari-Sari rates) + PROMO BANNERS + dashboard double-prefix bug FIX

**Request (pricing):** regular online member vs verified sari-sari store prices — guest = no price at all; **regular member = `online_price`** (0/blank → falls back to `mp.price`), NO unit selector; **sari-sari store = `mp.price` + full unit selection** (pc/by-N/box, PRICE SAMPLE via unit chips). Also: **Promo Banners** (dashboard-managed clickable banners on the shop front; 3 defaults seeded earlier) + the dashboard was broken with `/api/dashboard/dashboard/...` 404s on shop-content/google-auth/sari-sari/subdivision endpoints.

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `master_products.online_price NUMERIC NOT NULL DEFAULT 0` (0 = use default price). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`IsSariSari()`** helper (session → customer.is_sari_sari). **`ShopCatalog`** → 3-tier price (guest `price=null`/`priceHidden`; regular: `online_price>0?online_price:mp.price`; sari-sari: `mp.price`), response `{items, member, isSariSari}` + `onlinePrice` per item (SELECT idx 0-8). **`ShopProduct`** same (returns `onlinePrice` + `isSariSari`). **`GetMasterProducts`/`DownloadMasterCatalog`** include `online_price` (col 12; units 13). **`CreateMasterProduct`/`UpdateMasterProduct`** write `online_price` (`@op`). **flags PATCH** accepts `onlinePrice`. `SeedProductDto.OnlinePrice` + `MasterProductFlagsDto.OnlinePrice` added. **DOUBLE-PREFIX FIX (root cause of dashboard 404s):** action routes `[HttpGet("dashboard/end-shift-snapshot")]` → `end-shift-snapshot` and `[HttpPost("dashboard/shop-content")]` → `shop-content` (app.js `const API='/api/dashboard'` + `API+'/dashboard/...'` calls = `/api/dashboard/dashboard/...`). |
| `JumongCloudAPI/wwwroot/components.js` | **15 calls stripped of `/dashboard/`** (`API + '/dashboard/...'` → `API + '/...'`: shop-content, google-auth, sari-sari/applications, subdivision-suggestions, promo-banners, kb pending-count, driver login, remittances, ecom-shift, orders pick, etc.). **`saveOnlinePrice(x, val)`** in masterProductsPanel (PATCH flags `{onlinePrice}`, 0 = default). Promo-banners endpoints now called `API + '/promo-banners'`. |
| `JumongCloudAPI/wwwroot/index.html` | Master Products table: **Online Price** column (`<th>` + inline `<td>` input `:value="x.onlinePrice||''"` @change=saveOnlinePrice, placeholder "0 = default"). **NEW PROMO BANNERS SECTION** (`x-data="promoBannersPanel"`, E-COMMERCE sidebar item existed): banner card grid (image/preview, ACTIVE badge, target type+value, EDIT/DEL) + BANNER EDITOR (image file upload, targetType category/product/url/register, targetValue, sortOrder, active) + live **PointsRate** chip. Div balance 603/603. |
| `JumongCloudAPI/wwwroot/shop.html` | **PROMO BANNER CAROUSEL** under hero (`#bannerRow`, horizontal scroll): image banners from `/promo-banners`; `register` text banner (gradient card with dynamic "1 point for every ₱{pointsRate}"); click gating `bannerClick(b)`: register → `signIn()`; guest → `signIn()`; member → category (`alcohol|CLVB` = category|search → chips/grid + scroll), product ID → detail, url → navigate. **LOGIN GATE**: new `#guestShop` (🔒 "Members Only ang mga Presyo at Produkto" + SIGN IN WITH GOOGLE) + `#memberShop` wrapper (tiles/chips/grid hidden for guests); `applyMemberUI()` shows the right one after `loadMe()`. **SARI-SARI UNIT SELECTOR**: `openDetail` uses `detailUnit` (default = default unit; regular members get single default-unit price only), unit chips when `isSariSari && units.length>1` (`selectUnit(name)` re-renders), "Wholesale price" note for sari-sari with single unit; `addToCart` stores `{id, qty, unitName, price}`; cart row/cartTotal/coSummary/placeOrder use the stored unit (fallback defUnit for old carts). Cache v2 stores `isSariSari`. |
| — | **Verified live:** API 1.1.47 deployed (publish → WinRM stop/copy/start, first `Copy-Item` while service ran → only wwwroot landed; full publish copy landed after `net stop`); `/promo-banners` → 3 banners + pointsRate 200; guest catalog `member=False` prices blank; master products carry `onlinePrice`; **all double-prefix routes now 200** (shop/content, sari-sari/applications, subdivision-suggestions, google-auth, end-shift-snapshot); web files live with `promoBannersPanel`/`saveOnlinePrice`/`bannerRow`/`memberShop`/`guestShop`/`selectUnit` present; shop.html JS `node --check` OK, div balance 147/147. Git commit pending. |

**Notes:** `online_price` defaults 0 → all products show default price until the owner sets Online Price per product (Master Products inline column). Promo banners: 3 seeded live (CLVB image → `alcohol|CLVB`, FREE DELIVERY image → shop URL, REGISTER text card) — owner can add/edit/delete from dashboard (E-COMMERCE → Promo Banners); click targets: guests always → Google sign-in; member → category/product/url. Old carts (no unitName/price) still resolve via default unit (backward compatible).

### Same-session follow-up (v1.1.49): ONLINE ORDER RECEIPT PRINT (whmobile) + FRACTIONAL POINTS (₱100 = 0.5 pts visible)

User: walang resibo ang mga nag-order online — kulang ang warehouse print. Decision: **option A lang (warehouse app)** — walang driver printing. Plus **points clarity** (user rule): "baka mag compute sila base sa total mag reklamo" → receipt shows fractional per-item points (₱100 → 0.5, ₱50 → 0.25) and separates **WITH POINTS / WALANG POINTS** sections; exemption/Pts-U rules per master catalog (Exempt checkbox = wala; Pts/U > 0 = per-unit pts, hindi global; else `total ÷ PointsRate`).

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`AwardOrderPoints` CHANGED**: per-item points are now accumulated as DECIMAL (`acc += total/rate`, exempt=0, ppu>0 → `ppu*qty`) then **floored ONCE at the end** — ₱100+₱100 = 1 point (dati 0 dahil per-item truncation). **`ShopGetOrder`**: items query LEFT JOINs master_products (points_exempt/points_per_unit) → each item gets `points` (decimal, 2dp) + `pointsExempt`; order response gains `totalPoints` (decimal) + `awardPoints` (int = floor). **`CustomerOrders`** (`/customer/orders`): same per-order `totalPoints` (decimal, 2dp) + `awardPoints` (int) computed in SQL (rate param from store_settings). **NEW `PUT /shop/orders/{id}/items`** (admin edit before driver): body `{items:[{productId, unitName, qty}]}` — only `pending`/`confirmed` allowed (400 otherwise); resolves each item name/barcode/qtyPerUnit/price via master_product_units with **default-unit fallback** (unknown unit → default unit, qpu+price); **confirmed orders**: stock delta vs current items (guarded `stock_qty >= delta` → 400 insufficient) + ecommerce stock_trails; pending: no stock touch (reserve happens at confirm); rewrites online_order_items (clears order_pick_items — re-pick after edit), recomputes `total = Σ items + delivery_fee`, timeline "Items edited". Version → `"1.1.49"` (2 places). |
| `JumongCloudAPI/wwwroot/whmobile.html` | **`printOrderReceipt()`** (new): full receipt — ANDENGS SUPERSTORE header, order no/date/customer/phone/address/note, **`-- WITH POINTS --`** section (compact items: name × qty price) → `⭐ TOTAL: X.XX pts -> +X point(s) (to earn|earned)` (kung kulang: `+0 (kulang pa para sa 1 point)` + `⭐ Points to earn: X.XX pts -> +0 (P200 = 1 point)` line) → **`-- WALANG POINTS --`** section (compact) → SUBTOTAL/DELIVERY FEE/TOTAL → Payment → Salamat footer. Sections skipped when empty; `₱` avoided (printer = US-ASCII, use `P`/plain numbers). **PRINT RECEIPT button** in order detail for ALL statuses (except cancelled); PRINT PICK SLIP kept for confirmed. **AUTO-PRINT on CONFIRM**: `ooSetStatus('confirmed')` → after success, auto-prints the receipt if printer connected (else toast "i-tap ang PRINT RECEIPT"). **PICK-COMPLETE banner**: green "✅ Kumpleto na ang lahat ng item — i-assign na ang driver sa ibaba" when pickProgress total==picked && confirmed. **EDIT ITEMS (pending/confirmed only)**: ✏️ EDIT ITEMS button → edit mode (`renderOoEdit`): per-row qty −/+ steppers + ✕ remove + ADD ITEM (search `/shop/catalog/search?q=&limit=15` → tap result to add at default unit, qty 1) + live TOTAL + SAVE (`PUT /shop/orders/{id}/items`) / CANCEL. |
| `JumongCloudAPI/wwwroot/shop.html` | MY ORDERS cards: **⭐ Points line** (amber, 2dp) per order — `Points to earn/earned: X.XX pts → +N.00 point(s)` (shown when totalPoints > 0). |

**Verified live (API 1.1.49 + whmobile.html + shop.html):** `shop/orders/3` → per-item `points=0.15` (₱29.75/200), `totalPoints=0.15`, `awardPoints=0`. **BUG FOUND + FIXED (user: "shop failed to load order sa online order"):** whmobile Online Orders used `session.apiBase` but the whapp login response has NO `apiBase` field → `fetch(undefined + '/shop/orders')` = "Failed to load orders: Failed to fetch" — the Online Orders tab was DEAD since v1.1.54 (other tabs use the `API` constant, that's why they worked). FIX: all 8 Online Orders/edit fetch calls `session.apiBase` → `API + '/dashboard'` (verified live, no session.apiBase remains). **End-to-end fractional test (scratch, cleaned):** customer 1498 + order 8 (3 × ₱96 = ₱288; bawat item 0.48 pts) → GET: totalPoints=1.44 awardPoints=1 → driver pay (ken, cash 288) → **customer loyalty_points 0 → +1** (dati 0!). GOTCHA re-learned: AwardOrderPoints needs `qr_code` (online-registered policy) — test customer needed `AS-TEST01` set before pay. **Edit-items test (scratch order 9, cleaned):** pending order 1 item → PUT add CREAMSILK (empty unit) → resolved default unit "by 12" at ₱95 → total recompute 287 ✓ (96×2+95), items replaced; pending = no stock guard (reserve happens at confirm); bad product/qty 99999 → 200 (pending has no stock check — expected). All test data deleted (verified 0 remaining). `node --check` OK, div balances OK (whmobile 267/267). **FINAL RECEIPT flow (user: "resibo ng delivery dala na may points")**: pag confirmed at kumpleto na ang LAHAT ng item pick → **auto-print ang FINAL receipt** (may points, final items) + bagong prominent button **"🖨️ PRINT FINAL RECEIPT (para sa driver)"** sa pick-complete banner — i-print at ibigay sa driver kasama ng items. `ooAutoPrintedKey` (id:picked:total) prevents repeat auto-prints; togglePick re-arms. **CRISIS + FIX (encoding corruption):** isang PowerShell `.Replace([char]0x2B50,'**')` na may `Get-Content -Raw` (default ANSI) sa UTF-8 file ay nag-mangle ng LAHAT ng non-ASCII sa whmobile.html (₱/emoji/—→mojibake, ~298 lines); naulit pa sa 2 pang regex+WriteAllText full-file writes = **4 corruption passes**. Restore: 4 rounds ng `UTF8.GetString(bytes) → CP1252.GetBytes(s) → UTF8.GetString(b)` (round 4 = peso restored; verified ₱/✓/📍/— lahat back, JS OK, features intact — 1-3 rounds over/under-corrected). LESSON: **huwag gumamit ng Get-Content -Raw + WriteAllText sa mga UTF-8 file na may emoji — gamitin ang Edit tool o ReadAllText(UTF8)**.

### Same-session follow-up: BEAR BRAND duplicate barcode fix (4800361426916)

User: "master catalog i am having problem with this 4800361426916 when punch its test item" — punching the barcode returned **TEST BEAR** (id 666, test item, price 10/cost 5) instead of the real **BEAR BRAND SWAK BUY 10 GET FREE 1** (id 664). Root cause: master 664 carried `48003614269160` (13-digit code + extra trailing 0 — NOT a valid GTIN-14), while the test item held the real 13-digit code; additionally each store's local POS had TWO product rows (HQ: TEST BEAR pos 5596 = **154 pcs real stock** at maling price 10 vs BEAR BRAND pos 5901 = 104 pcs at ₱112). Fix: master 664 barcode → `4800361426916`, master 666 DELETEd; per-store cloud `products` merged (stock summed into the real row, barcode set to 13-digit, TEST BEAR rows deleted); **HQ/AA36/A80C local SQLite fixed via agent `sql`** (stock merged 103+154=257, BEAR BRAND barcode → 13-digit, TEST BEAR deactivated + barcode→`TEST-BEAR-OLD` to dodge the local full UNIQUE(barcode) index — BEGIN/COMMIT + duplicate barcode updates fail, order matters: clear test barcode FIRST). The first `UPDATE master_products SET barcode` failed on the unique index while 666 still existed → re-ran after DELETE. **Punch verified live:** `/shop/catalog/search?q=4800361426916` → id 664 BEAR BRAND SWAK ₱112; cloud stable across 2 push cycles (HQ 257 pcs, AA36 5 pcs, E174/A80C 0). E174 (Naic, PC off) self-corrects when back online + UPDATE MASTER. **PERF FIX (user: "dashboard too long to load the master catalog"):** ang dashboard `masterProducts.load()` ay gumagamit ng `/products/master/download` (ang POS-client download endpoint — **15.3 MB base64 images, 59.6s**!). Fix: `GetMasterProducts` gains `?noImages=true` (imageData='' — column positions kept), bagong `GET /products/master/{id}` (product + image + units para sa editor), dashboard `load()` → `?noImages=true`, `openEditor(id)` → async fetch ng single (image+units), Image column may IMG placeholder pag walang image. **Measured: list 59.6s/15.3MB → 1.5s/160KB (40x)**, single 640ms. Version → `"1.1.50"`. Git commit pending.

### Same-session follow-up: POS customer download CRASH (UNIQUE Phone conflict) — e-commerce duplicate phones + v1.1.55

User: "in pos client when updating the customer there is a error maybe because the new change in ecommerce" — HQ POS error.log every 5-min cycle: `SQLiteException UNIQUE constraint failed: Customers.Phone at SyncService.DownloadCustomersAsync()`. **Root cause:** e-commerce Google customers ("John Fernan Federico" phone 09945662959) duplicate an existing POS customer's phone ("abigail" id 546 same phone) → the name-match UPDATE path set Phone=@p on the local e-commerce row (id 1121, phone '') while another local row (id 771 "abigail") already held that phone → partial UNIQUE(Phone WHERE Phone!='') violated. **Fixes:** (1) HQ local via agent: `UPDATE Customers SET Phone='' WHERE Id=771` (verified no 09945662959 left); other stores (AA36/A80C/E174) had NO duplicate phones; cloud: `UPDATE customers SET phone='' WHERE id=546` (abigail — no sales, only 2 credit txns; John 1459 keeps phone + QR AS-AD36EB) — cloud now 0 duplicate phones. (2) **Client v1.1.55 hardening (SyncService.DownloadCustomersAsync):** name-match UPDATE now `Phone = CASE WHEN NOT EXISTS (SELECT 1 FROM Customers WHERE Phone=@p AND Phone!='' AND Id!=@id) THEN @p ELSE Phone END`; INSERT path checks phone-in-use first → inserts with Phone='' instead of violating. **CRISIS + FIX (DashboardController.cs also corrupted!):** ang version-bump PowerShells (`Get-Content -Raw` ANSI + `WriteAllText`) ay nag-mangle din ng DashboardController.cs (₱/emoji → mojibake, 3 passes) → **CS8103 "Combined length of user strings exceeds limit"** (mojibake inflated the #US heap past 2MB → publish FAILED). Restore: 3 rounds ng CP1252 reversal (round 3 = ₱ back, mojibake gone, versions intact — ASCII survived). LESSON re-confirmed: **version bumps din — Edit tool lang, HINDI PowerShell .Replace+WriteAllText**. **GitHub release v1.1.55** (id 375248599, asset 211,400,346 B, download 200 verified) + drop pushed; API redeployed (1.1.50 with latestVer 1.1.55). Stores update via UPDATE APP. Git commit pending.

### POS ORDER-TYPE FLOW + POINTS ELIGIBILITY (user explanation — reference, wag nang ipaliwanag ulit)

**OrderTypeForm (POS sales start):** 3 buttons — **WALK-IN** / **COUNTER (Track Customer)** / **ONLINE ORDER**. `SalesForm.PromptOrderType()`: Walk-in → direkta pasok (walang customer, `_orderType="Walk-in"`); Counter/Online → `SelectCustomerForm(_orderType)` (search by name, may Phone+Points columns) → pumili ng customer → `_selectedCustomer` set → sale opens na may pangalan (`UpdateCustomerDisplay`). **Points eligibility rule (user-locked):** ang **OLD/manual POS customers ay WALANG points** — ang **may points LANG ay ang mga online-registered** (may Google account sa shop.jumongdev.com, may `qr_code` AS-XXXXXX — ang `Customer.QrCode` ay non-empty). `UpdateCustomerDisplay` shows `⭐EARN` (may QR) / `⛔no points` (wala). **📱 SCAN CUSTOMER QR button (SalesForm, `ScanCustomerQr`):** input QR code o Customer ID → `CustomerService.GetByQrCode` → i-attach sa sale (walk-in type pa rin ang order) — ito ang daan para i-attach ang online customer na may QR sa isang sale para mag-earn ng points; `lblCustomerInfo` shows `⭐EARN`/`⛔no points`. **Award (SalesForm pay):** points ONLY kung `QrCode` non-empty; rate = `PointsRate` setting (default 200); per-item: exempt → 0, `PointsPerUnit` > 0 → ppu×qty, else total/rate (int truncate per item). **Nakabinbing feature request (user, 2026-08-23):** (a) sa Walk-in flow huwag magpakita ng SCAN CUSTOMER (sa Customer Track lang dapat); (b) sa `SelectCustomerForm` search, i-flag/markahan ang customer na may points (may QR) — kasi ang mga lumang pangalan ay walang points; ang e-commerce na may account/login lang ang may points. **IMPLEMENTED in v1.1.56:** (1) `OrderTypeForm` — ONLINE ORDER button REMOVED (user: "pos client walang Order online yan... nasa mobile yun" — ang online order fulfillment ay nasa whmobile/dashboard; ang "Online" sale type ay dead code, 2 remnants lang: PrinterService Mobile/Addr print + SelectCustomerForm header, left as-is); OrderTypeForm = WALK-IN + COUNTER na lang, ClientSize 380x225. (2) `SalesForm` — `_btnScanQr` promoted to field, `UpdateCustomerDisplay()` sets `_btnScanQr.Visible = _selectedCustomer != null` (SCAN QR hidden sa Walk-in, visible sa Customer Track). (3) `SelectCustomerForm` search — `⭐` prefix sa Name kapag `QrCode` non-empty (points-enabled/online account). Client `1.1.56`, API `latestVer` 1.1.56; GitHub release v1.1.56 (id 375261803, asset 211,400,346 B, download 200 verified); drop pushed; API redeployed. **CRISIS + ROOT CAUSE (user: "unsupported 16-bit application" sa U Got Mart pagkatapos ng UPDATE APP):** ang GitHub assets v1.1.55 at v1.1.56 ay **SIRA** — na-upload ko sila gamit ang `curl -F "file=@..."` (multipart) pero ang GitHub assets API ay **hindi sumusuporta sa multipart** → na-save ang RAW multipart body bilang asset (nagsisimula sa `--------`, hindi `MZ`) → ang mga store na nag-update ay nakakuha ng sira na exe → "Unsupported 16-bit application". Ang UpdateService size checks ay pumasa (pareho ang laki!) — walang MZ check. **FIXES:** (1) re-upload ang v1.1.55/1.1.56 assets gamit ang `curl --data-binary "@file"` + `Content-Type: application/octet-stream` (raw binary — verified MZ via API at CDN); (2) `UpdateService.DownloadAndUpdate` + **MZ header check** (0x4D 0x5A) pagkatapos ng download (future releases); (3) U Got Mart restored via agent `update` mula sa server wwwroot + restart (appVersion 1.1.56 verified); (4) v1.1.57: **SCAN QR button default `Visible=false`** (ang v1.1.56 code ay may init-order bug: `UpdateCustomerDisplay()` sa line 164 ay tinawag BAGO ma-create ang `_btnScanQr` sa line 1023 → null guard skip → button visible sa lahat ng mode!) + `UpdateCustomerDisplay()` call pagkatapos ng creation; (5) **CustomersForm grid (menu) ⭐ flag din** (may QR = points-enabled); release v1.1.57 (id 375274195, asset 526442917, **MZ verified**, size 211,412,412) + drop; HQ + U Got Mart pushed via agent (211,412,412 bytes verified). **LESSON (upload):** GitHub releases upload = `curl --data-binary "@file"` RAW — **HINDI** `-F` multipart. **v1.1.57 rollout (user: "pa update na rin HVR at acgs"):** lahat ng 4 stores na-push sa v1.1.57 via agent `update` mula sa server wwwroot temp URL (211,412,412 B verified per store) + restart — HQ `C:\Users\ADMIN\Desktop\JumongPosHW\`, UGOT MART `C:\JumongPos\`, HVR `C:\Users\ADMIN\Desktop\HVR_POS\` (agent path verified: `C:\Users\ADMIN\Desktop\HVR_POS\agent\Agent.exe`), ACGS `C:\JumongPos\`; appVersion sa health ay luma lang hanggang sa cashier login (DB Settings write post-login — E174 nagpakita 1.1.57 pagkatapos ng login ng user). Temp exe deleted sa server pagkatapos. Git commit pending.

### Same-session follow-up (v1.1.48): shop hero → carousel, member split layout, customer QR fix, COD-only checkout, DRIVER LOGIN FIX, payment_qrs shared with driver app

User directions this session: (1) logged-in members = NO hero (hero replaced by the **banner carousel**), categories on the LEFT, products/search on the RIGHT (desktop; mobile keeps horizontal chips); (2) customer QR "hindi ma-scan" → lib was loaded `async` (rendered as raw text sometimes) → now synchronous + retry + 120px + CorrectLevel L; (3) **alisin GCash sa customer checkout — COD lang** ("wala naman tayo api to do gcash payment"), driver collects at door; (4) **driver couldn't login** → root cause `driver.html const API = 'https://admin.jumongdev.com/api'` but all routes are under `/api/dashboard` → 404 (verified `/api/driver/orders` 404 vs `/api/dashboard/driver/orders` 401) → fixed to `/api/dashboard`; (5) driver payment = CASH / GCash with the **store QR** (same QR the admin pushes to POS clients) / SPLIT (cash + gcash w/ QR) → new **`payment_qrs` cloud table** shared with the driver app.

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/shop.html` | **HERO → CAROUSEL**: hero section + bannerRow replaced by `#carousel` (auto-rotate 5s, prev/next arrows, dots; slides = active `/promo-banners`; register slide = gradient card w/ dynamic pointsRate). Trust badges + wholesale banner moved inside `#landingExtras` (guest-only). **MEMBER SPLIT LAYOUT**: `#memberShop` = left `<aside>` categories (`#sideCats`, counts, active state, `renderSideCats()`) + right column (grid + `#gridCount`); mobile = chips only (`lg:hidden` wrapper); `#shop` anchor moved to split container; `renderTiles`/`setGroup` kept but tiles section removed (guarded no-ops). **CUSTOMER QR FIX**: qrcodejs now synchronous + `renderQr()` retry-until-loaded, 140→120px, CorrectLevel L. **COD-ONLY checkout**: GCash button + `#gcashRefRow` removed → static COD panel ("Bayaran sa driver... Pwede rin GCash sa driver"); `selectPay()` removed; `placeOrder()` always sends `paymentMethod:'COD'`, `gcashRef:''`; success modal always "Cash on Delivery". |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: new `payment_qrs` (id, header, file, is_active, sort_order, updated_at). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`GET /payment-qrs`** (public → driver app) + **`POST /payment-qrs`** (upsert list; ids retained, others DELETEd — incl. delete-all when empty). Version → `"1.1.48"` (2 places). |
| `JumongCloudAPI/wwwroot/driver.html` | **LOGIN FIX**: `const API = 'https://admin.jumongdev.com/api'` → `'https://admin.jumongdev.com/api/dashboard'` (was 404 on every call — driver app was completely dead). **GCash QR display**: `loadPaymentQrs()` fetches `/payment-qrs` (no auth), `renderPayQrs()` fills `#payQrBox1`/`#payQrBox2` (QR grid w/ headers, violet hint box) inside GCash + Split rows; called at startup + `openPay()`. Cash flow unchanged. |
| `JumongCloudAPI/wwwroot/components.js` (`posQrPanel`) | New `cloud` state + `loadCloud()` (GET `/payment-qrs`), `saveCloud()` (POST full list), `removeCloud(q)`; **after every successful PUSH → the uploaded QR is upserted into the cloud list + auto-saved** (driver app gets it immediately); `init` loads cloud when section opens. |
| `JumongCloudAPI/wwwroot/index.html` | POS QR panel: new **🔗 CLOUD QR LIST (driver app / shared)** card — per-QR thumbnail/title/file, ACTIVE checkbox, ✕ remove, 💾 SAVE TO CLOUD button + status message. Div balance 610/610. |

**Verified live:** API 1.1.48; `/payment-qrs` round-trip (POST 1 → GET 1 → empty-array POST deletes all → 0 — delete-all fixed after first deploy); **driver login `ken`/`12345` → OK (kenneth, driverId 16)**; shop.html live = no `payGcash`, COD panel present; driver.html live = `/api/dashboard` + `payQrBox1` + `loadPaymentQrs`; index.html live = CLOUD QR LIST + `saveCloud`; components.js live = `payment-qrs` calls; all JS `node --check` OK, div balances 153/153 (shop), 43/43 (driver), 610/610 (index). **Owner action:** sa POS QR panel, i-upload/i-push ang GCash QR (o i-SAVE TO CLOUD ang listahan) para lumabas sa driver app. **Cashier acceptance flow (driver → pera sa store):** driver 💵 ACCEPT sa door → order `delivered`+`paid`; driver 🏠 RETURN TO HQ → dashboard 💰 DELIVERY REMITTANCE → ✔ ACCEPT per payment (`remitted=true`); end of day 🌙 E-COMMERCE DAY → CLOSE DAY (delivered tallies + carried-over confirmed/shipped/arrived). Git commit pending.

### Same-session follow-up 2: driver app white page (browser cache) → DRIVER APP APK (v1.0.0) + no-cache hardening

User: driver app "empty white page" kahit gumagana ang API (verified: headless Edge render OK, live 200, JS syntax OK). **ROOT CAUSE (found via self-diagnostic crash_reports):** `logout()` called `show('login')` but the screen id is `loginScreen` → `show()` hid ALL 4 screens and showed nothing → **white page**. Triggered whenever a stale/invalid `drv_token` caused `loadOrders()` → 401 → logout() (whapp_tokens keeps last 5; older web sessions died). Diag evidence (`crash_reports` type=diag from the phones): `loginVisible:false, mainVisible:false, tw:true` at 3s. **FIX:** `logout()` → `show('loginScreen')` + `show()` falls back to loginScreen when target id missing (white page now impossible). Fixes: (1) no-cache meta tags sa driver.html; (2) `driver-app.html` (bagong URL = zero cache); (3) errBanner + 6s watchdog + self-diagnostic POST `/crash-report` (type `diag` — loginVisible/mainVisible/tw/bodyChildren/ua); (4) manifest start_url → `/driver-app.html`; (5) **DRIVER APP APK** — WebView wrapper `LOAD_NO_CACHE` + version-busted URL + dark bg (walang white flash) + SwipeRefreshLayout→`refreshAll()` + camera grant (GCash proof pic) + update (driver-version.json + `downloadAndInstall` + ReopenReceiver) + crash.log. **`DriverApp/`** (new repo folder, com.jumong.driver, versionCode 1 / 1.0.0, truck icon, signed jumong_sign.keystore): build + sign sa SERVER (`C:\Users\ADMIN\Desktop\JumongPosV1.01\DriverApp`, JAVA_HOME=Android Studio jbr, gradle 8.14.3 dist, apksigner 37.0.0) — **BUILD SUCCESSFUL 52s**, badging: `package name='com.jumong.driver' versionCode='1' versionName='1.0.0' application-label 'Andengs Driver'`; deployed `wwwroot/updates/JumongDriver.apk` (2,619,802 B, verified HTTP 200) + `driver-version.json` (1.0.0). driver.html/driver-app.html: **📲 DOWNLOAD APK button** sa login + **updateBar** (in-APK auto-check vs driver-version.json). NOTE: ang shop (e-commerce) ay gumagana sa browser dahil may sariling SW (network-first) + laging binubuksan fresh; ang driver PWA ay na-stuck sa lumang cache — APK ang permanenteng solusyon (walang browser cache sa WebView). Git commit pending.

## Previous Change (2026-08-21) — v1.1.46 (Cloud API) + web: MESSENGER BOT — Facebook Page AI Chatbot (llama3.1 + Knowledge Bank via Messenger API)

**Request:** "i am registering now to meta developers facebook account to access my page to what we make in ai chat bot" — the Messenger bot planned back in v1.1.30 ("Phase 2 — pending Messenger key") is now BUILT + LIVE. User created a Meta app **"pageMessenger" (App ID 1041403252013127)** via the Messenger "Get started" flow (the earlier app "Andengssuperstore" 1728439461754555 was Facebook-Login-only — new Meta UI doesn't allow adding Messenger to it; GOTCHA: the Messenger use case only appears via the docs Get-started wizard or apps created for Messenger). Page = **Andeng Superstore (ID 203372639529959)**. Bot answers via the EXISTING RAG pipeline (chat_kb + promo + product search → llama3.1:8b on the server), replies through the Messenger Send API, and sends the shop QR image on "shop"/"qr"/"menu"/"order" commands.

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `messenger_bot` (id=1 PK, page_id, page_token, verify_token, enabled, updated_at + seed row) + `messenger_convos` (psid, page_id, history JSONB `[{role,content}]`, updated_at + UNIQUE index on psid) — per-chat history for context. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`/chat` refactored** — RAG facts builder + llama call extracted into shared `GetBotReplyAsync(msg, history)` returning `(reply, sources)` (empty reply = Ollama fail); `/chat` behavior unchanged (rate limit/logging/502 preserved; also FIXED the mojibake `₱` in prodHits line during the move). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **New endpoints**: `GET /messenger/webhook` (Meta verification — hub_mode/hub_verify_token/hub_challenge vs config VerifyToken) + `POST /messenger/webhook` (events: message text + postback payload; skips is_echo; dedupes by message mid; 200 immediately, processing via Task.Run) + `GET/POST /messenger/config` (upsert id=1; GET masks the token `EAAO…ZDZD` style) + `POST /messenger/test` (validates page token via `messenger_profile` probe). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | Bot logic `ProcessMessengerMessage` (per-PSID SemaphoreSlim serialization): keywords shop/store/qr/menu/order/mag-order/buy → shop.jumongdev.com link + QR image attachment; postback (get_started `#store`) → greeting + QR; everything else → `GetBotReplyAsync` with convo history (last 10, saved to messenger_convos) → Send API text. `SendMessengerText`/`SendMessengerImage` via `graph.facebook.com/v26.0/me/messages?access_token=`. Version → `"1.1.46"` (2 places). DTO `MessengerConfigDto`. |
| `JumongCloudAPI/wwwroot/index.html` | Sidebar leaf `{ id:'msgr-bot', icon:'🤖', label:'Messenger Bot' }` in grp-pos (after Shop Content) + panel: SETTINGS card (Page ID, masked token w/ re-paste-to-change, Verify Token, ENABLED toggle, TEST CONNECTION, SAVE) + SETUP GUIDE card (webhook callback URL, verify token, subscription fields, test instructions). Div balance 527/527. |
| `JumongCloudAPI/wwwroot/components.js` | New `messengerBotPanel` Alpine component (load/save/test with status messages); `groupParents['msgr-bot']='grp-pos'` + grp-pos isGroupActive check updated. |
| PostgreSQL (live) | `messenger_bot` seeded: page_id 203372639529959, token (the user's page token, masked in GET), verify_token `jumongbot_verify_2026`, enabled=true. `shop_content`: messenger_link → `https://m.me/203372639529959`, facebook_link → `https://facebook.com/203372639529959`. |

**Verified live:** API v1.1.46; config round-trip (masked); `/messenger/test` → `{"ok":true,"detail":"Page token valid - Messenger API OK"}`; `POST /me/subscribed_apps?subscribed_fields=messages,messaging_postbacks` → success (page-level subscription done via API — app id 1041403252013127 listed); `messenger_profile` get_started `#store` + greeting set. Deployed via WinRM (stop→copy→start). **REMAINING (user must do in Meta UI — cannot be done via API):** App Dashboard → pageMessenger → Messenger → **Webhooks → Configure**: Callback URL `https://admin.jumongdev.com/api/dashboard/messenger/webhook`, Verify Token `jumongbot_verify_2026`, fields messages + messaging_postbacks → Verify and Save. Then message the page to test. Git commit pending. GOTCHA: the page token from Graph Explorer is SHORT-LIVED (~hours-days); exchange for a long-lived one (60 days) via `oauth/access_token?grant_type=fb_exchange_token` with the app secret, or the dashboard bot will die silently — also note the earlier fb_exchange attempt returned "Error validating client secret" (app was still Andengssuperstore at that point; retry with pageMessenger's secret). Meta "business verification" + App Review for `pages_messaging` are only needed when publishing for ALL customers (dev mode works for page admins/testers now).

### Same session follow-up (chat widget + learning loop + e-commerce hardening)

User's e-commerce direction (decided): **Messenger = Meta Business Agent only** (sends shop.jumongdev.com link on "shop"/"order" — user will enable it in Meta Business Suite; the custom Messenger webhook bot stays DEPLOYED as backup but its replies conflict with the Agent so only one should be live), **the REAL AI = chat widget on shop.jumongdev.com** (guide customers: how to register/order, stock, promos) + **learning loop** (unknown questions auto-save to dashboard for the owner to answer). Also found: user's page has an inbox INSTANT REPLY (old Zobaze text) — cosmetic, editable in Page Inbox settings; and the "Unable to send" for non-tester accounts is EXPECTED (dev mode restriction — only app admins/devs/testers can message the page).

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **Learning loop in `GetBotReplyAsync`**: when `facts.Count == 0` (no KB/promo/product/delivery/contact hit) → INSERT into `chat_kb` (`source='auto-pending'`, answer='', active=false, dedupe: same question within 24h, min 3 chars) + return deterministic canned reply "Wala pa pong nakarekord na sagot dito, pero nirecord po namin ang tanong niyo...". **New dynamic facts**: deliveryAsk (delivery/deliver/shipping/fee → reads `shop_settings` fee + free_min live → "FREE po ang delivery sa ngayon!" when 0) + contactAsk (contact/tawag/phone/messenger/fb/saan/branch/address → m.me/203372639529959 + Naic address). **New `GET /chat/kb/pending-count`** (unanswered auto-pending count). `/chat` rate limit 5 → **20 msg/min/IP**. |
| `JumongCloudAPI/wwwroot/shop.html` | **Catalog hardening**: cache key → `shop_catalog_cache_v2` (skips poisoned old cache); error message only shown when `catalog.length === 0` (a late render error no longer blanks a rendered grid); `watchImages` falls back to immediate loads when `IntersectionObserver` is undefined (old phones). **FREE DELIVERY banner** (`#freeDelBar`, emerald, shown automatically when `deliveryFee <= 0`). **AI CHAT WIDGET**: floating bubble (bottom-right, violet) → panel with header (🤖 AI Assistant + 💬 Messenger link via `data-msg`), message list (user right/violet, bot left/white), quick-reply chips (order/register/stock/promo/delivery), input + Send (Enter), typing dots, session history in localStorage `shop_chat_history` (last 10), posts to `/dashboard/chat` with 60s timeout; friendly fallback on error; Escape closes. Div balance 116/116, JS syntax-checked. |
| `JumongCloudAPI/wwwroot/components.js` | `_kbBadge: 0` store state; `kbPanel` gains `pendingOnly` toggle + `filtered` getter (source==='auto-pending' && !answer && !active) + `quickAnswer(k)` (PUT answer+active=true, then badge refresh) + `refreshBadge()` (polls pending-count). |
| `JumongCloudAPI/wwwroot/index.html` | Sidebar badge span now also shows `_kbBadge` on AI Knowledge item; AI KNOWLEDGE section: ⏳ **PENDING** toggle button (amber, shows count) + hint line; table now iterates `filtered`; pending rows get inline quick-answer input + amber **SAGUTIN** button (Enter saves). Div balance 528/528. |
| PostgreSQL (live) | All 9 seeded KB questions answered + active=true (order/register/payment/track/pickup/contact/return/delivery/branch — delivery answer in Taglish, no em-dash, ASCII-safe via psql; **hours left unanswered** for owner). |

**Verified live:** delivery question → llama answers with the live FREE-delivery fact (sources incl. `delivery`); unknown question → canned "nirecord po namin" reply + `pending-count` = 1 (test row deleted after); **order flow**: create SHOP-20260821-0004 → confirm (Kopiko 1238→1228, -10 pcs reserve) → cancel (→1238 restore) → test order deleted + sequence reset to 1; widget live on shop.jumongdev.com (chatBubble+sendChat present); catalog 682 products / 35 categories / product detail w/ image OK; deliveryFee=0/freeMin=0 kept (FREE delivery promo muna — user decision; phone still placeholder `09xx-xxx-xxxx` — user must supply). Deployed via WinRM + web copy. Git commit pending.

**Same-session refinement (user instruction):** widget must NOT present itself as AI and must NOT push customers back to Facebook/Messenger — "asa ecommerce kana, ihold ang customer dito". Changed: widget header → **"Customer Service"** (no 🤖 AI label, Messenger link REMOVED from widget header); greeting → "Welcome po sa Andengs Superstore online shop..."; unknown-answer fallback → **"Salamat po sa inyong katanungan! Ipa-check po namin ito sa aming team at babalikan po namin kayo dito sa chat. Samantala, pwede po kayong mag-browse at mag-order dito: https://shop.jumongdev.com 😊"** — NEVER says "hindi alam" or "nirecord ang tanong" (learning loop still saves the question silently for the owner's PENDING list); system prompt updated (no FB/Messenger redirects, keep the customer in the shop); dynamic contact fact → "magtanong dito mismo sa chat... Andengs Superstore, Naic, Cavite"; KB contact + return answers rewritten to hold the customer in chat (no m.me links). Verified live + API redeployed.

**Same-session perf (user: "mabagal mag reply"):** the server's Ollama (llama3.1:8b, **AMD Radeon RX 6600** = only ~8 tok/s; dev PC RTX 3080 Ti but Ollama NOT installed there — the old AGENTS.md dev-PC note is stale) made full JSON replies take 10-26s. Fixes: (1) **`keep_alive: "30m"`** per request + **NSSM `AppEnvironmentExtra OLLAMA_KEEP_ALIVE=-1`** on the Ollama service (model stays loaded 24/7 — no more 20-40s cold starts; server has 32GB RAM, model ~5GB); (2) **`num_predict` 300→120** (short customer-service replies); (3) **new `POST /chat/stream` (SSE)** — `BuildChatContextAsync` extracted from `GetBotReplyAsync` (returns `(messages, sources, fallback)`; shared by /chat, /chat/stream, Messenger bot); stream endpoint forwards Ollama's NDJSON as `data: {"d":"chunk"}` lines + `data: [DONE]` (rate-limited 20/min, same queue). Widget `sendChat` now uses fetch ReadableStream + TextDecoder, renders tokens live into the assistant bubble (addMsg returns the element). **Measured: first chunk ~2.7s** (was 10-26s full), full reply streams at ~8 tok/s, capped ~120 tokens.

### Same-session build (user request): Customer Accounts (Google Sign-In) + Price Gating + Sari-Sari Store Applications + mobile keyboard fix

User direction: **prices visible ONLY to registered (Gmail) members** ("purpose lang kailangan member lang ang pwede makakita ng price"); new sign-ups default to **retail**, profile has **Upgrade to Sari-Sari Store** (DTI upload → admin approval); **order requires login**; widget/answers hold the customer in the e-commerce (no FB/Messenger redirects); SHOP NOW + MESSAGE US buttons removed (hero/wholesale/footer Messenger+FB links); catalog sorted **in-stock first** then name (trimmed).

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `customers.google_sub` (+partial unique idx) + `customers.is_sari_sari`; `customer_addresses` (label/name/phone/address/is_default); `customer_sessions` (token PK, 30-day expiry); `google_auth` (id=1, client_id/secret/enabled); `sari_sari_applications` (customer_id, store_name, dti_file, status pending/approved/rejected, reviewed_at) + status idx; `online_orders.customer_id`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **Google OAuth**: `GET /auth/google/start` (redirect accounts.google.com, state cookie) + `GET /auth/google/callback` (code→token→id_token JWT decode (base64url) → find/create customer by google_sub (unique name loop) → `customer_sessions` insert → **`jshop` HttpOnly cookie** 30d → redirect shop.jumongdev.com/?logged=1). **Session helper** `CurrentCustomerId()` + `IsMember()` (cookie→session→customer). **Customer API**: `GET /customer/me` (profile+addresses+applicationStatus), `PUT /customer/me`, `POST/PUT/DELETE /customer/addresses` (auto first-address=default), `POST /customer/logout`, `POST /customer/sari-sari/apply` (multipart storeName+dtiFile → wwwroot/assets/dti/{guid}.{ext}, 5MB, jpg/png/pdf/webp), `POST /customer/sari-sari/apply`. **Admin**: `GET /sari-sari/applications?status=` (JOIN customers), `POST /sari-sari/applications/{id}/review` (approve → sets `customers.is_sari_sari`), `GET /sari-sari/pending-count`, `GET/POST /google-auth` (secret masked). **Price gating**: `ShopCatalog` → `{items, member}` shape + `price`=null + `priceHidden` when !member; `ShopProduct` same; **`cost` REMOVED from both** (never expose); catalog `ORDER BY (stock<=0), LOWER(BTRIM(name))` (in-stock first); `ShopCreateOrder` → **401 unless member**, stores customer_id. GOTCHA hit: routes written `dashboard/sari-sari/...` double-prefixed with controller route → 404 — fixed to `sari-sari/...`. |
| `JumongCloudAPI/wwwroot/shop.html` | **Viewport** `interactive-widget=resizes-content` + `visualViewport` resize listener (chat panel + checkout lifted above mobile keyboard). **Header** 👤 account button + green online dot. **Buttons removed**: hero SHOP NOW/MESSAGE US, wholesale MESSAGE US, footer Messenger/Facebook links. **Member flow**: `loadMe()` (cookie→/customer/me), `signIn()` → `/auth/google/start`; grid/detail show **`—` + "🔒 Sign in to see price"** + SIGN IN button for guests, `addToCart`/`openCheckout` gate on member; checkout prefills name/phone/address from default address. **Profile modal**: edit name/mobile, addresses CRUD (+SET DEFAULT), Sari-Sari status badge + apply box (store name + DTI file), SIGN OUT. Cache v2 stores `{items, member}`. |
| `JumongCloudAPI/wwwroot/index.html` + `components.js` | Sidebar `🏪 Sari-Sari Apps` (grp-pos, red `_ssBadge` pending count) + `🔑 Google Login` (grp-settings). Panels: `sariSariPanel` (filter pills pending/approved/rejected/all, DTI view link, APPROVE/REJECT, pending count) + `googleAuthPanel` (Client ID/Secret masked + enabled toggle + setup guide with redirect URI). |

**Verified live (test data cleaned after):** guest catalog → `member=False`, prices null/priceHidden; guest order → 401 "Sign in with Google to place an order"; test member session → /customer/me OK, address add (auto-default), member catalog prices visible (29.75), member order SHOP-20260822-0001 created; sari-sari apply (PNG upload → /assets/dti/...) → pending-count 1 → approve → `isSariSari=true`, applicationStatus approved; all test rows + DTI file deleted, customers back to 343, order seq reset. **PENDING (user action): Google Cloud OAuth Client ID+Secret** (console.cloud.google.com → APIs & Services → Credentials → OAuth Client ID → Web application → redirect `https://shop.jumongdev.com/api/dashboard/auth/google/callback`) → paste in dashboard → 🔑 Google Login panel → ENABLE. Until configured, `/auth/google/start` returns 400 and the shop sign-in button redirects nowhere. num_predict final = **250** (user: "sagot dapat intact" — 120 was truncating). Git commit pending.

### Same-session additions: Favorites + Product Suggestions + Checkout v2 (Blk/Lot/Subdivision + suggestions) + misc

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `customer_favorites` (unique customer_id+product_id); `customer_addresses` + `online_orders` ADD `block`/`lot`/`subdivision` columns; `subdivision_suggestions` (name, customer_id, status pending/approved/dismissed); **seed `shop_content.subdivisions`** = 24-item Naic subdivision list (Pasinaya North/West, Pagsibol 1&2, Ciudad Neuvo Ph1-5, Hills View Royal Ph1-5, Pasinaya Homes Central, Pagsibol Village South west phase 5/3B/4A, Pagsinnag Place South, Pasinaya Homes Pasinaraw Prime Central, Pasinaya Homes Prime North, Pagsinag Place West/North East, Pasinaya Homes Hilaga, Pagsinag Place East). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **Favorites**: `GET /customer/favorites` + `POST/DELETE /customer/favorites/{id}`. **Subdivision suggest**: `POST /customer/subdivision-suggest` (dedupe: no pending dup + not already in list) + `GET /subdivision-suggestions?status=` + `POST /subdivision-suggestions/{id}/approve` (appends to shop_content.subdivisions) + `/dismiss`. **Orders**: `ShopOrderRequest` + INSERT + ShopGetOrders/ShopGetOrder now carry `block/lot/subdivision` (GOTCHA: the second reader's property block kept OLD indexes after the SELECT change → `GetDecimal` on text col 500 — fixed indexes 4-14). **Customer addresses** carry block/lot/subdivision. |
| `JumongCloudAPI/wwwroot/shop.html` | **Favorites**: 🤍/❤️ heart on every card + detail (member only; guest → sign-in prompt), **❤️ Favorites** filter chip in category bar (`favOnly`), empty-favs friendly message, grid re-renders after every toggle (old code destroyed heart buttons + layout on click). **Checkout v2**: DELIVERY section = BLK + LOT inputs + **🏘️ Subdivision locked picker** (24 list + "➕ Iba pa — mag-suggest") + details + note; "+ ADD ADDRESS" button → saves to profile as default → re-prefills checkout; auto-fill from default address (force); validation before PLACE ORDER (name, PH mobile `09xx` regex, Blk, Lot, Subdivision); `subValue()`/`onSubdivisionChange()`/`fillSubdivisionSelects()`/`suggestSubdivision()` helpers; profile address form + MY ADDRESSES display "Blk X Lot Y, Subdivision". **"You may also like"** suggestions in product detail (same category, in stock, excl current, 6 items). Item count ("N items") removed per user. |
| `JumongCloudAPI/wwwroot/index.html` + `components.js` | Online Orders detail modal: Delivery Address shows Blk/Lot bold + Subdivision (violet) + details. Shop Content panel: new **DELIVERY** group with `subdivisions` textarea + **🏘️ SUBDIVISION SUGGESTIONS** card (pending list w/ red count, ✔ ADD TO LIST / ✕, auto-loads via init). |
| ComfyUI (dev PC) | User has Z-Image Turbo (logo/text) + Flux.1 Schnell fp8 + Wan2.2 video + qwen3-4b CLIP (type lumina2) + ae VAE — 4 logo-variant workflows built (image_z_image_turbo template: UNETLoader + CLIPLoader lumina2 + ModelSamplingAuraFlow shift 3 + KSampler 8 steps cfg 1 res_multistep/simple + EmptySD3LatentImage) — QUEUED then REMOVED (user was using ComfyUI); regenerate anytime. |

**Verified live:** favorites add/list/remove (test session cleaned); suggest → pending → approve → 25th subdivision in list → rolled back to 24 (cleanup); order with Blk 7/Lot 15/Ciudad Neuvo Phase 2 stored + returned in detail; live shop has coBlock/coSubdivision/suggest; subdivisions count = 24 via /shop/content. Git commit pending.

### Same-session build: DELIVERY DRIVER SYSTEM (assign driver + driver app + payment collection + payment history everywhere)

User request: admin assigns a driver to each delivery; a **driver app** shows the customer/address; at the door the driver collects payment — Cash (input amount) / GCash (ref + **proof picture upload**) / Split (cash+gcash); ACCEPT → order PAID + delivered; customer sees payment history in MY ORDERS; the GCash picture is visible in customer app + driver app + dashboard. | `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `online_orders` ADD `driver_id`, `paid_status` (unpaid/paid), `paid_at`, `delivered_at`; new `order_payments` (order_id, driver_id, method cash/gcash, amount, gcash_ref, gcash_pic, created_at). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **Driver APIs**: `GET /drivers` (users role='Driver' active), `POST /orders/{id}/assign-driver` (sets driver + confirmed→shipped), `POST /driver/login` (username+password_hash, role=Driver → whapp_tokens), `CurrentDriverId()` (Bearer whapp token + role Driver), `GET /driver/orders` (assigned, shipped/delivered, unpaid first), `GET /driver/orders/{id}`, `POST /driver/orders/{id}/pay` (multipart: payments JSON `[{method,amount,gcashRef}]` + pic0/pic1 uploads → validates sum==total → order_payments rows (gcash pics → wwwroot/assets/payments/{guid}.ext) → order paid+delivered). `LoadOrderPayments()` helper. ShopGetOrders/ShopGetOrder + /customer/orders now carry driverId/driverName/paidStatus/paidAt + payments[]. GOTCHAS hit: routes double-prefixed `dashboard/` → 404 (fixed); System.Text.Json is case-SENSITIVE by default → OrderPaymentDto needed `[JsonPropertyName("amount")]` etc. or "Invalid amount" (amount never parsed). |
| `JumongCloudAPI/wwwroot/driver.html` | **NEW driver mobile app** (admin.jumongdev.com/driver.html): login (username+password, role Driver) → MY DELIVERIES list (PAID/TO COLLECT badges, customer+address) → order detail (items, total) → 💵 COLLECT PAYMENT screen: CASH (input received → auto change) / GCASH (amount+ref+**proof picture via camera**) / SPLIT (cash+gcash parts) → validation (sum == total) → ✔ ACCEPT PAYMENT. |
| `JumongCloudAPI/wwwroot/index.html` + `components.js` | Online Orders detail modal: **🚚 DRIVER** card (driver dropdown + ASSIGN → marks shipped, shows assigned name + ✅ PAID) + **💳 PAYMENT HISTORY** list (method badge, amount, driver, date, GCash ref + PICTURE ↗ link). Users modal: **Driver (delivery)** role option. |
| `JumongCloudAPI/wwwroot/shop.html` | MY ORDERS: **✅ PAID** green box + per-payment lines (amount, method, GCash ref, PICTURE ↗ link). |

**Verified live (test data cleaned after):** created test driver → login → assign order 2 → driver list/detail OK → split payment (cash 8.75 + gcash 20.00 + PNG pic) accepted → dashboard detail shows both payments + driver name + pic URL; order_payments rows + gcash file on server; then reverted (payments deleted, order back to delivered/unpaid, driver user deleted). Real orders currently: SHOP-20260822-0001 (Abigail, ₱29.75, Blk 303 Lot 10 Pasinaya West) + SHOP-20260822-0002 (Rosabel, ₱28.75, Blk 303 Lot 4 Pasinaya West) — both delivered/unpaid (owner clicked MARK DELIVERED). **Owner action:** create driver accounts via Users panel (ROLE = Driver) → assign in order detail → driver uses admin.jumongdev.com/driver.html. Git commit pending.

### Same-session build 2: Customer QR + points eligibility, shop QR on receipt, delivery lifecycle, mobile fulfillment (v1.1.54 client / v1.1.47 API)

User policy (locked): **points only for ONLINE-registered customers** (have generated qr_code; manual/POS accounts = zero points even when attached — "pipilitin ang walk-in mag-register online"); points rate = existing PointsRate (200 default, ₱/point) + points_exempt per product; online orders earn at COMPLETE (paid+delivered). Receipt QR = shop.jumongdev.com on the **live retail receipt only** (NOT reprints — "yung resibo lang talaga"). Fulfillment (accept/pick/assign) lives in the **existing mobile app (whmobile.html)** + dashboard mirror — no role changes.

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `customers.qr_code` (+unique partial idx, backfilled for google_sub customers ONLY, format `AS-XXXXXX`); `order_timeline` (order_id/status/note/actor/at); `order_pick_items` (unique order_id+item_id); `driver_shifts`; `ecom_shifts` (+open seed); `order_payments.remitted/remitted_at/remitted_by`; `online_orders.arrived_at/cancelled_reason/cancelled_by`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **Customer QR**: generated in Google callback; `/customer/me` returns qrCode+loyaltyPoints; `/warehouse/customers` returns qrCode (POS sync). **Status flow**: 'arrived' added; timeline logged on every status change (Admin/Driver/Delivered). **Driver**: `POST /driver/orders/{id}/arrived`, `POST /driver/orders/{id}/cancel` (reason≥3 chars, shipped/arrived/confirmed only, restores HQ stock + trail 'Driver-Cancel'), `POST /driver/return-to-hq` (upserts driver_shifts with today's unremitted totals). **Remittance**: `GET /remittances` (open shifts + unremitted payments) + `POST /payments/{id}/remit`. **Close day**: `GET /ecom-shift` + `POST /ecom-shift/close` (tallies delivered today, carried-over = confirmed/shipped/arrived, opens new shift). **Picking**: `POST /orders/{id}/pick`; ShopGetOrder returns per-item `picked` + pickProgress + timeline + arrivedAt/cancelledReason. **Points**: `AwardOrderPoints` in DriverCollectPayment — eligible = customer_id exists AND qr_code non-empty; rate from store_settings PointsRate (HQ); exempt/points_per_unit honored. GOTCHAS hit: routes `dashboard/...` double-prefixed AGAIN (remittances/ecom-shift → 404, fixed); pay guard initially blocked 'arrived'. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **🛒 Online Orders** burger-menu section (`tabOnlineOrders`): filter pills ALL/PENDING/PREPARING/DELIVERY, order list, detail modal with item checkboxes (live pick state), ACCEPT (pending→confirmed), ASSIGN DRIVER dropdown (confirmed), 🖨️ PRINT PICK SLIP (Bluetooth via AndroidPrinter), cancelled reason shown. |
| `JumongCloudAPI/wwwroot/driver.html` | Order detail (unpaid): **📍 ARRIVED AT CUSTOMER** + **✖ CANCEL ORDER** (reason modal) + 💵 COLLECT PAYMENT; main header **🏠 RETURN TO HQ** (reports collected totals). |
| `JumongCloudAPI/wwwroot/shop.html` | Profile: **⭐ MY POINTS** card + **customer QR display** (qrcodejs CDN render of qrCode) + Customer ID + "ipakita sa cashier"; MY ORDERS shows **order timeline trail** (status+time) + paid payments + PICTURE links. |
| `JumongCloudAPI/wwwroot/index.html` + `components.js` | Online Orders: order modal **Pick checkbox per item** + PICKED x/y progress + **🕐 Order Trail** list; **💰 DELIVERY REMITTANCE** card (unremitted payments, ✔ ACCEPT); **🌙 E-COMMERCE DAY** card (delivered/cash/gcash + carried-over list + CLOSE DAY). |
| `JumongCloudAPI/wwwroot/landing.html`, `manifest-driver.json` | (earlier) driver app PWA manifest. |
| **POS client** | `PrinterService`: `PrintReceipt(..., bool includeShopQr=true)` + `ShopQrAscii` (25x25 half-block QR, 13 lines) printed after footer on LIVE retail prints; reprints (ReportsForm/StockMovementForm) pass `includeShopQr: false`. `Customer.QrCode` property; SQLite `ALTER TABLE Customers ADD COLUMN QrCode`; SyncService customer download maps qrCode; `CustomerService.GetByQrCode`; SalesForm **📱 SCAN QR** button (dialog → lookup → attach, shows ⭐EARN/⛔no points); points award gated on `QrCode` non-empty. AppVersion → **1.1.54**. |

**Verified live (test cleaned):** /customer/me → qrCode AS-3B16AE + points; full flow on test order SHOP-20260822-0003 (₱254, customer 1454 eligible): confirm → assign ken → driver arrived → pay cash 254 → **points +1** (254/200) → timeline confirmed/arrived/delivered → return-to-HQ (shift: 1 delivered, cash 254) → remittances shows payment → remit OK → close-day OK → picking toggle OK. All cleaned (order/payments/timeline/pick rows deleted, stock restored +20 Kopiko, points reverted, sessions/shifts deleted). GitHub release v1.1.54 (asset 524847553) created; client drop pushed; API 1.1.47 deployed. Git commit pending.

## Previous Change (2026-08-21) — v1.1.45 (Cloud API) + web: WAREHOUSE RETIRED — all wh_products UI removed (warehouse totally shifted to server products)

**Request:** "NGAYON ANG WAREHOUSE TOTALLY SHIFTED TO SERVER PRODUCT" — user confirmed the warehouse (wh_products) is fully drained and retired; chose **"Tanggalin lahat ng warehouse UI"**. Verified first: 402 wh_products items ALL at 0 stock, 0 pending transfers, last warehouse-source sale 2026-08-19 (v1.1.38 switch); the 170 warehouse sales / ₱699k in the last 7 days were all pre-08-20 history. Action: warehouse UI removed everywhere; **history preserved** (wh_walkin_sales, wh_stock_trails, wh_transfers, wh_daily_closes untouched for audit).

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`/stock-status` warehouse UNION branch REMOVED** — response now `products`-only (no STORE-WAREHOUSE rows; VIEW STOCK dialog + stock views are POS stores only). Version → `"1.1.45"` (2 places). All other wh_* endpoints left intact (still used by mobile app for HQ-stock flows). |
| `JumongCloudAPI/wwwroot/index.html` | Sidebar **grp-wh (WAREHOUSE) nav group + wh-* items REMOVED** + its `_whBadge` span; the entire ~1040-line **warehouse section** (x-data warehousePanel: products/inventory/online-order/transfer/receiving/sales subpages + Add/Edit/Order/Transfer modals) DELETED (div balance 511/511 verified). Store Transfer (HQ→POS, grp-inv) untouched. |
| `JumongCloudAPI/wwwroot/components.js` | **`warehousePanel` Alpine component deleted** (546 lines) + store state cleanup: `whSubpage`, `_whBadge`, wh-* groupParents, grp-wh isGroupActive, switchSection wh-* branch, switchWhSubpage, isActive wh-* branch. `storeTransferPanel` kept. productStockDialog store order list: STORE-WAREHOUSE removed. |
| `JumongCloudAPI/wwwroot/app.js` | wh-* CSV export branches (wh-product/wh-inventory/wh-inventory-activity/wh-onlineorder/wh-transfer/wh-receiving) + `cache[name.replace('wh-','')]` fallback removed. |
| `JumongCloudAPI/wwwroot/whmobile.html` | Burger menu **WAREHOUSE section → TOOLS** (label only); **Online Order row + tabOrders div + goOrders/loadOrders/loadOrderItems removed** (the only remaining wh-bound tab — external client orders via wh_orders). Product/Inventory/Sales/Transfer/Receiving/Credit/EndShift kept (all `source=hq` server-product flows). Store switcher isWh cosmetics kept (harmless). |
| PostgreSQL | `UPDATE wh_products SET is_active=false WHERE is_active=true` → **UPDATE 402** (items hidden; rows kept for audit). |

**Verified live:** API v1.1.45; `/stock-status` = 2825 rows / **0 warehouse rows**; `/warehouse/products` active = 0; dashboard nav has no WAREHOUSE group; whmobile shows Tools + no ONLINE ORDERS. Deployed via WinRM (stop→copy→start, PSCredential from `Jum0ng!Dev55`). Git `56e822f`. No POS client release. NOTE: mobile app's wh_* API endpoints still exist server-side (all HQ-stock now); `order.html` (external client ordering) still reachable by direct URL but orphaned from menus — kept as-is unless user wants it removed.

## Previous Change (2026-08-21) — v1.1.44 (Cloud API) + web: INVENTORY VALUE panel (per-store + grand total, POS stores only)

**Request:** "ito kasi gusto ko makita dashboard para sa inventory cost and value ng buong store kabuohan at ng per store" — new Reports panel showing the CURRENT stock value of the whole company (cost basis + gross/SRP basis) per store and combined. User explicitly said **NO warehouse** (`wh_products` excluded). Also answered this session: the Master Catalog table does NOT show stock — its API `stockQty` is the legacy `master_products.stock_qty` column that no pipeline ever updates (the 30s snapshot writes `products`, not `master_products`). Added a TOTAL STOCK column to Master Catalog first (web-only, sums `/stock-status` per barcode, cached 30s) — user later clarified this value panel was what they actually wanted.

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **New `GET /dashboard/inventory-value`** — one query: `SELECT store_id, COUNT(*) items, SUM(stock_qty) units, SUM(COALESCE(cost,0)*stock_qty) costValue, SUM(COALESCE(price,0)*stock_qty) grossValue, zero-cost count FROM products WHERE is_active=true AND store_id != 'STORE-WAREHOUSE' AND store_id != 'STORE-DEV-0001' GROUP BY store_id ORDER BY cost_value DESC` → `{asOf, stores[], total{items,units,costValue,grossValue,zeroCostItems}}` (totals summed server-side — ONE request for everything). Version → `"1.1.44"` (2 places). |
| `JumongCloudAPI/wwwroot/index.html` | Sidebar leaf `{ id:'rpt-invval', icon:'🏦', label:'Inventory Value' }` in grp-reports (after Inventory Cost) + panel: 4 summary cards (Total Units / Total Cost Value / Total Gross Value / Zero-Cost Items) + per-store table (Store, Items, Units, Cost Value, Gross Value, Potential Margin) + **GRAND TOTAL row** highlighted + REFRESH + CSV. |
| `JumongCloudAPI/wwwroot/components.js` | New `inventoryValuePanel` Alpine component (load on section open + 60s auto-refresh + refresh-data event); `groupParents['rpt-invval']='grp-reports'` + both isGroupActive grp-reports checks updated. |
| `JumongCloudAPI/wwwroot/app.js` | `exportCSV('rpt-invval')` branch (reads panel x-data directly, incl. GRAND TOTAL row). |

**Verified live:** API v1.1.44; endpoint returns 4 stores (HQ 706 items/71,531 units/₱3,276,745 cost/₱3,355,342 gross · Naic E174 ₱983,059 · ACGS A80C ₱814,735 · HVR AA36 ₱542,834) → **GRAND TOTAL: 114,784 units · ₱5,617,373 cost · ₱5,775,344 gross**, zero-cost = 3 items across stores (amber ⚠ badge per store + card). Warehouse + DEV excluded per request. Deployed via WinRM (stop→copy→start, PSCredential built from `Jum0ng!Dev55` — never username-only, that pops a dialog). Git `7606255`. No POS client release (API + web only). NOTE: semantics = base per-piece cost × stock (same basis as end-shift inventory reconciliation); products with zero cost understate value (flagged, not estimated).

## Previous Change (2026-08-21) — Warehouse Inventory: 7 items aligned to the stock-trail ledger (Family A divergence fixed)

**Request:** "in warehouse dashboard can you check the inventory why the list view different to product trail in trail negative in view still have stock i know in previous convo in agents.md it was raise already this concern maybe change only specific item not as a whole" — the warehouse dashboard **inventory list (`wh_products.stock_qty`) disagreed with the product trail viewer (SUM of `wh_stock_trails.qty_change`) for 36 active items** — the same divergence family already noted in the v1.1.16 entry ("id 65 +432, id 20 +360 — pre-existing, NOT touched, owner aware"). Root cause (documented history): the old transfer-guard bug (pre-v1.1.16) wrote `transfer_out` trails even when the column deduction never landed, plus the earlier ~115k snapshot-trail cleanup / stock recalcs → ledger vs column diverged.

**Findings (queried server PG via WinRM):** 36 mismatches split into 2 families — **Family A (column HIGHER than trail, view shows stock the ledger consumed):** id 65 LUCKY ME KALAMANSI (col 432 / trail 0), 38 DATU PUTI VINEGAR 100ML (72/0), 98 DINGDONG SNACK MIX ORANGE (60/0), 75 DATU PUTI SOY 200ML (60/0), 73 DATU PUTI SOY 350ML (24/0), 201 CALLA POWDER FLORAL (480/435), 61 LUCKY ME CHICKEN (72/−72); **Family B (~29 items, trail went NEGATIVE, column already correctly clamped at 0):** id 20 Kopiko Black Twin Pack (−360), 21 Kopiko Blanca (−120), 283, 116, 42, 238, 272, 280, 269, 279, 344, 301, 302, 355, 246, 104, 109, 426, 427, 101, 247, 196, 12, 303, 99, 115, 100, 117, 193.

**Fix applied (user chose "Family A only"):** `UPDATE wh_products w SET stock_qty = GREATEST(0, COALESCE(t.s,0)) FROM (SELECT product_id, SUM(qty_change) AS s FROM wh_stock_trails GROUP BY product_id) t WHERE w.id = t.product_id AND w.id IN (65,38,98,75,73,201,61)` → **UPDATE 7**; verified: 65/38/98/75/73 → 0, 201 → 435, 61 → 0 (clamped; trail −72 is phantom). Remaining 30 mismatches are Family B where the VIEW is already correct (0) — only the trail HISTORY is phantom-negative, deliberately NOT touched (no negative stock, no trail editing). Same precedent as the v1.1.16 Mighty Green fix (view = trail ledger). GOTCHA re-learned this session: `New-PSSession -Credential <username-only>` pops the WinRM password DIALOG on the dev PC every time — always build the PSCredential from the known password (`Jum0ng!Dev55` in the Machine Roles section) to keep WinRM prompts silent.

## Previous Change (2026-08-21, 00:35) — ACGS (STORE-20260626-A80C) Agent DEAD since 00:23:16 PH + POS slow-load investigation (OPEN — no fix yet)

**Request:** "can you check ACGS the apps load slow maybe there is a conflict again in ip" — investigated via agent + cloud PG. **FINDINGS (agent channel to ACGS is DOWN right now):** (1) ACGS agent heartbeat **stopped at 00:23:16 PH** (10+ min stale, verified across repeated polls) and the last 4 diagnostic commands (incl. trivial `SELECT 1`, cmd ids 17-20) were NEVER processed → agent channel dead; (2) the ACGS **POS app started 00:19:49** and was still at **73MB RAM at 00:23** (still loading/initializing — consistent with the user's slow-load report); (3) machine identity verified via the one successful `ps` diag (cmd 2) BEFORE it died: IP **192.168.0.103** (correct, matches table), GW 192.168.0.1, machine DESKTOP-TK63MO6, boot 08/20 06:53 (uptime 17.5h, no reboot), **SSD** disk (MediaType 12, 278GB free) → NOT the HDD slow-start issue; (4) cloud PG: ACGS last sale sync 18:01 PH, last daily close 18:38 PH — store closed for the night, no missing data (normal); (5) the 22:50-22:52 `FetchPromoMessage` errors on the AGENTS card were from OUR API restart during the v1.1.53 deploy (service down window), not ACGS; (6) direct ping from server AND dev PC to 192.168.0.103 and its gateway 192.168.0.1 = **False** — but that's EXPECTED (ACGS is on the separate 192.168.0.x segment; dev/server are 192.168.1.x — no route even when healthy; the agent is ACGS's only channel and it's dead).

**Working hypothesis (user's):** IP conflict on the 192.168.0.x segment — machine gets kicked off the network periodically → agent (3s heartbeat) stops + POS hangs on startup network calls (promo fetch, connection check, drain) → slow load. NOT yet confirmed (couldn't inspect the machine: no inbound route, agent down).

**Pending (next steps, awaiting user on-site check at the store):** (1) confirm Windows IP-conflict balloon / stuck loading screen / internet access on the ACGS PC; (2) fix at the ROUTER if confirmed (static/reserved IP for ACGS outside DHCP pool, or remove the conflicting device); (3) once the machine regains internet the agent auto-reconnects in ~3s → then resume diagnostics: check `error.log` tail + `SyncLog` (read via agent `sql` — GOTCHA: PS 5.1 on the agent machine CANNOT load the .NET 8 System.Data.SQLite.dll, so DB reads must use the agent `sql` command, NOT ps Add-Type; and crash-report POSTs from ACGS must use `https://admin.jumongdev.com/...` — the LAN `http://DESKTOP-I097OO9:5000` URL is unreachable from the 192.168.0.x segment, both diag1/diag2 POSTs failed silently for this reason); (4) ACGS is on app **1.1.45** (outdated=True) — needs UPDATE APP to 1.1.53 anyway once it's back.

**GOTCHAS learned this session (agent tooling):** (1) agent send request body fields are **`Type`** (not `command`) + `Payload` — wrong field name → the agent treats the payload as SQL (`SQL logic error near "$ip"`); (2) command ids are GLOBAL (`Interlocked.Increment(ref _cmdCounter)`, DashboardController.cs:5662) — don't assume sequential per-store ids; (3) the **dashboard AGENTS tab consumes the per-store results buffer** (return-then-clear, cap 50) — if the admin dashboard is open, background-poll your command's result IMMEDIATELY after the ~3-5s agent poll interval, and expect to find only stale leftovers on later reads; (4) agent results for ACGS stored: cmd 12 = `update` download of DeniseGlobe.jpg (82,700 B) to `C:\JumongPos\agent\..\assets\` — a QR push command from the dashboard that DID complete before the agent died.

## Previous Change (2026-08-20) — v1.1.53 (POS client): Receipt Audit No Longer Double-Counts Voided Receipts as DELETED/MISSING

**Request:** "voided receipt in pos client is also deleted mean because in report receipt said there is deleted resibo sa HQ 3 and voided 3 pa check na rin" — the dashboard Sale Profits **Deleted/Missing Receipts** column was showing **3 "deleted" resibo at HQ that were actually just VOIDED**. Verified against stored `receipt_audits` (PG): today's HQ audit had `voided_invoices = [INV-7159-20260820-0082, -0089, -0107]` AND `missing_invoices = [the SAME 3]` (Aug 18 had the same pattern with -0128). Root cause: `ComputeReceiptAudit()` gap detection (EndShiftForm.cs:307) filtered `AND IsVoided = 0` when building the sequential invoice-number list → voided invoices created phantom gaps (0081→0083, 0088→0090, 0106→0108) → the voided numbers got flagged as DELETED *in addition to* being counted as VOIDED. The receipts were NOT actually deleted — they exist with `IsVoided=1` (verified in cloud PG: `is_voided=t`, blank cashier_name, no microseconds on those rows).

| File | Change |
|---|---|
| `Forms/EndShiftForm.cs` | Gap-detection query: removed `AND IsVoided = 0` — voided invoices now fill the sequence, so only numbers genuinely ABSENT from Sales (hard-deleted, no stock-trail ref) trigger a gap. Step 3 (INV- stock-trail ref with no Sales row) still catches real deletions. |
| `Services/AppVersion.cs` | `"1.1.52"` → `"1.1.53"`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | `latestVer` → `"1.1.53"` (API stays 1.1.43). |

**Deployed:** client v1.1.53 (exe 211,383,740 B) → drop + **GitHub release v1.1.53** (release id 373817326, published, verified). Git `08874c8`. **PG data fixed immediately:** `UPDATE receipt_audits SET deleted_count=0, missing_invoices='[]'::jsonb, lost_value=0 WHERE store_id='STORE-20260602-7159' AND shift_date >= '2026-08-18'` (3 rows) → dashboard now shows Deleted/Missing = None for HQ. Future end shifts recompute correctly after stores UPDATE APP. GOTCHA this session: GitHub draft releases are NOT visible to unauthenticated API calls (`/releases/tags/<tag>` and `/releases/<id>` return 404 until published) — always send the Authorization header when checking an in-progress upload.

## Previous Change (2026-08-20) — v1.1.52 (POS) + v1.1.43 (API): Stock Movement SOURCE Column + E-Commerce Trails

**Request:** "pwede isabay pos client - product - stock movement - new column sana for reference - kunwari kung galing sa mobile transfer yung item kung sold sa pos or sold sa ecommerce or sold sa mobile para madali ko malaman kung ano source ng transaction" — two changes: (1) `StockMovementForm` (POS → Products → VIEW STOCK MOV'T) now has a **SOURCE** column derived from the trail Reference: `Transfer #`/`WH-Transfer` → **Transfer**, `SHOP-` → **E-Commerce**, `WH-`/`Wholesale`/`RECV-` → **Mobile**, everything else → **POS** (`DeriveSource()` helper; local receiving `RR-`, sales `INV-`, voids, adjustments = POS). (2) **E-commerce CONFIRM/CANCEL previously updated `products.stock_qty` with NO trail** — same lost-update family as the old transfer bug: the HQ 30s snapshot push would overwrite the reservation. Now `ShopUpdateOrderStatus` writes `stock_trails` rows (pos_id=-NEXTVAL, store=HQ, ref `SHOP-<orderNo> -> <customer>`, user 'E-commerce', channel='ecommerce', before/after captured) so the offset formula keeps the reservation AND the HQ pull applies it locally → visible in Stock Movement with SOURCE=E-Commerce.

| File | Change |
|---|---|
| `Forms/StockMovementForm.cs` | New SOURCE column (Width 95, centered) after TYPE; `CellFormatting` fills it via new `DeriveSource(StockTrail)`; TYPE column now 110 fixed + REFERENCE gets Fill. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | `ShopUpdateOrderStatus` rewritten item loop: resolves HQ product row (`pos_id, stock_qty, name` by barcode), guarded UPDATE by pos_id, then INSERT trail (pos_id<0, ±pcs, before/after, ref `SHOP-...`, channel='ecommerce'). Version → `"1.1.43"`; `latestVer` → `"1.1.52"`. |
| `Services/AppVersion.cs` | `"1.1.51"` → `"1.1.52"`. |

**Deployed:** API v1.1.43 live; client v1.1.52 (exe 211,383,740 B) → drop + **GitHub release v1.1.52** (release id 373791532, published, verified). Git `9e672fb`. Stores via UPDATE APP.

**Also answered this session:** the POS sidebar **📊 Inventory Count** button = stocktaking viewer (`InventoryHistoryForm`); it is BLANK because sessions are created from the MOBILE counting page served by the POS itself at `http://<POS-IP>:5002` (PIN `1234`, `InventoryWebServer`) — start a session on the phone (same store Wi-Fi), scan/count items, end session, then the POS shows session/variance/report. Not a bug — no sessions yet.

## Previous Change (2026-08-20) — v1.1.51 (POS client): End-Shift HISTORY REPRINT Previous-Inventory Bug FIXED (phantom SHORT)

**Request:** "bakit kasi sa resibo iba ang pag print ng reconciliation mo pwede paki check" + "inventory reconciliation ng u got mart need ko ng buong paliwanang bakit short ng 146553.85" — the U Got Mart Aug 20 shift is actually **BALANCED** (Expected = Previous ₱925,825.19 + Received ₱263,568 − COGS ₱117,014.15 = ₱1,072,379.04 = Actual). The **SHORT ₱146,553.85 came from the history REPRINT path only**: `btnHistory_Click` reprint used `GetLastInventoryCost()` as "Previous Inventory" — by reprint time that returns the NEWEST close (the shift being reprinted itself: ₱1,072,379.04), not the close before it → Expected inflated by exactly (current − previous) = **146,553.85** → phantom SHORT. The LIVE close path (line 204) calls GetLastInventoryCost BEFORE saving, so live receipts/emails were always correct; only reprints were wrong.

| File | Change |
|---|---|
| `Services/DailyCloseService.cs` | New `GetPreviousInventoryCost(int currentCloseId)` — `SELECT TotalInventoryCost FROM DailyClose WHERE Id < @id ORDER BY Id DESC LIMIT 1`. |
| `Forms/EndShiftForm.cs:431` | Reprint path: `var prevInv2 = since != null ? GetLastInventoryCost() : 0m;` → `var prevInv2 = DailyCloseService.GetPreviousInventoryCost(dc.Id);` |
| `Services/AppVersion.cs` | `"1.1.50"` → `"1.1.51"`; API `latestVer` → `"1.1.51"` (API stays 1.1.42). |

**Deployed:** client v1.1.51 (exe 211,383,740 B) → drop `C:\JumongAPI\client\` + **GitHub release v1.1.51** (release id/asset uploaded+published, verified). API redeployed (latestVer). Git `42818b7`. Stores reprint correctly after UPDATE APP. NOTE: the already-stored daily_closes rows are CORRECT (they store the live-computed values) — only the reprint computation was wrong, so no data fix needed.

## Previous Change (2026-08-20) — web-only: Mobile TRANSFER Tab Now Uses HQ/Server Products (source=hq)

**Request:** "in mobile you said its server product but with transferring old warehouse stock" — the mobile app's TRANSFER tab was still moving OLD warehouse stock while SELL/INVENTORY/RECEIVING had already switched to HQ/server products (v1.1.38). Now consistent: mobile TRANSFER = **HQ→POS** transfers from server `products`.

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/whmobile.html` | `searchTxProducts()` → product picker now `&source=hq` (HQ/server products + stock). `createTransfer()` body → `source: 'hq'` (WhCreateTransfer hq branch: validates HQ products, WhReceiveTransfer hq branch deducts server HQ stock + trail `channel='transfer'` on receive; HQ POS sees the deduction in ~10s via the stock pull). `loadTxClients()` → excludes `STORE-20260602-7159` (HQ can't transfer to itself) + warehouse as before. |

**Kept:** dashboard WAREHOUSE → Transfer panel remains warehouse→HQ (for draining any remaining wh_products stock into HQ). Receiving stores still pick transfers up via PENDING TRANSFERS (unchanged). Web-only — no API change, no version bump. Deployed live (verified `source: 'hq'` + client filter). Git `6e48066`.

## Previous Change (2026-08-20) — v1.1.42 (Cloud API) + web: Shop Content Panel + Shop Landing Redesign (dashboard-editable)

**Request:** "pwede ba mag karoon sa dashboard ng mga pwede sasagutan para sa mga content for the mean time mag lagay ka ng default tapos edit ko sa dashboard" — the shop landing page content is now **editable from the dashboard** (sidebar POS CLIENT → 🛍️ Shop Content): hero title/subtitle/CTA, wholesale banner, 4 trust badges, delivery coverage, pickup address, phone, messenger/fb links, about text — stored in a new `shop_content` key-value PG table with seeded defaults. shop.html now has the **full landing redesign**: HERO section (gradient + CTAs SHOP NOW / MESSAGE US), TRUST BADGES row (🚚🏪💵📱), WHOLESALE banner, SHOP BY CATEGORY tiles (9 groups mapped to real catalog categories incl. dynamic **Wholesale Bundles** = products with bulk units), ABOUT/WHY-US section, and a 4-column footer. Content is cached 5 min in localStorage (`shop_content_cache`) like the catalog.

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `shop_content` table (key TEXT PK, value, updated_at) + seed of 18 defaults (hero_title 'Fresh Groceries Delivered to Your Door', hero_subtitle, hero_cta, wholesale_banner, trust_* ×8, delivery_coverage, pickup_address, phone, messenger_link, facebook_link, about_text) `ON CONFLICT DO NOTHING`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`GET /shop/content`** (public dict) + **`POST /dashboard/shop-content`** (upsert each key, returns `{saved}`). Version `"1.1.41"` → `"1.1.42"` (2 places). latestVer unchanged (1.1.50 — no client release). |
| `JumongCloudAPI/wwwroot/index.html` + `components.js` | Sidebar item `{ id:'shop-content', icon:'🛍️', label:'Shop Content' }` in grp-pos + `groupParents['shop-content']='grp-pos'`; new **`shopContentPanel`** Alpine component (fields list with groups HERO/WHOLESALE/TRUST BADGES/CONTACT-ABOUT, load/save) + section UI (2-col field grid, REFRESH + 💾 SAVE ALL). |
| `JumongCloudAPI/wwwroot/shop.html` | Landing redesign: hero (`heroTitle/heroSub/heroCta` + `data-msg` buttons), trust badges (`trust*` ids), wholesale banner (`wholesaleText` + MESSAGE US), `#shop` category tiles (`catTiles` + `CATEGORY_GROUPS` 9 groups, `renderTiles()/setGroup()`, group filter in `visibleProducts()`, `__bulk__` = units qtyPerUnit>1), about/why-us (`aboutText/coverageText/addressText/phoneText`), 4-col footer (`fbLink`, phoneText2). `CONTENT_DEFAULTS` fallback + `loadShopContent()` (localStorage 5-min cache, background refresh) + `applyContent()`. |

**Verified live:** API v1.1.42; `/shop/content` returns 18 seeded keys; shop.html live has hero/tiles/wholesale/footer; dashboard has shop-content nav + panel + SAVE ALL. Deployed via WinRM + web copy. Git `03b5b3f`.

**NOTES:** the shop page renders instantly from cached content + defaults even if the API is down; the "Message Us" buttons default to `https://m.me/jumongdev` until the owner edits the real links in the dashboard. Best Sellers section (top-sold endpoint) + Google Sign-In for customers = planned next (user asked about Google auth — needs the owner's Google Cloud OAuth Client ID/Secret first).

## Previous Change (2026-08-20) — web-only: Shop Mobile Caching (instant repeat opens on phones)

**Request:** "pag na load na sa cellphone sasave ba ito sa mobile nila para bumilis sa mga susunod na open?" — YES now. Previously every shop open re-fetched the catalog (~250ms warm, but adds up on phone data). Now: (1) `shop.html` caches the catalog in **localStorage (`shop_catalog_cache`, 5-min TTL)** — first paint renders instantly from cache on repeat opens, then background-refreshes from the server; offline → still shows the cached catalog (order CONFIRM still validates stock server-side, so stale stock ≤5 min is safe); (2) `GET /shop/product/{id}` now sends **`Cache-Control: public, max-age=300`** so phone browsers reuse the ~60KB base64 product images instead of re-downloading them every visit. No version bump (web + header only). Deployed live + publish copy; verified `Cache-Control` header + cache code live. Git `1441e6a`.

## Previous Change (2026-08-20) — perf: shop catalog 6.4s → 250ms + PG pool 300 (100-customer concurrency verified)

**Request:** "mga customer connecting to this server in 100 customer my lag?" — investigated server capacity for 100 concurrent shop customers. Found ONE real bottleneck: **`GET /shop/catalog` took 6.4s** (the LATERAL join on `products WHERE store_id=@sid AND barcode=mp.barcode` had NO index → scanned all 703 HQ products PER catalog row = ~485k row filters). Also raised the Npgsql pool (default 100 → 300) as cheap insurance.

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `CREATE INDEX IF NOT EXISTS idx_products_store_barcode ON products(store_id, barcode)` — LATERAL now uses an Index Scan (EXPLAIN cost 92,621 → 5,821; full query **6.7ms**). |
| `JumongCloudAPI/appsettings.json` | Connection string append `;Maximum Pool Size=300` (default 100 → headroom for 100+ concurrent customers). |

**Verified live (2026-08-20):** catalog warm = **~250ms** (was 6.4s cold + bad query — first call after restart is slow, always re-time warm); **40 parallel catalog requests via curl --parallel = 0.86s wall** (vs 8.8s sequential); **120 concurrent = 0 failures**. GOTCHA: PowerShell 5.1 `Invoke-WebRequest` adds ~2s/request overhead (IE proxy autodetect) — parallel PS tests show ~2s avg even for the 12ms `/version` endpoint; ALWAYS use curl.exe for real measurements. Baseline (curl, single warm): `/version` 12ms, `/stock-status` 106ms, `/warehouse/products?source=hq` 332ms, `/shop/catalog?withImages=false` 222ms. No version bump (config+migration only; API stays 1.1.41). Git `818479f`.

## Previous Change (2026-08-20) — v1.1.41 (Cloud API) + v1.1.50 (POS client): End-Shift v2 — Per-Channel Breakdown from PG (Phase 2 complete)

**Request:** "sige" — finish the remaining Phase 2 pieces: end-shift v2 (per-channel inventory reporting from PG, "same inventory, separate reporting") + `channel` tagging on stock_trails. The end-shift receipt/email now shows a **CHANNEL BREAKDOWN (SERVER)** section: wholesale mobile sales, e-commerce orders, server received pcs, HQ→POS transfer-out pcs — all from the server PG for the shift window (since the last daily close, fallback PH midnight). The local reconciliation math is UNCHANGED (local == server mirror via the pull, so it already balances); the server section adds the per-channel transparency on top.

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **New `GET /dashboard/end-shift-snapshot?storeId=`** — `since` = last `daily_closes.close_date` (fallback `date_trunc('day', NOW() AT TIME ZONE 'Asia/Manila')`); returns `{since, prevInventoryCost (last close total_inventory_cost), serverInventoryCost (Σ stock_qty×cost active), retail{count,total,cogs} (sales header-only + sale_items COGS `COALESCE(NULLIF(si.unit_cost,0),p.cost,0)*si.quantity`), mobile{count,total,cogs} (`wh_walkin_sales` + `si.stock_deduction × COALESCE(mp.cost, wp.box_cost/NULLIF(wp.box_qty,0),0)`), ecommerce{orders,total} (online_orders status confirmed/shipped/delivered), receivedPcs (Σ `RECV-%` trails pos_id<0), transferOutPcs (−Σ `Transfer #%` trails)}`. GOTCHA: `sales` uses `grand_total` NOT `total_amount` (42703 first deploy). Version → `"1.1.41"`; `latestVer` → `"1.1.50"`. |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `stock_trails ADD COLUMN IF NOT EXISTS channel TEXT NOT NULL DEFAULT 'other'` — set `'mobile'` (WhSell hq, WhCreateReceiving hq, WhVoidSale hq, WhEditSale hq) / `'transfer'` (WhReceiveTransfer hq). POS-synced local trails stay `'other'`. |
| `Services/SyncService.cs` (client) | **New `GetEndShiftSnapshot()`** (synchronous, HQ-gated, `GetAwaiter().GetResult()` + `ConfigureAwait(false)`; null on failure/non-HQ). Returns `(MobileSales, MobileTotal, EcomOrders, EcomTotal, ReceivedPcs, TransferOutPcs)?`. |
| `Services/PrinterService.cs` | `PrintAuditEndShiftReport` + `BuildAuditEndShiftReportLines` — new optional params `(mobileSales, mobileTotal, ecomOrders, ecomTotal, receivedPcs, transferOutPcs)` (defaults 0 → history reprints unchanged); prints **CHANNEL BREAKDOWN (SERVER)** section after INVENTORY RECONCILIATION when any value > 0. |
| `Services/EmailService.cs` | `SendEndShiftReport` — same optional params + **Channel Breakdown (Server)** HTML table (Wholesale (mobile) N sale(s) / E-commerce N order(s) / Received (server) +N pcs / Transfers out (HQ->POS) −N pcs). |
| `Forms/EndShiftForm.cs` | On save: `var channel = SyncService.GetEndShiftSnapshot(); var ch = channel ?? (0,0m,0,0m,0,0);` passed to both print and email calls. |
| `Services/AppVersion.cs` | `"1.1.49"` → `"1.1.50"`. |

**Verified live:** API v1.1.41 — `/dashboard/end-shift-snapshot?storeId=STORE-20260602-7159` → `{since: last close, prevInventoryCost: 3,072,828.27, serverInventoryCost: 3,296,951.67, retail: {3 sales, ₱421.25, cogs 406}, mobile: 0, ecommerce: 0, receivedPcs: 2500, transferOutPcs: 0}`. Client v1.1.50 (exe 211,383,740 B) → drop + **GitHub release v1.1.50** (release id 373211352, asset id 521116237, verified latest). Git `2a1ca27`. **Phase 2 is now COMPLETE** (live guards v1.1.49 + end-shift v2 + channel column). HQ gets everything via UPDATE APP.

**Also verified (2026-08-20, channel linkage):** e-commerce `GET /shop/catalog?withImages=false` returns **680 rows** (active + `sell_online=true` of 703) with `hqStock` from PG `products` via barcode join — DINGDONG 101 = server 101, GINEBRA ROUND 4,766 = server 4,766. CONFIRM reserves stock on the same table (v1.1.33). So all three consumers (HQ POS / mobile / e-commerce) read the ONE server inventory; the shop shows only sell_online items by design.

## Previous Change (2026-08-20) — v1.1.40 (Cloud API) + v1.1.49 (POS client): HQ Live Stock Guards (Phase 2, Option B — decision: B over A)

**Request:** user weighed Option A (pure live reads) vs B (live guards + local cache); they deferred the decision ("ikaw kung ano dapat") and I chose **Option B**: the POS display keeps the local mirror (instant, ≤10s via the Phase 1 pull), but the DECISION POINTS — add-to-cart, qty-change, and pre-pay — now check LIVE server stock for HQ. Guard formula: **available = min(local mirror, server live)** — a mobile/ecommerce delta (server lower) AND the HQ's own not-yet-pushed sales (local lower) are both respected, so the HQ POS can never oversell, even between pulls. Option A (rewrite ~50 SQLite read touchpoints to live calls) was rejected as high-risk for no extra business value.

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **New `GET /warehouse/stock-live?ids=1,2,3`** — returns `[{productId (=pos_id), stockQty}]` for `STORE-20260602-7159` (`pos_id = ANY(@ids)`, int[] Npgsql array param, cap 50 ids, empty ids → `[]`). The `products.stock_qty` column IS the live value (mobile sell/receive + transfer receive update it directly; snapshot UPSERT recomputes `@q + Σ(unapplied pos_id<0)`). Version → `"1.1.40"` (2 places); `latestVer` → `"1.1.49"`. |
| `Services/SyncService.cs` (client) | **New `GetLiveStockAsync(IEnumerable<int>)`** — HQ-gated (other stores → empty dict, zero API calls); `ConfigureAwait(false)` throughout so UI callers may block with `GetAwaiter().GetResult()` (LAN ~10-30ms) without deadlock; empty dict on any failure → caller falls back to local. |
| `Forms/SalesForm.cs` | **`AvailablePieces(productId, localStock)`** helper — HQ: `min(local, server)`; else local. Applied at the **add-to-cart guard** (AddToCart), the **qty-change dialog guard**, and a NEW **pre-pay guard** in `btnPay_Click` (batch live check of the whole cart by ProductId groups; blocks with an itemized message listing what changed server-side). Message text shows the live available count. |

**Deployed/verified:** API v1.1.40 live (`/warehouse/stock-live?ids=5628,5500` → 101 / 4766, matches local). Client v1.1.49 (exe 211,359,164 B) → drop `C:\JumongAPI\client\` + **GitHub release v1.1.49** (release id 373202414, asset id 521071806, verified latest). Git `fb2ac96`. HQ gets it via UPDATE APP. Non-HQ stores: `GetLiveStockAsync` returns empty → guards behave exactly as before (local-only). Snapshot push + 10s pull remain ON (they are the correct sync machinery; Phase 2's "push OFF + delta writes" was NOT needed — the offset formula already reconciles concurrency safely). **Remaining Phase 2 pieces: end-shift v2 (per-channel inventory reconciliation from PG) + optional `channel` column on stock_trails.**

## Previous Change (2026-08-20) — v1.1.48 (POS client): HQ POS Wholesale Form/Button REMOVED (mobile app now handles wholesale from HQ stock)

**Request:** "ang wholesale sa pos hq pwede na alisin?" — the HQ POS sidebar **🏪 Wholesale** button (`MainForm.cs` `btnWhSell_Click` + sidebar tuple) and the whole `Forms/WarehouseSellForm.cs` (warehouse-stock walk-in sell UI + WH-INVENTORY viewer + wholesale REPORT popup) are DELETED. Since v1.1.38 the warehouse mobile app (whmobile.html) handles wholesale selling/receiving from HQ stock, so the POS-side wholesale (which sold from the retiring `wh_products`/warehouse stock via `/warehouse/sell` without `stockSource=hq`) is redundant. Client AppVersion `"1.1.47"`→`"1.1.48"`; API `latestVer` `"1.1.47"`→`"1.1.48"` (API stays 1.1.39). Published (exe 211,350,972 B) + drop pushed + **GitHub release v1.1.48** (release id 373189616, asset id 521064238, verified latest). Git `29b92c0`. Wholesale REPORT/inventory/end-shift remain available on the mobile app; other stores unaffected (the button was HQ/DEV-only).

## Previous Change (2026-08-19) — v1.1.39 (Cloud API) + v1.1.47 (POS client): HQ Stock Pull IMPLEMENTED (Phase 1 of the HQ Stock Sync Roadmap)

**Context:** the plan session below (previous entry) was approved and Phase 1 is now BUILT + DEPLOYED. The pre-implementation check first CONFIRMED the bug live: server vs HQ local comparison showed local = 55,487 units vs server = 53,357 — a **+2,130 gap on exactly 5 items**, 100% explained by the owner's transfers #838 (20:06) + #840 (20:18) to ACGS (Ginebra Round −1,800, Ginebra Frasco −180, Cobra by12 −50, Mountain Dew by12 −50, Sting by12 −50). Server trails (`pos_id<0`, ref `Transfer #838/#840 -> ACGS - Naic Market`) proved the deductions exist server-side while HQ local never knew.

### What was implemented (Phase 1)

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `ALTER TABLE stock_trails ADD COLUMN IF NOT EXISTS applied_at TIMESTAMPTZ` (server deltas consumed by the HQ POS local apply). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | `WhStockSnapshot` offset → `@q + SUM(quantity_added WHERE store_id=@sid AND product_id=@pid AND pos_id < 0 AND applied_at IS NULL)`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **New `GET /warehouse/stock-deltas?storeId=&afterId=`** — unapplied `pos_id<0` trails (id, productId, productName, barcode, quantityAdded, stockBefore, stockAfter, reference, createdAt) ORDER BY id LIMIT 1000. **New `POST /warehouse/stock-deltas/ack`** `{storeId, maxId}` → `UPDATE ... SET applied_at=NOW() WHERE store_id=@sid AND pos_id<0 AND id<=@maxId AND applied_at IS NULL` (returns `{applied}`; validates maxId>0). DTO `WhStockDeltasAckDto`. Version → `"1.1.39"` (2 places); `latestVer` → `"1.1.47"`. |
| `Services/SyncService.cs` (client) | **New `PullStockDeltasAsync()`** — HQ-gated (`StoreId != "STORE-20260602-7159"` → return). Cycle: (0) retry pending ack if `StockPullAckedId < StockPullLastTrailId` (push snapshot + re-ack); (1) GET deltas `afterId=StockPullLastTrailId`; (2) apply each in ONE tx: `UPDATE Products SET StockQty += delta` + INSERT local `StockTrail` (`Synced=1` — never pushes back, ref copied, before/after from local current stock, UserName 'System') + cursor `INSERT OR REPLACE Settings StockPullLastTrailId` **inside the same tx** (crash-safe — rollback restores both); (3) `PushStockSnapshotAsync()` (delta push of affected products); (4) POST ack → save `StockPullAckedId`. Skips products missing locally. |
| `Forms/MainForm.cs` (client) | New **10s timer** calling `PullStockDeltasAsync()` (gate inside the method; non-HQ stores just return — zero API calls) + one run in the 3s startup drain task. |
| `Services/AppVersion.cs` (client) | `"1.1.46"` → `"1.1.47"`. |

**Verified live (server side):** API 1.1.39; `/warehouse/stock-deltas?storeId=HQ` returns exactly the 5 unapplied transfer trails (−2,130); ack endpoint verified (accidentally acked the 5 real trails during a maxId test → **UNDONE immediately**: `UPDATE stock_trails SET applied_at=NULL WHERE reference LIKE 'Transfer #838/%840%'`, test trail deleted; Ginebra Round server stock correctly self-corrected 6,566→4,766 on the next 30s push, proving the applied_at formula + push cycle works end-to-end). Client v1.1.47 published (exe 211,830,204 B) + drop pushed to `C:\JumongAPI\client\`. **GitHub release v1.1.47 created + published 2026-08-19** (release id 373178253, asset id 521045225, state=uploaded, verified downloadable). GOTCHA hit: `"$upUrl?name=..."` — PowerShell treats `upUrl?name` as ONE variable name (`?` is legal in PS variable names) → empty URL → "Invalid URI: The hostname could not be parsed"; use `($upUrl + '?name=...')`. Git `a592bdb`.

**NEXT: user taps UPDATE APP at HQ → first pull applies the −2,130 (local 55,487 → 53,357) as 'System' trails → HQ local mirrors server. MUST warn staff: HQ stock drops once (auto-reconcile) — expected, all trailable. After that, HQ local follows server within ~10s; transfers/mobile sales visible at HQ POS in ~10s. Remaining: Phase 2 (stock authority to PG, snapshot push OFF for HQ, delta writes, end-shift v2 per-channel reporting) — plan in the entry below. Optional pre-pay server check still undecided. NOTE: the stock-pull timer fires every 10s on ALL stores but returns instantly for non-HQ (gate inside the method).**

### Pre-implementation check (server vs HQ local stock, verified 2026-08-19 ~10:30 PM)

- Server PG `products` for `STORE-20260602-7159`: 703 rows (psql export). HQ local: 703 rows (read via **agent `sql` command** in 3 chunks — GOTCHAS: (1) `Copy-Item` of the live `JumongPos.db` fails "being used by another process" (POS holds it; a FileShare.ReadWrite stream copy DOES work but isn't needed); (2) PS 5.1 cannot load the .NET 8 `System.Data.SQLite.dll` ("Could not load type 'System.Object' from assembly 'System.Private.CoreLib'") — always use the agent channel instead; (3) agent results may contain duplicated chunk outputs — dedupe by barcode+name; (4) agent sql output = TSV with header row `Barcode\tName\tStockQty`, cap 500 rows/query).
- Result: 698 identical, 5 differ (all transfers #838/#840), 0 local-only, 0 server-only. Local 55,487 vs server 53,357 = **+2,130 exactly the transferred amount** — bug confirmed live.

## Previous Change (2026-08-19) — PLAN SESSION (approved 2026-08-19, Phase 1 now IMPLEMENTED — see above): HQ Stock Sync Roadmap — "One Live Inventory, Separate Channel Reporting"

**Context:** after v1.1.38 (mobile app now sells/receives from HQ stock on the server), the owner identified the core gap: **the HQ POS local SQLite never learns about server-side stock deltas** (HQ→POS transfers, mobile sales/receives) → HQ keeps selling stock that physically left or was already sold (oversell), and end-shift inventory reconciliation mismatches. Long discussion (options weighed: webhook-via-agent 3s poll, SQLite-over-SMB share, PG direct via LAN, stock-pull) produced the following AGREED architecture and phased plan. **The plan below was user-approved ("ok") on 2026-08-19 with the instruction: save this to AGENTS.md BEFORE implementing. Implementation starts with the server-vs-local stock comparison, then Phase 1.**

### Agreed end-state architecture ("same inventory, separate reporting")

```
        SERVER PG — products (ONE live inventory per item, STORE-20260602-7159)
           ▲            ▲            ▲
      HQ POS (Phase 2:    MOBILE app (DONE:    E-COMMERCE (DONE:
      stock ops via LAN   source=hq v1.1.38,   shop.html hqStock +
      API, push OFF,      sells/receives on    stock reservation on
      delta writes)       products)            confirm v1.1.33)
           │            │            │
     separate ledgers: sales / wh_walkin_sales / online_orders
     → separate sales reporting, ONE inventory
```
- Tagging: add `channel` column to `stock_trails` (retail/mobile/ecommerce/transfer/receive) so every movement shows who used it. Much tagging already exists (trail references, user_name Mobile/System, ledger tables).
- End-Shift v2 (HQ): money/cash = retail drawer only (unchanged); inventory reconciliation reads SERVER inventory cost (Actual) + per-channel movement lines (retail COGS / mobile wholesale / ecommerce / receivings) → one inventory, per-channel report.
- **Other stores (HVR/ACGS/Naic) are NOT affected** — all PG rows are store-scoped (`store_id`), and every HQ-only behavior is gated on `StoreId == STORE-20260602-7159` (same build for everyone, same pattern as the HQ-only Warehouse Sell button).

### Phase 1 — Pull @ 10s (HQ local becomes a mirror of PG) — NEXT TO IMPLEMENT

| Area | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `ALTER TABLE stock_trails ADD COLUMN IF NOT EXISTS applied_at TIMESTAMPTZ` (marks server-written deltas consumed by the HQ POS local apply). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | `WhStockSnapshot` offset formula → `@q + SUM(quantity_added WHERE store_id=@sid AND product_id=@pid AND pos_id < 0 AND applied_at IS NULL)`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | New `GET /warehouse/stock-deltas?storeId=&afterId=` → unapplied `pos_id<0` trails (id, productId, productName, barcode, quantityAdded, stockBefore, stockAfter, reference, createdAt), ordered by id, LIMIT 1000. New `POST /warehouse/stock-deltas/ack` `{storeId, maxId}` → `UPDATE stock_trails SET applied_at=NOW() WHERE store_id=@sid AND pos_id<0 AND id<=@maxId AND applied_at IS NULL`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | Version 1.1.38 → 1.1.39. |
| `Services/SyncService.cs` (client) | New `PullStockDeltasAsync()` — **HQ-gated** (`StoreId` check). Loop: (1) GET deltas where `id > StockPullLastTrailId` (Settings key); (2) apply each locally: `UPDATE Products SET StockQty += delta` + INSERT local `StockTrail` row (reference copied from server, Synced=1 — **never pushed back**, prevents duplicate server history); (3) `await PushStockSnapshotAsync()` (delta push of affected products); (4) POST ack with max id. Idempotent: save `StockPullLastTrailId` BEFORE applying, so a failed push/ack just retries next cycle without re-applying. Transient double-count window (push landed, ack not yet) is self-correcting on the next cycle. |
| `Forms/MainForm.cs` (client) | New 10s timer, HQ-gated + STORE-DEV-0001 skip; also one run at startup (after the 3s drain). |
| `Services/AppVersion.cs` (client) | 1.1.46 → 1.1.47; API `latestVer` 1.1.46 → 1.1.47. |

**First-run effect (EXPECTED + correct, must warn staff):** the first pull applies ALL historic unapplied server deltas (all past HQ→POS transfer deductions + today's mobile ops the local never knew) → HQ local stock drops once to match the server. One-time auto-reconcile, everything has a trail.
**Operating rule (already in force):** one entry point per movement — receiving on mobile = only mobile; HQ POS staff keep selling normally (their deltas flow through pushed snapshots). After Phase 1, HQ local mirrors server within ~10s so the POS stock guard (can't add out-of-stock) protects against oversell.
**Optional upgrade (user undecided):** pre-pay server stock check (zero oversell between pulls) — deferred.

### Phase 2 — Stock authority moves to PG (the "point HQ stock source to server via LAN" the owner asked for)

| Item | Detail |
|---|---|
| HQ snapshot push | **OFF** (HQ-gated) — HQ no longer full-pushes stock; it is direct. |
| HQ stock writes | Convert to **delta writes** (guarded `UPDATE products SET stock_qty = stock_qty ± N WHERE ... AND stock_qty >= N`) instead of absolute-set, so concurrent mobile/ecommerce writes never get clobbered (no lost update). |
| HQ stock reads | Option A (pure live reads — simple, stalls if server hangs) vs **Option B (live guards + local cache refreshed ~10s — recommended)**. User to pick at Phase 2 time. |
| End-Shift v2 | Inventory reconciliation from PG: new endpoint `/dashboard/end-shift-snapshot?storeId=HQ` returning per-channel shift summary (retail/wholesale/ecommerce/receivings) + current server inventory cost; receipt/email shows per-channel lines. Cash/denom math stays local retail. |
| Other stores | No change — local SQLite stays master, PG mirror, their pushes continue. Same build; all HQ behavior StoreId-gated. |

### What was REJECTED and why (recorded decisions)

| Idea | Verdict |
|---|---|
| SQLite file moved to server + HQ points to share | **NO** — SQLite over SMB has unreliable file locking, 2 concurrent writers (HQ POS + API) = corruption risk, mid-write disconnect can corrupt the DB. Client-server PG is the right container for a shared DB. |
| Webhook via agent (3s poll) routing all mobile/ecommerce ops through HQ local | **NO (as carrier)** — makes mobile/ecommerce stock view lag ~35s (they read PG; would wait for the 30s push-back) → oversell risk between devices; adds a second writer to the live POS SQLite. Agent channel may still be used later as an instant trigger only. |
| Full PG-direct rewrite of the POS app in one jump | **NO for now** — ~50 SQLite stock touchpoints, live store risk. Phased path (1 → 2) reaches the same end-state safely. |

### Data map (verified live 2026-08-19, PG already holds HQ data)

`hq_products=703, hq_sales=15,839, hq_trails=88,172, hq_closes=188, customers=341` — the HQ DB is ALREADY synced into PG; the gap is authority (local master pushes; PG is a copy), not data. Phase 1-2 flip the authority for INVENTORY only; sales/history stay local-first synced (that's what gives separate channel reporting).

### Status of pre-implementation check (server vs HQ local stock comparison)

- Server PG: `products` for `STORE-20260602-7159` (703 rows) exported to `C:\Users\ADMIN\AppData\Local\Temp\opencode\server_hq_stock.csv`.
- HQ local read ATTEMPT 1 failed: `LoadFrom(SQLite.Interop.dll)` throws BadImageFormat (it's a NATIVE dll, not an assembly). **Retry approach:** load ONLY `System.Data.SQLite.dll` from `C:\Users\ADMIN\Desktop\JumongPosHW\agent\` (interop auto-resolves from the same dir), open `JumongPos.db` with `Read Only=True`, stream `SELECT Barcode, Name, StockQty FROM Products WHERE IsActive=1` over the WinRM pipeline (no file written on HQ; the DB file itself is locked for Copy-Item by the running POS but SQLite readers are fine). Fallback: agent `sql` command via `/dashboard/agent/send` (buffer may be eaten by the dashboard AGENTS tab — poll immediately).
- Comparison method: match by barcode (trimmed, case-insensitive), fallback exact name for empty-barcode items; report matched-same/matched-diff (top 20 by |diff|), local-only, server-only, and total units per side. Expected: local total > server total by Σ unapplied server deltas (today's transfers + mobile ops).

## Previous Change (2026-08-19) — v1.1.38 (Cloud API) + mobile web: Warehouse Mobile App Now Uses HQ Stock (+ dashboard Warehouse Inventory stock filter + modal ADD button fixes)

**Request:** "in warehouse-inventory can you give me filter to see what else have stock so i can transfer to HQ" + "tapos yung mobile app from warehouse stock change to HQ stock" + "pati ang receiving stock goes to HQ na dating warehouse ang dagdag" — (1) dashboard Warehouse → Inventory now has **ALL / IN STOCK / OUT OF STOCK** filter buttons (with counts) to quickly find what can be transferred to HQ; (2) the **warehouse mobile app (whmobile.html) now sells/receives from HQ stock** (`products` table, `STORE-20260602-7159`) instead of `wh_products`; (3) receiving now ADDS to HQ stock (was warehouse).

### Mobile app → HQ stock switch (the big one)

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`WhGetProducts`** — new `?source=hq`: reads `products` WHERE `store_id='STORE-20260602-7159'`, `id = pos_id`, units via `master_products` **barcode** join (duplicate-barcode safe: json_agg subquery), price/cost from the products row (maintained by snapshot UPSERT), imageData from master by barcode. Same 12-column shape as the wh query (boxQty=1, piecePrice=price). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`WhSell`** — `WhWalkinSellRequest.StockSource` (`'hq'`); hq branch: product lookup + units (barcode join) + guarded stock check + `UPDATE products SET stock_qty = stock_qty - sd WHERE store_id='STORE-20260602-7159' AND pos_id=@pid` + trail `INSERT INTO stock_trails (pos_id=-NEXTVAL, store_id=HQ, product_id=pos_id, quantity_added=-sd, stock_before/after, reference='WH-… | customer | unit x qty | Mobile', user_name)`. Sale header writes `wh_walkin_sales.stock_source` (`'hq'`/`'warehouse'`). Warehouse branch unchanged. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`WhVoidSale`** — reads `stock_source`; hq → restores `products` + reversal trail (pos_id<0, `+dedQty`, ref `Wholesale [Partial] Void #{id}`). **`WhEditSale`** — same restore branch. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **Receiving** — `WhReceivingDto.StockSource`; `WhCreateReceiving` hq → `UPDATE products += qty` + stock_trails trail (pos_id<0, `RECV-… | supplier`). `WhGetReceivings`/`WhGetReceivingItems` — `?source=hq` reads stock_trails (`reference LIKE 'RECV-%'` grouped / per-ref items). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`WhInventorySummary`** — `?source=hq` → products-based (items/stock/cost/price/zero-cost). **`WhGetStockTrails`** — `?source=hq` → stock_trails for HQ store + computed `type` (CASE on reference: RECV→manual_receive, %Void%→void_return, WH-→walkin_sale, Transfer #→transfer_out, else manual_set). `quantity_added::int` cast (NUMERIC column). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`WhStockSnapshot` offset GENERALIZED** — was `@q - Σ(-quantity_added WHERE quantity_added<0 AND ref LIKE 'Transfer #%')`; now **`@q + Σ(quantity_added WHERE store_id=@sid AND product_id=@pid AND pos_id < 0)`** — ALL server-written trails (negative pos_id = never synced from a POS local DB; local-synced trails always carry positive local ids). This single formula covers transfer-outs (−), mobile hq sales (−), voids (+), mobile receiving (+) — verified live each direction survives the 30s full push. |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `ALTER TABLE wh_walkin_sales ADD COLUMN IF NOT EXISTS stock_source TEXT NOT NULL DEFAULT 'warehouse'`. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1400,1921` | Version bumped `"1.1.37"` → `"1.1.38"`. |
| `JumongCloudAPI/wwwroot/whmobile.html` | SELL search + unit-picker cache + product detail + INVENTORY list + inventory-summary + stock-trail detail + RECEIVING search/history/reprint now pass `source=hq` (sell body `stockSource:'hq'`, receive body `stockSource:'hq'`). **Transfer tab + orders + clients stay warehouse** (transfers still move warehouse stock → HQ). |

**Verified live (DINGDONG pos 5628, all cleaned after):** sell hq 101→100 + trail `walkin_sale −1 (101→100)` → **waited 50s → 100 stays** (offset survives snapshot); void #708 → 101 restored + reversal trail; receive +3 → 104 → **waited 50s → 104 stays** (positive offset survives); receiving history `?source=hq` shows the RECV entry (1 item, 3 pcs). Test rows deleted (sales/void logs/trails), stock restored 101. Web files + API deployed via WinRM stop→copy→start; version 1.1.38 verified. Git `4066bc6`.

**IMPORTANT operating rule (double-entry risk):** the offset model means deliveries received on the **mobile app (HQ)** and mobile sales are server-side deltas the HQ POS local DB never sees. **Never record the same delivery at the HQ POS locally too** — that would double the stock (local pushed value rises AND the positive offset stays). One entry point per movement: receiving via mobile = only mobile. HQ POS staff keep selling normally (their deltas flow through the pushed snapshot). If the user later wants a local copy of mobile receivings on the HQ POS, that needs a stock pull feature (future work).

### Same session, web-only (before the mobile switch)

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/index.html` + `components.js` | **Warehouse Inventory stock filter** — ALL (n) / IN STOCK (n) / OUT OF STOCK (n) pills in the WAREHOUSE — INVENTORY header; `warehousePanel.stockFilter` + `setStockFilter()` + `stockCounts`; `filtered` getter applies `(x.stockQty||0)>0` / `<=0` when `sp==='inventory'`. |
| `JumongCloudAPI/wwwroot/index.html` | **+ ADD button overflow fix (New Transfer — HQ to POS modal)** — product `<select>` `flex-1` had no `min-w-0` → min-content = longest option pushed qty/+ ADD off-screen (needed horizontal scroll). Added `min-w-0` to the selects (stTrfProd, trfProd, whOrdProd), `shrink-0` on qty inputs, `shrink-0 whitespace-nowrap` on the three + ADD buttons (store transfer, warehouse transfer, wholesale order modals). |

## Previous Change (2026-08-19) — v1.1.37 (Cloud API) + web-only: HQ→POS Transfer Fixes (clickable + NEW TRANSFER + deduction survives the 30s snapshot push)

**Request:** "i cannot click the new transfer in HQ - pos" + "stock trail pala d nagana" — TWO real bugs found in the v1.1.36 HQ→POS transfer feature:

**(1) Modal never opened (can't click + NEW TRANSFER).** The `stTransferModal` + `stTransferViewOpen` modals were placed AFTER the warehouse section in `index.html` — OUTSIDE the `storeTransferPanel` Alpine x-data scope (the panel div closes at the end of the transfer subpage). `x-show="stTransferModal"` resolved against the enclosing `warehousePanel` scope → ReferenceError → the modal stayed hidden forever, so clicking "+ NEW TRANSFER" did nothing. **Fix: moved BOTH modal blocks inside the `storeTransferPanel` div** (after the pagination `</template>`, before the panel's closing `</div>`). Fixed-position overlays work fine inside the section div (same pattern as the warehouse modals). Div balance 640/640, template 215/215; 115 lines moved (git diff = exactly 115+/115−).

**(2) HQ stock deduction UNDONE within 30 seconds (this is the "stock trail d nagana" + duplicated-stock bug).** `WhReceiveTransfer` (hq branch) deducts the HQ `products` row server-side and writes a `stock_trails` row (`pos_id = -NEXTVAL`, `product_id` = HQ pos_id, ref `Transfer #{id} -> {clientName}`, user `System`). BUT the HQ POS runs `PushAllUnsyncedAsync` every 30s, which sends its FULL local product list (local stock never knows about the transfer deduction) to `/warehouse/stock-snapshot` → the UPSERT `ON CONFLICT (store_id, pos_id) DO UPDATE SET stock_qty = @q` blindly overwrote the deduction back to the pre-transfer value → HQ stock NEVER actually decreased and the receiving store's +1 made stock DUPLICATED. **Fix (DashboardController.cs `WhStockSnapshot` ~line 3532):** the conflict UPDATE now subtracts outstanding HQ transfer deductions:
```sql
SET stock_qty = @q - COALESCE((
    SELECT SUM(-st.quantity_added) FROM stock_trails st
    WHERE st.store_id = @sid AND st.product_id = @pid
      AND st.quantity_added < 0 AND st.reference LIKE 'Transfer #%'
), 0), name = @n, barcode = @b, synced_at = NOW()
```
**GOTCHA (first fix attempt failed):** the trail's `pos_id` is the NEGATIVE `-NEXTVAL` (collision-avoidance), NOT the HQ pos_id — the subquery must match on **`st.product_id`** (= HQ pos_id), not `st.pos_id`. With `pos_id` it matched nothing → deduction still overwritten (verified live: 102→101→102). Switched to `product_id` → verified live: **102 → 101 (receive) → still 101 after 45s** (one full 30s push window passed). The offset model is accounting-correct forever: server stock = pushed local − Σ transfer-out trails (the local DB never applies those deductions itself; when HQ later receives/sells, the local delta still flows correctly).

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:3532` | `WhStockSnapshot` UPSERT `stock_qty` now subtracts outstanding HQ transfer-deduction trails (match `store_id` + `product_id`, `quantity_added < 0`, ref `LIKE 'Transfer #%'`). |
| `JumongCloudAPI/wwwroot/index.html` | Moved `stTransferModal` + `stTransferViewOpen` blocks inside the `storeTransferPanel` x-data div (fixes modal scope — + NEW TRANSFER now opens, form/items/save resolve correctly). |
| `JumongCloudAPI/Controllers/DashboardController.cs:1400,1921` | Version bumped `"1.1.36"` → `"1.1.37"`. |

**Verified live:** v1.1.37 (API), served page has modal inside panel scope (`x-data="storeTransferPanel"` before the modal comment, warehousePanel after). Live transfer tests #835/#836 (DINGDONG SWEET & SPICY 100G, HQ pos_id 5628, stock 102): receive → 101, wait 45s (snapshot window) → **101 stays** (fix #2 proven; pre-fix it bounced to 102). Test data cleaned after each run (stock restored to 102, trail + transfer rows deleted via psql). Deployed via WinRM stop→copy→start (GOTCHA: the FIRST copy ran while the service was live → `System.*.dll` locked → JumongCloudAPI.dll itself did NOT land (version still 1.1.36) — ALWAYS `net stop` BEFORE `Copy-Item`). Web files live + publish copy synced. Git pushed (`df84a74`). **Remaining:** none blocking. NOTE for the future: the 🛤️ Stock Trail panel reads each POS's LOCAL SQLite via agent — server-side HQ transfer deduction trails are NOT visible there by design (they live only in cloud `stock_trails`; visible via Stock Status / Store Transfer panel / psql). Offer to surface them in the panel if the user wants them on-screen.

## Previous Change (2026-08-19) — v1.1.46 (POS client): Stock Receiving Receipt + History Reprint — Item Names Wrap (80mm, no more truncation)

**Request:** "in pos client print what receivied can you fix the resibo in reprint to fit the name of the item in 80mm" + "d ko kasi mabasa sa pos client pag nag reprint sila ng history" + "item name wrap to second [line] - cur rcv after?" — the STOCK RECEIVING receipt (`PrintStockReceiving`) and the receiving HISTORY reprint (`PrintStockReceivingHistory`, via StockReceivingForm → HISTORY → PRINT) truncated long item names with `".."` (same bug family as the retail/wholesale receipts fixed in v1.1.40, but these two paths were missed). Long names are now WRAPPED onto continuation lines, and the **Cur/Rcv/New stats move to the END of the wrapped name (last line)** so the numbers stay readable.

| File | Change |
|---|---|
| `Services/PrinterService.cs` (PrintStockReceiving ~line 703) | Per-item loop rewritten: `WrapText(name, maxNameWidth - 2)` → first line = `chunk.PadRight(maxNameWidth)` (name only), continuation lines = `"  " + chunk.PadRight(maxNameWidth - 2)`, **stats `{0,4}{1,4}{2,4}` appended only on the LAST line**. Single-line names render exactly as before (name padded + stats on one line). |
| `Services/PrinterService.cs` (PrintStockReceivingHistory ~line 812) | Same wrap + stats-on-last-line layout for the receiving log reprint (per trail entry: ProductName, StockBefore, QuantityAdded, StockAfter). |
| `Services/AppVersion.cs` | `"1.1.45"` → `"1.1.46"`. |
| `JumongCloudAPI/Controllers/DashboardController.cs:5174` | `latestVer` `"1.1.45"` → `"1.1.46"` (repo only — API deploy PENDING, so the dashboard OUTDATED badge still says 1.1.45 until the next API deploy). |

**Result layout (long name, e.g. 45 chars at lineChars=41, maxNameWidth=31):** line 1 = first 29 chars of the name; line 2 = `  ` + rest of the name padded + `   4    5    9` (Cur/Rcv/New after the name, aligned with the header). No more `".."` cut-offs — full item name readable on 80mm.

**Deployed:** publish 0 errors → `C:\dev\out\client\` (exe 211,760,572 B @ 18:23, sha256 `9488513d…`, verified ≠ v1.1.45's `4d0bfc8b…`). GOTCHA hit: the dev PC's own agent (runs from `C:\dev\out\client\Agent\Agent.exe`) locks Agent DLLs → `Stop-Process -Name Agent` BEFORE publish, `Start-Process` AFTER (same as the v1.1.45 session). Also: an earlier publish's output was checked with a TRUNCATED filter (`Select-Object -First 20`) and a stale exe timestamp — always verify the final exe hash/timestamp against the previous version's hash before pushing. Client drop push to `C:\JumongAPI\client\` + GitHub release v1.1.46 = pending (PC shutdown risk — AGENTS.md saved first).

## Previous Change (2026-08-19) — v1.1.36 (Cloud API) + web-only: HQ→POS Store Transfer Panel + Snapshot UPSERT + Required-Fields Guard

**Request:** "gawa ka nalang ng bago sa menu under ng pos client same na same" + "gagamitin ko pa yuan to transfer all item in warehouse to HQ but for now need ko na ng HQ to pos also" — new HQ→POS stock transfer UI under POS CLIENT → Inventory, **same look/behavior as the warehouse transfer panel**, while the warehouse transfer stays intact (user still uses it warehouse→HQ, HQ = clientId 5). Also: 5 HQ products existed locally but never reached server `products` (server stock-status showed 698 rows vs local 703), and blank-barcode master products were creatable (partial unique index).

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **WhStockSnapshot → UPSERT** — INSERT (via LATERAL master_products barcode match for category/price/cost) ON CONFLICT (store_id, pos_id) DO UPDATE (stock_qty/name/barcode/synced_at). Server now auto-creates rows for products that never synced — parity fixed at the source (client unchanged; `PushAllUnsyncedAsync` full push every 30s created the 5 missing rows: pos 5900/5903/5933/5934/5937). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **CreateMasterProduct/UpdateMasterProduct required-fields guard** — Name + Barcode required (trimmed), Price > 0, Cost > 0 → 400 with message (closes the partial-unique-index hole for blank barcodes). |
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `wh_transfers ADD COLUMN IF NOT EXISTS source TEXT NOT NULL DEFAULT 'warehouse'` + index on (source, status); `DROP CONSTRAINT IF EXISTS wh_transfer_items_product_id_fkey` (FK was warehouse-only; HQ items store `products.pos_id` — barcode is the true linkage). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **WhGetTransfers** — new `source` query param + `source` field returned (column order: id0, client_id1, client_name2, status3, notes4, store_id5, **source6**, created_at7, updated_at8, has_shortage9, total_count10). **WhCreateTransfer** — `source` required (`warehouse`/`hq`); hq branch validates against `products` (HQ store `STORE-20260602-7159`), warehouse branch against `wh_products`; **stock held on create** (no deduction — same as warehouse). **WhGetTransferItems** — CASE source → HQ `products` stock vs `wh_products` stock. **WhReceiveTransfer** — reads `t.source`; hq branch: guarded UPDATE `products SET stock_qty = stock_qty - qty WHERE store_id='STORE-20260602-7159' AND pos_id=@pid AND stock_qty>=@qty` (0 rows → shortage, no trail), then `INSERT INTO stock_trails (pos_id, store_id, product_id, product_name, barcode, quantity_added, stock_before, stock_after, reference, user_name) VALUES (-NEXTVAL('stock_trails_id_seq'), 'STORE-20260602-7159', ...)` — negative pos_id avoids collision with POS-synced trail ids (UNIQUE(store_id, pos_id)); ref `Transfer #{id} -> {clientName}`; user_name 'System'. **WhGetPendingTransferCount** — `?source=` param. **WhTransferDto.Source** added. `/stock-status` now returns `posId` (last column idx 7; warehouse UNION branch `NULL AS pos_id`) — feeds the HQ transfer product picker. Version bumped `"1.1.35"`→`"1.1.36"` (2 places); `latestVer` `"1.1.42"`→`"1.1.45"` (DashboardController.cs:5095). |
| `JumongCloudAPI/wwwroot/components.js` | New `_stBadge` store state; `groupParents['st-transfer']='grp-inv'`; grp-inv active check += 'st-transfer'. warehousePanel transfer list + badge now filter `source=warehouse` (legacy rows preserved — 747 completed + 0 pending). New **`storeTransferPanel`** Alpine component (after warehousePanel): list/filter/pagination + `st*`-prefixed state (stTransferModal/stTransferForm/stTransferFormItems/stTransferViewOpen), `+ NEW TRANSFER` (product picker = `/stock-status?storeId=STORE-20260602-7159` using `posId`), save posts `source:'hq'`, badge poll `pending-count?source=hq` → `_stBadge` (30s interval while on subpage). |
| `JumongCloudAPI/wwwroot/index.html` | Nav leaf `🚚 Store Transfer` in grp-inv (Inventory) + red badge span (`_stBadge`); stock header title + tab bar third button; **Store Transfer subpage** (x-data storeTransferPanel, "HQ TO POS STORE TRANSFERS" toolbar + table + pagination, hint "Source: HQ stock · POS clients receive via PENDING TRANSFERS"); new `stTransferModal` (header "New Transfer — HQ to POS", POS client dropdown excludes HQ + warehouse, HQ stock product picker) + `stTransferViewOpen` modal (copies of the warehouse ones with st* names). |
| `JumongCloudAPI/wwwroot/app.js` | CSV export branch for `st-transfer` (reads `storeTransferPanel` transfers). |

**Impact:** (1) server↔HQ stock parity self-heals — 703 HQ rows now (was 698), all 5 missing products auto-created by the UPSERT (qty 0 = their pushed snapshot value; refreshes every 30s); (2) no more blank-barcode master products; (3) HQ can push stock to any POS client (HVR/Naic/ACGS) from the dashboard with the same UX as warehouse transfers — POS clients pick them up in PENDING TRANSFERS automatically (WhGetPendingTransfers unchanged, no client release). Warehouse transfer untouched for warehouse→HQ moves. **Deployed:** publish 0 errors → WinRM (net stop → copy → start), web files live, version `1.1.36` verified. **Verified live:** stock-status 703 rows + posId; `?source=hq` list empty; `?source=warehouse` = 747 (all legacy); create test transfer #830 (DINGDONG pos 5628, HQ stock 102) → pending hq=1, items currentStock=102 (HQ read), HQ stock UNTOUCHED on create (102) → cancel → pending 0, list shows cancelled, stock still 102 → test row deleted via psql. Insufficient-stock path: KOPIKO BLACK SINGLE (qty 0) → 400 "Insufficient stock for ...: only 0 available, 1 requested. Receive stock first." GOTCHA: PS 5.1 `ConvertFrom-Json` returns top-level JSON arrays UNENUMERATED — `@(Invoke-RestMethod)` wraps as ONE element (`$x.Count`=1, `$x[0]` = the array); use the raw result and index it.

## Previous Change (2026-08-19) — v1.1.45 (POS client): End-Shift Receipt Debt Collections split Cash/Wallet + Cash-on-Hand per-denom totals + HVR Machine Migration (new PC, always-on)

**Request:** (1) "separate collected in cash then total separate collected in wallet then total" — the end-shift receipt/email DEBT COLLECTIONS printed ONE combined `TOTAL COLLECTED` (e.g. 8,999.25 = cash 8,230 + wallet 769.25), so the cash-vs-e-wallet split wasn't visible (user: "debt collection is right but in the total the ewallet was gone" — wanted a grand total that still includes e-wallet); (2) "fix the print on cash on hand 1000 x 26 at end = 26,000" — the thermal print's CASH ON HAND showed only the count per denomination, not the computed amount.

| File | Change |
|---|---|
| `Services/PrinterService.cs` | `BuildAuditEndShiftReportLines` — DEBT COLLECTIONS split: cash payments rendered first with `TOTAL COLLECTED (CASH)`, then wallet payments with `TOTAL COLLECTED (WALLET)`, then a final grand `TOTAL COLLECTED` = all methods (e-wallet no longer missing from the total). CASH ON HAND rows now `"  1000  x  26"` / right `"= 26,000.00"` (per-denomination computed amount; Coins row `= 1,234.50`). |
| `Services/EmailService.cs` | Same split in the email Debt Collections table — separate rows/totals for CASH and E-WALLET (Payment Method column shows CASH/E-WALLET) + `Total Collected (All)`. Email already showed per-denom amounts in the denomination table, so only the print needed the per-row totals. |
| `Services/AppVersion.cs` | `"1.1.44"` → `"1.1.45"`. |

**Impact:** the end-shift report now separates what went into the cash drawer (Cash collections) from what went to GCash (Wallet collections) with per-method subtotals + an overall total; the thermal print's cash count shows each denomination's computed value (26 × ₱1,000 = ₱26,000). **Deployed:** Release publish (0 errors) → `C:\dev\out\client\` → WinRM push to `C:\JumongAPI\client\`. **GitHub release v1.1.45 created (2026-08-19)** (id 372358176) with exe asset (211,760,572 B, sha256 `4d0bfc8b…`, `state: uploaded`); stores get it via UPDATE APP.

**GOTCHAS this session:** (1) **publish FAILED until the dev PC's own agent was stopped** — the agent runs from `C:\dev\out\client\Agent\Agent.exe` and locks its own DLLs (MSB3021 "file is locked by Agent (pid)"); `Stop-Process -Id <pid>` before `dotnet publish`, then `Start-Process C:\dev\out\client\Agent\Agent.exe` after. (2) **GitHub upload poll condition must include BOTH `starter` AND `uploading`** — an in-flight upload sits in `starter` state, which a `-eq 'uploading'` check misses (loop exits early); include `starter` or the loop aborts while the 211MB upload is still running. (3) **agent result buffer is consumed by the dashboard AGENTS tab** — `/agent/results/{store}` returns-then-clears and any open poller (e.g. the web dashboard) eats results continuously, so agent `sql`/`ps` read-back is unreliable (results "not retrievable"). Workaround used: agent `ps` command POSTs diagnostics to `/dashboard/crash-report` (open endpoint → `crash_reports` PG table), then query the table via server psql; clean up rows after (`DELETE FROM crash_reports WHERE app LIKE 'powercfg-check%'`).

### HVR Mountain Dew trail (Aug 17) — +5 phantom surplus from Aug 16 quadruple-sale voids

**Request:** "check HVR mountain dew trail august 17 there is a problem". Verified local (agent) + cloud (PG psql) `STORE-20260602-AA36` Mountain Dew 290ml by 12 (local/cloud pos_id 308, barcode 14803925130325). **Aug 17 trail itself is CLEAN** (3 rows, all `Synced=1`, chains perfectly): 14:01 `-3` sale 0061 (7→4), 15:28 `+10` WH-Transfer #774 (4→14 — transfer created 15:25, completed, wh product id 52, warehouse now 0), 19:07 `-5` sale 0117 (14→9). **But the trail has a +5 discontinuity at the Aug 16 13:14 void restocks:** the 4 sales drove stock 7→4→1→-2→-5 (real chain), yet the 3 void restocks recorded `StockBefore/StockAfter` as **0→3, 3→6, 6→9** (base 0, not -5) → voids over-credited **+5** (recorded end 9 vs real 4). That +5 rode into Aug 17 (opening 7 instead of 2) → today's stock 9 ≈ **5 packs (~₱955 at cost 191) too high** vs physical ~4. Same root-cause family as the v1.1.44 double-pay incident (one duplicate sale's stock update didn't land consistently). No correction entered yet (cashier's claimed -5 adjustment never appeared in StockTrail — "Adjustment/return/over" search returned 0 rows).

### HVR receipt INV-AA36-20260817-0117 — wrong customer on the server

**Request:** "decalred customer is wrong encode". On HVR's LOCAL DB, `Sales.customer_id = 1` is a customer literally named **"WRONG ENCODE"** (a placeholder, not a person). On the CLOUD, `customers.id = 1` = **SUZAINE FANTONIAL** → numeric-ID mismatch: the synced sale displays SUZAINE but the printed receipt declared "WRONG ENCODE". **7 HVR sales** (Aug 17 0117, Aug 1 ×2, Jul 29, Jul 27, Jul 15, Jul 14) are all `customer_id=1` on the cloud. **Fix applied (server, verified `UPDATE 1`):** `UPDATE sales SET customer_id = NULL WHERE pos_id = 11908 AND store_id='STORE-20260602-AA36'` (0117 → walk-in; matches HVR convention — 417 sales since Aug 16 have no customer, only 2 have names). Local `UPDATE Sales SET CustomerId = 0` dispatched via agent (cmd 24) but execution NOT verified (agent-result gotcha above). Other 6 sales left untouched pending user decision (couldn't confirm those dates were also local CustomerId=1).

### HVR machine migrated to a NEW PC + set always-on

- **Agent status now shows `STORE-20260602-AA36` on `DESKTOP-U5BO3Q0` @ `192.168.1.100`** (was DESKTOP-TK63MO6 @ 192.168.1.15), app v1.1.45 — the store replaced/relocated the POS PC.
- **Inbound remote access blocked on the new PC** — tested from the dev PC: ping (ICMP), WinRM 5985, RDP 3389 ALL unreachable. Agent still works (dials OUT to the cloud). Same situation as HQ's lanfix (2026-08-15) — to enable RDP/WinRM needs the agent `ps` + `Start-Process powershell -Verb RunAs` → **one UAC click at the store** (not yet done).
- **Sleep set to NEVER** via agent `powercfg /change standby/monitor/hibernate-timeout-ac/dc 0`. **Verified** via `powercfg /query SCHEME_CURRENT SUB_SLEEP STANDBYIDLE` (posted to crash-report): AC and DC both `0x00000000` (Never). NIC: Realtek PCIe GbE, MAC `44-45-6F-0E-E1-62`.
- **WOL analysis:** the agent CANNOT wake a sleeping PC (OS suspended). WOL only works with a magic packet from **inside the branch LAN** — and **HVR is at a different physical location** (own router/NAT; the `192.168.1.100` is its local IP, not the office LAN), so remote WOL from the office/server is impossible without the branch router supporting WOL-from-WAN (on-site config). **Decision: keep HVR always-on (never sleep)** so Google Remote Desktop (outbound) + agent stay reachable 24/7. The earlier "can't access after close" was fixed by never-sleep.

## Previous Change (2026-08-17) — v1.1.44 (POS client): Pay Double-Submit Guard (no more duplicate same-cart sales)

**Request:** user pasted 3 identical HVR sales (INV-AA36-20260816-0002/0003/0004, all ₱1,085, Cash, Walk-in, 13:10) from the dashboard and asked what happened. **Investigation (server PG):** all 4 sales 0001-0004 (13:09:28→13:10:20, ~10s apart) were REAL distinct rows — each with its own `sale_items` (Camel Yellow 1×145 + Marlboro Red 2×176 + Mountain Dew by-12 3×196) AND its own stock-trail deductions; the cashier (Vangie Asi) voided 0002/0003/0004 at 13:14:30-49 (VoidSale + per-item logs, stock restored, `is_voided=t`). Records were consistent — the root cause was the same cart being paid 4× in under a minute: `btnPay_Click` (and the F4 shortcut) had **no re-entry guard**, so Pay could re-fire while the payment flow was already running. (NOTE: sale_items links via `si.sale_id = s.pos_id`, NOT `s.id` — first join attempt returned 0 rows.)

| File | Change |
|---|---|
| `Forms/SalesForm.cs` | `btnPay_Click` — added `_paying` guard: `if (_paying) return;` + `btnPay.Enabled=false` right after the cart-empty check; restored on BOTH exit paths (payment form cancelled, sale complete). F4 shortcut (line ~1259) calls `btnPay_Click` directly so it's covered by the same guard. New field `private bool _paying;` next to `btnPay`. |
| `Services/AppVersion.cs` | `"1.1.43"` → `"1.1.44"`. |

**Impact:** repeating Pay (double-tap, stuck key, or F4 spam) can no longer create duplicate sales of the same cart. Single-machine accounting stays clean (HVR's 4×-charge was self-corrected by voids; other identical-total clusters across stores are normal same-price retail). **Deployed:** Release publish (0 errors) → `C:\dev\out\client\` → WinRM push to `C:\JumongAPI\client\` (exe 211,752,380 B; `Copy-Item -Recurse` needs `-Force` or subdirs like Agent/ throw "already exists"). **GitHub release v1.1.44 created (2026-08-17)** with exe asset (id 517099243, `state: uploaded`, sha256 `8005d339…`), download URL verified HTTP 206. GOTCHAS this session: (1) curl `-d` with a JSON string literal can 400 "Problems parsing JSON" — write the body to a file and use `--data-binary "@file"`; (2) 211MB upload exceeds the 15-min tool timeout AND `Start-Process` mangles `-H` headers containing spaces (→ `Invalid Content-Type'`) — run the upload as a background `powershell -File script.ps1` (script reads PAT via `git credential fill` itself) and poll the release assets until `state:"uploaded"`; (3) a killed upload leaves a `starter`-state asset — `DELETE /releases/assets/{id}` before retrying. Stores get it via UPDATE APP (auto-picks `releases/latest`).

## Previous Change (2026-08-16) — web-only: VIEW STOCK dialog now SERVER-ONLY (agent live reads removed)

**Request:** "mas ok ba sa server nalang tayo mag hugot kasi live naman nag se-send ang mga pos?" — the per-product VIEW STOCK dialog (added earlier today) read "Live (POS now)" via per-store agent `sql` commands. The user verified the server snapshot pipeline is healthy (each POS pushes its full stock every 30s + per-sale SyncProduct updates), so the agent read was redundant — same source (the POS's own SQLite), only seconds fresher, at the cost of 10-30s dialog-open delay + TIMEOUT/ERROR/NOT IN STORE failure states. Dialog now shows **server stock only** (`/stock-status`, includes warehouse), auto-refreshing every 20s while open.

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/components.js` | `productStockDialog` stripped: removed `agents`/`live`/`busy`/`_lastLive`, `fetchLive()`, `queryOne()`, `liveQty()`, `liveBadge()`, `esc()`, `parseTSV()`, and the 45s live auto-refresh. `load()`/`refresh()` now just fetch `/stock-status` → filter/dedupe by barcode → sort HQ→HVR→ACGS→Naic→Warehouse→DEV; 20s auto-refresh timer while open; status shows "Refreshed HH:MM:SS · auto-refresh 20s". `storeLabel` no longer consults agents. |
| `JumongCloudAPI/wwwroot/index.html` | Dialog table drops the **Live (POS now)** column + **REFRESH LIVE** button; columns now Store \| Stock \| Price \| Cost; legend "Server stock = pushed by each POS every ~30s · auto-refresh 20s". |

**Verified live:** `node --check` OK; tag balance 611/611 divs; live server serves new code (liveBadge/REFRESH LIVE gone, dialog present). No API change, no client release, no version bump. Agents untouched and still used by AGENTS tab + Stock Trail panel (their `agent/status` refs remain in `storeTrailPanel`/`agentsPanel`). Caveat unchanged: POS app closed (ACGS/Naic) → server keeps last pushed stock until the POS reopens.

## Previous Change (2026-08-16) — web-only: Stock Status subpage REMOVED → per-product VIEW STOCK dialog (server PG stock + live POS stock via agents)

**Request:** "inventory lets remove the stock status replace this inside the master catalog list view there is column view stock then in dialog this what we see: Main (server) = get in server stock, pos client available = fetch using current in the pos client using agents" — the old Inventory → **Stock Status** subpage (flat per-store product list, redundant with Recent Receiving/Stock Trail) is gone; Master Products now has a **VIEW STOCK** button per row opening a dialog showing that product's stock **per store** two ways: **Server** (PG `products.stock_qty`, snapshot pipeline ~30s) and **Live (POS now)** (direct SQLite read via the store's agent `sql` command).

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/index.html` | Removed `st-status` nav leaf, its tab button + header ternary branch, and the whole Stock Status subpage block. Master Products Actions column gains **VIEW STOCK** button (`openStock(x)`). New `productStockDialog` modal: header (product name + barcode), legend "Server = synced snapshot (~30s) · Live = read directly from the store PC via agent", table `Store | Server Stock | Live (POS now) | Price | Cost`, HQ row bold + `MAIN` badge + cyan tint, REFRESH LIVE button, CLOSE. |
| `JumongCloudAPI/wwwroot/components.js` | Deleted `stockStatus` component; `'st-status'` dropped from `groupParents` + grp-inv active check. New `productStockDialog` component: `load()` on `stock-dialog-open` event (fetches `/stock-status` once ~1,600 rows, filters client-side by **barcode**, dedupes per store — dup-barcode gotcha, sorts HQ→HVR→ACGS→Naic→Warehouse→DEV); `fetchLive()` sends **parallel** agent sql per live agent (`SELECT Name, Barcode, StockQty, IsActive FROM Products WHERE Barcode='<escaped>' LIMIT 5`, poll 2s × 15 = 30s cap, reuses esc/parseTSV pattern); `liveQty()` encodes states (value / NOT IN STORE / TIMEOUT / ERROR); `liveBadge()` returns colored badges (red 0 / amber <10 / green). `masterProducts.openStock(x)` sets `stockProduct`+`stockOpen` store state + dispatches the event. |
| `JumongCloudAPI/wwwroot/app.js` | `'stock'` removed from `exportAllCSV` cache list (cache.stock no longer populated). |

**Verified live:** deployed `index.html`/`components.js`/`app.js` → `C:\JumongAPI\wwwroot\` + publish wwwroot; live fetch checks pass (modal + VIEW STOCK present, Stock Status/stockStatus gone); `node --check` OK; tag balance 612/612 divs. No API change, no version bump. GOTCHAS: local product id ≠ master id (barcode is the linkage — never use ids); agents only reachable when store PC on (Naic shows server stock only, "no live agents" note when none respond); warehouse has no agent (server stock only).

## Previous Change (2026-08-16) — POS client: End-Shift Receipt Inventory Reconciliation No Longer Mismatches (Void Returns + Adjustments Down accounted)

**Request:** "fix when it is printing so no mismatch data given to us in report receipt" — the end-shift receipt/email **INVENTORY RECONCILIATION** printed numbers that didn't add up. Root causes (verified vs live HQ data, current shift was `-₱41` off = 0.005%): (1) "+ Stock Received" was labeled receiving but actually summed **ALL positive trails** incl. void returns and adjust-ups; (2) **manual −adjustments (down) were never subtracted** → phantom SHORT variance; (3) mixed cost bases (received/actual at current `Products.Cost`, previous/COGS at old costs) — no cost history in SQLite, accepted as inherent.

| File | Change |
|---|---|
| `Services/DailyCloseService.cs` | `GetShiftTotals()` tuple now **12 elements**: + `TotalVoidReturns` + `TotalAdjustDown`. Received query restricted to `QuantityAdded > 0 AND Reference NOT LIKE '%void%'` (receiving-only semantics — also what's stored in `TotalStockReceivedCost` going forward); void returns = positive `LIKE '%void%'`; adjust-down = `QuantityAdded < 0 AND Reference NOT LIKE 'INV-%'` (INV- = sale deductions; adjustments write `Reference = "Adjustment: <reason>"`). |
| `Forms/EndShiftForm.cs` | New fields `_voidReturns`/`_adjustDown`; 12-tuple destructure; print + both email call sites pass the new values. |
| `Services/PrinterService.cs` | `PrintAuditEndShiftReport`/`BuildAuditEndShiftReportLines` gain optional `voidReturns = 0, adjustDown = 0` (defaults keep **history reprints** compiling/passing stored values only). Reconciliation: `Expected = Previous + Stock Received + Void Returns − COGS − Adjustments/Loss`; new conditional lines `+ Void Returns` and `- Adjustments / Loss`; BALANCED/OVER/SHORT label unchanged. |
| `Services/EmailService.cs` | Same optional params + HTML table rows (Void Returns, Adjustments/Loss) and corrected formula. |
| `Forms/MainForm.cs` | 12-tuple discard fix (was 10). |

**Verified:** Release build 0 errors; `Services/AppVersion.cs` → `"1.1.43"`; exe (211,752,380 B) published to `C:\dev\out\client` + pushed to server drop `C:\JumongAPI\client\` (5:52 PM). **GitHub release v1.1.43 created (2026-08-16)** with the exe asset (uploaded via curl using the PAT from Windows Credential Manager — `git credential fill`; GOTCHA: the extracted token carries a trailing `\r` — must `.Trim()` or the API returns "Bad credentials"). Download URL verified HTTP 206. Stores update via UPDATE APP (auto-picks latest via `releases/latest`). NOTE: history rows in `DailyCloses` keep OLD semantics (stored received included returns) — reprints of old closes print stored values, new live closes store receiving-only.

## Previous Change (2026-08-16) — web-only: Store Stock Trail Panel (POS client LOCAL DB via agents)

**Request:** "pick store → pick category → show the list → click item → see the trail" — new **🛤️ Stock Trail** subpage under POS CLIENT → Inventory that drills into each store's **local SQLite `StockTrail`** remotely through the store's agent (`sql` command). No API/agent changes — all machinery (send → poll) already existed in `agentsPanel`.

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/components.js` | New `storeTrailPanel` Alpine component: store dropdown (from `/agent/status` — only stores with live agents), category dropdown (local DB `SELECT DISTINCT Category ...`), product list (`Id, Name, Barcode, StockQty`, client-side search, LIMIT 500), trail detail (`SELECT CreatedAt, QuantityAdded, StockBefore, StockAfter, Reference, UserName, InvoiceNo, Synced FROM StockTrail WHERE ProductId=<LOCAL id> ORDER BY Id DESC LIMIT 200` — **local Id**, not master id!). `query()` helper reuses the agentsPanel send→poll pattern (2s × 20 tries); single-quote-escaped SQL values; TSV→objects parsing; Type badge (Sale if InvoiceNo / Void-return if ref contains 'void' / Receiving if ref starts RECV-, RR-, WH-TRANSFER / —), amber **NOT SYNCED** badge when `Synced=0`, +green/−red qty, inline CSV export, back navigation. Wiring: `groupParents['st-trail']='grp-inv'` + grp-inv active check. |
| `JumongCloudAPI/wwwroot/index.html` | Nav leaf `{ id:'st-trail', icon:'🛤️', label:'Stock Trail' }` in the Inventory group (under POS CLIENT); header title ternary + third tab button **🛤️ Stock Trail**; panel markup (`st-` prefix routing needed zero extra code). |

**Verified live end-to-end (ACGS):** categories list → products in 'alcohol' (CLVB EMPERADOR = local id 108, stock 301) → trail shows today's `WH-Transfer #717` +180 receiving (12:02) then 8 sales −2/−6/−12/−18 down to 301, all `Synced=1`. Cloud `stock-status` matches local (301) — snapshot pipeline healthy (an earlier 325 was pre-sale timing, not a bug). Deployed: `index.html` + `components.js` → live `C:\JumongAPI\wwwroot\` + publish wwwroot (verified 200 with new content). **Caveats:** Naic has no agent → not listed; offline agent → 40s poll timeout message; agent caps at 500 rows (queries LIMIT 200-500). GOTCHA hit during verification: local product Id ≠ cloud master id (ACGS CLVB = 108 local vs 106 master) — the panel must always use the id from the products query, never a master id.

## Previous Change (2026-08-15) — v1.1.34 (Cloud API): OUTDATED Badge Fixed + Store Rollout Completed

**Request:** "everythings should be same across the board" / "dont fix per shop" — final piece of the all-stores-uniform rollout: the AGENTS dashboard OUTDATED badge compared against a stale hardcoded `latestVer = "1.1.38"`, so a store still on 1.1.38 (Naic) would NOT show as outdated once back online.

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:5111` | `latestVer` `"1.1.38"` → `"1.1.42"` — OUTDATED badge now truthful (HQ/ACGS 1.1.42 = not outdated; HVR shows True until next cashier login writes 1.1.42 to the DB, then self-corrects). |
| `JumongCloudAPI/Controllers/DashboardController.cs:1399,1936` | Version bumped `"1.1.33"`→`"1.1.34"` (2 places). |

**Rollout status (all done via the global agent fix — no per-shop hacks):** HQ 1.1.42 + new agent + sync chip LIVE; ACGS 1.1.42 + new agent + chip LIVE; HVR 1.1.42 exe remotely swapped (agent download 211,748,284 bytes → kill → copy → start; DB version + chip appear after next cashier login since Program.cs:90-106 writes AppVersion post-login); Naic still 1.1.38 + old agent (PC off — UPDATE APP + agent push when back); DEV PC agent still old build until next reboot (SYSTEM task can't be replaced from a non-elevated shell — functional, cosmetic `v?`). GitHub release v1.1.42 with exe asset is the store update source. Deployed via WinRM (stop → copy → start), live version verified `1.1.34`, outdated flags verified live.

## Previous Change (2026-08-15) — v1.1.33 (Cloud API) + web: Online Orders Admin Panel, HQ Stock Reservation, Delivery Fee

**Request:** "what else we need in ecommerce" — Phase 1 of the e-commerce buildout: the dashboard had NO way to see/process online orders (they only sat in `online_orders`), orders never touched stock, and checkout had no delivery fee. Now: full admin order management UI, stock reserved from HQ on confirm (returned on cancel), configurable delivery fee + free-delivery minimum charged at checkout.

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `ALTER TABLE online_orders ADD COLUMN IF NOT EXISTS delivery_fee NUMERIC NOT NULL DEFAULT 0` + new `shop_settings` table (id PK, delivery_fee, free_delivery_min, updated_at) with id=1 seed (0/0). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`ShopCreateOrder`** — accepts `DeliveryFee` (≥0), stores it, `total = items + delivery_fee`. **`ShopGetOrders`/`ShopGetOrder`** — return `deliveryFee` (SELECT + reader indices shifted: total idx 9, delivery_fee idx 10, created_at idx 11). **`ShopUpdateOrderStatus`** — **stock reservation**: CONFIRM (pending→confirmed) deducts `products.stock_qty` at HQ (`STORE-20260602-7159`) per item: barcode via `master_products.id`, pcs = `qty × qty_per_unit` (from `master_product_units` by `unit_name` — NOTE the column is `unit_name`, NOT `name`; wrong column = 42703 500). Guarded UPDATE (`stock_qty >= pcs`); any item short → 400 `"Insufficient HQ stock: <unit> x<qty> (<barcode>)"`, whole tx rolled back. CANCEL (from confirmed/shipped) restores the reserved pcs. Delivered orders can't be cancelled; only pending can be confirmed. **New `GET/POST /shop/settings`** — delivery fee config upsert. Version bumped `"1.1.32"`→`"1.1.33"` (2 places). |
| `JumongCloudAPI/wwwroot/index.html` | Sidebar: **🛒 Online Orders** item in `grp-pos` (after Master Products) with red pending-count badge on the standalone-item row (`_shopBadge`). New section `online-orders`: status filter pills (ALL/pending/confirmed/shipped/delivered/cancelled), orders table (order no/customer/phone/payment+GCash ref/total/status/date/VIEW), **DELIVERY SETTINGS** card (fee + free-over inputs + SAVE), order detail modal (customer, address, note, payment, items with unit price, Subtotal/Delivery Fee/TOTAL, contextual action buttons: pending→✔ CONFIRM + ✕ CANCEL, confirmed→🚚 SHIP + ✕ CANCEL, shipped→✅ DELIVER). |
| `JumongCloudAPI/wwwroot/components.js` | `_shopBadge: 0` state + `groupParents['online-orders']='grp-pos'` + grp-pos active check. New `onlineOrdersPanel` Alpine component: `load()` (limit 200, status filter), `setFilter`, `filtered` (client search on orderNo/name/phone), `openDetail` (fetch order+items), `subtotal()`, `setStatus` (confirm() prompts; raw `fetch` + JSON error body parsing since `fetchJSON` only surfaces `HTTP <code>`; toast on error — e.g. insufficient stock message), `loadSettings`/`saveSettings`, `loadBadge()` (new-count every 30s + on load). |
| `JumongCloudAPI/wwwroot/shop.html` | **Delivery fee at checkout**: `loadShopSettings()` on init, `feeFor(subtotal)` (0 fee if configured 0, or subtotal ≥ free_delivery_min → FREE); cart drawer now shows Subtotal/Delivery/Total rows; checkout summary adds Delivery Fee line (FREE in green when waived); `placeOrder()` sends `deliveryFee`; success modal total includes it. |
| — | **Verified live (test orders cleaned up):** v1.1.33; settings round-trip 50/1000; order `SHOP-20260815-0001` total 112 = 62 item + 50 fee; CONFIRM reserved stock (product 431: 1→0), CANCEL restored (0→1); qty-2 confirm → 400 "Insufficient HQ stock: pack x2 (4800092112782)"; test orders deleted, `setval('online_orders_id_seq', 1, false)` so next real order = `SHOP-…-0001`, settings reset 0/0. |

**GOTCHA hit:** (1) `master_product_units` column is `unit_name` not `name` — first deploy of the confirm path 500'd `42703: column "name" does not exist`; (2) psql `-c` with multiple statements runs as ONE implicit transaction — if any statement errors, EVERYTHING (including an earlier DELETE) rolls back. Run destructive ops as separate `-c` calls. Deployed via WinRM (net stop → copy → start), live version verified 1.1.33, web files live (nav item, onlineOrdersPanel, loadShopSettings present). Next phases (not started): customer notifications (SMS/email), Messenger bot key, payment gateway, product photos for the 582 imageless products.

## Previous Change (2026-08-15) — v1.0.17 (APK) + web-only: Warehouse Mobile App White Screen Fixed (Render-Blocking Tailwind CDN)

**Request:** "when i load the app the screen is white" (mobile app) — opening the warehouse app showed a **plain white screen** (sometimes several seconds) before the dark login screen appeared.

**Root cause:** `whmobile.html` loaded `https://cdn.tailwindcss.com` as a **synchronous render-blocking `<script>` in `<head>`**. The WebView cannot paint ANYTHING (and the end-of-body `init()` never runs) until that ~400KB JIT-compiler script downloads AND compiles on the phone's network — during which the WebView's default background (WHITE) shows through. The HTML loading screen itself was never white — it just never rendered until the CDN finished. Native splash + `windowBackground` were already dark (`#10102a`, `themes.xml`).

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/whmobile.html:8` | Tailwind CDN script → `<script src="https://cdn.tailwindcss.com" defer></script>` — no longer blocks first paint; page paints dark instantly from the inline `<style>`. |
| `JumongCloudAPI/wwwroot/whmobile.html:9-17` | Added `#critical-css` block (`.flex/.flex-1/.flex-col/.items-center/.justify-center/.text-center/.hidden`) so the loading/login/store screens render centered on dark bg even before Tailwind compiles. |
| `WarehouseApp/.../MainActivity.kt:91-92` | `webView.setBackgroundColor(android.graphics.Color.rgb(16, 16, 42))` — WebView area dark from the first frame, so no white flash under ANY network condition (pre-first-paint gap now matches the native splash). |
| `WarehouseApp/app/build.gradle` | versionCode 17→18, versionName `"1.0.16"`→`"1.0.17"`. |
| `JumongCloudAPI/wwwroot/updates/warehouse-version.json` | `"1.0.17"` — changelog "Dark WebView background - no more white screen while loading" (ASCII only, no BOM — `WriteAllText` + `UTF8Encoding($false)`). |

**Deployed:** web files copied live (`C:\JumongAPI\wwwroot\`) + publish copy; APK built + signed on the SERVER (`C:\Users\ADMIN\Desktop\JumongPosV1.01\WarehouseApp`, JAVA_HOME = Android Studio jbr, `apksigner` + `jumong_sign.keystore`) and copied to live `C:\JumongAPI\wwwroot\updates\` + dev repo + publish. Verified: `versionCode='18' versionName='1.0.17'` via aapt2 badging, `setBackgroundColor` present in classes.dex (index 4943115), V3.0 signer cert OK, public URLs 200. **Users get it via SETUP → UPDATE APP.**

**GOTCHAS hit:** (1) PowerShell `Set-Content -Encoding UTF8` writes a **BOM** → Groovy fails parsing build.gradle ("Unexpected character: ''") — always `[System.IO.File]::WriteAllText` with `UTF8Encoding($false)` for gradle/json files; (2) apksigner.bat needs `$env:JAVA_HOME` set in the SAME session; (3) a stale `JumongWarehouse.apk` left in the repo folder silently masked a failed sign (delete before signing); (4) aapt2 output can come back empty when piped through WinRM `Select-String` — use `Out-String` + full output. NOTE: the exact 2632258-byte size match with v1.0.16 was a coincidence; the dex + badging prove the new build is live.

## Previous Change (2026-08-15) — v1.1.32 (Cloud API): Mobile Credit Pay Per-Receipt "Exceeds Remaining" False Rejection Fixed

**Request:** "kindly check mobile credit pay credit there is a problem when paying exceed amount warning" — paying a specific receipt's FULL displayed remaining on the mobile app was rejected with "Amount exceeds remaining (0.00)" / "already fully paid".

**Root cause:** `WhCreditPay`'s per-receipt remaining formula was **inconsistent with `WhCreditBreakdown`'s FIFO sweep**. Breakdown sweeps the unallocated payment pool across ALL receipts oldest-first (each receipt's displayed remaining = amount − allocated − pool share LEFT AFTER older receipts). WhCreditPay computed `remaining = amount − allocated − min(pool, olderUnpaid)` — subtracting the pool consumed by OLDER receipts from EVERY receipt. Result: (1) newer receipts were understated (pool already eaten by older ones counted against them → paying the displayed remaining always rejected, e.g. B displayed 110 but validated as 60); (2) the OLDEST receipt ignored the pool assigned to it (overpay allowed up to full amount even though UI showed less).

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:3654-3691` | `WhCreditPay` per-receipt validation replaced with the **exact same FIFO sweep as `WhCreditBreakdown`** (CTE `alloc` per-receipt allocated sums + `wh_walkin_sales` ordered by created_at/id + poolCopy sweep, `remaining = bal − share` for the target invoice). Removed the old `poolBefore`/`poolAllocatedToOlder` CTE + unused `created` var. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1399,1936` | Version bumped `"1.1.31"`→`"1.1.32"` (2 places). |

**Verified live (scratch customer id 1208, cleaned up after):** receipts A=110 (oldest) + B=110, general pool pay 50 → breakdown showed A remaining **60** (pool hit it first), B remaining **110**. OLD code would have rejected `PAY B 110` ("exceeds remaining (60.00)") and wrongly allowed `PAY A 70`; NEW code: `PAY B 110` → **200 OK**, `PAY A 70` → **400 "Amount exceeds remaining of ... (60.00)"**, `PAY A 60` → **200 OK**, final balance 0 + both receipts remaining 0 (breakdown consistent). Test data deleted (stock restored 204→200, 350→169, trails/credit txns/sales/customer removed). Deployed via WinRM (net stop → copy → start, stop BEFORE copy to avoid locked System*.dll). NOTE: psql lives at `C:\Program Files\PostgreSQL\18\bin\psql.exe` on the server (NOT on PATH); connection = `Host=localhost;Database=jumongpos;Username=postgres;Password=postgres` (appsettings.json). The web client (`whmobile.html`) was already correct — its checks use the breakdown's `remaining`, so no web change needed.

## Previous Change (2026-08-15) — v1.1.31 (Cloud API) + web: Warehouse Receiving History (Date Filter Fix) + Expandable Sidebar Groups

**Request:** "check during august 6 what was received product in warehouse i cannot see your date picker does not work" + "pwede gawin sila expandable pang click sa Report expand and close? lahat sana my icon para cute" — warehouse receiving history now visible on the dashboard with a WORKING date filter; sidebar groups are now click-to-expand/collapse with icons everywhere.

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:2643-2666` | **`WhGetReceivings` fixed** — removed the `AND reference LIKE 'RECV-%'` filter (that's why old receivings with cashier-name references like "aira"/"jnj"/"chaknu" NEVER showed) + new optional `from`/`to`/`limit` query params (DateTime-parsed, `to` = next day, LIMIT capped 1000). Version bumped `"1.1.30"`→`"1.1.31"` (2 places). |
| `JumongCloudAPI/wwwroot/index.html` | **Warehouse → new 📥 Receiving subpage** (sidebar `wh-receiving` in grp-wh + tab button): date picker (From→To) + TODAY/FILTER/ALL buttons, table (Reference/Items/Total Qty/Date/VIEW), items modal (`recvViewOpen`, product/barcode/qty). |
| `JumongCloudAPI/wwwroot/components.js` | `warehousePanel` — new `recvData`/`recvFrom`/`recvTo`/`recvView*` state + `loadReceivings()`/`recvToday()`/`viewReceiving()`/`closeReceivingView()`; `load()` handles `sp==='receiving'`. `groupParents` + `'wh-receiving': 'grp-wh'`. |
| `JumongCloudAPI/wwwroot/app.js` | CSV export for `wh-receiving` (Reference,Items,TotalQty,Date). |
| `JumongCloudAPI/wwwroot/index.html` + `components.js` | **Sidebar restructure** — nav is now GROUP-BASED (`group:true, items:[...]`, 3 levels max): `grp-ai` (AI Chat/AI Knowledge), `grp-system` (Server Health/1PC CHECK), `grp-pos` → `grp-reports` (Sales/Inventory Cost/Shift History/**Analytics** moved here) + Master Products + `grp-inv` (Recent Receiving/Stock Status), `grp-wh`, `grp-settings` (Settings/POS Promo/POS QR/Branding). Every group + item has an emoji icon; groups render with ▸/▾ chevron (rotates on expand). **Collapsed by default** (`groupOpen={}`); `toggleGroup(id)`/`isGroupOpen(id)`/`isGroupActive(id)`; **auto-expand ancestors** via `openAncestors(id)` in `switchSection` (so clicking a wh-transfer badge opens grp-wh). `_whBadge` shows on grp-wh header too. Old `isHeader`/`isSub`/`indent` flat nav REMOVED. NOTE: plain `x-show` (no Alpine collapse plugin loaded). |

**Verified live:** v1.1.31 — `GET /warehouse/receivings?from=2026-08-06&to=2026-08-06` → 4 batches (jnj +2484, brew +200, bimboy ombet lalad +240, aira +1000 = **3,924 pcs** Aug 6); sidebar live with `'wh-receiving'` + `loadReceivings()` present. Deployed via WinRM (stop → copy → start) + web copy. **Data answer:** chaknu (Jul 21: Marlboro Red 1500 + Gold 500) & jovani (Jul 30: Chesterfield 450; Aug 13: Mighty Green 500 + Camel Yellow 500).

## Previous Change (2026-08-14) — v1.1.30 (Cloud API) + web: AI Knowledge Bank (RAG) + shop.jumongdev.com Subdomain + Shop QR

**Request:** "gawa ka nalang ng mga sasagotan ko sa dashboard" + "ang kailangan nyan yung messenger bot ipakita sa customer either they go to webaddress or scan the qrcode" — AI chat now answers from REAL business info (Knowledge Bank RAG with ✅ TAMA / ✏️ I-CORRECT review flow); shop moved to clean URL `shop.jumongdev.com`; shop QR hosted for Messenger bot.

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs:798-855` | **2 new tables**: `chat_kb` (id, category business/project/approved-reply, keywords, question, answer, active, source manual/seed/approved-reply/project-ingest, timestamps) + `chat_review_log` (user_message, bot_reply, verdict approved/corrected, corrected_answer, kb_entry_id, created_at). **Seed migration** (idempotent via `WHERE NOT EXISTS question = exact`) — 10 entries: website (answered, active) + 9 blank business questions (hours/delivery/contact/branch/payment/order/tracking/pickup/returns) with `active=false` → dashboard shows **SAGUTIN** badge until user answers. GOTCHA: first seed used `LIKE '%pickup%'` etc. which didn't match "pick-up"/"makokontak" → duplicated on each restart; fixed with exact-question match. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **RAG in `POST /chat`**: (1) KB retrieval — exact-question match (score 3) + answer-first-word match (score 1), fallback keyword loop; **blank answers NEVER injected** (prevents hallucination on unanswered questions); (2) promo questions → live `pos_promo` row; (3) product questions → `master_products` filtered `sell_online=true AND is_active=true` with **token-based search** (stopwords stripped, ALL tokens must match name/barcode, LIMIT 3) + HQ stock via barcode join — **cost NEVER exposed**. Facts injected into system prompt; response includes `sources[]` (`kb:N`, `promo`, `product`). System prompt hardened: "Huwag mag-imbento... sabihin 'Wala pa pong nakarekord na sagot dito'". |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **8 new endpoints**: `GET/POST/PUT/DELETE /chat/kb`, `POST /chat/kb/review` (approved → saves bot reply as KB entry source=approved-reply; corrected → saves corrected answer; logs to chat_review_log), `GET /chat/kb/reviews`, `POST /chat/kb/ingest-project` (parses server `C:\Users\ADMIN\Desktop\JumongPosV1.01\AGENTS.md` sections → project KB entries). |
| `JumongCloudAPI/Controllers/DashboardController.cs:1398,1919` | Version bumped `"1.1.29"`→`"1.1.30"`. |
| `JumongCloudAPI/wwwroot/index.html` | AI CHAT panel: **✅ TAMA / ✏️ I-CORRECT** buttons under each bot reply (I-CORRECT → textarea → SAVE CORRECTED ANSWER), reviewed-state label, sources line (📚 kb:1 · product · promo), subtitle fixed to "llama3.1:8b · dev PC → server failover · Knowledge Bank". Sidebar **📚 AI KNOWLEDGE** item + full section: KB table (category filter, SAGUTIN/ACTIVE/OFF badges, EDIT/ACTIVATE/DEL, + ADD ENTRY, 📥 INGEST PROJECT), Review Log table, editor modal. |
| `JumongCloudAPI/wwwroot/components.js` | `aiChatPanel` — `review(m, verdict)` + `startCorrect(m)` post to `/chat/kb/review`; `sources` state. New `kbPanel` Alpine component (load/review/CRUD/toggle/ingest). |
| `JumongCloudAPI/Program.cs` | **Clean URL rewrite middleware** — host `shop.jumongdev.com` path `/` or `/index.html` → serve `/shop.html`. |
| `JumongCloudAPI/wwwroot/assets/shop_qrcode_300.png` + `shop_qrcode_1000.png` | Shop QR images (300px for Messenger attachment, 1000px for print) generated with node `qrcode` pkg → `https://admin.jumongdev.com/assets/shop_qrcode_300.png` (verified 200). For the Messenger bot to attach: `attachment:{type:"image", payload:{url:"https://admin.jumongdev.com/assets/shop_qrcode_300.png"}}` alongside shop.jumongdev.com text (Phase 2 — pending Messenger key). |
| — | **Server tunnel:** `C:\Users\ADMIN\.cloudflared\config.yml` added ingress `shop.jumongdev.com → http://localhost:5000`. **cloudflared now runs as SYSTEM scheduled task `cloudflared-tunnel`** (was a plain background process that died when the WinRM session closed → Cloudflare 530 errors). DNS: user added `CNAME shop → 0b400db6-d379-464b-82d2-eb1149afeffc.cfargotunnel.com` (Proxied) in Cloudflare dashboard. |
| — | **DB data:** duplicate seed rows cleaned (DELETE ... NOT IN (SELECT MIN(id) GROUP BY question)), test review entries + review log cleared. |

**Verified live:** v1.1.30 — `shop.jumongdev.com` serves "Andengs Online Shop" (clean URL, no login); `admin.jumongdev.com` still serves admin login; KB endpoint 200 with 10 entries; chat RAG: "anong website nyo?" → "Website namin ay shop.jumongdev.com!" (source kb:1); "magkano zonrox 225ml?" → "₱26.50 ... 34 pcs" (source product — token search now matches "ZONROX BLEACH COLORSAFE (VIOLET) 225ML"; was hallucinating ₱35); "may promo ba ngayon?" → "Coke mismo 60+1, Kopiko all flavors 1460, C2 solo 312" (source promo, from live pos_promo); "bukas ba kayo ngayon?" → "Wala pa pong nakarekord na sagot dito" (no hallucination — entry still SAGUTIN). Review flow: approved → KB entry created; corrected → corrected answer saved. GOTCHAS FIXED: (1) `PgDatabaseHelper.GetConnection()` ALREADY opens the connection — extra `.Open()` throws "Connection already open" (removed all 9); (2) `ExecuteScalar()` returns Int32 not Int64 — use `Convert.ToInt32`; (3) interpolated string with newline inside `$")` breaks compile. Deployed via WinRM (stop → copy → start), live version verified 1.1.30.

## Previous Change (2026-08-14) — v1.1.29 (Cloud API) + web: Hybrid Chat Backend (Dev PC Primary, Server Failover)

**Request:** "tuloy na natin ang llama dito sa dev pc tapos ito muna ang first option sa chat tapos pag nag fail sa server na ang talon" — dev PC Ollama (GPU-capable) serves chat when online; server takes over when unreachable.

| File | Change |
|---|---|
| — | **Dev PC:** `ollama pull llama3.1:8b` (4.9GB, same model as server). `OLLAMA_HOST=0.0.0.0:11434` set (User env var) + inbound firewall rule **Ollama LAN** (TCP 11434, Private). Ollama now listens on `0.0.0.0:11434` (was 127.0.0.1). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`POST /chat` failover** — tries `http://DESKTOP-Q36S34R:11434/api/chat` (dev PC, 30s timeout) first → on any failure falls back to `http://localhost:11434` (server). Response now includes `backend: "dev"\|"server"`; `ChatLogEntry` gained `Backend` field; `/chat/stats` recent[] includes `backend`. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1398,1546` | Version bumped `"1.1.28"`→`"1.1.29"`. |
| `JumongCloudAPI/wwwroot/index.html:2710` | Recent-response chips show backend tag: **DEV** (cyan bold) / **SRV** (amber bold); tooltip shows "chars via backend". |

**Verified live:** v1.1.29 — dev PC online → `backend: dev` (27.5s cold load, first llama load on dev PC); dev Ollama stopped → `backend: server` (43.8s cold server load); stats recent[] show `backend=dev` / `backend=server`. Deployed via WinRM (stop → copy → start) + web copy. NOTE: `ollama app` tray relaunches the server at logon; user-level `OLLAMA_HOST` is picked up then (current session uses `Start-Process` with `$env:OLLAMA_HOST` set manually after edits).

## Previous Change (2026-08-14) — v1.1.28 (Cloud API): Chat Model Switched to Llama 3.1 8b (Qwen 7b Deleted)

**Request:** user na-test ang dalawang models side-by-side at pinili ang Llama. Qwen 7b tinanggal sa server.

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:1441` | `POST /chat` model changed `"qwen2.5:7b-instruct"` → `"llama3.1:8b"`. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1398,1546` | Version bumped `"1.1.27"`→`"1.1.28"`. |
| — | **Server:** `ollama rm qwen2.5:7b-instruct` — Qwen deleted, only `llama3.1:8b` (4.9GB) remains. |

**Model comparison test (direct to Ollama API, same 3 questions, system prompt = Taglish assistant):** Llama won — warmer 2.6s vs 3.7s avg, more structured/confident answers. Both hallucinate store facts (24/7, delivery fees, website) — next step is real store info sa system prompt. **Verified live:** v1.1.28, `POST /chat` → Llama reply 5.5s warm, accurate system-prompt info (admin.jumongdev.com/shop.html). Deployed via WinRM.

## Previous Change (2026-08-14) — v1.1.27 (Cloud API) + web: AI Chat Performance Monitoring

**Request:** "malalaman mo ba kung may problema sa slowdown sa chat?" — nagdagdag ng per-request logging + stats para mamonitor ang chat speed.

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`POST /chat` now logs every call** — `_chatLog` ConcurrentQueue (cap 500) records timestamp, duration ms, success/fail, reply length, error message. Logged sa 3 exit paths: Ollama non-200, success, exception. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **New `GET /chat/stats`** — last 60 min window: `{total, ok, fail, avgMs, maxMs, recent:[{at, ms, ok, err, replyLen}]}` (recent = last 30). In-memory lang — nagre-reset pag restart ang service. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1398,1546` | Version bumped `"1.1.26"`→`"1.1.27"`. |
| `JumongCloudAPI/wwwroot/index.html` | AI CHAT panel: stats line (⚡ Avg/Max/ok/fail) + recent-response chips (time + duration, green ok / red fail, hover = error detail). |
| `JumongCloudAPI/wwwroot/components.js` | `aiChatPanel` — `loadStats()` sa init + after bawat send + every 15s habang nasa section. |

**Verified live:** version 1.1.27; 2 test chats → stats `total=2 ok=2 fail=0 avg=5964ms max=8142ms`; recent chips show per-request `09:13:28 | 8142ms | ok`. Deployed via WinRM (net stop → copy → net start) + web copy.

## Previous Change (2026-08-14) — v1.1.26 (Cloud API) + web: AI Chat Test (Ollama 7b-instruct sa Server) + Ollama Server Install

**Request:** test kung kaya sumagot ng AI at gumawa ng sample chat sa dashboard. Nag-install ng **Ollama sa server** (portable zip + NSSM service, hindi installer exe — na-stuck sa hidden UAC prompt sa WinRM session) at nag-pull ng `qwen2.5:7b-instruct`. Bagong **AI CHAT** section sa dashboard para ma-test ang bot.

| File | Change |
|---|---|
| — | **Server:** `C:\Ollama\` (ollama.exe + lib), NSSM service **Ollama** (Automatic, `serve`), `OLLAMA_KEEP_ALIVE=5m` (RAM guard — model unloads pag 5 min idle; RAM free 7.8GB lang, model ~4.7GB). Model `qwen2.5:7b-instruct` pulled. Test: first run 13.7s (model load), warm 2.6s. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1395-1466` | **New `POST /chat`** — body `{message, history[]}` (max 500 chars/msg, history ≤ 10) → forward sa `http://localhost:11434/api/chat` (model `qwen2.5:7b-instruct`, system prompt = Taglish assistant ng Andengs Superstore) → returns `{reply}`. **Rate limit 5 msg/min/IP** (in-memory ConcurrentDictionary, 429 kapag lumampas). Timeout 120s, 502 kapag down ang Ollama. |
| `JumongCloudAPI/Controllers/DashboardController.cs:4659-4661` | `ChatRequest` + `ChatMessage` DTO classes. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1398,1448` | Version bumped `"1.1.25"`→`"1.1.26"`. |
| `JumongCloudAPI/wwwroot/index.html` | Sidebar: **AI CHAT** nav item (🤖, after AGENTS) + panel: 420px scrollable message list (user right cyan / bot left), typing indicator, input + SEND + CLEAR. |
| `JumongCloudAPI/wwwroot/components.js` | `aiChatPanel` Alpine component — `msgs[]`, `send()` (posts history, pushes reply), `clearChat()`. |

**Verified live:** version 1.1.26; `POST /api/dashboard/chat` → 200 "Bukas kami mula 8:00 AM hanggang 10:00 PM..." (14.7s cold / 9.2s semi-warm); rate limit 429 sa ika-4+ msg sa loob ng 1 min. Deployed via WinRM (net stop → copy → net start) + web copy. NOTE: dashboard login (web_access) ang gate para sa UI, pero ang `/chat` endpoint mismo ay open (walang auth) — rate-limited lang; fine para sa test phase.

## Previous Change (2026-08-14) — v1.1.25 (Cloud API) + web: Master Products Quick Toggles (Sell Online / Active / Exempt)

**Request:** "per item if he will be displayed in online? lagyan ng checkbox sa master catalog table para madali pag setup" — 3 checkbox columns sa Master Products table, direktang tick/untick na auto-save, walang editor.

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs:563-566` | Migration: `ALTER TABLE master_products ADD COLUMN IF NOT EXISTS sell_online BOOLEAN NOT NULL DEFAULT TRUE` — default TRUE = lahat ng items ay nananatiling visible sa online shop pagkatapos i-deploy. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **New `PATCH /products/master/{id}/flags`** — body `{sellOnline?, isActive?, pointsExempt?}` (nullable booleans, tanging mga ipinadala lang ang ina-update, `updated_at=NOW()`). Ginamit ng table checkboxes. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | `SeedProductDto` + `SellOnline` (default true); `CreateMasterProduct`/`UpdateMasterProduct` INSERT/UPDATE kasama `sell_online`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | `GetMasterProducts` + `DownloadMasterCatalog` — ibinabalik na ang `sellOnline` (at `isActive` sa GetMasterProducts). |
| `JumongCloudAPI/Controllers/DashboardController.cs:4013,4056,4086,4109` | `ShopCatalog` / `ShopProduct` / `ShopCatalogSearch` / `ShopCategories` — `AND mp.sell_online = true` (active AND sell-online lang ang lumalabas sa ecommerce). |
| `JumongCloudAPI/Controllers/DashboardController.cs:1398,1448` | Version bumped `"1.1.24"`→`"1.1.25"`. |
| `JumongCloudAPI/wwwroot/index.html` | Master Products table: **Sell Online / Active / Exempt** checkbox columns (accent cyan/emerald/amber) + editor SELL ONLINE checkbox. |
| `JumongCloudAPI/wwwroot/components.js` | `masterProducts.toggleFlag(x, field, val)` — optimistic update, PATCH sa flags endpoint, revert + toast sa fail. `productEditor` + `sellOnline` state (load/save/reset). |

**Verified live:** version 1.1.25; product 431 toggled off → hidden from `/shop/catalog`; toggled back on → visible again; pointsExempt true/false round-trip; isActive false → download shows `isActive:false`, restored true. Web files live (toggleFlag + Sell Online present). Deployed via WinRM (net stop → copy → net start) + web copy. POS clients unaffected (sell_online = shop-only; points/active changes propagate via existing UPDATE MASTER flow).


**Bug:** AGENTS dashboard cards showed `⚠` + ERROR with nonsense like `WhSellForm: Constructor end | Startup: App v1.1.41 started by bella`. Root cause: `GetErrorSummary` (tools/Agent/Program.cs) grabbed the **last 3 timestamped lines** of error.log regardless of whether they were real errors — error.log also contains INFO entries (`ErrorLogger.Log(source, message)`) interleaved with exceptions.

**Fix:** only count a line as an error if the **next line starts with `Type:`** (exception entries always have `  Type: ...` right after the header — ErrorLogger.cs:38; INFO entries are single-line).

| File | Change |
|---|---|
| `tools/Agent/Program.cs:251-253` | Added `if (i + 1 < lines.Length && lines[i + 1].TrimStart().StartsWith("Type:")) recent.Add(line);` — INFO lines skipped. |
| — | Rebuilt agent, refreshed `agent.zip` (34.7 MB, live 200), pushed to HQ (`C:\Users\ADMIN\Desktop\JumongPosHW\agent`), HVR (`C:\Users\Admin\Desktop\JumongPos\Agent`), ACGS (`C:\JumongPos\Agent`) via `ps` command (download → kill → expand → start). All verified heartbeating. Naic (`STORE-20260622-E174`) has NO agent running. |

**Also explained (not a bug):** HQ `hasError=True` at 11:38–11:40 was `FetchPromoMessage` 502s — the API service was stopped for the v1.1.24 deploy (Cloudflare → 502 while origin down). `HasRecentErrors` = SyncLog failures in the last hour → auto-clears ~12:40 PM; `errorSummary` = last 2h window → auto-clears ~13:40 PM.

## Previous Change (2026-08-14) — v1.1.24 (Cloud API): Online Shop Stock Fix (barcode JOIN)

**Bug:** lahat ng products sa shop.html ay nagpakita ng OUT OF STOCK kahit may stock ang HQ. Root cause: `ShopCatalog`/`ShopProduct` nag-JOIN ng `products p ON p.store_id = @sid AND p.pos_id = mp.id` — pero `products.pos_id` ay ang **local SQLite ID ng store** (in-assign via `last_insert_rowid()`/barcode-match sa SyncService.cs:1124), HINDI ang master catalog ID. 0/693 ang nag-match → lahat `hqStock=0`.

**Fix:** barcode ang linkage (689/693 match, nagsi-sync from cloud v1.0.48):

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:4009,4050` | `LEFT JOIN products p ON p.store_id = @sid AND p.pos_id = mp.id` → `LEFT JOIN LATERAL (SELECT stock_qty FROM products WHERE store_id = @sid AND barcode = mp.barcode AND is_active = true ORDER BY pos_id LIMIT 1) p ON true` sa parehong `ShopCatalog` at `ShopProduct`. LATERAL+LIMIT 1 = proteksyon sa duplicate barcode (hal. "10014"). |
| `JumongCloudAPI/Controllers/DashboardController.cs:1398,1448` | Version bumped `"1.1.23"`→`"1.1.24"`. |

**Verified live:** version 1.1.24, catalog 693 rows → 518 na may hqStock>0 (dati 0 lahat), tugma sa `stock-status` per barcode (ZONROX 225ML=37, 1000ML=23, 450ML=0 — totoong 0 sa HQ). Deployed via WinRM (net stop → copy → net start). No schema change, no data fix. NOTE: `stock-status` joins `products` directly (correct); `wh_products` may tamang `master_product_id` linkage kaya warehouse stock (whStock) ay hindi naapektuhan ng bug.

## Previous Change (2026-08-14) — v1.1.23 (Cloud API) + web-only: Online Shop Frontend (shop.html)

Customer-facing e-commerce page for the shop backend (v1.1.21). Accessible at `https://admin.jumongdev.com/shop.html` (also via dashboard sidebar **ONLINE SHOP** → opens in new tab). Mobile-first design (Tailwind CDN + vanilla JS), HQ store `STORE-20260602-7159`.

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`ShopCatalog`** — bagong `withImages` query param (default `true`, backward compatible): `withImages=false` → `imageData` replaced by `''` (column position kept so reader indices unchanged). Shop page uses it to keep the 693-product grid payload small (~60KB vs ~7MB). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **New `GET /shop/product/{id}`** — single product row (same shape as catalog + imageData + hq/wh stock + units). Used by the shop page for LAZY image loading per product (IntersectionObserver + per-id cache; only 111 of 693 products have images, avg 60KB each). |
| `JumongCloudAPI/wwwroot/shop.html` | **New file** — modern mobile-first storefront: sticky header (branding via `/dashboard/branding` — title/logo/primary color, fallback violet + "Andengs Online Shop"), promo banner (latest `shop_notifications` for a 30-day window), search (debounced 200ms), category chips (36 categories), 2-col mobile / 5-col desktop product grid with OUT OF STOCK overlay + LOW STOCK badge, per-unit pricing (default unit from `units[]`, qtyPerUnit-aware stock math), cart drawer (localStorage-persisted, steppers, subtotal), checkout sheet (name/phone validation, address, note, COD vs GCash + ref number), order success modal (order no, payment, total), order tracking by phone (`GET /shop/orders?phone=` → status chips pending/confirmed/shipped/delivered/cancelled + item detail). `fetchT(url, opts, ms)` helper = AbortController timeout wrapper (standard `{timeout}` option is NOT a fetch option). Images lazy-load via `/shop/product/{id}` only when scrolled into view. |
| `JumongCloudAPI/wwwroot/index.html` | Sidebar nav: `{ id:'shop', icon:'🛍️', label:'ONLINE SHOP' }` after Server Health. |
| `JumongCloudAPI/wwwroot/components.js:71` | `switchSection` — `if (section === 'shop') { window.open('shop.html', '_blank'); return; }`. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1398,1448` | Version bumped `"1.1.22"`→`"1.1.23"`. |

**Gotchas/notes:** (1) fetch `{timeout}` option is silently ignored by browsers — AbortController wrapper required; (2) base64 `imageData` may lack the `data:` prefix — page prepends `data:image/jpeg;base64,` when needed; (3) stock checks use default unit qtyPerUnit (e.g. 1 BY 6 = 6 pcs) so cart qty never exceeds `hqStock / qtyPerUnit`; (4) order `order_no` = `SHOP-yyyyMMdd-NNNN` (RETURNING id → padded). **Verified live:** catalog 693 w/o images (imageData len 0), `/shop/product/1` returns image+stock, 36 categories, full order loop (create SHOP-20260814-0001 → detail → PUT status confirmed) — test order deleted + sequence reset (`setval` → next real order = `SHOP-20260814-0001`). Deployed via WinRM (net stop → copy → net start), live `1.1.23`. Admin order management UI (dashboard) = next phase; payments are COD/GCash-ref only (no online payment gateway yet).

## Latest Change (2026-08-13) — v1.1.22 (Cloud API) + web-only: Per-Resibo Credit Payment (Mobile)

User request: "paki ayos ng mobile credit pwede per resibo ang payment" — pwede na magbayad ng CREDIT per resibo, hindi lang general amount.

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs:370-372` | Migration: `ALTER TABLE credit_transactions ADD COLUMN IF NOT EXISTS invoice_no TEXT DEFAULT ''` + index `idx_credit_transactions_invoice_no`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`WhCreditPay`** — tatanggap ng optional `invoiceNo`: kapag may resibo, i-validate na (1) umiiral ang resibo at sa tamang customer, (2) hindi lalampas sa *remaining* nito (allocated payments + FIFO pool share na pumapasok sa mas lumang resibo). Description ng txn nagiging `Payment - {name} ({invoiceNo})`. Backward compatible — walang invoiceNo = general pool payment (FIFO sa pinakalumang resibo). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`WhCreditBreakdown`** — rewrite: per-receipt `remaining` = amount − allocated (invoice_no match) − FIFO pool share; bagong `trail[]` field per resibo (payment id/amount/method/cashier/date); `paidTotal` ngayon = unallocated pool lang. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | `WhCreditPayRequest` + `InvoiceNo` property. Version bumped `"1.1.21"`→`"1.1.22"`. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **Breakdown list** — clickable rows (tap resibo): `openReceiptDetail(inv)` shows invoice, date, total/paid/remaining, **Payment Trail**, `💵 PAY THIS RECEIPT`. PATAGO button per row para sa hindi pa bayad. **Pay modal** — dagdag `Receipt` line + `creditPayInvoice`; `openCreditPayForReceipt(inv)` pre-fills amount = remaining (editable para sa partial); `submitCreditPay` may client-side remaining check + `invoiceNo` sa body. Voucher print may `Invoice W#` line. After pay: auto-refresh billing + breakdown. General COLLECT PAYMENT (customer-level) hindi ginagalaw. |

**Gotchas fixed during verification:** (1) trail query nag-quote ng `amount` column na wala sa credit_transactions (dapat `credit`) — 500; (2) off-by-one sa reader index matapos tanggalin ang column — `GetDecimal(3)` binasa ang `payment_method` (text) → `InvalidCastException`. Na-verify live: 2 test receipts (500+300) — bayad 300 sa PALABAGONG resibo → nanatili 500 ang natitira sa luma (proof na hindi lump FIFO), negative 501 → 400, partial 200 E-Wallet → heart, general 100 → FIFO sa natitira, totalBal 200, trail per resibo tama. Deployed via WinRM (net stop → copy → net start), live `1.1.22` + `whmobile.html` na verify (openReceiptDetail/PAY THIS RECEIPT present). Test data (scratch customer `ZZZ CREDIT TEST`) nilinis pagkatapos.

## Latest Change (2026-08-13) — v1.1.21 (Cloud API): E-Commerce Shop Backend

Backend-only phase ng online shop module. Orders mula sa customer-facing shop (future web/POS) ay dito pumapasok.

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs:458-489` | **3 new tables**: `online_orders` (order_no, customer_name, phone, address, payment_method COD/GCash, gcash_ref, delivery_note, status pending→confirmed→shipped→delivered/cancelled, total, timestamps), `online_order_items` (product_id, product_name, unit_name, qty, price, total — CASCADE on order), `shop_notifications` (store_id, message — para sa POS banner pushes). Indexes sa phone/status/created_at/order_id/store_id. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **9 new shop endpoints** (route `api/dashboard/shop/…`): |
| — | `GET /shop/catalog?storeId=&category=` — master catalog na may **HQ stock** (LEFT JOIN products per-store, pos_id = master id) + **Warehouse stock** (`wh_products` via master_product_id: whStock/whBoxQty/whBoxPrice) + units JSON. Default storeId = HQ `STORE-20260602-7159`. |
| — | `GET /shop/catalog/search?q=&limit=` — name/barcode ILIKE (server-side, for POS search popup) |
| — | `GET /shop/categories` — distinct categories |
| — | `POST /shop/orders` — customer checkout (tx: INSERT order → order_no `SHOP-yyyyMMdd-NNNN` via RETURNING id → items). Returns `{id, orderNo, status, total}`. |
| — | `GET /shop/orders?phone=&status=&limit=` — order list; `GET /shop/orders/{id}` — order + items |
| — | `PUT /shop/orders/{id}/status` — valid statuses only (pending/confirmed/shipped/delivered/cancelled) |
| — | `GET /shop/orders/new-count?since=` — pending count (POS update banner); `POST /shop/notify` + `GET /shop/notifications/{storeId}?since=` — store notification push |
| — | `ShopOrderRequest/ShopOrderItemRequest/ShopStatusRequest/ShopNotifyRequest` DTO classes. Version bumped `"1.1.20"`→`"1.1.21"`. |

**Gotcha fixed during verification:** Npgsql rejects `DBNull.Value` params in mixed-context ORs (`could not determine data type of parameter $N` / `timestamp with time zone > text`). Followed the existing `StoreFilter` pattern — build SQL conditionally, bind params ONLY when non-empty (`since` parsed to `DateTime`). Test data cleaned (sequences reset so first real order = `SHOP-…-0001`). Deployed via WinRM (net stop → copy → net start), live version verified `1.1.21`, all 9 endpoints verified live (catalog 692 rows, coffee filter 15, order create/detail/status, notify + fetch).

**Stock semantics (owner decision):** retail/by-6 availability = **HQ store stock** (`products` table); per-box = **Warehouse** (`wh_products`). UI (shop page) na lang ang kulang — next phase.

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
| `DESKTOP-U5BO3Q0` @ `192.168.1.100` | HVR store (moved to new PC 2026-08-19; was DESKTOP-TK63MO6 @ 192.168.1.15) | POS client (path TBD on new PC), agent on `DESKTOP-U5BO3Q0`; inbound RDP/WinRM/ICMP BLOCKED (lanfix not yet done); sleep = never |
| `DESKTOP-NISQ3Q7` @ `192.168.1.152` | U Got Minimart - Naic | (needs path verify) |
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
> **HQ → server LAN API switch (2026-08-15, COMPLETE):** HQ POS `CloudApiUrl` = **`http://DESKTOP-I097OO9:5000/api`** (LAN, no internet/Cloudflare hop) — owner's plan: HQ hosts ALL stock (retail + wholesale + e-commerce) in one DB; warehouse to be retired. The POS reads the setting per sync call, so the flip is zero-downtime (verified: SyncLog all-OK after flip). **Phase 2 done 2026-08-15 20:31** — HQ updated to v1.1.42 via GitHub release (pos-status now posts → dashboard sync chip live) and the agent was restarted so it also uses the LAN URL (it caches at startup). **GitHub release v1.1.42 created 2026-08-15** with the exe asset (the release had been created WITHOUT the asset earlier → stores got "DOWNLOAD FAILED" 404s until the 211 MB upload finished via curl). Rollback anytime: `UPDATE Settings SET Value='https://admin.jumongdev.com/api' WHERE Key='CloudApiUrl'` + restart. The startup URL-fix migrations only rewrite `%railway%`/`%digitalocean%` values → the LAN URL survives restarts. No auto-failover: if the LAN drops, HQ sync pauses (POS keeps selling offline, auto-drains on reconnect) — same as an internet outage. Store rollout 2026-08-15: HQ 1.1.42 ✅, ACGS 1.1.42 ✅ (tapped UPDATE APP), HVR 1.1.42 ✅ (remote exe swap; version/chip show after next cashier login), Naic still 1.1.38 (PC off — UPDATE APP when back online).

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
- **DB connection:** `DATABASE_URL` env var (PostgreSQL, default `localhost:5432`), or check Helpers/CloudDatabaseHelper.cs

## Stores (in Cloud / Local PG)
| Store ID | Name | Machine | IP |
|---|---|---|---|
| `STORE-20260602-7159` | Andengs Superstore - HQ | DESKTOP-UU8E0D4 | 192.168.1.25 |
| `STORE-20260602-AA36` | Andengs Superstore - HVR | DESKTOP-U5BO3Q0 | 192.168.1.100 |
| `STORE-20260622-E174` | U Got Minimart - Naic | DESKTOP-NISQ3Q7 | 192.168.1.152 |
| `STORE-20260626-A80C` | ACGS - Naic Market | DESKTOP-TK63MO6 | 192.168.0.103 |
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
| Forms/SettingsForm.cs:469-513 | Added **QR CODES** section (Admin only) — DataGridView with HEADER/FILE columns, +ADD/REMOVE buttons, loaded/saved via StoreQrCodes JSON setting |
| Forms/SalesForm.cs:1168-1195,1208,1226,1244,1356-1379,1409-1447,1498-1505 | Added QR carousel in right panel: _pbQr PictureBox, _lblQrHeader, _btnQrPrev/_btnQrNext nav buttons, _qrEntries list, LoadQrCodes(), ShowQrIndex(), Recalc() layout below Pay button |
| Services/AppVersion.cs | Current bumped to "1.0.85" |

**Impact:** POS sales screen shows QR code images (GCash, Maya, etc.) in right panel below totals. Admin configures in Settings.

### v1.0.86 — Inventory Reconciliation in End Shift

| File | Change |
|---|---|
| Models/DailyClose.cs | Added TotalInventoryCost, TotalCostSold, TotalStockReceivedCost |
| Data/DatabaseHelper.cs | Migration: adds TotalInventoryCost, TotalCostSold, TotalStockReceivedCost columns |
| Services/DailyCloseService.cs:64 | GetShiftTotals() returns 10-element tuple. Added GetLastInventoryCost(). |
| Forms/EndShiftForm.cs:98-107 | Computes 	otalInvCost = SUM(StockQty — Cost) before save |
| Services/PrinterService.cs | Prints **Inventory Reconciliation** section with variance |
| Services/EmailService.cs | Inventory reconciliation table in end-shift email |
| Services/SyncService.cs | SyncDailyClose() includes new fields |
| JumongCloudAPI/Data/PgDatabaseHelper.cs | Migration: adds inventory cost columns |
| JumongCloudAPI/Controllers/SyncController.cs | daily closes includes new columns |
| Services/AppVersion.cs | Current bumped to "1.0.86" |

**Impact:** End shift captures total inventory cost, COGS, stock received cost. Prints/emails reconciliation with variance.

### v1.0.87 — QR Click-to-Enlarge, Browse Button, Crash Fixes

| File | Change |
|---|---|
| Forms/SalesForm.cs:1175-1190 | Click handler on QR PictureBox — opens full-size maximized Form |
| Forms/SalesForm.cs:1191 | ToolTip "Click to enlarge" on QR |
| Forms/SettingsForm.cs:501-513 | ADD QR now opens file picker, auto-copies to assets/ |
| Forms/SettingsForm.cs:519 | QR section height 235?275 (buttons were clipped) |
| Services/SyncService.cs:914,932 | Fixed InvalidCastException — Convert.ToInt32() for SQLite long |
| Services/AppVersion.cs | Current bumped to "1.0.87" |

### v1.0.88 — Auto-Cleanup on Startup (Slow HDD Fix)

| File | Change |
|---|---|
| Helpers/ErrorLogger.cs | Added TrimLog() — keeps last 500 lines if error.log > 1MB |
| Services/EmailService.cs:407-410 | FlushQueue() discards entries older than 7 days |
| Program.cs:66-71 | Startup: ErrorLogger.TrimLog(), delete SyncLog > 30 days |
| Services/AppVersion.cs | Current bumped to "1.0.88" |

**Impact:** Fixes 5-10 min startup on HDD. Logs auto-trim. Old failed emails cleared.

### v1.0.89 — POS Promo Message (Local Settings)

| File | Change |
|---|---|
| Forms/SettingsForm.cs:521-540 | Added POS PROMO section with multiline textbox |
| Forms/SettingsForm.cs:110-111 | Loads PosPromoMessage in LoadSettings |
| Forms/SettingsForm.cs:176-185 | Saves PosPromoMessage on Save |
| Forms/SalesForm.cs:1228-1237,1246 | Added _lblPromo Label below QR, _promoText field |
| Forms/SalesForm.cs:1406-1412 | Recalc shows/hides promo label |
| Forms/SalesForm.cs:1435-1436 | Loads PosPromoMessage from local Settings |
| Services/AppVersion.cs | Current bumped to "1.0.89" |

### v1.0.90 — Cloud-Managed POS Promo (Dashboard + Auto-Fetch)

| File | Change |
|---|---|
| JumongCloudAPI/Data/PgDatabaseHelper.cs:551-557 | Added pos_promo table (id, message, updated_at) with seed |
| JumongCloudAPI/Controllers/DashboardController.cs:2586-2610 | GET/POST /dashboard/pos-promo endpoints |
| JumongCloudAPI/wwwroot/components.js:953-973 | posPromoPanel Alpine component |
| JumongCloudAPI/wwwroot/index.html:62 | POS Promo nav item in sidebar |
| JumongCloudAPI/wwwroot/index.html:1988-2012 | POS Promo section panel with textarea + SAVE |
| Services/SyncService.cs:976-994 | FetchPromoMessageAsync() — cloud API with local fallback |
| Forms/SalesForm.cs:1441-1456 | FetchCloudPromoAsync() after LoadQrCodes |
| Forms/SettingsForm.cs:504-516 | Fixed IOEception in ADD QR — delete + retry on locked file |
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
| Stores confirmed | HQ `7159` (DESKTOP-UU8E0D4 / 192.168.1.26 / app 1.1.38), HVR `AA36` (DESKTOP-TK63MO6 / 192.168.1.15 / app 1.1.38), U Got Minimart `E174` (DESKTOP-NISQ3Q7 / 192.168.1.152 / app 1.1.38), ACGS `A80C` (DESKTOP-TK63MO6 / 192.168.0.103 / app 1.1.38) |
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

### 2026-08-11 — Dev PC Migration (DESKTOP-Q36S34R @ 192.168.1.55)

| Item | Detail |
|---|---|
| New DEV PC | `DESKTOP-Q36S34R` @ `192.168.1.55` — all development now happens here; repo cloned at `C:\dev\JumongPosV1.01`, non-git assets at `C:\dev\extras\`, publish output `C:\dev\out\client`, Gradle at `C:\dev\gradle\gradle-8.14.3` |
| Server role change | `DESKTOP-I097OO9` @ `192.168.1.21` (Ethernet) + `.41` (Wi-Fi) is now **SERVER ONLY** (Cloud API host, no dev). Repo clone kept at `C:\Users\ADMIN\Desktop\JumongPosV1.01` as read-only reference |
| WinRM both ways | Dev PC → server as `DESKTOP-I097OO9\remotedev` / `Jum0ng!Dev55`; server → dev PC as `DESKTOP-Q36S34R\serverdev` / `Jum0ng!Dev55`. TrustedHosts + `LocalAccountTokenFilterPolicy=1` on both machines. Ports 5985 + ICMP open both ways |
| Ethernet fixed | Server NIC re-negotiated to **1 Gbps full duplex** (cable swap, 2026-08-11; was 10 Mbps) |
| Deploy flow | Deploys now run **from the dev PC via WinRM** (push publish → `net stop/start JumongCloudAPI`). Server-side bats (`deploy_api.bat` / `deploy_web.bat` / `deploy_client.bat`) remain as fallback only |
| AGENTS.md | Machine Roles table updated (server-only vs dev PC), WinRM access section added, Deploying Cloud API + Build & Deploy rewritten for the dev PC driver flow |

**Impact:** The dev PC agent must use `C:\dev\JumongPosV1.01` for all builds/edits and push via WinRM to `DESKTOP-I097OO9`; the server clone is reference-only. Any agent on the dev PC that only knows server paths (`C:\Users\ADMIN\Desktop\JumongPosV1.01`, `C:\JumongAPI\`) is reading a stale AGENTS.md — pull after this push.


## Warehouse Mobile App (Android, WarehouseApp/)

WebView wrapper app that loads https://admin.jumongdev.com/whmobile.html (login via whapp API, SELL/INVENTORY/SETUP tabs, Bluetooth thermal printing, in-app update). Source: Kotlin, no gradle wrapper — use system Gradle 8.14.3 from C:\dev\gradle\gradle-8.14.3\ with Android Studio JBR 21.

> **NOTE (2026-08-11):** APK builds still run on the **SERVER** (`C:\Users\ADMIN\Desktop\JumongPosV1.01\WarehouseApp`) until Android Studio + Android SDK are installed on the dev PC. The Gradle zip + keystores are already on the dev PC (`C:\dev\gradle\`, `C:\dev\JumongPosV1.01\WarehouseApp\*.keystore`). When the dev PC is APK-ready: `sdk.dir=C:/Users/<you>/AppData/Local/Android/Sdk` (forward slashes) in `local.properties`, `JAVA_HOME=C:\Program Files\Android\Android Studio\jbr`, and use `C:\dev\gradle\gradle-8.14.3\bin\gradle.bat`.

### Build & Sign
`powershell
Set-Location "C:\Users\ADMIN\Desktop\JumongPosV1.01\WarehouseApp"   # SERVER (until dev PC has Android Studio)
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

### v1.1.39 (POS) + v1.0.15 (APK) — All Print Fonts Bold

| File | Change |
|---|---|
| `Forms/ProductsForm.cs:383` | CHECKLIST print font: `new Font("Courier New", 9F)` — added `FontStyle.Bold` (was Regular) |
| `Forms/CreditManagementForm.cs:329` | Credit statement PRINT font: `new Font("Consolas", 10F)` — added `FontStyle.Bold` (was Regular) |
| `WarehouseApp/.../BluetoothPrinter.kt` | **ESC_BOLD_ON** (`0x1B 0x45 0x01`) written before every line's content in `printText()` and `printTest()` — all mobile thermal prints (sale receipt, reprint, receiving voucher, end shift, credit payment, inventory) now print BOLD |
| `WarehouseApp/app/build.gradle` | versionCode 16, versionName `"1.0.15"` |
| `JumongCloudAPI/wwwroot/updates/warehouse-version.json` | `"1.0.15"` changelog; rebuilt + signed APK (same jumong_sign keystore) deployed to all update folders, no BOM |
| `Services/AppVersion.cs` | Current bumped to `"1.1.39"` |
| `AGENTS.md` | Fixed 12 U+FFFD mojibake chars (v1.0.85–1.0.90 section) from last night's bad encoding save |

**Impact:** Every print path in the POS client (receipts already bold, now also CHECKLIST + credit statement) and every mobile warehouse print (ESC/POS ESC E bold) prints bold. Cheap thermal clones universally support `ESC E`; if a specific printer ignores it, fallback is `ESC !` bit 3 (not yet needed). Client published to `C:\JumongAPI\client`; APK live at `admin.jumongdev.com/updates/JumongWarehouse.apk` (verified 200).

### v1.1.40 (Agent only) — Fix Stale ERROR Badges on AGENTS Dashboard

| File | Change |
|---|---|
| `tools/Agent/Program.cs:231` | **`HasRecentErrors` timezone fix** — was `CreatedAt > datetime('now','-1 hour')` (UTC), but `SyncLog.CreatedAt` is stored as localtime. PH is UTC+8, so any failure from the last ~9 hours counted as "recent", keeping the red ERROR badge lit long after the store recovered. Now `datetime('now','localtime','-1 hour')` — badge clears ~1 hour after the last real failure. |
| `tools/Agent/Program.cs:246-257` | **`GetErrorSummary` rewrite** — was returning the last 3 lines containing `[ERROR]`/`Exception` anywhere in the whole error.log (stale stack traces shown forever). Now walks backward from the end, parses the `[yyyy-MM-dd HH:mm:ss]` header on each entry, and shows at most 3 entries newer than 2 hours (timestamps included). |
| — | Rebuilt agent (`dotnet publish -c Release -r win-x64 --self-contained true`), refreshed `agent.zip` at `C:\JumongAPI\wwwroot\agent.zip` (34.7 MB, live 200), and pushed to all 3 erroring stores via `ps` command (download zip → kill Agent.exe → expand → start). All stores verified: `hasError=false`, summaries show only current-window entries. |

**Impact:** The AGENTS dashboard no longer shows stale red ERROR badges for hours after recovery. Root cause of the 2026-08-11 badges: HQ/HVR/ACGS had real sync failures (DNS "No such host is known" + TCP connect timeouts on `FetchPromoMessageAsync`/`DownloadCustomersAsync`, last FAILs 19:47/18:44/18:43 PH) — those were transient (store internet/DNS outage window), all stores reconnected and synced OK afterwards (Naic never had recent failures). Agent-only change — no POS client or API deploy needed; stores with the new agent.zip already updated.

### v1.1.40 (POS) + v1.0.16 (APK) — Full Item Names on Receipts (Wrap Not Truncate) + Double-Height Mobile Item Names + Mobile Inventory Stock Trail

| File | Change |
|---|---|
| `Services/PrinterService.cs:958-967` | **Wholesale receipt fix** — removed `name[..(chars - 11)] + "..."` truncation. Long item names now WRAP onto continuation lines (indented `"  "`) via existing `WrapText()` helper — full name always prints. |
| `Services/PrinterService.cs:154-166` | **Retail receipt fix** (`BuildReceiptLines`) — same: removed `name[..(lineChars - 2)] + ".."`, wrapped names via `WrapText()` with indented continuations. |
| `JumongCloudAPI/wwwroot/whmobile.html:1972-2013` | **Mobile reprint fix** — `buildReceiptTextReprint()` replaced `fit()` truncation (48 chars + `~`) with `wrap()` — full names on reprints. |
| `JumongCloudAPI/wwwroot/whmobile.html:1655-1661` | **Mobile sale receipt** — item-name lines now prefixed `<<BIG>>` (first line) / `<<BIG>>  ` (continuation) for double-height printing. |
| `WarehouseApp/.../BluetoothPrinter.kt` | **`<<BIG>>` marker support** — `printText()` detects `<<BIG>>` prefix, sends `GS ! 0x10` (double-height, same width) before the line + `GS ! 0x00` after. Item names print ~2× taller for readability. |
| `WarehouseApp/app/build.gradle` | versionCode 17, versionName `"1.0.16"` |
| `JumongCloudAPI/wwwroot/updates/warehouse-version.json` | `"1.0.16"` changelog; rebuilt + signed APK (same jumong_sign keystore) deployed to all update folders, no BOM |
| `JumongCloudAPI/wwwroot/whmobile.html:1745` | **Mobile inventory search → stock trail** — `searchInventory()` now sets `prodCache = data` so the detail modal can find searched products. |
| `JumongCloudAPI/wwwroot/whmobile.html:1762` | **Mobile inventory cards now tappable** — `onclick="showProductDetail(p.id)"` added to inventory search results (same as Products tab) → opens detail modal → **📜 STOCK TRAIL** button → `loadStockTrail()` (was unreachable from Inventory tab; endpoint `/dashboard/warehouse/stock-trails?productId=` already existed). |
| `Services/AppVersion.cs` | Current bumped to `"1.1.40"` |

**Impact:** Long wholesale/retail item names (e.g. `LUCKY ME! INSTANT PANCIT CANTON KALAMANSI 80G (BY 6)`) now print IN FULL everywhere — POS wholesale + retail receipts wrap to continuation lines instead of `...` cut-off, and mobile sale/reprint receipts wrap + print item names DOUBLE-HEIGHT (GS !) for readability. Mobile inventory search items can now be tapped to view product details + full stock trail (receive/sale/void/transfer/manual set with qty change and before→after stock). Deployed: web `whmobile.html` + version json to live `C:\JumongAPI\wwwroot\` + server repo copy; POS client `v1.1.40` to `C:\JumongAPI\client\` (stores via UPDATE APP); APK v1.0.16 signed + live (verified 200, 2.6 MB). No cloud API change (endpoint already existed).

### 2026-08-12 — Store IP + POS Path Verification (via agents)

| Item | Detail |
|---|---|
| HQ | `DESKTOP-UU8E0D4` @ `192.168.1.26` (was .37, DHCP change), POS at `C:\Users\ADMIN\Desktop\JumongPosHW\`, agent at `...\JumongPosHW\agent\Agent.exe` |
| HVR | `DESKTOP-TK63MO6` @ `192.168.1.15` (was .4, DHCP change), POS at `C:\Users\Admin\Desktop\JumongPos\`, agent at `...\JumongPos\Agent\Agent.exe` |
| ACGS | `DESKTOP-TK63MO6` @ `192.168.0.103` (was .100, DHCP change), POS at `C:\JumongPos\`, agent at `C:\JumongPos\Agent\Agent.exe` |
| Naic | `DESKTOP-NISQ3Q7` @ `192.168.1.152` (unchanged) |
| Note | Machine Roles table + Stores table in AGENTS.md updated to verified IPs/paths. All 4 stores confirmed on app v1.1.38, all CloudApiUrl = `https://admin.jumongdev.com/api`. |

### v1.1.15 (Cloud API) — Wholesale Void Fix for Credit Sales (500 on void)

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:3324-3326` | **`WhVoidSale` credit reversal pos_id collision fixed** — voiding a **Credit** sale inserted a `credit_transactions` reversal row with `pos_id = saleId, store_id = ''`, which collides with the original `Sale` row (`UNIQUE(store_id, pos_id)` on `credit_transactions`). Result: **all wholesale voids of credit sales failed with 500** and the whole void transaction rolled back — stock NOT restored, sale NOT marked voided. Affected both POS client (WarehouseSellForm VOID/VOID ITEM) and mobile app (`whmobile.html` VOID SALE / item void) since they share the endpoint. Fix: pos_id now uses `-NEXTVAL('credit_transactions_id_seq')` (same pattern as the credit-pay endpoint). |
| `JumongCloudAPI/Controllers/DashboardController.cs:1175,1225` | Version bumped to `"1.1.15"`. |
| — | **Diagnosis via WinRM**: dry-run of the exact INSERT on the live DB confirmed `duplicate key value violates unique constraint "credit_transactions_store_id_pos_id_key"` for sale 475. After fix, same insert returns `INSERT 0 1`. Non-destructive (rolled back). |

**Impact:** Void now works for wholesale Credit sales again (POS + mobile). Cash sale voids were never affected (no credit reversal row). Deployed via WinRM (`net stop` → copy publish → `net start`), live API version verified `1.1.15`, `/dashboard/health` = `db: ok`. Interestingly, void log audit shows the reversal was silently skipped pre-v1.1.37 when the constraint didn't exist — the collision only bites since `credit_transactions_store_id_pos_id_key` was (re)created by the startup migration block in PgDatabaseHelper.cs:347-349.

### v1.1.16 (Cloud API) — Transfer Stock Guard (no more negative stock / silent skipped deductions)

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:2519-2546` | **`WhCreateTransfer` now BLOCKS insufficient stock** — the product check previously validated existence only. Now reads `stock_qty` and returns `BadRequest "Insufficient stock for {name}: only {X} available, {Y} requested. Receive stock first."` if `stock_qty < qty` (transaction rolled back, transfer not created). Manager must receive stock into the system before creating a transfer. |
| `JumongCloudAPI/Controllers/DashboardController.cs:2603-2660` | **`WhReceiveTransfer` now honors the deduction guard** — the guarded UPDATE (`... WHERE id = @pid AND stock_qty >= @bq`) previously had its `ExecuteNonQuery()` result DISCARDED: when stock was insufficient (0 rows affected) the code still wrote the `transfer_out` trail (−qty), set `received_qty = qty`, and marked the transfer `completed`. This is what caused the "negative stock" trail story (e.g., Mighty Green Menthol id 5: transfer #630 asked 100, only 10 in system → trail replayed to −90 while the stock column stayed 10). Now: 0 rows affected → item treated as **shortage** (`received_qty = 0`, **no trail row written**, added to `shortages`, transfer status `partial`). Trail is only ever written when the deduction actually succeeded. Also removed dead `transferOut` variable. |
| `JumongCloudAPI/Controllers/DashboardController.cs:1175,1225` | Version bumped to `"1.1.16"`. |
| PostgreSQL data (2026-08-13) | **Mighty Green Menthol (id 5) corrected**: stock column was 510 (10 recorded + 500 RECEIVE by jovani) while the trail replay was 410 (−90 + 500) — the transfer #630 −100 deduction never landed on the column because of the guard bug. Applied `UPDATE wh_products SET stock_qty = stock_qty - 100 WHERE id = 5` (no trail row — the −100 was already in the trail). View = trail = 410 verified via live API. Left as-is per owner: Transfer #630 (100 pcs to ACGS from an unrecorded new delivery) is a real movement, ACGS store stock untouched, +500 receiving record untouched. |

**Impact:** Transfer creation beyond system stock is now rejected with a clear message, and any stock that runs out between transfer creation and POS receive is reported as a PARTIAL shortage with a correct trail — silent negative stock is impossible. Post-fix integrity scan note: several OTHER products still show column vs trail-replay divergence (e.g., id 65 +432, id 20 +360, etc.) — pre-existing, created by the earlier agent snapshot cleanup / stock recalcs — NOT touched (owner aware; only id 5 was in scope). WinRM deploy note: `Copy-Item -ToSession` while the service is running fails on locked System*.dll files — always `net stop` BEFORE copying, then `net start` (verified: fresh exe timestamp + `/dashboard/version` = 1.1.16).

### Web-only (2026-08-13) — Dashboard menu rename: Products → Master Products

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/index.html:54` | Sidebar nav item `{ id:'products', label:'Products' }` → `'Master Products'` (the main section = master catalog; warehouse subpage `Product` unchanged). |
| `JumongCloudAPI/wwwroot/index.html:214` | Dashboard summary card label `'Products / Customers'` → `'Master Products / Customers'` for consistency. |

**Impact:** Dashboard sidebar now says **Master Products** for the master catalog section. Web-only change — copied to live `C:\JumongAPI\wwwroot\index.html`, verified live (`Master Products` present). No API rebuild/deploy needed.

### Web-only (2026-08-13) — Warehouse Product EDIT/DEL removed (read-only from master)

| File | Change |
|---|---|
| `JumongCloudAPI/wwwroot/index.html:1173-1176` | **Warehouse → Product subpage Actions column: EDIT and DEL buttons REMOVED** — replaced with a single read-only **TRAIL** button (`showTrail(x.id)`). Employees can no longer change warehouse product details (name/barcode/price/cost/units); all detail edits must be done on the **Master Products** section, which auto-syncs to warehouse + POS clients. Stock changes still happen via Inventory subpage ADJUST/+RETURN and RECEIVE. Client EDIT/DEL in Online Order subpage untouched; master catalog EDIT/DEL untouched. |

**Impact:** No more employee-driven divergence between `wh_products` and `master_products` from the web dashboard. Owner decision: warehouse product editing was "walang kwenta" — the master catalog is the single source of truth. Web-only change — copied to live `C:\JumongAPI\wwwroot\index.html`, verified live (`openEdit(x.id)` now appears only once = clients table).

### v1.1.17 (Cloud API) + web-only — Web Dashboard Login (Username + PIN, per-user WEB ACCESS)

| File | Change |
|---|---|
| `JumongCloudAPI/Data/PgDatabaseHelper.cs` | Migration: `ALTER TABLE users ADD COLUMN IF NOT EXISTS web_access BOOLEAN NOT NULL DEFAULT FALSE` + seed `UPDATE users SET web_access = TRUE WHERE role = 'Admin'` (all admins get access on startup). |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **3 new endpoints**: `POST /web/login` (username + password_hash match, `is_active` + `web_access=true` required; Admin → ALL stores, non-admin → only `user_stores`; token in `whapp_tokens`, keep last 5), `GET /web/me` (token validation for page reloads), `POST /web/logout`. Users GET now returns `webAccess`; CreateUser/UpdateUser read/write `web_access`. Version bumped `"1.1.16"` → `"1.1.17"`. |
| `JumongCloudAPI/wwwroot/index.html` | **Full-screen login screen** (`#loginScreen`) shown until valid session; session in sessionStorage (`jpos_web_user`/`jpos_web_token`); auto-validate via `/web/me` on load; **LOGOUT** button in header; sidebar + app body hidden until login (`#appRoot`). Store selector: Admin → "All Stores" + all stores (as before); non-admin → only assigned stores, auto-selected first store (no "All Stores" option). |
| `JumongCloudAPI/wwwroot/components.js` + `index.html` | User Manager: `webAccess` added to `openAdd`/`openEdit`/`save` form + **WEB** badge (green) in users table + **Web Access (admin.jumongdev.com)** checkbox in user modal. |
| PostgreSQL | `web_access` column added; all existing Admin users flagged. |

**Impact:** `admin.jumongdev.com` now requires username + the user's POS PIN to open. Only users ticked **Web Access** in User Manager can log in (Admin users get it automatically = all access to all stores). Sessions last until the browser tab closes; LOGOUT kills the token server-side. This is a page-level gate — the API endpoints stay open for POS clients/agents/mobile (no auth there yet, so a technical user could still call the API directly). Deployed: API rebuilt (WinRM stop → copy → start, live version verified `1.1.17`), `index.html`/`components.js` copied live. Verified: admin login OK (6 stores, allStores=true), wrong PIN → 401, cashier without WEB ACCESS → 401, logout → token invalidated (`/web/me` → 401).

### v1.1.18 (Cloud API) + v1.1.41 (POS) — Stock Snapshot Pipeline Restored + Stock Status Warehouse Merge + Sidebar Restructure

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs:2921-2950` | **`WhStockSnapshot` implemented (was a no-op `return Ok`)**: POS clients were already sending full stock every 30s (`PushAllUnsyncedAsync`) + delta snapshots, but the endpoint discarded everything → server `products.stock_qty` only updated opportunistically via SyncProduct (sale/receiving) → server vs client stock mismatch (e.g. HVR). Now reads `storeId` from body and `UPDATE products SET stock_qty=@q, name=@n, barcode=@b, synced_at=NOW() WHERE store_id=@sid AND pos_id=@pid` (transactional; **never touches `wh_products`** — the reason it was neutered; skips rows that don't exist yet — SyncProduct creates them). Old clients without storeId → `skipped` message, no crash. |
| `JumongCloudAPI/Controllers/DashboardController.cs:974-1012` | **`/stock-status` warehouse merge** — when NO store filter: `UNION ALL` of per-store `products` + `wh_products` rows (LEFT JOIN `master_products` via `master_product_id` for per-pc price/cost fallbacks, `storeId='STORE-WAREHOUSE'`). Specific store → store-only as before. `ORDER BY stock_qty ASC` across the union. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | Version bumped `"1.1.17"` → `"1.1.18"`. |
| `Services/SyncService.cs:1280` | `SyncStockSnapshotAsync` payload now includes `storeId = StoreId` (both the 30s full push and the 5s-debounce delta push use this method). |
| `Services/AppVersion.cs` | Bumped to `"1.1.41"` — **stores must UPDATE APP** so snapshots carry storeId; otherwise the server skips their snapshots (guard message says "update app"). |
| `JumongCloudAPI/wwwroot/index.html` | **Sidebar restructured**: `POS CLIENT` header with `Reports` sub-header (Sales Report / Inventory Cost / Shift History), `Master Products`, `Inventory` sub-header (renamed from STOCK; Recent Receiving / Stock Status). New `isSub` + `item.indent` support in the nav template. Warehouse group unchanged (owner: "ok na"). Stock section header text `STOCK —` → `INVENTORY —`. |

**Impact:** Server stock now equals each POS client's live stock within ~30s (full push every 30s + delta on change) — the HVR-style server-vs-client mismatch is fixed at the source. Stock Status with All Stores shows every location including Warehouse (394 wh rows in the live view). Sidebar groups everything POS-related under POS CLIENT with Reports/Inventory sub-headers. Deployed: API rebuilt (WinRM, version `1.1.18` verified), web files copied live, POS client published to `C:\JumongAPI\client\` (stores via UPDATE APP). Verified: snapshot probe `updated:1` on real product (idempotent echo), missing-storeId → skipped message, stock-status union returns warehouse rows, per-store filter excludes warehouse, menu renders POS CLIENT/Reports/Inventory.

### v1.1.19 (Cloud API) + v1.1.42 (POS) — Startup/Reconnect Auto-Drain + POS Sync Status on Dashboard

| File | Change |
|---|---|
| `Services/SyncService.cs` | **`GetPendingCounts()`** — single SQL with subselects counting unsynced rows in Sales (non-voided), StockTrail, VoidLog, CreditTransactions, DailyClose, Expenses + SyncQueue. **`PostPosStatusAsync()`** — POSTs `{storeId, pending}` to `/dashboard/pos-status` after every `PushAllUnsyncedAsync` (so ~every 30s per store). **`DrainAllUnsyncedAsync()`** — loops `RetryFailedAsync` (drains SyncQueue) + `PushAllUnsyncedAsync` up to 10 passes until nothing pending, then posts status. |
| `Forms/MainForm.cs` | **Startup flush** — 3s after MainForm loads, background task runs `DrainAllUnsyncedAsync()` (offline-overnight stores now push EVERYTHING — sales, closes, expenses, queued failures — automatically on open; no manual SYNC ALL). **Reconnect trigger** — `_wasConnected` field: when the 10s connection check transitions OFF→ON, instantly runs `DrainAllUnsyncedAsync()` (covers mid-day outages). |
| `Services/AppVersion.cs` | Bumped to `"1.1.42"`. |
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`POST /dashboard/pos-status`** (stores pending counts in memory per storeId) + **`GET /dashboard/pos-status`** (list for dashboard). `_posStatus` ConcurrentDictionary next to `_agents`. Version bumped `"1.1.18"` → `"1.1.19"`. |
| `JumongCloudAPI/wwwroot/index.html` + `components.js` | **AGENTS cards now show a sync chip**: 🟢 **SYNC OK** (0 pending, posted < 90s ago) / 🟠 **N PENDING** (with breakdown tooltip) / gray **offline** or **—** (no status yet). `agentsPanel` fetches `/pos-status` on load + every 15s while the section is open. |

**Impact:** Converts passive "background pushes whenever the timer ticks" into active draining — startup flush + reconnect trigger eliminate the HVR 2026-08-12 scenario (98 sales stranded until a manual 22:37 SYNC) and the 2097-item SyncQueue backlog (auto-retried, no manual button needed). Dashboard AGENTS tab shows each store's live sync health at a glance — no more digging through SQL to find unsynced records. DailyClose was never broken: it syncs immediately on creation (verified: HVR 08-12 close created 06:52:17, arrived server 06:52:35 — 18s gap; total 661.00 = exact sum of the 98 sales). Deployed: API v1.1.19 live (verified), web files live (syncChip + pos-status present), POS client published to `C:\JumongAPI\client\` (stores via UPDATE APP). EXCEPTION: `STORE-DEV-0001` (dev PC) never posts pos-status.

### v1.1.20 (Cloud API) + web-only — Mobile End Shift: Zero-Cash Save Guard + Expected Cash Breakdown

| File | Change |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | **`WhEndShift` save guard** — `cashOnHand <= 0 && expectedCash > 0` → `400` rejection (no INSERT). Fixes the 2026-08-12 warehouse close #4 that was SAVED with `cash_on_hand=0` → bogus `difference=-325,314`. Version bumped `"1.1.19"` → `"1.1.20"`. |
| `JumongCloudAPI/wwwroot/whmobile.html` | **Expected Cash breakdown line** — `Cash Sales ₱X + Credit Collected ₱Y` under Expected Cash (removes the "why is expected so big" confusion; e.g. today: 11,701 cash sales + 86,776 CHOICHAI credit payment = 98,477). **Save block** — client-side rejects SAVE when entered cash = 0 and expected > 0; **large-discrepancy confirm** (>₱50k asks "Continue saving?"). JS validated with node --check. |
| — | **Diagnosis:** "-98,477" seen on mobile was the SHORT line (cash entered = 0) vs expected 98,477 — correct math (cash sales 11,701 + 86,776 credit collected today as Cash). Real bug was the DUAL issue: (1) close #4 saved with 0 denominations; (2) expected cash includes big credit payments that shocked the cashier. Verified live: guard returns 400 + message, no close row created. |

**Impact:** No more zero-cash end shifts at the warehouse. Cashiers now see WHY expected cash is big (credit collections counted). Deployed: API v1.1.20 live (WinRM, verified), whmobile.html live. NOTE: historical close #4 (2026-08-12) still has the wrong `-325,314` — pending owner input for actual drawer cash to correct.
