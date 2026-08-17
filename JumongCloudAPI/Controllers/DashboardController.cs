using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;
using System.Data;
using JumongCloudAPI.Data;

namespace JumongCloudAPI.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private string StoreFilter(string? storeId, string prefix)
    {
        if (string.IsNullOrEmpty(storeId)) return "";
        return $" AND {prefix}.store_id = @storeId";
    }

    private string TimeframeClause(string? range, string col, NpgsqlCommand cmd)
    {
        if (string.IsNullOrEmpty(range) || range == "all") return "";

        var qDate = HttpContext.Request.Query["date"].FirstOrDefault();
        var qDateTo = HttpContext.Request.Query["date_to"].FirstOrDefault();

        if (!string.IsNullOrEmpty(qDateTo) && !string.IsNullOrEmpty(qDate)
            && DateTime.TryParse(qDate, out var dt) && DateTime.TryParse(qDateTo, out var dt2))
        {
            cmd.Parameters.AddWithValue("date_from", dt);
            cmd.Parameters.AddWithValue("date_to", dt2);
            return $" AND {col}::date >= @date_from AND {col}::date <= @date_to";
        }
        if (!string.IsNullOrEmpty(qDate) && DateTime.TryParse(qDate, out var d))
        {
            cmd.Parameters.AddWithValue("date", d);
            return $" AND {col}::date = @date";
        }

        return range switch
        {
            "today"    => $" AND {col}::date = CURRENT_DATE",
            "yesterday"=> $" AND {col}::date = CURRENT_DATE - INTERVAL '1 day'",
            "week"     => $" AND {col} >= CURRENT_DATE - INTERVAL '7 days'",
            "month"    => $" AND {col} >= CURRENT_DATE - INTERVAL '30 days'",
            _          => ""
        };
    }

    [HttpGet("stores")]
        public IActionResult GetStores()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT DISTINCT s.store_id, COALESCE(st.store_name, '') AS store_name
                FROM (
                    SELECT store_id FROM sales WHERE store_id != ''
                    UNION
                    SELECT store_id FROM stores
                ) s
                LEFT JOIN stores st ON s.store_id = st.store_id
                ORDER BY s.store_id";
            var stores = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                stores.Add(new { storeId = reader.GetString(0), storeName = reader.GetString(1) });
            return Ok(stores);
        }

        [HttpPost("stores/rename")]
        public IActionResult RenameStore([FromBody] RenameStoreRequest req)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO stores (store_id, store_name) VALUES (@id, @name)
                ON CONFLICT (store_id) DO UPDATE SET store_name = @name";
            cmd.Parameters.AddWithValue("id", req.StoreId);
            cmd.Parameters.AddWithValue("name", req.StoreName);
            cmd.ExecuteNonQuery();
            return Ok(new { success = true });
        }

        [HttpGet("summary")]
        public IActionResult GetSummary([FromQuery] string? storeId = null, [FromQuery] string? range = null, [FromQuery] string? date = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);

            var tfSales = TimeframeClause(range, "sale_date", cmd);
            var tfExp = TimeframeClause(range, "timestamp", cmd);
            if (string.IsNullOrEmpty(range) || range == "all") tfSales = "";

            var slj = "FROM sale_items si JOIN sales s ON si.sale_id = s.pos_id AND si.store_id = s.store_id";
            cmd.CommandText = $@"
                SELECT 
                    (SELECT COUNT(*) FROM sales WHERE is_voided = false {StoreFilter(storeId, "sales")}{tfSales}) AS total_sales,
                    (SELECT COALESCE(SUM(si.total_price),0) {slj} WHERE s.is_voided = false AND si.is_voided = false {StoreFilter(storeId, "s")}{tfSales.Replace("sale_date","s.sale_date")}) AS total_revenue,
                    (SELECT COALESCE(SUM(amount),0) FROM expenses WHERE 1=1 {StoreFilter(storeId, "expenses")}{tfExp}) AS total_expenses,
                    (SELECT COUNT(*) FROM products WHERE 1=1 {StoreFilter(storeId, "products")}) AS total_products,
                    (SELECT COUNT(*) FROM customers WHERE is_active = true {StoreFilter(storeId, "customers")}) AS total_customers,
                    (SELECT COALESCE(SUM(si.total_price),0) {slj} WHERE s.is_voided = false AND si.is_voided = false AND s.sale_date::date = CURRENT_DATE {StoreFilter(storeId, "s")}) AS today_revenue,
                    (SELECT COUNT(*) FROM sales WHERE is_voided = false AND sale_date::date = CURRENT_DATE {StoreFilter(storeId, "sales")}) AS today_sales,
                    (SELECT COALESCE(SUM(si.total_price),0) {slj} WHERE s.is_voided = false AND si.is_voided = false AND s.payment_method = 'Cash' {StoreFilter(storeId, "s")}{tfSales.Replace("sale_date","s.sale_date")}) AS total_cash_sales,
                    (SELECT COALESCE(SUM(si.total_price),0) {slj} WHERE s.is_voided = false AND si.is_voided = false AND s.payment_method = 'E-Wallet' {StoreFilter(storeId, "s")}{tfSales.Replace("sale_date","s.sale_date")}) AS total_ewallet_sales,
                    (SELECT COALESCE(SUM(si.total_price),0) {slj} WHERE s.is_voided = false AND si.is_voided = false AND s.payment_method = 'Credit' {StoreFilter(storeId, "s")}{tfSales.Replace("sale_date","s.sale_date")}) AS total_credit_sales,
                    (SELECT COALESCE(SUM(si.total_price),0) {slj} WHERE s.is_voided = false AND si.is_voided = true {StoreFilter(storeId, "s")}{tfSales.Replace("sale_date","s.sale_date")}) AS total_voided,
                    (SELECT COALESCE(SUM(difference),0) FROM daily_closes WHERE close_date::date = CURRENT_DATE {StoreFilter(storeId, "daily_closes")}) AS today_variance
            ";
            var row = cmd.ExecuteReader();
            row.Read();
            var result = new
            {
                totalSales = row.GetInt32(0),
                totalRevenue = row.GetDecimal(1),
                totalExpenses = row.GetDecimal(2),
                totalProducts = row.GetInt32(3),
                totalCustomers = row.GetInt32(4),
                todayRevenue = row.GetDecimal(5),
                todaySales = row.GetInt32(6),
                totalCashSales = row.GetDecimal(7),
                totalEwalletSales = row.GetDecimal(8),
                totalCreditSales = row.GetDecimal(9),
                totalVoided = row.GetDecimal(10),
                todayVariance = row.GetDecimal(11)
            };
            return Ok(result);
        }

        [HttpGet("trends")]
        public IActionResult GetTrends([FromQuery] int days = 30, [FromQuery] string? storeId = null, [FromQuery] string? range = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var tf = TimeframeClause(range, "sale_date", cmd);
            if (string.IsNullOrEmpty(range) || range == "all") tf = "";
            cmd.CommandText = $@"
                SELECT sale_date::date AS day, 
                       COUNT(*) AS sales_count, 
                       COALESCE(SUM(grand_total),0) AS revenue,
                       COUNT(DISTINCT user_id) AS cashiers
                FROM sales s
                WHERE is_voided = false {StoreFilter(storeId, "s")}{tf}
                GROUP BY sale_date::date 
                ORDER BY day";
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new { day = reader.GetDateTime(0).ToString("yyyy-MM-dd"), salesCount = reader.GetInt32(1), revenue = reader.GetDecimal(2), cashiers = reader.GetInt32(3) });
            return Ok(data);
        }

        [HttpGet("top-products")]
        public IActionResult GetTopProducts([FromQuery] int limit = 10, [FromQuery] string? storeId = null, [FromQuery] string? range = null, [FromQuery] string? sort = "qty")
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var tf = TimeframeClause(range, "s.sale_date", cmd);
            if (string.IsNullOrEmpty(range) || range == "all") tf = "";
            var orderBy = sort == "profit"
                ? "ORDER BY total_profit DESC"
                : "ORDER BY total_qty DESC";
            cmd.CommandText = $@"
                SELECT si.product_name,
                       COALESCE(p.barcode, '') AS barcode,
                       COALESCE(p.category, '') AS category,
                       COALESCE(si.unit_name, '') AS unit_name,
                       SUM(si.quantity) AS total_qty,
                       SUM(si.total_price) AS total_revenue,
                       SUM(si.quantity * COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0)) AS total_cost,
                       SUM(si.total_price) - SUM(si.quantity * COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0)) AS total_profit
                FROM sale_items si
                JOIN sales s ON si.sale_id = s.pos_id AND si.store_id = s.store_id
                LEFT JOIN products p ON si.product_id = p.pos_id AND si.store_id = p.store_id
                WHERE s.is_voided = false AND si.is_voided = false {StoreFilter(storeId, "s")}{tf}
                GROUP BY si.product_name, p.barcode, p.category, si.unit_name
                {orderBy}
                LIMIT @limit";
            cmd.Parameters.AddWithValue("limit", limit);
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var revenue = reader.GetDecimal(5);
                var cost = reader.GetDecimal(6);
                var profit = revenue - cost;
                var margin = revenue > 0 ? (profit / revenue * 100).ToString("F1") : "0.0";
                data.Add(new {
                    productName = reader.GetString(0),
                    barcode = reader.GetString(1),
                    category = reader.GetString(2),
                    unitName = reader.GetString(3),
                    totalQty = reader.GetInt32(4),
                    totalRevenue = revenue,
                    totalCost = cost,
                    totalProfit = profit,
                    marginPct = margin
                });
            }
            return Ok(data);
        }

        [HttpGet("recent-sales")]
        public IActionResult GetRecentSales([FromQuery] int limit = 50, [FromQuery] string? storeId = null, [FromQuery] string? range = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var tf = TimeframeClause(range, "s.sale_date", cmd);
            if (string.IsNullOrEmpty(range) || range == "all") tf = "";
            cmd.CommandText = $@"
                SELECT s.invoice_no, s.sale_date, s.grand_total, s.payment_method, s.order_type, s.is_voided,
                       COALESCE(NULLIF(s.cashier_name,''), NULLIF(u.full_name,''), NULLIF(u.username,''),
                         (SELECT s2.cashier_name FROM sales s2 WHERE s2.store_id = s.store_id AND s2.user_id = s.user_id AND COALESCE(s2.cashier_name,'') <> '' ORDER BY s2.sale_date DESC, s2.id DESC LIMIT 1),
                         'Cashier #' || COALESCE(s.user_id::text,'')) AS cashier, s.store_id
                FROM sales s
                LEFT JOIN users u ON s.user_id = u.pos_id AND s.store_id = u.store_id
                WHERE 1=1 {StoreFilter(storeId, "s")}{tf}
                ORDER BY s.sale_date DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("limit", limit);
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new { invoiceNo = reader.GetString(0), saleDate = reader.GetDateTime(1), grandTotal = reader.GetDecimal(2), paymentMethod = reader.GetString(3), orderType = reader.GetString(4), isVoided = reader.GetBoolean(5), cashier = reader.IsDBNull(6) ? "" : reader.GetString(6), storeId = reader.GetString(7) });
            return Ok(data);
        }

        [HttpGet("void-logs")]
        public IActionResult GetVoidLogs([FromQuery] int limit = 50, [FromQuery] string? storeId = null, [FromQuery] string? range = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var tf = TimeframeClause(range, "vl.created_at", cmd);
            cmd.CommandText = $@"
                SELECT vl.invoice_no, vl.action, vl.reason, vl.product_name, vl.quantity, vl.amount, vl.user_name, vl.created_at, vl.store_id
                FROM void_logs vl
                WHERE 1=1 {StoreFilter(storeId, "vl")}{tf}
                ORDER BY vl.created_at DESC, vl.pos_id
                LIMIT @limit";
            cmd.Parameters.AddWithValue("limit", limit);
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            var seen = new HashSet<string>();
            while (reader.Read())
            {
                var key = reader.GetString(0) + "|" + reader.GetString(1) + "|" + reader.GetInt32(4);
                if (!seen.Add(key)) continue;
                data.Add(new { invoiceNo = reader.GetString(0), action = reader.GetString(1), reason = reader.GetString(2), productName = reader.IsDBNull(3) ? "" : reader.GetString(3), quantity = reader.GetInt32(4), amount = reader.GetDecimal(5), userName = reader.IsDBNull(6) ? "" : reader.GetString(6), createdAt = reader.GetDateTime(7), storeId = reader.GetString(8) });
            }
            return Ok(data);
        }

        [HttpGet("customers")]
        public IActionResult GetCustomers([FromQuery] string? storeId = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"SELECT c.pos_id, c.name, c.phone, c.email, c.loyalty_points, c.credit_balance, c.credit_limit, c.is_active, c.created_at, c.store_id
                FROM customers c WHERE 1=1 {StoreFilter(storeId, "c")} ORDER BY c.name LIMIT 500";
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) data.Add(new { posId = r.GetInt32(0), name = r.GetString(1), phone = r.IsDBNull(2) ? "" : r.GetString(2), email = r.IsDBNull(3) ? "" : r.GetString(3), loyaltyPoints = r.GetInt32(4), creditBalance = r.GetDecimal(5), creditLimit = r.GetDecimal(6), isActive = r.GetBoolean(7), storeId = r.GetString(9) });
            return Ok(data);
        }

        [HttpGet("users")]
        public IActionResult GetUsers([FromQuery] string? storeId = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT u.pos_id, u.username, u.role, u.full_name, u.is_active, u.password_hash, u.mobile_access, u.web_access,
                       COALESCE((SELECT json_agg(us.store_id) FROM user_stores us WHERE us.user_pos_id = u.pos_id), '[]'::json) AS store_ids
                FROM users u
                WHERE u.is_active = true {StoreFilter(storeId, "u")}
                GROUP BY u.pos_id, u.username, u.role, u.full_name, u.is_active, u.password_hash, u.mobile_access, u.web_access
                ORDER BY u.username LIMIT 500";
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var storeIds = r.IsDBNull(8) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.GetString(8)) ?? new();
                data.Add(new { posId = r.GetInt32(0), username = r.GetString(1), role = r.GetString(2), fullName = r.IsDBNull(3) ? "" : r.GetString(3), isActive = r.GetBoolean(4), passwordHash = r.IsDBNull(5) ? "12345" : r.GetString(5), mobileAccess = r.GetBoolean(6), webAccess = r.IsDBNull(7) ? false : r.GetBoolean(7), storeIds });
            }
            return Ok(data);
        }

        [HttpPost("users")]
        public IActionResult CreateUser([FromBody] JsonElement body)
        {
            var username = body.GetProperty("username").GetString() ?? "";
            var fullName = body.TryGetProperty("fullName", out var fn) ? fn.GetString() ?? "" : "";
            var role = body.TryGetProperty("role", out var rl) ? rl.GetString() ?? "Cashier" : "Cashier";
            var passwordHash = body.TryGetProperty("passwordHash", out var ph) ? ph.GetString() ?? "12345" : "12345";
            var mobileAccess = body.TryGetProperty("mobileAccess", out var ma) ? ma.GetBoolean() : false;
            var webAccess = body.TryGetProperty("webAccess", out var wa) ? wa.GetBoolean() : false;
            var storeIds = body.TryGetProperty("storeIds", out var si) && si.ValueKind == JsonValueKind.Array
                ? si.EnumerateArray().Select(x => x.GetString()).Where(s => !string.IsNullOrEmpty(s)).ToList()
                : new List<string?>();

            if (string.IsNullOrEmpty(username)) return BadRequest(new { error = "Username is required" });
            if (storeIds.Count == 0) return BadRequest(new { error = "Select at least one store" });

            using var conn = Data.PgDatabaseHelper.GetConnection();

            // Check duplicate username globally
            using var dupCmd = conn.CreateCommand();
            dupCmd.CommandText = "SELECT COUNT(*) FROM users WHERE LOWER(username) = LOWER(@u)";
            dupCmd.Parameters.AddWithValue("u", username);
            var exists = Convert.ToInt32(dupCmd.ExecuteScalar());
            if (exists > 0) return Conflict(new { error = "Username already exists" });

            // Generate new pos_id (global sequential)
            using var maxCmd = conn.CreateCommand();
            maxCmd.CommandText = "SELECT COALESCE(MAX(pos_id), 0) + 1 FROM users";
            var newPosId = Convert.ToInt32(maxCmd.ExecuteScalar());

            // Insert user (store_id = '' for cloud-managed users)
            using var insCmd = conn.CreateCommand();
            insCmd.CommandText = @"INSERT INTO users (pos_id, store_id, username, role, full_name, is_active, password_hash, mobile_access, web_access, synced_at)
                VALUES (@p, '', @u, @r, @fn, true, @ph, @ma, @wa, NOW()) RETURNING id";
            insCmd.Parameters.AddWithValue("p", newPosId);
            insCmd.Parameters.AddWithValue("u", username);
            insCmd.Parameters.AddWithValue("r", role);
            insCmd.Parameters.AddWithValue("fn", fullName);
            insCmd.Parameters.AddWithValue("ph", passwordHash);
            insCmd.Parameters.AddWithValue("ma", mobileAccess);
            insCmd.Parameters.AddWithValue("wa", webAccess);
            insCmd.ExecuteNonQuery();

            // Insert user_stores entries
            foreach (var sid in storeIds)
            {
                if (string.IsNullOrEmpty(sid)) continue;
                using var usCmd = conn.CreateCommand();
                usCmd.CommandText = "INSERT INTO user_stores (user_pos_id, store_id) VALUES (@p, @sid) ON CONFLICT DO NOTHING";
                usCmd.Parameters.AddWithValue("p", newPosId);
                usCmd.Parameters.AddWithValue("sid", sid);
                usCmd.ExecuteNonQuery();
            }

            return Ok(new { posId = newPosId, username, role, fullName, isActive = true, storeIds });
        }

        [HttpPut("users/{posId}")]
        public IActionResult UpdateUser(int posId, [FromBody] JsonElement body)
        {
            var username = body.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
            var fullName = body.TryGetProperty("fullName", out var fn) ? fn.GetString() ?? "" : "";
            var role = body.TryGetProperty("role", out var rl) ? rl.GetString() ?? "Cashier" : "Cashier";
            var isActive = body.TryGetProperty("isActive", out var ia) ? ia.GetBoolean() : true;
            var passwordHash = body.TryGetProperty("passwordHash", out var ph) ? ph.GetString() : null;
            var mobileAccess = body.TryGetProperty("mobileAccess", out var ma) ? ma.GetBoolean() : false;
            var webAccess = body.TryGetProperty("webAccess", out var wa) ? wa.GetBoolean() : false;
            var storeIds = body.TryGetProperty("storeIds", out var si) && si.ValueKind == JsonValueKind.Array
                ? si.EnumerateArray().Select(x => x.GetString()).Where(s => !string.IsNullOrEmpty(s)).ToList()
                : new List<string?>();

            if (string.IsNullOrEmpty(username)) return BadRequest(new { error = "Username is required" });

            using var conn = Data.PgDatabaseHelper.GetConnection();

            using var cmd = conn.CreateCommand();
            var setClause = "username = @u, role = @r, full_name = @fn, is_active = @ia, mobile_access = @ma, web_access = @wa, synced_at = NOW()";
            if (passwordHash != null) setClause += ", password_hash = @ph";

            cmd.CommandText = $"UPDATE users SET {setClause} WHERE pos_id = @pid";
            cmd.Parameters.AddWithValue("pid", posId);
            cmd.Parameters.AddWithValue("u", username);
            cmd.Parameters.AddWithValue("r", role);
            cmd.Parameters.AddWithValue("fn", fullName);
            cmd.Parameters.AddWithValue("ia", isActive);
            cmd.Parameters.AddWithValue("ma", mobileAccess);
            cmd.Parameters.AddWithValue("wa", webAccess);
            if (passwordHash != null) cmd.Parameters.AddWithValue("ph", passwordHash);

            var rows = cmd.ExecuteNonQuery();
            if (rows == 0) return NotFound(new { error = "User not found" });

            // Replace store tags
            using var delUs = conn.CreateCommand();
            delUs.CommandText = "DELETE FROM user_stores WHERE user_pos_id = @pid";
            delUs.Parameters.AddWithValue("pid", posId);
            delUs.ExecuteNonQuery();

            foreach (var sid in storeIds)
            {
                if (string.IsNullOrEmpty(sid)) continue;
                using var usCmd = conn.CreateCommand();
                usCmd.CommandText = "INSERT INTO user_stores (user_pos_id, store_id) VALUES (@p, @sid) ON CONFLICT DO NOTHING";
                usCmd.Parameters.AddWithValue("p", posId);
                usCmd.Parameters.AddWithValue("sid", sid);
                usCmd.ExecuteNonQuery();
            }

            return Ok(new { success = true });
        }

        [HttpDelete("users/{posId}")]
        public IActionResult DeleteUser(int posId)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();

            using var delUs = conn.CreateCommand();
            delUs.CommandText = "DELETE FROM user_stores WHERE user_pos_id = @pid";
            delUs.Parameters.AddWithValue("pid", posId);
            delUs.ExecuteNonQuery();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE users SET is_active = false, username = username || '_d_' || pos_id, synced_at = NOW() WHERE pos_id = @pid";
            cmd.Parameters.AddWithValue("pid", posId);
            cmd.ExecuteNonQuery();

            return Ok(new { success = true });
        }

        [HttpPost("users/{posId}/change-pin")]
        public IActionResult ChangeUserPin(int posId, [FromBody] JsonElement body)
        {
            var oldPin = body.GetProperty("oldPin").GetString() ?? "";
            var newPin = body.GetProperty("newPin").GetString() ?? "";

            if (string.IsNullOrEmpty(newPin) || newPin.Length < 4)
                return BadRequest(new { error = "PIN must be at least 4 characters" });

            using var conn = Data.PgDatabaseHelper.GetConnection();

            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT password_hash FROM users WHERE pos_id = @pid AND is_active = true";
            checkCmd.Parameters.AddWithValue("pid", posId);
            var currentHash = checkCmd.ExecuteScalar()?.ToString() ?? "";

            if (currentHash != oldPin)
                return Unauthorized(new { error = "Current PIN is incorrect" });

            using var updCmd = conn.CreateCommand();
            updCmd.CommandText = "UPDATE users SET password_hash = @ph, synced_at = NOW() WHERE pos_id = @pid";
            updCmd.Parameters.AddWithValue("ph", newPin);
            updCmd.Parameters.AddWithValue("pid", posId);
            updCmd.ExecuteNonQuery();

            return Ok(new { success = true });
        }

        [HttpGet("users/download")]
        public IActionResult DownloadUsers([FromQuery] string? storeId = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT DISTINCT u.pos_id, u.username, u.role, u.full_name, u.is_active, u.password_hash, u.mobile_access
                FROM users u
                WHERE u.is_active = true
                AND (
                  EXISTS (SELECT 1 FROM user_stores us WHERE us.user_pos_id = u.pos_id AND us.store_id = @sid)
                  OR (u.store_id = @sid AND NOT EXISTS (SELECT 1 FROM user_stores us WHERE us.user_pos_id = u.pos_id))
                )
                ORDER BY u.username";
            cmd.Parameters.AddWithValue("sid", storeId ?? "");
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                data.Add(new { posId = r.GetInt32(0), username = r.GetString(1), role = r.GetString(2), fullName = r.IsDBNull(3) ? "" : r.GetString(3), isActive = r.GetBoolean(4), passwordHash = r.IsDBNull(5) ? "12345" : r.GetString(5), mobileAccess = r.IsDBNull(6) ? false : r.GetBoolean(6) });
            return Ok(data);
        }

        [HttpPost("whapp/login")]
        public IActionResult WhAppLogin([FromBody] JsonElement body)
        {
            var username = body.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
            var password = body.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";
            var storeId = body.TryGetProperty("storeId", out var sid) ? sid.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return BadRequest(new { success = false, error = "Username and password are required" });

            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT u.pos_id, u.username, u.role, u.full_name, u.mobile_access, u.store_id,
                       COALESCE((SELECT json_agg(us.store_id) FROM user_stores us WHERE us.user_pos_id = u.pos_id), '[]'::json) AS store_ids
                FROM users u
                WHERE LOWER(u.username) = LOWER(@u) AND u.password_hash = @p AND u.is_active = true
                LIMIT 1";
            cmd.Parameters.AddWithValue("u", username);
            cmd.Parameters.AddWithValue("p", password);

            int posId = 0;
            string uname = "", role = "Cashier", fullName = "";
            bool mobileAccess = false;
            string baseStoreId = "";
            List<string> storeIds = new();
            bool found = false;

            using (var r = cmd.ExecuteReader())
            {
                if (r.Read())
                {
                    found = true;
                    posId = r.GetInt32(0);
                    uname = r.GetString(1);
                    role = r.GetString(2);
                    fullName = r.IsDBNull(3) ? "" : r.GetString(3);
                    mobileAccess = r.GetBoolean(4);
                    baseStoreId = r.IsDBNull(5) ? "" : r.GetString(5);
                    storeIds = r.IsDBNull(6) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.GetString(6)) ?? new();
                }
            }

            if (!found)
                return Unauthorized(new { success = false, error = "Invalid username or password" });

            if (!mobileAccess)
                return Unauthorized(new { success = false, error = "No mobile access granted. Ask the admin to enable Mobile Access for this user." });

            var allStores = storeIds.Count > 0 ? storeIds : (baseStoreId == "" ? new List<string>() : new List<string> { baseStoreId });

            // Resolve store names (reader closed, safe to run new command)
            var storeNames = new Dictionary<string, string>();
            if (allStores.Count > 0)
            {
                using var scmd = conn.CreateCommand();
                scmd.CommandText = "SELECT store_id, store_name FROM stores WHERE store_id = ANY(@ids)";
                scmd.Parameters.AddWithValue("ids", allStores.ToArray());
                using var sr = scmd.ExecuteReader();
                while (sr.Read()) storeNames[sr.GetString(0)] = sr.GetString(1);
            }

            var userStoreId = !string.IsNullOrEmpty(storeId) && allStores.Contains(storeId) ? storeId
                : allStores.Contains("STORE-WAREHOUSE") ? "STORE-WAREHOUSE"
                : allStores.Contains("STORE-20260602-7159") ? "STORE-20260602-7159"
                : allStores.FirstOrDefault() ?? "";

            // Generate session token for subsequent validation (multi-device: keep last 5)
            var token = Guid.NewGuid().ToString("N");
            using (var insTok = conn.CreateCommand())
            {
                insTok.CommandText = "INSERT INTO whapp_tokens (user_pos_id, token) VALUES (@pid, @tok)";
                insTok.Parameters.AddWithValue("pid", posId);
                insTok.Parameters.AddWithValue("tok", token);
                insTok.ExecuteNonQuery();
            }
            using (var cleanTok = conn.CreateCommand())
            {
                cleanTok.CommandText = @"
                    DELETE FROM whapp_tokens WHERE id IN (
                        SELECT id FROM whapp_tokens WHERE user_pos_id = @pid
                        ORDER BY created_at DESC OFFSET 5
                    )";
                cleanTok.Parameters.AddWithValue("pid", posId);
                cleanTok.ExecuteNonQuery();
            }

            return Ok(new
            {
                success = true,
                posId,
                username = uname,
                role,
                fullName,
                token,
                storeId = userStoreId,
                storeName = userStoreId != "" && storeNames.TryGetValue(userStoreId, out var sn) ? sn : "",
                stores = allStores.Select(s => new { storeId = s, storeName = storeNames.TryGetValue(s, out var sn2) ? sn2 : s })
            });
        }

        [HttpPost("whapp/validate")]
        public IActionResult WhAppValidate([FromBody] JsonElement body)
        {
            var username = body.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
            var token = body.TryGetProperty("token", out var t) ? t.GetString() ?? "" : "";
            var storeId = body.TryGetProperty("storeId", out var sid) ? sid.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
                return BadRequest(new { success = false, error = "Username and token are required" });

            using var conn = Data.PgDatabaseHelper.GetConnection();

            // Check token is valid and user is still active + has mobile access
            int posId = 0;
            string uname = "", role = "Cashier", fullName = "";
            bool mobileAccess = false;
            string baseStoreId = "";
            List<string> storeIds = new();
            bool found = false;

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT u.pos_id, u.username, u.role, u.full_name, u.mobile_access, u.store_id,
                           COALESCE((SELECT json_agg(us.store_id) FROM user_stores us WHERE us.user_pos_id = u.pos_id), '[]'::json) AS store_ids
                    FROM users u
                    JOIN whapp_tokens wt ON wt.user_pos_id = u.pos_id AND wt.token = @tok
                    WHERE LOWER(u.username) = LOWER(@u) AND u.is_active = true
                    LIMIT 1";
                cmd.Parameters.AddWithValue("u", username);
                cmd.Parameters.AddWithValue("tok", token);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    found = true;
                    posId = r.GetInt32(0);
                    uname = r.GetString(1);
                    role = r.GetString(2);
                    fullName = r.IsDBNull(3) ? "" : r.GetString(3);
                    mobileAccess = r.GetBoolean(4);
                    baseStoreId = r.IsDBNull(5) ? "" : r.GetString(5);
                    storeIds = r.IsDBNull(6) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.GetString(6)) ?? new();
                }
            }

            if (!found)
                return Unauthorized(new { success = false, error = "Session expired. Please log in again." });

            if (!mobileAccess)
                return Unauthorized(new { success = false, error = "Mobile access revoked. Please contact the admin." });

            var allStores = storeIds.Count > 0 ? storeIds : (baseStoreId == "" ? new List<string>() : new List<string> { baseStoreId });
            if (allStores.Count == 0)
                return Unauthorized(new { success = false, error = "No store access. Please contact the admin." });

            // Resolve store names (reader closed, safe to run new command)
            var storeNames = new Dictionary<string, string>();
            using (var scmd = conn.CreateCommand())
            {
                scmd.CommandText = "SELECT store_id, store_name FROM stores WHERE store_id = ANY(@ids)";
                scmd.Parameters.AddWithValue("ids", allStores.ToArray());
                using var sr = scmd.ExecuteReader();
                while (sr.Read()) storeNames[sr.GetString(0)] = sr.GetString(1);
            }

            var userStoreId = !string.IsNullOrEmpty(storeId) && allStores.Contains(storeId) ? storeId
                : allStores.Contains("STORE-WAREHOUSE") ? "STORE-WAREHOUSE"
                : allStores.Contains("STORE-20260602-7159") ? "STORE-20260602-7159"
                : allStores.FirstOrDefault() ?? "";

            return Ok(new
            {
                success = true,
                posId,
                username = uname,
                role,
                fullName,
                storeId = userStoreId,
                storeName = userStoreId != "" && storeNames.TryGetValue(userStoreId, out var sn) ? sn : "",
                stores = allStores.Select(s => new { storeId = s, storeName = storeNames.TryGetValue(s, out var sn2) ? sn2 : s })
            });
        }

        [HttpPost("whapp/logout")]
        public IActionResult WhAppLogout([FromBody] JsonElement body)
        {
            var username = body.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
            var token = body.TryGetProperty("token", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
                return BadRequest(new { success = false });

            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"DELETE FROM whapp_tokens wt
                USING users u
                WHERE wt.token = @tok AND wt.user_pos_id = u.pos_id AND LOWER(u.username) = LOWER(@u)";
            cmd.Parameters.AddWithValue("tok", token);
            cmd.Parameters.AddWithValue("u", username);
            cmd.ExecuteNonQuery();
            return Ok(new { success = true });
        }

        [HttpPost("web/login")]
        public IActionResult WebLogin([FromBody] JsonElement body)
        {
            var username = body.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
            var password = body.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return BadRequest(new { success = false, error = "Username and PIN are required" });

            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT u.pos_id, u.username, u.role, u.full_name, u.web_access, u.store_id,
                       COALESCE((SELECT json_agg(us.store_id) FROM user_stores us WHERE us.user_pos_id = u.pos_id), '[]'::json) AS store_ids
                FROM users u
                WHERE LOWER(u.username) = LOWER(@u) AND u.password_hash = @p AND u.is_active = true
                LIMIT 1";
            cmd.Parameters.AddWithValue("u", username);
            cmd.Parameters.AddWithValue("p", password);

            int posId = 0;
            string uname = "", role = "Cashier", fullName = "", baseStoreId = "";
            bool webAccess = false;
            List<string> storeIds = new();
            bool found = false;

            using (var r = cmd.ExecuteReader())
            {
                if (r.Read())
                {
                    found = true;
                    posId = r.GetInt32(0);
                    uname = r.GetString(1);
                    role = r.GetString(2);
                    fullName = r.IsDBNull(3) ? "" : r.GetString(3);
                    webAccess = r.IsDBNull(4) ? false : r.GetBoolean(4);
                    baseStoreId = r.IsDBNull(5) ? "" : r.GetString(5);
                    storeIds = r.IsDBNull(6) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.GetString(6)) ?? new();
                }
            }

            if (!found)
                return Unauthorized(new { success = false, error = "Invalid username or PIN" });

            if (!webAccess)
                return Unauthorized(new { success = false, error = "No web access granted. Ask the admin to enable WEB ACCESS for this user." });

            // Admin ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ all stores; otherwise only the user's assigned stores
            var allStoreIds = new List<string>();
            if (role == "Admin")
            {
                using var scmd = conn.CreateCommand();
                scmd.CommandText = "SELECT store_id FROM stores ORDER BY store_name";
                using var sr = scmd.ExecuteReader();
                while (sr.Read()) allStoreIds.Add(sr.GetString(0));
            }
            else
            {
                allStoreIds = storeIds.Count > 0 ? storeIds : (baseStoreId == "" ? new List<string>() : new List<string> { baseStoreId });
            }

            var storeNames = new Dictionary<string, string>();
            if (allStoreIds.Count > 0)
            {
                using var scmd = conn.CreateCommand();
                scmd.CommandText = "SELECT store_id, store_name FROM stores WHERE store_id = ANY(@ids)";
                scmd.Parameters.AddWithValue("ids", allStoreIds.ToArray());
                using var sr = scmd.ExecuteReader();
                while (sr.Read()) storeNames[sr.GetString(0)] = sr.GetString(1);
            }

            // Session token (multi-device: keep last 5, same table as mobile)
            var token = Guid.NewGuid().ToString("N");
            using (var insTok = conn.CreateCommand())
            {
                insTok.CommandText = "INSERT INTO whapp_tokens (user_pos_id, token) VALUES (@pid, @tok)";
                insTok.Parameters.AddWithValue("pid", posId);
                insTok.Parameters.AddWithValue("tok", token);
                insTok.ExecuteNonQuery();
            }
            using (var cleanTok = conn.CreateCommand())
            {
                cleanTok.CommandText = @"
                    DELETE FROM whapp_tokens WHERE id IN (
                        SELECT id FROM whapp_tokens WHERE user_pos_id = @pid
                        ORDER BY created_at DESC OFFSET 5
                    )";
                cleanTok.Parameters.AddWithValue("pid", posId);
                cleanTok.ExecuteNonQuery();
            }

            return Ok(new
            {
                success = true,
                posId,
                username = uname,
                role,
                fullName,
                token,
                allStores = role == "Admin",
                stores = allStoreIds.Select(s => new { storeId = s, storeName = storeNames.TryGetValue(s, out var sn) ? sn : s })
            });
        }

        [HttpGet("web/me")]
        public IActionResult WebMe([FromQuery] string? username = null, [FromQuery] string? token = null)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
                return Unauthorized(new { success = false, error = "Session expired. Please log in again." });

            using var conn = Data.PgDatabaseHelper.GetConnection();
            int posId = 0;
            string uname = "", role = "Cashier", fullName = "", baseStoreId = "";
            bool webAccess = false;
            List<string> storeIds = new();
            bool found = false;

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT u.pos_id, u.username, u.role, u.full_name, u.web_access, u.store_id,
                           COALESCE((SELECT json_agg(us.store_id) FROM user_stores us WHERE us.user_pos_id = u.pos_id), '[]'::json) AS store_ids
                    FROM users u
                    JOIN whapp_tokens wt ON wt.user_pos_id = u.pos_id AND wt.token = @tok
                    WHERE LOWER(u.username) = LOWER(@u) AND u.is_active = true
                    LIMIT 1";
                cmd.Parameters.AddWithValue("u", username);
                cmd.Parameters.AddWithValue("tok", token);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    found = true;
                    posId = r.GetInt32(0);
                    uname = r.GetString(1);
                    role = r.GetString(2);
                    fullName = r.IsDBNull(3) ? "" : r.GetString(3);
                    webAccess = r.IsDBNull(4) ? false : r.GetBoolean(4);
                    baseStoreId = r.IsDBNull(5) ? "" : r.GetString(5);
                    storeIds = r.IsDBNull(6) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.GetString(6)) ?? new();
                }
            }

            if (!found)
                return Unauthorized(new { success = false, error = "Session expired. Please log in again." });

            if (!webAccess)
                return Unauthorized(new { success = false, error = "Web access revoked. Please contact the admin." });

            var allStoreIds = new List<string>();
            if (role == "Admin")
            {
                using var scmd = conn.CreateCommand();
                scmd.CommandText = "SELECT store_id FROM stores ORDER BY store_name";
                using var sr = scmd.ExecuteReader();
                while (sr.Read()) allStoreIds.Add(sr.GetString(0));
            }
            else
            {
                allStoreIds = storeIds.Count > 0 ? storeIds : (baseStoreId == "" ? new List<string>() : new List<string> { baseStoreId });
            }

            var storeNames = new Dictionary<string, string>();
            if (allStoreIds.Count > 0)
            {
                using var scmd = conn.CreateCommand();
                scmd.CommandText = "SELECT store_id, store_name FROM stores WHERE store_id = ANY(@ids)";
                scmd.Parameters.AddWithValue("ids", allStoreIds.ToArray());
                using var sr = scmd.ExecuteReader();
                while (sr.Read()) storeNames[sr.GetString(0)] = sr.GetString(1);
            }

            return Ok(new
            {
                success = true,
                posId,
                username = uname,
                role,
                fullName,
                token,
                allStores = role == "Admin",
                stores = allStoreIds.Select(s => new { storeId = s, storeName = storeNames.TryGetValue(s, out var sn) ? sn : s })
            });
        }

        [HttpPost("web/logout")]
        public IActionResult WebLogout([FromBody] JsonElement body)
        {
            var username = body.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
            var token = body.TryGetProperty("token", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
                return BadRequest(new { success = false });

            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"DELETE FROM whapp_tokens wt
                USING users u
                WHERE wt.token = @tok AND wt.user_pos_id = u.pos_id AND LOWER(u.username) = LOWER(@u)";
            cmd.Parameters.AddWithValue("tok", token);
            cmd.Parameters.AddWithValue("u", username);
            cmd.ExecuteNonQuery();
            return Ok(new { success = true });
        }

        [HttpGet("expenses-summary")]
        public IActionResult GetExpensesSummary([FromQuery] string? storeId = null, [FromQuery] string? range = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var tf = TimeframeClause(range, "timestamp", cmd);
            if (string.IsNullOrEmpty(range) || range == "all") tf = "";
            cmd.CommandText = $@"
                SELECT category, COALESCE(SUM(amount),0) AS total
                FROM expenses e
                WHERE 1=1 {StoreFilter(storeId, "e")}{tf}
                GROUP BY category
                ORDER BY total DESC";
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new { category = reader.GetString(0), total = reader.GetDecimal(1) });
            return Ok(data);
        }

        [HttpGet("expenses-list")]
        public IActionResult GetExpensesList([FromQuery] string? storeId = null, [FromQuery] string? range = null, [FromQuery] int limit = 200)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var tf = TimeframeClause(range, "timestamp", cmd);
            if (string.IsNullOrEmpty(range) || range == "all") tf = "";
            cmd.CommandText = $@"
                SELECT e.amount, e.category, e.description, e.reference_no, e.cashier_username, e.timestamp
                FROM expenses e
                WHERE 1=1 {StoreFilter(storeId, "e")}{tf}
                ORDER BY e.timestamp DESC
                LIMIT @lim";
            cmd.Parameters.AddWithValue("lim", limit);
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new {
                    amount = reader.GetDecimal(0),
                    category = reader.GetString(1),
                    description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    referenceNo = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    cashier = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    timestamp = reader.GetDateTime(5)
                });
            return Ok(data);
        }

        [HttpGet("shift-history")]
        public IActionResult GetShiftHistory([FromQuery] int days = 60, [FromQuery] string? storeId = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT close_date, total_sales, total_cash, total_ewallet, total_credit, total_voided, 
                       total_expenses, cash_on_hand, difference, user_name, notes, store_id,
                       COALESCE(total_inventory_cost, 0), COALESCE(total_cost_sold, 0), COALESCE(total_stock_received_cost, 0)
                FROM daily_closes d
                WHERE close_date >= CURRENT_DATE - @days {StoreFilter(storeId, "d")}
                ORDER BY close_date DESC";
            cmd.Parameters.AddWithValue("days", days);
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new { closeDate = reader.GetDateTime(0), totalSales = reader.GetDecimal(1), totalCash = reader.GetDecimal(2), totalEwallet = reader.GetDecimal(3), totalCredit = reader.GetDecimal(4), totalVoided = reader.GetDecimal(5), totalExpenses = reader.GetDecimal(6), cashOnHand = reader.GetDecimal(7), difference = reader.GetDecimal(8), userName = reader.GetString(9), notes = reader.IsDBNull(10) ? "" : reader.GetString(10), storeId = reader.GetString(11), totalInventoryCost = reader.GetDecimal(12), totalCostSold = reader.GetDecimal(13), totalStockReceivedCost = reader.GetDecimal(14) });
            return Ok(data);
        }

        [HttpGet("recent-receiving")]
        public IActionResult GetRecentReceiving([FromQuery] int limit = 30, [FromQuery] string? storeId = null, [FromQuery] string? range = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var tf = TimeframeClause(range, "st.created_at", cmd);
            if (string.IsNullOrEmpty(range) || range == "all") tf = "";
            cmd.CommandText = $@"
                SELECT st.product_name, st.barcode, st.quantity_added, st.stock_before, st.stock_after, st.reference, st.user_name, st.created_at, st.store_id
                FROM stock_trails st
                WHERE st.quantity_added > 0 AND (st.reference IS NULL OR st.reference NOT LIKE '% - void (%') {StoreFilter(storeId, "st")}{tf}
                ORDER BY st.created_at DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("limit", limit);
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new { productName = reader.GetString(0), barcode = reader.IsDBNull(1) ? "" : reader.GetString(1), quantityAdded = reader.GetDecimal(2), stockBefore = reader.GetInt32(3), stockAfter = reader.GetInt32(4), reference = reader.IsDBNull(5) ? "" : reader.GetString(5), userName = reader.IsDBNull(6) ? "" : reader.GetString(6), createdAt = reader.GetDateTime(7), storeId = reader.GetString(8) });
            return Ok(data);
        }

        [HttpGet("stock-status")]
        public IActionResult GetStockStatus([FromQuery] string? storeId = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var storeClause = StoreFilter(storeId, "p");
            var whereClause = string.IsNullOrEmpty(storeId)
                ? "WHERE p.is_active = true"
                : "WHERE p.is_active = true " + storeClause;
            cmd.CommandText = $@"
                SELECT name, barcode, category, stock_qty, price, cost, store_id
                FROM products p
                {whereClause}
                {(string.IsNullOrEmpty(storeId) ? @"
                UNION ALL
                SELECT w.name,
                       COALESCE(NULLIF(w.barcode,''), mp.barcode, '') AS barcode,
                       COALESCE(NULLIF(w.category,''), mp.category, '') AS category,
                       w.stock_qty,
                       COALESCE(w.piece_price, w.box_price/NULLIF(w.box_qty,0), mp.price, 0) AS price,
                       COALESCE(mp.cost, w.box_cost/NULLIF(w.box_qty,0), 0) AS cost,
                       'STORE-WAREHOUSE' AS store_id
                FROM wh_products w
                LEFT JOIN master_products mp ON mp.id = w.master_product_id
                WHERE w.is_active = true" : "")}
                ORDER BY stock_qty ASC";
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new { name = reader.GetString(0), barcode = reader.IsDBNull(1) ? "" : reader.GetString(1), category = reader.IsDBNull(2) ? "" : reader.GetString(2), stockQty = reader.GetInt32(3), price = reader.GetDecimal(4), cost = reader.GetDecimal(5), storeId = reader.IsDBNull(6) ? "" : reader.GetString(6) });
            return Ok(data);
        }

        [HttpDelete("reset-db")]
        public IActionResult ResetDatabase()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                DROP TABLE IF EXISTS sale_items CASCADE;
                DROP TABLE IF EXISTS void_logs CASCADE;
                DROP TABLE IF EXISTS stock_trails CASCADE;
                DROP TABLE IF EXISTS credit_transactions CASCADE;
                DROP TABLE IF EXISTS sales CASCADE;
                DROP TABLE IF EXISTS daily_closes CASCADE;
                DROP TABLE IF EXISTS expenses CASCADE;
                DROP TABLE IF EXISTS products CASCADE;
                DROP TABLE IF EXISTS customers CASCADE;
                DROP TABLE IF EXISTS users CASCADE;
            ";
            cmd.ExecuteNonQuery();
            Data.PgDatabaseHelper.Initialize();
            return Ok(new { success = true, message = "All tables dropped and recreated" });
        }

        [HttpDelete("stores/{storeId}")]
        public IActionResult DeleteStore(string storeId)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    DELETE FROM sale_items WHERE store_id = @sid;
                    DELETE FROM void_logs WHERE store_id = @sid;
                    DELETE FROM stock_trails WHERE store_id = @sid;
                    DELETE FROM credit_transactions WHERE store_id = @sid;
                    DELETE FROM daily_closes WHERE store_id = @sid;
                    DELETE FROM expenses WHERE store_id = @sid;
                    DELETE FROM sales WHERE store_id = @sid;
                    DELETE FROM products WHERE store_id = @sid;
                    DELETE FROM customers WHERE store_id = @sid;
                    DELETE FROM users WHERE store_id = @sid;
                    DELETE FROM stores WHERE store_id = @sid;
                ";
                cmd.Parameters.AddWithValue("sid", storeId);
                cmd.ExecuteNonQuery();
                tx.Commit();
                return Ok(new { success = true, message = $"Store {storeId} deleted" });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet("cashier-performance")]
        public IActionResult GetCashierPerformance([FromQuery] string? storeId = null, [FromQuery] string? range = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var tf = TimeframeClause(range, "s.sale_date", cmd);
            if (string.IsNullOrEmpty(range) || range == "all") tf = "";
            cmd.CommandText = $@"
                SELECT COALESCE(NULLIF(s.cashier_name,''), NULLIF(u.full_name,''), NULLIF(u.username,''),
                         (SELECT s2.cashier_name FROM sales s2 WHERE s2.store_id = s.store_id AND s2.user_id = s.user_id AND COALESCE(s2.cashier_name,'') <> '' ORDER BY s2.sale_date DESC, s2.id DESC LIMIT 1),
                         'Cashier #' || COALESCE(s.user_id::text,'Unknown')) AS cashier,
                       COUNT(*) AS total_sales,
                       COALESCE(SUM(s.grand_total),0) AS total_revenue,
                       COALESCE(AVG(s.grand_total),0) AS avg_transaction,
                       COUNT(*) FILTER (WHERE s.payment_method = 'Cash') AS cash_count,
                       COUNT(*) FILTER (WHERE s.payment_method = 'E-Wallet') AS ewallet_count,
                       COUNT(*) FILTER (WHERE s.payment_method = 'Credit') AS credit_count
                FROM sales s
                LEFT JOIN users u ON s.user_id = u.pos_id AND s.store_id = u.store_id
                WHERE s.is_voided = false {StoreFilter(storeId, "s")}{tf}
                GROUP BY COALESCE(NULLIF(s.cashier_name,''), NULLIF(u.full_name,''), NULLIF(u.username,''),
                         (SELECT s2.cashier_name FROM sales s2 WHERE s2.store_id = s.store_id AND s2.user_id = s.user_id AND COALESCE(s2.cashier_name,'') <> '' ORDER BY s2.sale_date DESC, s2.id DESC LIMIT 1),
                         'Cashier #' || COALESCE(s.user_id::text,'Unknown'))
                ORDER BY total_revenue DESC";
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new {
                    cashier = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0),
                    totalSales = reader.GetInt32(1),
                    totalRevenue = reader.GetDecimal(2),
                    avgTransaction = reader.GetDecimal(3),
                    cashCount = reader.GetInt32(4),
                    ewalletCount = reader.GetInt32(5),
                    creditCount = reader.GetInt32(6)
                });
            return Ok(data);
        }

        [HttpGet("peak-hours")]
        public IActionResult GetPeakHours([FromQuery] string? storeId = null, [FromQuery] string? range = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var tf = TimeframeClause(range, "sale_date", cmd);
            if (string.IsNullOrEmpty(range) || range == "all") tf = "";
            cmd.CommandText = $@"
                SELECT EXTRACT(HOUR FROM sale_date)::int AS hour,
                       COUNT(*) AS sales_count,
                       COALESCE(SUM(grand_total),0) AS revenue
                FROM sales
                WHERE is_voided = false {StoreFilter(storeId, "sales")}{tf}
                GROUP BY EXTRACT(HOUR FROM sale_date)
                ORDER BY hour";
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new {
                    hour = reader.GetInt32(0),
                    salesCount = reader.GetInt32(1),
                    revenue = reader.GetDecimal(2)
                });
            return Ok(data);
        }

        [HttpGet("sale-profits")]
        public IActionResult GetSaleProfits([FromQuery] int limit = 100, [FromQuery] string? storeId = null, [FromQuery] string? range = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var tf = TimeframeClause(range, "s.sale_date", cmd);
            if (string.IsNullOrEmpty(range) || range == "all") tf = "";
            cmd.CommandText = $@"
                SELECT 
                    s.invoice_no,
                    s.sale_date,
                    COALESCE(SUM(si.total_price), 0) AS revenue,
                    COALESCE(SUM(COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0) * si.quantity), 0) AS total_cost,
                    COALESCE(SUM(si.total_price), 0) - COALESCE(SUM(COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0) * si.quantity), 0) AS profit,
                    CASE WHEN COALESCE(SUM(si.total_price), 0) > 0 THEN ROUND((COALESCE(SUM(si.total_price), 0) - COALESCE(SUM(COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0) * si.quantity), 0)) / COALESCE(SUM(si.total_price), 0) * 100, 1) ELSE 0 END AS margin_pct,
                    COALESCE(NULLIF(s.cashier_name,''), NULLIF(u.full_name,''), NULLIF(u.username,''),
                      (SELECT s2.cashier_name FROM sales s2 WHERE s2.store_id = s.store_id AND s2.user_id = s.user_id AND COALESCE(s2.cashier_name,'') <> '' ORDER BY s2.sale_date DESC, s2.id DESC LIMIT 1),
                      'Cashier #' || COALESCE(s.user_id::text,'')) AS cashier,
                    s.store_id
                FROM sales s
                LEFT JOIN sale_items si ON si.sale_id = s.pos_id AND si.store_id = s.store_id AND si.is_voided = false
                LEFT JOIN products p ON si.product_id = p.pos_id AND si.store_id = p.store_id
                LEFT JOIN users u ON s.user_id = u.pos_id AND s.store_id = u.store_id
                WHERE s.is_voided = false {StoreFilter(storeId, "s")}{tf}
                GROUP BY s.invoice_no, s.sale_date, s.cashier_name, s.user_id, u.full_name, u.username, s.store_id
                ORDER BY s.sale_date DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("limit", limit);
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new {
                    invoiceNo = reader.GetString(0),
                    saleDate = reader.GetDateTime(1),
                    revenue = reader.GetDecimal(2),
                    cost = reader.GetDecimal(3),
                    profit = reader.GetDecimal(4),
                    marginPct = reader.GetDecimal(5),
                    cashier = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    storeId = reader.GetString(7)
                });
            return Ok(data);
        }

        [HttpGet("profit-summary")]
        public IActionResult GetProfitSummary([FromQuery] string? storeId = null, [FromQuery] string? range = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var tfSales = TimeframeClause(range, "sale_date", cmd);
            var tfExp = TimeframeClause(range, "timestamp", cmd);
            if (string.IsNullOrEmpty(range) || range == "all") { tfSales = ""; tfExp = ""; }

            var itemsJoin = $"FROM sale_items si JOIN sales s ON si.sale_id = s.pos_id AND si.store_id = s.store_id";
            cmd.CommandText = $@"
                SELECT
                    (SELECT COALESCE(SUM(si.total_price),0) {itemsJoin} WHERE s.is_voided = false AND si.is_voided = false {StoreFilter(storeId, "s")}{tfSales.Replace("sale_date","s.sale_date")}) AS total_revenue,
                    (SELECT COALESCE(SUM(amount),0) FROM expenses WHERE 1=1 {StoreFilter(storeId, "expenses")}{tfExp}) AS total_expenses,
                    (SELECT COALESCE(SUM(si.total_price - (COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0) * si.quantity)),0)
                     {itemsJoin}
                     JOIN products p ON si.product_id = p.pos_id AND si.store_id = p.store_id
                     WHERE s.is_voided = false AND si.is_voided = false {StoreFilter(storeId, "s")}{tfSales.Replace("sale_date","s.sale_date")}) AS gross_profit,
                    (SELECT COUNT(*) FROM sales WHERE is_voided = true {StoreFilter(storeId, "sales")}{tfSales}) AS voided_count,
                    (SELECT COUNT(*) FROM sales WHERE is_voided = false {StoreFilter(storeId, "sales")}{tfSales}) AS valid_count,
                    (SELECT COALESCE(AVG(si.total_price),0) {itemsJoin} WHERE s.is_voided = false AND si.is_voided = false {StoreFilter(storeId, "s")}{tfSales.Replace("sale_date","s.sale_date")}) AS avg_transaction,
                    (SELECT COALESCE(MAX(si.total_price),0) {itemsJoin} WHERE s.is_voided = false AND si.is_voided = false {StoreFilter(storeId, "s")}{tfSales.Replace("sale_date","s.sale_date")}) AS max_transaction,
                    (SELECT COALESCE(MIN(si.total_price),0) {itemsJoin} WHERE s.is_voided = false AND si.is_voided = false {StoreFilter(storeId, "s")}{tfSales.Replace("sale_date","s.sale_date")}) AS min_transaction
            ";
            var row = cmd.ExecuteReader();
            row.Read();
            var revenue = row.GetDecimal(0);
            var expenses = row.GetDecimal(1);
            var grossProfit = row.GetDecimal(2);
            var voidedCount = row.GetInt32(3);
            var validCount = row.GetInt32(4);
            var avgTx = row.GetDecimal(5);
            var maxTx = row.GetDecimal(6);
            var minTx = row.GetDecimal(7);
            var totalCount = voidedCount + validCount;
            var voidRate = totalCount > 0 ? Math.Round((decimal)voidedCount / totalCount * 100, 1) : 0;
            var netProfit = revenue - expenses;
            var margin = revenue > 0 ? Math.Round(netProfit / revenue * 100, 1) : 0;
            var grossMargin = revenue > 0 ? Math.Round(grossProfit / revenue * 100, 1) : 0;

            return Ok(new {
                totalRevenue = revenue,
                totalExpenses = expenses,
                netProfit = netProfit,
                netMargin = margin,
                grossProfit = grossProfit,
                grossMargin = grossMargin,
                voidedCount = voidedCount,
                validCount = validCount,
                voidRate = voidRate,
                avgTransaction = avgTx,
                maxTransaction = maxTx,
                minTransaction = minTx
            });
        }

        [HttpGet("sale-items")]
        public IActionResult GetSaleItems([FromQuery] string invoiceNo, [FromQuery] string? storeId = null)
        {
            if (string.IsNullOrEmpty(invoiceNo)) return BadRequest("invoiceNo required");
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.Parameters.AddWithValue("inv", invoiceNo);
            if (!string.IsNullOrEmpty(storeId))
                cmd.Parameters.AddWithValue("sid", storeId);

            var storeFilter = !string.IsNullOrEmpty(storeId) ? " AND s.store_id = @sid" : "";

            // Single query: JOIN sales -> sale_items -> products (same pattern as GetSaleProfits)
            string? paymentMethod = null, referenceNo = null;
            decimal? ewPaid = null, grandTotal = null;
            var items = new List<object>();

            cmd.CommandText = @"
                SELECT s.payment_method, s.reference_no, s.ew_paid, s.grand_total,
                       si.product_name, si.barcode, si.quantity, si.price, si.total_price,
                       COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0) AS unit_cost, si.qty_per_unit,
                       si.quantity * COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0) AS total_cost,
si.total_price - (si.quantity * COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0)) AS profit,
                       si.points_earned AS points_earned,
                        p.pos_id AS product_pos_id
                FROM sales s
                LEFT JOIN sale_items si ON si.sale_id = s.pos_id AND si.store_id = s.store_id AND si.is_voided = false
                LEFT JOIN products p ON si.product_id = p.pos_id AND si.store_id = p.store_id
                WHERE s.invoice_no = @inv" + storeFilter + @"
                ORDER BY si.product_name";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (paymentMethod == null)
                {
                    paymentMethod = reader.IsDBNull(0) ? null : reader.GetString(0);
                    referenceNo = reader.IsDBNull(1) ? null : reader.GetString(1);
                    ewPaid = reader.IsDBNull(2) ? null : reader.GetDecimal(2);
                    grandTotal = reader.IsDBNull(3) ? null : reader.GetDecimal(3);
                }
                if (reader.IsDBNull(4)) continue;
                items.Add(new {
                    productName = reader.GetString(4),
                    barcode = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    quantity = reader.GetInt32(6),
                    price = reader.GetDecimal(7),
                    totalPrice = reader.GetDecimal(8),
                    unitCost = reader.GetDecimal(9),
                    qtyPerUnit = reader.GetInt32(10),
                    totalCost = reader.GetDecimal(11),
                    profit = reader.GetDecimal(12),
                    pointsEarned = reader.GetInt32(13),
                    productPosId = reader.IsDBNull(14) ? 0 : reader.GetInt32(14)
                });
            }
            return Ok(new { items, paymentMethod, referenceNo, ewPaid, grandTotal });
        }

        [HttpGet("debug-missing-profits")]
        public IActionResult DebugMissingProfits([FromQuery] string? storeId = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            cmd.CommandText = $@"
                SELECT s.invoice_no, s.grand_total, 
                       (SELECT COUNT(*) FROM sale_items si WHERE si.sale_id = s.pos_id AND si.store_id = s.store_id) as item_count
                FROM sales s
                WHERE s.is_voided = false {StoreFilter(storeId, "s")}
                AND s.sale_date::date = CURRENT_DATE
                ORDER BY s.invoice_no";
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                data.Add(new { invoiceNo = r.GetString(0), total = r.GetDecimal(1), itemCount = r.GetInt32(2) });
            return Ok(data);
        }

        [HttpGet("debug-gross-profit")]
        public IActionResult DebugGrossProfit([FromQuery] string? storeId = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            
            // Get product & customer counts
            int productCount = 0, customerAll = 0, customerActive = 0;
            using (var c2 = conn.CreateCommand())
            {
                c2.CommandText = "SELECT COUNT(*) FROM products";
                productCount = Convert.ToInt32(c2.ExecuteScalar());
            }
            using (var c2 = conn.CreateCommand())
            {
                c2.CommandText = "SELECT COUNT(*) FROM customers";
                customerAll = Convert.ToInt32(c2.ExecuteScalar());
            }
            using (var c2 = conn.CreateCommand())
            {
                c2.CommandText = "SELECT COUNT(*) FROM customers WHERE is_active = true";
                customerActive = Convert.ToInt32(c2.ExecuteScalar());
            }

            cmd.CommandText = $@"
                SELECT 
                    COUNT(*) as total_sale_items,
                    COUNT(p.id) as matched_products,
                    COUNT(*) - COUNT(p.id) as unmatched_items,
                    COALESCE(SUM(si.total_price),0) as total_revenue,
                    COALESCE(SUM(si.quantity * COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0)),0) as total_cogs,
                    COALESCE(SUM(si.total_price - (si.quantity * COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0))),0) as gross_profit
                FROM sale_items si
                JOIN sales s ON si.sale_id = s.pos_id AND si.store_id = s.store_id
                LEFT JOIN products p ON si.product_id = p.pos_id AND si.store_id = p.store_id
                WHERE s.is_voided = false AND si.is_voided = false {StoreFilter(storeId, "s")}
            ";
            
            using var reader = cmd.ExecuteReader();
            reader.Read();
            return Ok(new {
                totalSaleItems = reader.GetInt32(0),
                matchedProducts = reader.GetInt32(1),
                unmatchedItems = reader.GetInt32(2),
                totalRevenue = reader.GetDecimal(3),
                totalCOGS = reader.GetDecimal(4),
                grossProfit = reader.GetDecimal(5),
                totalProducts = productCount,
                totalCustomersAll = customerAll,
                totalCustomersActive = customerActive
            });
        }

    [HttpGet("settings/{storeId}/{key}")]
    public IActionResult GetStoreSetting(string storeId, string key)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM store_settings WHERE store_id = @sid AND key = @k";
        cmd.Parameters.AddWithValue("sid", storeId);
        cmd.Parameters.AddWithValue("k", key);
        var val = cmd.ExecuteScalar();
        return Ok(new { key, value = val?.ToString() ?? "" });
    }

    [HttpPut("settings/{storeId}/{key}")]
    public IActionResult SetStoreSetting(string storeId, string key, [FromBody] JsonElement body)
    {
        var value = body.TryGetProperty("value", out var v) ? v.GetString() : "";
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO store_settings (store_id, key, value) VALUES (@sid, @k, @v)
            ON CONFLICT (store_id, key) DO UPDATE SET value = @v";
        cmd.Parameters.AddWithValue("sid", storeId);
        cmd.Parameters.AddWithValue("k", key);
        cmd.Parameters.AddWithValue("v", value ?? "");
        cmd.ExecuteNonQuery();
        return Ok(new { success = true });
    }

    [HttpGet("version")]
    public IActionResult GetVersion()
    {
            return Ok(new { version = "1.1.35" });
    }

    private static readonly HttpClient _ollamaClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> _chatRate = new ConcurrentDictionary<string, Queue<DateTime>>();
    private static readonly ConcurrentQueue<ChatLogEntry> _chatLog = new ConcurrentQueue<ChatLogEntry>();

    [HttpPost("chat")]
    public async Task<IActionResult> PostChat([FromBody] ChatRequest req)
    {
        var msg = (req?.Message ?? "").Trim();
        if (msg.Length == 0) return BadRequest(new { error = "Empty message" });
        if (msg.Length > 500) return BadRequest(new { error = "Message too long (max 500)" });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;
        var q = _chatRate.GetOrAdd(ip, _ => new Queue<DateTime>());
        lock (q)
        {
            while (q.Count > 0 && (now - q.Peek()).TotalMinutes > 1) q.Dequeue();
            if (q.Count >= 5) return StatusCode(429, new { error = "Rate limit: 5 messages per minute" });
            q.Enqueue(now);
        }

        var facts = new List<string>();
        var sources = new List<string>();

        try
        {
            using var conn = PgDatabaseHelper.GetConnection();
            
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, question, answer FROM chat_kb
                WHERE active = true AND answer <> ''
                ORDER BY id DESC LIMIT 200";
            using var rd = cmd.ExecuteReader();
            var kbList = new List<(int Id, string Question, string Answer)>();
            while (rd.Read()) kbList.Add((rd.GetInt32(0), rd.GetString(1), rd.GetString(2)));
            rd.Close();

            var ml = msg.ToLowerInvariant();
            var scored = kbList.Select(k => (K: k, Score:
                (k.Question.Length > 0 && k.Answer.Length > 0 && ml.Contains(k.Question.ToLowerInvariant()) ? 3 : 0) +
                (k.Answer.Length > 0 && ml.Contains(k.Answer.ToLowerInvariant().Split(' ').FirstOrDefault(w => w.Length > 3) ?? "") ? 1 : 0)
            )).OrderByDescending(x => x.Score).Take(3).ToArray();
            var hits = scored.Where(x => x.Score > 0).Select(x => x.K).ToList();

            if (hits.Count == 0)
            {
                foreach (var kw in new[] { "bukas", "oras", "close", "sarado", "operasyon", "hours", "delivery", "deliver", "contact", "tawag", "phone", "numero", "branch", "saan", "payment", "bayad", "gcash", "cod", "ewallet", "order", "pickup", "return", "refund", "ibalik", "website", "site", "online", "promo", "price", "presyo", "magkano", "stock", "available", "meron" })
                {
                    if (ml.Contains(kw))
                    {
                        var kwMatch = kbList.Where(k => k.Answer.Length > 0 && (k.Question + " " + k.Answer).ToLowerInvariant().Contains(kw)).Take(2).ToList();
                        hits.AddRange(kwMatch);
                        if (hits.Count >= 3) break;
                    }
                }
                hits = hits.Distinct().Take(3).ToList();
            }

            foreach (var h in hits)
            {
                facts.Add($"- {h.Question}: {h.Answer}");
                sources.Add($"kb:{h.Id}");
            }

            bool promoAsk = ml.Contains("promo") || ml.Contains("sale") || ml.Contains("discount") || ml.Contains("free");
            if (promoAsk)
            {
                try
                {
                    using var pc = conn.CreateCommand();
                    pc.CommandText = "SELECT message FROM pos_promo WHERE id = 1";
                    var promoMsg = pc.ExecuteScalar()?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(promoMsg))
                    {
                        facts.Add($"- Kasalukuyang mga promo ng tindahan:\n{promoMsg}");
                        sources.Add("promo");
                    }
                }
                catch { }
            }

            bool productAsk = ml.Contains("magkano") || ml.Contains("presyo") || ml.Contains("price") || ml.Contains("stock") || ml.Contains("available") || ml.Contains("meron") || ml.Contains("bili") || ml.Contains("bumili") || ml.Contains("cost");
            if (productAsk)
            {
                try
                {
                    var stopwords = new[] { "magkano", "presyo", "price", "stock", "available", "meron", "bili", "bumili", "cost", "anong", "ang", "ng", "kayo", "ba", "may", "po", "mo", "ninyo", "niyo", "saan", "pwede", "ano", "paano", "nyo", "namin", "kami", "gusto", "ko", "akin", "yung", "mga", "na", "sa", "at" };
                    var tokens = msg.ToLowerInvariant()
                        .Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '(', ')', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(t => t.Length >= 3 && !stopwords.Contains(t))
                        .Distinct()
                        .Take(6)
                        .ToArray();
                    if (tokens.Length > 0)
                    {
                        using var pc = conn.CreateCommand();
                        var sql = @"
                            SELECT mp.name, mp.price,
                                   COALESCE((SELECT json_agg(json_build_object('unit', u.unit_name, 'price', u.price, 'qtyPerUnit', u.qty_per_unit, 'isDefault', u.is_default))
                                             FROM master_product_units u WHERE u.product_id = mp.id), '[]'::json) AS units,
                                   (SELECT COALESCE(SUM(p.stock_qty), 0) FROM products p WHERE p.store_id = 'STORE-20260602-7159' AND p.barcode = mp.barcode AND p.is_active = true) AS hq_stock
                            FROM master_products mp
                            WHERE mp.is_active = true AND mp.sell_online = true
                              AND (";
                        for (int ti = 0; ti < tokens.Length; ti++)
                        {
                            if (ti > 0) sql += " AND ";
                            sql += $"(mp.name ILIKE '%' || @t{ti} || '%' OR mp.barcode ILIKE '%' || @t{ti} || '%')";
                            pc.Parameters.AddWithValue($"t{ti}", tokens[ti]);
                        }
                        sql += ") ORDER BY mp.name LIMIT 3";
                        pc.CommandText = sql;
                        using var rd2 = pc.ExecuteReader();
                        var prodHits = new List<string>();
                        while (rd2.Read())
                        {
                            var name = rd2.GetString(0);
                            var price = rd2.GetDecimal(1);
                            var units = rd2.IsDBNull(2) ? "" : rd2.GetString(2);
                            var stock = rd2.GetInt64(3);
                            var status = stock > 0 ? "may stock" : "out of stock";
                            prodHits.Add($"- {name}: ₱{price:0.00} ({(stock > 0 ? $"{stock} pcs {status}" : status)}){(units.Length > 0 && units != "[]" ? $" | units: {units}" : "")}");
                        }
                        rd2.Close();
                        foreach (var p in prodHits.Take(3))
                        {
                            facts.Add(p);
                            sources.Add("product");
                        }
                    }
                }
                catch { }
            }
        }
        catch { }

        var sysContent = "Ikaw ay AI assistant ng Andengs Superstore online shop (shop.jumongdev.com). Sumagot sa natural na Taglish, maikli, magalang at kapaki-pakinabang. IMPORTANTE: Huwag mag-imbento ng oras, presyo, o impormasyon na wala sa mga binigay na facts. Kung walang kaalaman tungkol sa tanong (hal. oras ng bukas, delivery), sabihin na 'Wala pa pong nakarekord na sagot dito — pakimessage po kami sa tindahan o sa Facebook page namin.' at banggitin ang shop.jumongdev.com.";
        if (facts.Count > 0)
        {
            sysContent += "\n\nMga totoong impormasyon (gawing batayan ng sagot mo, huwag mag-imbento ng iba):\n" + string.Join("\n", facts);
        }

        var messages = new List<object>
        {
            new { role = "system", content = sysContent }
        };
        if (req.History != null)
        {
            foreach (var h in req.History.Take(10))
            {
                var role = (h.Role == "assistant" || h.Role == "user") ? h.Role : "user";
                var content = (h.Content ?? "").Trim();
                if (content.Length == 0) continue;
                if (content.Length > 500) content = content.Substring(0, 500);
                messages.Add(new { role, content });
            }
        }
        messages.Add(new { role = "user", content = msg });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        string backend = "server";
        try
        {
            var body = new { model = "llama3.1:8b", messages, stream = false, options = new { num_predict = 300, temperature = 0.7 } };
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var resp = await _ollamaClient.PostAsync("http://localhost:11434/api/chat", content);
            sw.Stop();
            if (!resp.IsSuccessStatusCode)
            {
                _chatLog.Enqueue(new ChatLogEntry { At = now, Ms = sw.ElapsedMilliseconds, Ok = false, ReplyLen = 0, Err = $"Ollama {(int)resp.StatusCode}", Backend = "server" });
                TrimChatLog();
                return StatusCode(502, new { error = $"Ollama error {(int)resp.StatusCode}" });
            }
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var reply = doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
            _chatLog.Enqueue(new ChatLogEntry { At = now, Ms = sw.ElapsedMilliseconds, Ok = true, ReplyLen = reply.Length, Err = "", Backend = "server" });
            TrimChatLog();
            return Ok(new { reply = reply.Trim(), backend = "server", sources });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _chatLog.Enqueue(new ChatLogEntry { At = now, Ms = sw.ElapsedMilliseconds, Ok = false, ReplyLen = 0, Err = ex.Message, Backend = backend });
            TrimChatLog();
            return StatusCode(502, new { error = "Ollama unavailable: " + ex.Message });
        }
    }

    private static void TrimChatLog()
    {
        while (_chatLog.Count > 500) { _chatLog.TryDequeue(out _); }
    }

    [HttpGet("chat/stats")]
    public IActionResult GetChatStats()
    {
        var since = DateTime.UtcNow.AddMinutes(-60);
        var entries = _chatLog.Where(e => e.At >= since).ToArray();
        var ok = entries.Count(e => e.Ok);
        var fail = entries.Count(e => !e.Ok);
        var recent = entries.OrderByDescending(e => e.At).Take(30).Select(e => new
        {
            at = e.At.ToString("HH:mm:ss"),
            ms = e.Ms,
            ok = e.Ok,
            err = e.Err ?? "",
            replyLen = e.ReplyLen,
            backend = e.Backend
        }).ToArray();
        return Ok(new
        {
            total = entries.Length,
            ok,
            fail,
            avgMs = entries.Length > 0 ? (long)Math.Round(entries.Average(e => e.Ms)) : 0,
            maxMs = entries.Length > 0 ? entries.Max(e => e.Ms) : 0,
            recent
        });
    }

    [HttpGet("chat/kb")]
    public IActionResult GetKb([FromQuery] string? category = null, [FromQuery] string? q = null)
    {
        try
        {
            using var conn = PgDatabaseHelper.GetConnection();
            
            using var cmd = conn.CreateCommand();
            var sql = "SELECT id, category, keywords, question, answer, active, source, created_at, updated_at FROM chat_kb WHERE 1=1";
            if (!string.IsNullOrEmpty(category)) { sql += " AND category = @cat"; cmd.Parameters.AddWithValue("cat", category); }
            if (!string.IsNullOrEmpty(q)) { sql += " AND (question ILIKE '%' || @q || '%' OR answer ILIKE '%' || @q || '%')"; cmd.Parameters.AddWithValue("q", q); }
            sql += " ORDER BY id DESC LIMIT 500";
            cmd.CommandText = sql;
            using var rd = cmd.ExecuteReader();
            var list = new List<object>();
            while (rd.Read())
            {
                list.Add(new
                {
                    id = rd.GetInt32(0),
                    category = rd.GetString(1),
                    keywords = rd.GetString(2),
                    question = rd.GetString(3),
                    answer = rd.GetString(4),
                    active = rd.GetBoolean(5),
                    source = rd.GetString(6),
                    createdAt = rd.GetDateTime(7).ToString("yyyy-MM-dd HH:mm"),
                    updatedAt = rd.GetDateTime(8).ToString("yyyy-MM-dd HH:mm")
                });
            }
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    public class KbEntryRequest { public string? Category { get; set; } public string? Keywords { get; set; } public string? Question { get; set; } public string? Answer { get; set; } public bool? Active { get; set; } }

    [HttpPost("chat/kb")]
    public IActionResult CreateKb([FromBody] KbEntryRequest req)
    {
        try
        {
            var cat = (req?.Category ?? "business").Trim();
            var kw = (req?.Keywords ?? "").Trim();
            var question = (req?.Question ?? "").Trim();
            var answer = (req?.Answer ?? "").Trim();
            if (answer.Length == 0) return BadRequest(new { error = "Answer is required" });
            using var conn = PgDatabaseHelper.GetConnection();
            
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO chat_kb (category, keywords, question, answer, active, source) VALUES (@cat, @kw, @q, @a, @active, 'manual') RETURNING id";
            cmd.Parameters.AddWithValue("cat", cat);
            cmd.Parameters.AddWithValue("kw", kw);
            cmd.Parameters.AddWithValue("q", question);
            cmd.Parameters.AddWithValue("a", answer);
            cmd.Parameters.AddWithValue("active", req?.Active ?? true);
            var id = Convert.ToInt32(cmd.ExecuteScalar());
            return Ok(new { id });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPut("chat/kb/{id}")]
    public IActionResult UpdateKb(int id, [FromBody] KbEntryRequest req)
    {
        try
        {
            using var conn = PgDatabaseHelper.GetConnection();
            
            using var cmd = conn.CreateCommand();
            var sets = new List<string>();
            if (req?.Category != null) sets.Add("category = @cat");
            if (req?.Keywords != null) sets.Add("keywords = @kw");
            if (req?.Question != null) sets.Add("question = @q");
            if (req?.Answer != null) sets.Add("answer = @a");
            if (req?.Active != null) sets.Add("active = @active");
            if (sets.Count == 0) return BadRequest(new { error = "Nothing to update" });
            sets.Add("updated_at = NOW()");
            cmd.CommandText = "UPDATE chat_kb SET " + string.Join(", ", sets) + " WHERE id = @id";
            if (req?.Category != null) cmd.Parameters.AddWithValue("cat", req.Category);
            if (req?.Keywords != null) cmd.Parameters.AddWithValue("kw", req.Keywords);
            if (req?.Question != null) cmd.Parameters.AddWithValue("q", req.Question);
            if (req?.Answer != null) cmd.Parameters.AddWithValue("a", req.Answer);
            if (req?.Active != null) cmd.Parameters.AddWithValue("active", req.Active.Value);
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
            return Ok(new { ok = true });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpDelete("chat/kb/{id}")]
    public IActionResult DeleteKb(int id)
    {
        try
        {
            using var conn = PgDatabaseHelper.GetConnection();
            
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM chat_kb WHERE id = @id";
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
            return Ok(new { ok = true });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    public class ChatReviewRequest { public string? UserMessage { get; set; } public string? BotReply { get; set; } public string? Verdict { get; set; } public string? CorrectedAnswer { get; set; } }

    [HttpPost("chat/kb/review")]
    public IActionResult ReviewChat([FromBody] ChatReviewRequest req)
    {
        try
        {
            var verdict = (req?.Verdict ?? "approved").Trim();
            var userMsg = (req?.UserMessage ?? "").Trim();
            var botReply = (req?.BotReply ?? "").Trim();
            var corrected = (req?.CorrectedAnswer ?? "").Trim();

            int kbId = 0;
            if (verdict == "approved" && botReply.Length > 0)
            {
                var ans = botReply;
                using var conn = PgDatabaseHelper.GetConnection();
                
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO chat_kb (category, keywords, question, answer, active, source)
                    VALUES ('approved-reply', '', @q, @a, true, 'approved-reply') RETURNING id";
                cmd.Parameters.AddWithValue("q", userMsg.Length > 80 ? userMsg.Substring(0, 80) : userMsg);
                cmd.Parameters.AddWithValue("a", ans.Length > 2000 ? ans.Substring(0, 2000) : ans);
                kbId = Convert.ToInt32(cmd.ExecuteScalar());
            }
            else if (verdict == "corrected" && corrected.Length > 0)
            {
                using var conn = PgDatabaseHelper.GetConnection();
                
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO chat_kb (category, keywords, question, answer, active, source)
                    VALUES ('approved-reply', '', @q, @a, true, 'approved-reply') RETURNING id";
                cmd.Parameters.AddWithValue("q", userMsg.Length > 80 ? userMsg.Substring(0, 80) : userMsg);
                cmd.Parameters.AddWithValue("a", corrected.Length > 2000 ? corrected.Substring(0, 2000) : corrected);
                kbId = Convert.ToInt32(cmd.ExecuteScalar());
            }

            using var conn2 = PgDatabaseHelper.GetConnection();
            
            using var cmd2 = conn2.CreateCommand();
            cmd2.CommandText = "INSERT INTO chat_review_log (user_message, bot_reply, verdict, corrected_answer, kb_entry_id) VALUES (@um, @br, @v, @ca, @kid)";
            cmd2.Parameters.AddWithValue("um", userMsg);
            cmd2.Parameters.AddWithValue("br", botReply);
            cmd2.Parameters.AddWithValue("v", verdict);
            cmd2.Parameters.AddWithValue("ca", corrected);
            cmd2.Parameters.AddWithValue("kid", kbId);
            cmd2.ExecuteNonQuery();

            return Ok(new { ok = true, kbEntryId = kbId });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpGet("chat/kb/reviews")]
    public IActionResult GetReviews([FromQuery] int limit = 100)
    {
        try
        {
            using var conn = PgDatabaseHelper.GetConnection();
            
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, user_message, bot_reply, verdict, corrected_answer, kb_entry_id, created_at FROM chat_review_log ORDER BY id DESC LIMIT @lim";
            cmd.Parameters.AddWithValue("lim", limit);
            using var rd = cmd.ExecuteReader();
            var list = new List<object>();
            while (rd.Read())
            {
                list.Add(new
                {
                    id = rd.GetInt32(0),
                    userMessage = rd.GetString(1),
                    botReply = rd.GetString(2),
                    verdict = rd.GetString(3),
                    correctedAnswer = rd.GetString(4),
                    kbEntryId = rd.GetInt32(5),
                    createdAt = rd.GetDateTime(6).ToString("yyyy-MM-dd HH:mm")
                });
            }
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPost("chat/kb/ingest-project")]
    public IActionResult IngestProjectKb()
    {
        try
        {
            var paths = new[]
            {
                @"C:\Users\ADMIN\Desktop\JumongPosV1.01\AGENTS.md",
                @"C:\dev\JumongPosV1.01\AGENTS.md"
            };
            var path = paths.FirstOrDefault(p => System.IO.File.Exists(p));
            if (path == null) return StatusCode(404, new { error = "AGENTS.md not found on server" });

            var content = System.IO.File.ReadAllText(path);
            var lines = content.Split('\n');
            var sections = new List<(string Title, string Body)>();
            string? curTitle = null;
            var curBody = new System.Text.StringBuilder();
            foreach (var raw in lines)
            {
                var line = raw.TrimEnd('\r');
                if (line.StartsWith("## ") || line.StartsWith("### "))
                {
                    if (curTitle != null) sections.Add((curTitle, curBody.ToString().Trim()));
                    curTitle = line.TrimStart('#', ' ').Trim();
                    curBody = new System.Text.StringBuilder();
                }
                else if (curTitle != null && line.Trim().Length > 0)
                {
                    curBody.AppendLine(line.Trim());
                }
            }
            if (curTitle != null) sections.Add((curTitle, curBody.ToString().Trim()));

            int added = 0;
            using var conn = PgDatabaseHelper.GetConnection();
            
            foreach (var s in sections)
            {
                if (s.Title.Length == 0 || s.Body.Length < 30) continue;
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO chat_kb (category, keywords, question, answer, active, source)
                    VALUES ('project', @kw, @q, @a, true, 'project-ingest')
                    ON CONFLICT DO NOTHING";
                cmd.Parameters.AddWithValue("kw", s.Title);
                cmd.Parameters.AddWithValue("q", s.Title);
                cmd.Parameters.AddWithValue("a", s.Body.Length > 3000 ? s.Body.Substring(0, 3000) : s.Body);
                cmd.ExecuteNonQuery();
                added++;
            }
            return Ok(new { ok = true, sections = added, path = path });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        ulong memTotal = 0, memFree = 0;
        try
        {
            var ms = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref ms)) { memTotal = ms.ullTotalPhys; memFree = ms.ullAvailPhys; }
        }
        catch { }

        long diskFree = 0, diskTotal = 0;
        try { var d = new DriveInfo("C"); if (d.IsReady) { diskFree = d.TotalFreeSpace; diskTotal = d.TotalSize; } } catch { }

        bool dbOk = false;
        try { using var conn = Data.PgDatabaseHelper.GetConnection(); using var cmd = conn.CreateCommand(); cmd.CommandText = "SELECT 1"; cmd.ExecuteScalar(); dbOk = true; } catch { }

        var agents = _agents.Select(a => new
        {
            storeId = a.Key,
            lastSeen = a.Value.lastSeen,
            machine = a.Value.machine,
            ip = a.Value.ip,
            appVersion = a.Value.appVersion,
            hasError = a.Value.hasError
        }).OrderBy(a => a.storeId);

        return Ok(new
        {
            api = "ok",
            version = "1.1.35",
            db = dbOk ? "ok" : "down",
            uptimeSeconds = Environment.TickCount64 / 1000,
            memory = new { totalMb = (long)(memTotal / (1024 * 1024)), freeMb = (long)(memFree / (1024 * 1024)) },
            disk = new { totalMb = diskTotal / (1024 * 1024), freeMb = diskFree / (1024 * 1024) },
            agents
        });
    }

    [HttpPost("crash-report")]
    public IActionResult PostCrashReport([FromBody] CrashReportRequest req)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using (var ensure = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS crash_reports (
                id BIGSERIAL PRIMARY KEY,
                app TEXT NOT NULL DEFAULT '',
                version TEXT NOT NULL DEFAULT '',
                device TEXT NOT NULL DEFAULT '',
                type TEXT NOT NULL DEFAULT 'crash',
                log TEXT NOT NULL DEFAULT '',
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW())", conn))
        {
            ensure.ExecuteNonQuery();
        }
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO crash_reports (app, version, device, type, log)
            VALUES (@app, @ver, @dev, @t, @log)", conn);
        cmd.Parameters.AddWithValue("app", req.App ?? "");
        cmd.Parameters.AddWithValue("ver", req.Version ?? "");
        cmd.Parameters.AddWithValue("dev", req.Device ?? "");
        cmd.Parameters.AddWithValue("t", req.Type ?? "crash");
        var log = req.Log ?? "";
        cmd.Parameters.AddWithValue("log", log.Length > 20000 ? log.Substring(0, 20000) : log);
        cmd.ExecuteNonQuery();
        return Ok(new { ok = true });
    }

    [HttpGet("crash-reports")]
    public IActionResult GetCrashReports(int limit = 50)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = new NpgsqlCommand(@"SELECT id, app, version, device, type, log, created_at
            FROM crash_reports ORDER BY id DESC LIMIT @lim", conn);
        cmd.Parameters.AddWithValue("lim", Math.Clamp(limit, 1, 500));
        using var rdr = cmd.ExecuteReader();
        var list = new List<object>();
        while (rdr.Read())
            list.Add(new { id = rdr.GetInt64(0), app = rdr.GetString(1), version = rdr.GetString(2), device = rdr.GetString(3), type = rdr.GetString(4), log = rdr.GetString(5), createdAt = rdr.GetDateTime(6) });
        return Ok(list);
    }

        [HttpGet("fix-hvr-times")]
        public IActionResult FixHvrTimes()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var total = 0;
            // Both AA36 (HVR) and 7159 (HQ) machines have UTC clock with PH timezone,
            // so timestamps June 13 UTC < midnight are still 8h behind.
            // Skip June 13 invoices that are NOT June 14 (keep historical June 13 data).
            cmd.CommandText = @"
                UPDATE sales SET sale_date = sale_date + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND invoice_no LIKE '%-20260614%'
                  AND sale_date < '2026-06-14 00:00:00+00'::timestamptz";
            total += cmd.ExecuteNonQuery();
            cmd.CommandText = @"
                UPDATE void_logs SET created_at = created_at + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND created_at < '2026-06-14 00:00:00+00'::timestamptz";
            total += cmd.ExecuteNonQuery();
            cmd.CommandText = @"
                UPDATE stock_trails SET created_at = created_at + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND created_at < '2026-06-14 00:00:00+00'::timestamptz";
            total += cmd.ExecuteNonQuery();
            cmd.CommandText = @"
                UPDATE credit_transactions SET created_at = created_at + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND created_at < '2026-06-14 00:00:00+00'::timestamptz";
            total += cmd.ExecuteNonQuery();
            cmd.CommandText = @"
                UPDATE daily_closes SET close_date = close_date + INTERVAL '8 hours',
                                        created_at = created_at + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND close_date < '2026-06-14 00:00:00+00'::timestamptz";
            total += cmd.ExecuteNonQuery();
            cmd.CommandText = @"
                UPDATE expenses SET timestamp = timestamp + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND timestamp < '2026-06-14 00:00:00+00'::timestamptz";
            total += cmd.ExecuteNonQuery();
            return Ok(new { @fixed = total, message = $"Fixed {total} records across both stores ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â added 8h to timestamps" });
        }

        [HttpGet("fix-stock-trails-after-jun14")]
        public IActionResult FixStockTrailsAfterJun14()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var total = 0;
            // HVR and HQ machines have UTC clocks but PH timezone configured.
            // Before v1.0.49 desktop fix, stock trails used SQLite datetime('now','localtime')
            // which stored UTC time. Sync then appended +08:00 offset, turning UTC time into
            // a wrong +08:00 time. This fix adds 8 hours to timestamps that are clearly off
            // (stored < 08:00 AM UTC would mean actual time was between 8AM-4PM Manila time).
            // Only targets records with hour < 8 (likely wrong UTC-based timestamps).
            cmd.CommandText = @"
                UPDATE stock_trails SET created_at = created_at + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND EXTRACT(HOUR FROM created_at AT TIME ZONE 'Asia/Manila') < 8";
            total += cmd.ExecuteNonQuery();
            cmd.CommandText = @"
                UPDATE void_logs SET created_at = created_at + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND EXTRACT(HOUR FROM created_at AT TIME ZONE 'Asia/Manila') < 8";
            total += cmd.ExecuteNonQuery();
            cmd.CommandText = @"
                UPDATE credit_transactions SET created_at = created_at + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND EXTRACT(HOUR FROM created_at AT TIME ZONE 'Asia/Manila') < 8";
            total += cmd.ExecuteNonQuery();
            return Ok(new { @fixed = total, message = $"Fixed {total} records across both stores ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â added 8h to timestamps where Manila hour < 8" });
        }

        [HttpGet("fix-sync-table-times")]
        public IActionResult FixSyncTableTimes()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var total = 0;
            // SyncController.SyncTable used DateTime.TryParse with default styles,
            // which converted offset strings (+08:00) to server local time (UTC),
            // then Npgsql (session Asia/Manila) double-converted them ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â stored 8h behind.
            // This fix adds 8h to ALL records affected (stored Manila hour >= 8,
            // since hour < 8 was already handled by fix-stock-trails-after-jun14).
            cmd.CommandText = @"
                UPDATE stock_trails SET created_at = created_at + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND EXTRACT(HOUR FROM created_at AT TIME ZONE 'Asia/Manila') >= 8";
            total += cmd.ExecuteNonQuery();
            cmd.CommandText = @"
                UPDATE void_logs SET created_at = created_at + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND EXTRACT(HOUR FROM created_at AT TIME ZONE 'Asia/Manila') >= 8";
            total += cmd.ExecuteNonQuery();
            cmd.CommandText = @"
                UPDATE credit_transactions SET created_at = created_at + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND EXTRACT(HOUR FROM created_at AT TIME ZONE 'Asia/Manila') >= 8";
            total += cmd.ExecuteNonQuery();
            cmd.CommandText = @"
                UPDATE daily_closes SET close_date = close_date + INTERVAL '8 hours',
                                        created_at = created_at + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND EXTRACT(HOUR FROM close_date AT TIME ZONE 'Asia/Manila') >= 8";
            total += cmd.ExecuteNonQuery();
            cmd.CommandText = @"
                UPDATE expenses SET timestamp = timestamp + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND EXTRACT(HOUR FROM timestamp AT TIME ZONE 'Asia/Manila') >= 8";
            total += cmd.ExecuteNonQuery();
            // Also fix products and customers created_at
            cmd.CommandText = @"
                UPDATE products SET created_at = created_at + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND EXTRACT(HOUR FROM created_at AT TIME ZONE 'Asia/Manila') >= 8";
            total += cmd.ExecuteNonQuery();
            cmd.CommandText = @"
                UPDATE customers SET created_at = created_at + INTERVAL '8 hours'
                WHERE store_id IN ('STORE-20260602-AA36','STORE-20260602-7159')
                  AND EXTRACT(HOUR FROM created_at AT TIME ZONE 'Asia/Manila') >= 8";
            total += cmd.ExecuteNonQuery();
            return Ok(new { @fixed = total, message = $"Fixed {total} records ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â added 8h to all Maria hour >= 8 timestamps (SyncTable double-conversion fix)" });
        }

        [HttpGet("products/master")]
        public IActionResult GetMasterProducts()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, name, barcode, category, price, cost, stock_qty, image_data, is_active, points_exempt, points_per_unit, sell_online
                FROM master_products WHERE is_active = true ORDER BY name";
            using var reader = cmd.ExecuteReader();
            var products = new List<object>();
            while (reader.Read())
                products.Add(new {
                    id = reader.GetInt32(0),
                    name = reader.GetString(1),
                    barcode = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    category = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    price = reader.GetDecimal(4),
                    cost = reader.GetDecimal(5),
                    stockQty = reader.GetInt32(6),
                    imageData = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    isActive = reader.GetBoolean(8),
                    pointsExempt = reader.GetBoolean(9),
                    pointsPerUnit = reader.GetInt32(10),
                    sellOnline = reader.GetBoolean(11)
                });
            return Ok(products);
        }

        [HttpGet("products/master/{id}/units")]
        public IActionResult GetMasterProductUnits(int id)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, product_id, unit_name, price, cost, qty_per_unit, is_default, points_per_unit
                FROM master_product_units WHERE product_id = @pid ORDER BY is_default DESC, unit_name";
            cmd.Parameters.AddWithValue("pid", id);
            using var reader = cmd.ExecuteReader();
            var units = new List<object>();
            while (reader.Read())
                units.Add(new {
                    id = reader.GetInt32(0),
                    productId = reader.GetInt32(1),
                    unitName = reader.GetString(2),
                    price = reader.GetDecimal(3),
                    cost = reader.GetDecimal(4),
                    qtyPerUnit = reader.GetInt32(5),
                    isDefault = reader.GetBoolean(6),
                    pointsPerUnit = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
                });
            return Ok(units);
        }

        [HttpGet("products/categories")]
        public IActionResult GetCategories()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT category FROM master_products WHERE category IS NOT NULL AND category != '' ORDER BY category";
            var list = new List<string>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(r.GetString(0));
            return Ok(list);
        }

        [HttpGet("products/master/download")]
        public IActionResult DownloadMasterCatalog([FromQuery] string? since = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var where = "1=1";
            if (!string.IsNullOrEmpty(since) && DateTime.TryParse(since, out var sinceDate))
            {
                where += " AND mp.updated_at > @since";
                cmd.Parameters.AddWithValue("since", sinceDate);
            }
            cmd.CommandText = $@"
                SELECT mp.id, mp.name, mp.barcode, mp.category, mp.price, mp.cost, mp.stock_qty, mp.image_data,
                       mp.points_exempt, mp.points_per_unit, mp.is_active, mp.sell_online,
                       COALESCE(json_agg(
                           json_build_object('unitName', mpu.unit_name, 'price', mpu.price, 'cost', mpu.cost, 'qtyPerUnit', mpu.qty_per_unit, 'isDefault', mpu.is_default, 'pointsPerUnit', mpu.points_per_unit)
                           ORDER BY mpu.is_default DESC, mpu.unit_name
                       ) FILTER (WHERE mpu.id IS NOT NULL), '[]') AS units
                FROM master_products mp
                LEFT JOIN master_product_units mpu ON mpu.product_id = mp.id
                WHERE {where}
                GROUP BY mp.id ORDER BY mp.name";
            using var reader = cmd.ExecuteReader();
            var products = new List<object>();
            while (reader.Read())
                    products.Add(new {
                    id = reader.GetInt32(0),
                    name = reader.GetString(1),
                    barcode = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    category = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    price = reader.GetDecimal(4),
                    cost = reader.GetDecimal(5),
                    stockQty = reader.GetInt32(6),
                    imageData = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    pointsExempt = reader.GetBoolean(8),
                    pointsPerUnit = reader.GetInt32(9),
                    isActive = reader.GetBoolean(10),
                    sellOnline = reader.GetBoolean(11),
                    units = reader.IsDBNull(12) ? null : System.Text.Json.JsonSerializer.Deserialize<object>(reader.GetString(12))
                });
            return Ok(products);
        }

        [HttpPost("products/master/seed")]
        public IActionResult SeedMasterProducts([FromBody] List<SeedProductDto> products)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                var skipped = 0;
                foreach (var p in products)
                {
                    // Skip duplicate barcodes
                    if (!string.IsNullOrEmpty(p.Barcode))
                    {
                        using var chk = new NpgsqlCommand("SELECT id FROM master_products WHERE barcode = @b AND is_active = true", conn, tx);
                        chk.Parameters.AddWithValue("b", p.Barcode);
                        using var chr = chk.ExecuteReader();
                        if (chr.Read()) { skipped++; continue; }
                    }

                    using var cmd = new NpgsqlCommand(@"
                        INSERT INTO master_products (name, barcode, category, price, cost, stock_qty, image_data, updated_at)
                        VALUES (@name, @barcode, @cat, @price, @cost, @qty, @img, NOW()) RETURNING id", conn, tx);
                    cmd.Parameters.AddWithValue("name", p.Name);
                    cmd.Parameters.AddWithValue("barcode", (object?)p.Barcode ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("cat", p.Category ?? "");
                    cmd.Parameters.AddWithValue("price", p.Price);
                    cmd.Parameters.AddWithValue("cost", p.Cost);
                    cmd.Parameters.AddWithValue("qty", p.StockQty);
                    cmd.Parameters.AddWithValue("img", p.ImageData ?? "");
                    var productId = Convert.ToInt32(cmd.ExecuteScalar());

                    if (p.Units != null)
                    {
                        foreach (var u in p.Units)
                        {
                            using var ucmd = new NpgsqlCommand(@"
                                INSERT INTO master_product_units (product_id, unit_name, price, cost, qty_per_unit, is_default)
                                VALUES (@pid, @un, @pr, @co, @qpu, @def)", conn, tx);
                            ucmd.Parameters.AddWithValue("pid", productId);
                            ucmd.Parameters.AddWithValue("un", u.UnitName);
                            ucmd.Parameters.AddWithValue("pr", u.Price);
                            ucmd.Parameters.AddWithValue("co", u.Cost);
                            ucmd.Parameters.AddWithValue("qpu", u.QtyPerUnit);
                            ucmd.Parameters.AddWithValue("def", u.IsDefault);
                            ucmd.ExecuteNonQuery();
                        }
                    }
                }
                tx.Commit();
                return Ok(new { success = true, count = products.Count - skipped, skipped });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost("products/master")]
        public IActionResult CreateMasterProduct([FromBody] SeedProductDto p)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // Check duplicate barcode
                if (!string.IsNullOrEmpty(p.Barcode))
                {
                    using var chk = new NpgsqlCommand("SELECT id, name FROM master_products WHERE barcode = @b AND is_active = true", conn, tx);
                    chk.Parameters.AddWithValue("b", p.Barcode);
                    using var chr = chk.ExecuteReader();
                    if (chr.Read()) return Conflict(new { error = $"Barcode '{p.Barcode}' already used by: {chr.GetString(1)}" });
                }

                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO master_products (name, barcode, category, price, cost, stock_qty, image_data, points_exempt, points_per_unit, sell_online, updated_at)
                    VALUES (@n, @b, @c, @p, @co, 0, @img, @pe, @ppu, @so, NOW()) RETURNING id", conn, tx);
                cmd.Parameters.AddWithValue("n", p.Name);
                cmd.Parameters.AddWithValue("b", (object?)p.Barcode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("c", p.Category ?? "");
                cmd.Parameters.AddWithValue("p", p.Price);
                cmd.Parameters.AddWithValue("co", p.Cost);
                cmd.Parameters.AddWithValue("img", p.ImageData ?? "");
                cmd.Parameters.AddWithValue("pe", p.PointsExempt);
                cmd.Parameters.AddWithValue("ppu", p.PointsPerUnit);
                cmd.Parameters.AddWithValue("so", p.SellOnline);
                var id = Convert.ToInt32(cmd.ExecuteScalar());

                if (p.Units != null)
                {
                    foreach (var u in p.Units)
                    {
                        using var ucmd = new NpgsqlCommand(@"
                            INSERT INTO master_product_units (product_id, unit_name, price, cost, qty_per_unit, is_default, points_per_unit)
                            VALUES (@pid, @un, @pr, @co, @qpu, @def, @ppu)", conn, tx);
                        ucmd.Parameters.AddWithValue("pid", id);
                        ucmd.Parameters.AddWithValue("un", u.UnitName);
                        ucmd.Parameters.AddWithValue("pr", u.Price);
                        ucmd.Parameters.AddWithValue("co", u.Cost);
                        ucmd.Parameters.AddWithValue("qpu", u.QtyPerUnit);
                        ucmd.Parameters.AddWithValue("def", u.IsDefault);
                        ucmd.Parameters.AddWithValue("ppu", u.PointsPerUnit);
                        ucmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
                return Ok(new { success = true, id });
            }
            catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPut("products/master/{id}")]
        public IActionResult UpdateMasterProduct(int id, [FromBody] SeedProductDto p)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // Check duplicate barcode
                if (!string.IsNullOrEmpty(p.Barcode))
                {
                    using var chk = new NpgsqlCommand("SELECT id, name FROM master_products WHERE barcode = @b AND is_active = true AND id != @id", conn, tx);
                    chk.Parameters.AddWithValue("b", p.Barcode);
                    chk.Parameters.AddWithValue("id", id);
                    using var chr = chk.ExecuteReader();
                    if (chr.Read()) return Conflict(new { error = $"Barcode '{p.Barcode}' already used by: {chr.GetString(1)}" });
                }

                using var cmd = new NpgsqlCommand(@"
                    UPDATE master_products SET name=@n, barcode=@b, category=@c, price=@p, cost=@co, image_data=@img, points_exempt=@pe, points_per_unit=@ppu, is_active=@ia, sell_online=@so, updated_at=NOW()
                    WHERE id=@id", conn, tx);
                cmd.Parameters.AddWithValue("ia", p.IsActive);
                cmd.Parameters.AddWithValue("n", p.Name);
                cmd.Parameters.AddWithValue("b", (object?)p.Barcode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("c", p.Category ?? "");
                cmd.Parameters.AddWithValue("p", p.Price);
                cmd.Parameters.AddWithValue("co", p.Cost);
                cmd.Parameters.AddWithValue("img", p.ImageData ?? "");
                cmd.Parameters.AddWithValue("pe", p.PointsExempt);
                cmd.Parameters.AddWithValue("ppu", p.PointsPerUnit);
                cmd.Parameters.AddWithValue("so", p.SellOnline);
                cmd.Parameters.AddWithValue("id", id);
                cmd.ExecuteNonQuery();

                using var del = new NpgsqlCommand("DELETE FROM master_product_units WHERE product_id = @pid", conn, tx);
                del.Parameters.AddWithValue("pid", id);
                del.ExecuteNonQuery();

                if (p.Units != null)
                {
                    foreach (var u in p.Units)
                    {
                        using var ucmd = new NpgsqlCommand(@"
                            INSERT INTO master_product_units (product_id, unit_name, price, cost, qty_per_unit, is_default, points_per_unit)
                            VALUES (@pid, @un, @pr, @co, @qpu, @def, @ppu)", conn, tx);
                        ucmd.Parameters.AddWithValue("pid", id);
                        ucmd.Parameters.AddWithValue("un", u.UnitName);
                        ucmd.Parameters.AddWithValue("pr", u.Price);
                        ucmd.Parameters.AddWithValue("co", u.Cost);
                        ucmd.Parameters.AddWithValue("qpu", u.QtyPerUnit);
                        ucmd.Parameters.AddWithValue("def", u.IsDefault);
                        ucmd.Parameters.AddWithValue("ppu", u.PointsPerUnit);
                        ucmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();

                // Auto-sync linked warehouse products
                try
                {
                    using var sync = conn.CreateCommand();
                    sync.CommandText = @"
                        UPDATE wh_products SET
                            name = mp.name,
                            barcode = mp.barcode,
                            category = mp.category,
                            piece_price = mp.price,
                            box_price = mp.price * wh_products.box_qty,
                            box_cost = mp.cost * wh_products.box_qty
                        FROM master_products mp
                        WHERE wh_products.master_product_id = mp.id AND mp.id = @mid";
                    sync.Parameters.AddWithValue("mid", id);
                    sync.ExecuteNonQuery();
                }
                catch { }

                return Ok(new { success = true });
            }
            catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpDelete("products/master/{id}")]
        public IActionResult DeleteMasterProduct(int id)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE master_products SET is_active = false WHERE id = @id";
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
            return Ok(new { success = true });
        }

        [HttpPatch("products/master/{id}/flags")]
        public IActionResult PatchMasterProductFlags(int id, [FromBody] MasterProductFlagsDto f)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            var sets = new List<string>();
            if (f.SellOnline.HasValue) sets.Add("sell_online = @so");
            if (f.IsActive.HasValue) sets.Add("is_active = @ia");
            if (f.PointsExempt.HasValue) sets.Add("points_exempt = @pe");
            if (sets.Count == 0) return Ok(new { success = true });
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE master_products SET {string.Join(", ", sets)}, updated_at = NOW() WHERE id = @id";
            cmd.Parameters.AddWithValue("id", id);
            if (f.SellOnline.HasValue) cmd.Parameters.AddWithValue("so", f.SellOnline.Value);
            if (f.IsActive.HasValue) cmd.Parameters.AddWithValue("ia", f.IsActive.Value);
            if (f.PointsExempt.HasValue) cmd.Parameters.AddWithValue("pe", f.PointsExempt.Value);
            cmd.ExecuteNonQuery();
            return Ok(new { success = true });
        }

        // ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ Warehouse API ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬
        [HttpGet("warehouse/products")]
        public IActionResult WhGetProducts([FromQuery] bool activeOnly = true, [FromQuery] string? search = null, [FromQuery] bool noImage = false)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var where = activeOnly ? "wp.is_active = true" : "1=1";
            if (!string.IsNullOrEmpty(search))
                where += $" AND (wp.name ILIKE @s OR wp.barcode ILIKE @s)";
            cmd.CommandText = $@"
                SELECT wp.id, wp.name, wp.barcode, wp.category, wp.box_price, wp.box_cost, wp.box_qty, wp.piece_price, wp.stock_qty,
                       CASE WHEN wp.master_product_id IS NOT NULL THEN
                           (SELECT COALESCE(json_agg(json_build_object('unitName', mpu.unit_name, 'price', mpu.price, 'cost', mpu.cost, 'qtyPerUnit', mpu.qty_per_unit, 'isDefault', mpu.is_default) ORDER BY mpu.is_default DESC, mpu.id), '[]'::json)
                            FROM master_product_units mpu WHERE mpu.product_id = wp.master_product_id)
                       ELSE '[]'::json END AS units,
                       COALESCE(mp.image_data, '') AS imageData,
                       COALESCE(mp.cost, wp.box_cost / NULLIF(wp.box_qty, 0), 0) AS cost
                FROM wh_products wp
                LEFT JOIN master_products mp ON mp.id = wp.master_product_id
                WHERE {where} ORDER BY wp.name {(string.IsNullOrEmpty(search) ? "" : "LIMIT 500")}";
            if (!string.IsNullOrEmpty(search))
                cmd.Parameters.AddWithValue("s", $"%{search}%");
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var unitsJson = r.GetString(9);
                var imageData = r.IsDBNull(10) ? "" : r.GetString(10);
                var cost = r.IsDBNull(11) ? 0m : r.GetDecimal(11);
                data.Add(new {
                    id = r.GetInt32(0),
                    name = r.GetString(1),
                    barcode = r.IsDBNull(2) ? "" : r.GetString(2),
                    category = r.IsDBNull(3) ? "" : r.GetString(3),
                    boxPrice = r.GetDecimal(4),
                    boxCost = r.GetDecimal(5),
                    boxQty = r.GetInt32(6),
                    piecePrice = r.GetDecimal(7),
                    stockQty = r.GetInt32(8),
                    units = unitsJson != "[]" ? System.Text.Json.JsonSerializer.Deserialize<object>(unitsJson) : null,
                    imageData = noImage ? "" : (r.IsDBNull(10) ? "" : r.GetString(10)),
                    cost = r.IsDBNull(11) ? 0m : r.GetDecimal(11)
                });
            }
            return Ok(data);
        }

        [HttpPost("warehouse/products")]
        public IActionResult WhCreateProduct([FromBody] WhProductDto p)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO wh_products (name, barcode, category, box_price, box_cost, box_qty, piece_price, stock_qty) VALUES (@n, @b, @c, @bp, @bc, @bq, @pp, 0) RETURNING id";
            cmd.Parameters.AddWithValue("n", p.Name); cmd.Parameters.AddWithValue("b", (object?)p.Barcode ?? DBNull.Value); cmd.Parameters.AddWithValue("c", p.Category ?? ""); cmd.Parameters.AddWithValue("bp", p.BoxPrice); cmd.Parameters.AddWithValue("bc", p.BoxCost); cmd.Parameters.AddWithValue("bq", p.BoxQty); cmd.Parameters.AddWithValue("pp", p.PiecePrice);
            return Ok(new { id = Convert.ToInt32(cmd.ExecuteScalar()) });
        }

        [HttpPut("warehouse/products/{id}")]
        public IActionResult WhUpdateProduct(int id, [FromBody] WhProductDto p)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE wh_products SET name=@n, barcode=@b, category=@c, box_price=@bp, box_cost=@bc, box_qty=@bq, piece_price=@pp WHERE id=@id";
            cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("n", p.Name); cmd.Parameters.AddWithValue("b", (object?)p.Barcode ?? DBNull.Value); cmd.Parameters.AddWithValue("c", p.Category ?? ""); cmd.Parameters.AddWithValue("bp", p.BoxPrice); cmd.Parameters.AddWithValue("bc", p.BoxCost); cmd.Parameters.AddWithValue("bq", p.BoxQty); cmd.Parameters.AddWithValue("pp", p.PiecePrice);
            cmd.ExecuteNonQuery();
            return Ok(new { success = true });
        }

        [HttpGet("warehouse/products/imported-ids")]
        public IActionResult WhGetImportedIds()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT master_product_id FROM wh_products WHERE master_product_id IS NOT NULL AND is_active = true";
            var ids = new List<int>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) ids.Add(r.GetInt32(0));
            return Ok(ids);
        }

        [HttpGet("warehouse/inventory-summary")]
        public IActionResult WhInventorySummary()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    COUNT(*)::bigint AS total_items,
                    COALESCE(SUM(w.stock_qty), 0)::bigint AS total_stock_qty,
                    COALESCE(SUM(COALESCE(mp.cost, w.box_cost / NULLIF(w.box_qty, 0), 0) * w.stock_qty), 0) AS total_cost,
                    COALESCE(SUM(w.piece_price * w.stock_qty), 0) AS total_price,
                    COUNT(*) FILTER (WHERE COALESCE(mp.cost, w.box_cost) = 0 OR COALESCE(mp.cost, w.box_cost) IS NULL)::bigint AS zero_cost_items
                FROM wh_products w
                LEFT JOIN master_products mp ON mp.id = w.master_product_id
                WHERE w.is_active = true";
            using var r = cmd.ExecuteReader();
            if (r.Read()) return Ok(new {
                totalItems = r.GetInt64(0),
                totalStockQty = r.GetInt64(1),
                totalCost = r.GetDecimal(2),
                totalPrice = r.GetDecimal(3),
                zeroCostItems = r.GetInt64(4)
            });
            return Ok(new { totalItems = 0L, totalStockQty = 0L, totalCost = 0m, totalPrice = 0m, zeroCostItems = 0L });
        }

        [HttpPut("warehouse/products/{id}/stock-move")]
        public IActionResult WhStockMove(int id, [FromBody] WhStockMoveDto s)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // Get current product info
                string? name = null, barcode = null;
                using (var get = conn.CreateCommand()) { get.Transaction = tx;
                    get.CommandText = "SELECT name, barcode, stock_qty FROM wh_products WHERE id = @id";
                    get.Parameters.AddWithValue("id", id);
                    using var r = get.ExecuteReader();
                    if (!r.Read()) return NotFound(new { error = "Product not found" });
                    name = r.GetString(0); barcode = r.IsDBNull(1) ? null : r.GetString(1);
                    var currentStock = r.GetInt32(2);
                    if (currentStock + s.QtyChange < 0)
                        return BadRequest(new { error = "Not enough stock (have " + currentStock + ")" });
                }

                // Update stock
                using var upd = conn.CreateCommand(); upd.Transaction = tx;
                upd.CommandText = "UPDATE wh_products SET stock_qty = stock_qty + @change WHERE id = @id";
                upd.Parameters.AddWithValue("change", s.QtyChange);
                upd.Parameters.AddWithValue("id", id);
                upd.ExecuteNonQuery();

                // Log trail
                var refType = s.QtyChange > 0 ? "manual_receive" : "manual_return";
                var refText = s.Reason;
                var mvSrc = string.Equals(s.Source, "mobile", StringComparison.OrdinalIgnoreCase) ? "mobile" : "desktop";
                using var trail = conn.CreateCommand(); trail.Transaction = tx;
                trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, barcode, qty_change, reference, reference_type, source) VALUES (@pid, @pn, @bc, @qty, @ref, @rt, @src)";
                trail.Parameters.AddWithValue("pid", id);
                trail.Parameters.AddWithValue("pn", name ?? "");
                trail.Parameters.AddWithValue("bc", barcode ?? "");
                trail.Parameters.AddWithValue("qty", s.QtyChange);
                trail.Parameters.AddWithValue("ref", refText + (mvSrc == "mobile" ? (string.IsNullOrEmpty(refText) ? "Mobile receiving" : " | Mobile") : ""));
                trail.Parameters.AddWithValue("rt", refType);
                trail.Parameters.AddWithValue("src", mvSrc);
                trail.ExecuteNonQuery();

                tx.Commit();
                return Ok(new { success = true });
            }
            catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost("warehouse/receivings")]
        public IActionResult WhCreateReceiving([FromBody] WhReceivingDto b)
        {
            if (string.IsNullOrWhiteSpace(b.Source))
                return BadRequest(new { error = "Source is required" });
            if (b.Items == null || b.Items.Count == 0)
                return BadRequest(new { error = "No items" });
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                var reference = "RECV-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + " | " + b.Source.Trim();

                foreach (var it in b.Items)
                {
                    if (it.Qty <= 0) { tx.Rollback(); return BadRequest(new { error = "Invalid quantity for item " + it.ProductId }); }
                    string? name = null, barcode = null; int stock = 0;
                    using (var get = conn.CreateCommand()) { get.Transaction = tx;
                        get.CommandText = "SELECT name, barcode, stock_qty FROM wh_products WHERE id = @id";
                        get.Parameters.AddWithValue("id", it.ProductId);
                        using var r = get.ExecuteReader();
                        if (!r.Read()) { tx.Rollback(); return NotFound(new { error = "Product not found: " + it.ProductId }); }
                        name = r.GetString(0); barcode = r.IsDBNull(1) ? null : r.GetString(1); stock = r.GetInt32(2);
                    }
                    using var upd = conn.CreateCommand(); upd.Transaction = tx;
                    upd.CommandText = "UPDATE wh_products SET stock_qty = stock_qty + @q WHERE id = @id";
                    upd.Parameters.AddWithValue("q", it.Qty);
                    upd.Parameters.AddWithValue("id", it.ProductId);
                    upd.ExecuteNonQuery();

                    using var trail = conn.CreateCommand(); trail.Transaction = tx;
                    trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, barcode, qty_change, reference, reference_type, source) VALUES (@pid, @pn, @bc, @qty, @ref, 'manual_receive', @src)";
                    trail.Parameters.AddWithValue("pid", it.ProductId);
                    trail.Parameters.AddWithValue("pn", name ?? "");
                    trail.Parameters.AddWithValue("bc", barcode ?? "");
                    trail.Parameters.AddWithValue("qty", it.Qty);
                    trail.Parameters.AddWithValue("ref", reference);
                    trail.Parameters.AddWithValue("src", b.Source2 == "desktop" ? "desktop" : "mobile");
                    trail.ExecuteNonQuery();
                }

                tx.Commit();
                return Ok(new { success = true, reference });
            }
            catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("warehouse/receivings")]
        public IActionResult WhGetReceivings([FromQuery] string? from, [FromQuery] string? to, [FromQuery] int? limit)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var sql = @"
                SELECT reference, MIN(created_at) AS created_at, COUNT(*) AS item_count, SUM(qty_change) AS total_qty
                FROM wh_stock_trails
                WHERE reference_type = 'manual_receive'";
            if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var fd))
                sql += " AND created_at >= @from";
            if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var td))
                sql += " AND created_at < @to";
            sql += " GROUP BY reference ORDER BY created_at DESC LIMIT " + Math.Min(limit ?? 100, 1000);
            cmd.CommandText = sql;
            if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var f2)) cmd.Parameters.AddWithValue("from", f2);
            if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var t2)) cmd.Parameters.AddWithValue("to", t2.Date.AddDays(1));
            var list = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new { reference = r.GetString(0), createdAt = r.GetDateTime(1), itemCount = r.GetInt64(2), totalQty = r.GetInt64(3) });
            return Ok(list);
        }

        [HttpGet("warehouse/receivings/{ref}/items")]
        public IActionResult WhGetReceivingItems(string @ref)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT product_id, product_name, barcode, qty_change, created_at FROM wh_stock_trails WHERE reference = @ref OR reference LIKE @ref || ' |%' ORDER BY id";
            cmd.Parameters.AddWithValue("ref", @ref);
            var list = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new { productId = r.GetInt32(0), productName = r.GetString(1), barcode = r.IsDBNull(2) ? "" : r.GetString(2), qty = r.GetInt32(3), createdAt = r.GetDateTime(4) });
            return Ok(list);
        }

        [HttpPut("warehouse/products/{id}/stock-set")]
        public IActionResult WhSetStock(int id, [FromBody] WhStockDto s)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                string? name = null, barcode = null;
                int oldStock = 0;
                using (var get = conn.CreateCommand()) { get.Transaction = tx;
                    get.CommandText = "SELECT name, barcode, stock_qty FROM wh_products WHERE id = @id";
                    get.Parameters.AddWithValue("id", id);
                    using var r = get.ExecuteReader();
                    if (!r.Read()) return NotFound(new { error = "Product not found" });
                    name = r.GetString(0); barcode = r.IsDBNull(1) ? null : r.GetString(1);
                    oldStock = r.GetInt32(2);
                }

                using var upd = conn.CreateCommand(); upd.Transaction = tx;
                upd.CommandText = "UPDATE wh_products SET stock_qty = @qty WHERE id = @id";
                upd.Parameters.AddWithValue("qty", s.StockQty);
                upd.Parameters.AddWithValue("id", id);
                upd.ExecuteNonQuery();

                var diff = s.StockQty - oldStock;
                if (diff != 0)
                {
                    using var trail = conn.CreateCommand(); trail.Transaction = tx;
                    trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, barcode, qty_change, reference, reference_type) VALUES (@pid, @pn, @bc, @qty, 'Manual set: ' || @ref, 'manual_set')";
                    trail.Parameters.AddWithValue("pid", id);
                    trail.Parameters.AddWithValue("pn", name ?? "");
                    trail.Parameters.AddWithValue("bc", barcode ?? "");
                    trail.Parameters.AddWithValue("qty", diff);
                    trail.Parameters.AddWithValue("ref", $"from {oldStock} to {s.StockQty}");
                    trail.ExecuteNonQuery();
                }

                tx.Commit();
                return Ok(new { success = true });
            }
            catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpDelete("warehouse/products/{id}")]
        public IActionResult WhDeleteProduct(int id)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE wh_products SET is_active = false WHERE id = @id";
            cmd.Parameters.AddWithValue("id", id); cmd.ExecuteNonQuery();
            return Ok(new { success = true });
        }

        [HttpGet("warehouse/clients")]
        public IActionResult WhGetClients([FromQuery] string? storeId = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var storeFilter = string.IsNullOrEmpty(storeId) ? "" : " AND store_id = @sid";
            cmd.CommandText = $"SELECT id, name, contact, address, store_type, store_id FROM wh_clients WHERE is_active = true{storeFilter} ORDER BY name";
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("sid", storeId);
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) data.Add(new { id = r.GetInt32(0), name = r.GetString(1), contact = r.IsDBNull(2) ? "" : r.GetString(2), address = r.IsDBNull(3) ? "" : r.GetString(3), storeType = r.GetString(4), storeId = r.IsDBNull(5) ? "" : r.GetString(5) });
            return Ok(data);
        }

        [HttpPost("warehouse/clients")]
        public IActionResult WhCreateClient([FromBody] WhClientDto c)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO wh_clients (name, contact, address, store_type, store_id) VALUES (@n, @ct, @a, @st, @sid) RETURNING id";
            cmd.Parameters.AddWithValue("n", c.Name); cmd.Parameters.AddWithValue("ct", c.Contact ?? ""); cmd.Parameters.AddWithValue("a", c.Address ?? ""); cmd.Parameters.AddWithValue("st", c.StoreType ?? "pos"); cmd.Parameters.AddWithValue("sid", c.StoreId ?? "");
            return Ok(new { id = Convert.ToInt32(cmd.ExecuteScalar()) });
        }

        [HttpPut("warehouse/clients/{id}")]
        public IActionResult WhUpdateClient(int id, [FromBody] WhClientDto c)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE wh_clients SET name=@n, contact=@ct, address=@a, store_type=@st, store_id=@sid WHERE id=@id";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("n", c.Name);
            cmd.Parameters.AddWithValue("ct", c.Contact ?? "");
            cmd.Parameters.AddWithValue("a", c.Address ?? "");
            cmd.Parameters.AddWithValue("st", c.StoreType ?? "pos");
            cmd.Parameters.AddWithValue("sid", c.StoreId ?? "");
            cmd.ExecuteNonQuery();
            return Ok(new { success = true });
        }

        [HttpDelete("warehouse/clients/{id}")]
        public IActionResult WhDeleteClient(int id)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE wh_clients SET is_active = false WHERE id = @id";
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
            return Ok(new { success = true });
        }

        [HttpPost("warehouse/products/from-master/{masterId}")]
        public IActionResult WhAddFromMaster(int masterId, [FromQuery] int boxQty = 1)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();

            // Check if already imported ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â if so, update instead of duplicate
            cmd.CommandText = "SELECT id FROM wh_products WHERE master_product_id = @mid ORDER BY id LIMIT 1";
            cmd.Parameters.AddWithValue("mid", masterId);
            var existingId = cmd.ExecuteScalar();

            if (existingId != null)
            {
                // Reactivate and refresh from master
                var wid = Convert.ToInt32(existingId);
                // Clean up any extra duplicates pointing to same master
                using var cleanup = conn.CreateCommand();
                cleanup.CommandText = "UPDATE wh_products SET master_product_id = NULL WHERE master_product_id = @mid AND id != @wid";
                cleanup.Parameters.AddWithValue("mid", masterId);
                cleanup.Parameters.AddWithValue("wid", wid);
                cleanup.ExecuteNonQuery();

                cmd.CommandText = @"
                    WITH default_unit AS (
                        SELECT qty_per_unit, price
                        FROM master_product_units
                        WHERE product_id = @mid AND is_default = true
                        LIMIT 1
                    )
                    UPDATE wh_products SET
                        name = mp.name,
                        barcode = mp.barcode,
                        category = mp.category,
                        box_price = COALESCE((SELECT price FROM default_unit), mp.price * @bq),
                        box_cost = mp.cost * COALESCE((SELECT qty_per_unit FROM default_unit), @bq),
                        box_qty = COALESCE((SELECT qty_per_unit FROM default_unit), @bq),
                        piece_price = mp.price,
                        is_active = true
                    FROM master_products mp
                    WHERE wh_products.id = @wid AND mp.id = @mid AND mp.is_active = true";
                cmd.Parameters.AddWithValue("wid", wid);
                cmd.Parameters.AddWithValue("bq", boxQty);
                cmd.ExecuteNonQuery();
                return Ok(new { id = wid, updated = true });
            }

            cmd.CommandText = @"
                WITH default_unit AS (
                    SELECT qty_per_unit, price
                    FROM master_product_units
                    WHERE product_id = @mid AND is_default = true
                    LIMIT 1
                )
                INSERT INTO wh_products (name, barcode, category, box_price, box_cost, box_qty, piece_price, stock_qty, master_product_id)
                SELECT
                    mp.name, mp.barcode, mp.category,
                    COALESCE((SELECT price FROM default_unit), mp.price * @bq),
                    mp.cost * COALESCE((SELECT qty_per_unit FROM default_unit), @bq),
                    COALESCE((SELECT qty_per_unit FROM default_unit), @bq),
                    mp.price, 0, mp.id
                FROM master_products mp
                WHERE mp.id = @mid AND mp.is_active = true
                RETURNING id";
            cmd.Parameters.AddWithValue("bq", boxQty);
            var result = cmd.ExecuteScalar();
            if (result == null) return NotFound(new { error = "Master product not found" });
            return Ok(new { id = Convert.ToInt32(result) });
        }

        [HttpPost("warehouse/products/from-master/category/{category}")]
        public IActionResult WhBulkImportFromMaster(string category, [FromQuery] int boxQty = 1)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                WITH default_units AS (
                    SELECT DISTINCT ON (product_id) product_id, qty_per_unit, price
                    FROM master_product_units
                    WHERE is_default = true
                )
                INSERT INTO wh_products (name, barcode, category, box_price, box_cost, box_qty, piece_price, stock_qty, master_product_id)
                SELECT
                    mp.name, mp.barcode, mp.category,
                    COALESCE(du.price, mp.price * @bq),
                    mp.cost * COALESCE(du.qty_per_unit, @bq),
                    COALESCE(du.qty_per_unit, @bq),
                    mp.price, 0, mp.id
                FROM master_products mp
                LEFT JOIN default_units du ON du.product_id = mp.id
                WHERE mp.category = @cat AND mp.is_active = true
                AND mp.id NOT IN (SELECT master_product_id FROM wh_products WHERE master_product_id IS NOT NULL)
                RETURNING id";
            cmd.Parameters.AddWithValue("cat", category);
            cmd.Parameters.AddWithValue("bq", boxQty);
            var count = 0;
            using var r = cmd.ExecuteReader();
            while (r.Read()) count++;
            return Ok(new { imported = count });
        }

        [HttpPost("warehouse/sync-from-master")]
        public IActionResult WhSyncFromMaster()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE wh_products SET
                    name = mp.name,
                    barcode = mp.barcode,
                    category = mp.category,
                    piece_price = mp.price,
                    box_price = mp.price * wh_products.box_qty,
                    box_cost = mp.cost * wh_products.box_qty
                FROM master_products mp
                WHERE wh_products.master_product_id = mp.id AND mp.is_active = true";
            var updated = cmd.ExecuteNonQuery();
            // Deactivate warehouse products whose master was deleted
            using var deact = conn.CreateCommand();
            deact.CommandText = @"
                UPDATE wh_products SET is_active = false
                WHERE master_product_id IS NOT NULL
                AND master_product_id NOT IN (SELECT id FROM master_products WHERE is_active = true)";
            var deactivated = deact.ExecuteNonQuery();
            return Ok(new { updated, deactivated });
        }

        [HttpGet("warehouse/orders")]
        public IActionResult WhGetOrders([FromQuery] string? status = null, [FromQuery] int? clientId = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var filters = new List<string>();
            if (!string.IsNullOrEmpty(status)) { filters.Add("o.status = @st"); cmd.Parameters.AddWithValue("st", status); }
            if (clientId.HasValue) { filters.Add("o.client_id = @ci"); cmd.Parameters.AddWithValue("ci", clientId.Value); }
            var where = filters.Count > 0 ? " WHERE " + string.Join(" AND ", filters) : "";
            cmd.CommandText = $@"
                SELECT o.id, o.client_id, o.client_name, o.status, o.notes, o.total_amount, o.created_at, o.updated_at,
                       COALESCE(SUM(CASE WHEN oi.received_qty < oi.base_qty THEN 1 ELSE 0 END), 0) > 0 AS has_shortage
                FROM wh_orders o
                LEFT JOIN wh_order_items oi ON oi.order_id = o.id
                {where}
                GROUP BY o.id
                ORDER BY o.created_at DESC LIMIT 200";
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) data.Add(new { id = r.GetInt32(0), clientId = r.GetInt32(1), clientName = r.GetString(2), status = r.GetString(3), notes = r.IsDBNull(4) ? "" : r.GetString(4), totalAmount = r.GetDecimal(5), createdAt = r.GetDateTime(6), updatedAt = r.GetDateTime(7), hasShortage = r.GetBoolean(8) });
            return Ok(data);
        }

        [HttpGet("warehouse/orders/{id}")]
        public IActionResult WhGetOrder(int id)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT oi.product_name, oi.unit_type, oi.qty, oi.price, oi.total_price,
                       oi.base_qty, oi.base_unit_name, oi.product_id,
                       COALESCE(mp.id, 0) AS master_id
                FROM wh_order_items oi
                LEFT JOIN wh_products wp ON oi.product_id = wp.id
                LEFT JOIN master_products mp ON wp.master_product_id = mp.id
                WHERE oi.order_id = @oid ORDER BY oi.product_name";
            cmd.Parameters.AddWithValue("oid", id);
            var items = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) items.Add(new {
                productName = r.GetString(0),
                unitType = r.GetString(1),
                qty = r.GetInt32(2),
                price = r.GetDecimal(3),
                totalPrice = r.GetDecimal(4),
                baseQty = r.GetInt32(5),
                baseUnitName = r.GetString(6),
                productId = r.GetInt32(7),
                masterId = r.GetInt32(8)
            });
            return Ok(items);
        }

        [HttpGet("warehouse/orders/{id}/items")]
        public IActionResult WhGetOrderItems(int id)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT oi.product_id, oi.product_name, oi.base_qty, oi.base_unit_name,
                       COALESCE(wp.barcode, '') AS barcode, wp.master_product_id,
                       oi.received_qty
                FROM wh_order_items oi
                LEFT JOIN wh_products wp ON oi.product_id = wp.id
                WHERE oi.order_id = @oid ORDER BY oi.product_name";
            cmd.Parameters.AddWithValue("oid", id);
            var items = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) items.Add(new {
                productId = r.GetInt32(0),
                productName = r.GetString(1),
                baseQty = r.GetInt32(2),
                baseUnitName = r.GetString(3),
                barcode = r.GetString(4),
                masterProductId = r.IsDBNull(5) ? 0 : r.GetInt32(5),
                receivedQty = r.GetInt32(6)
            });
            return Ok(items);
        }

        [HttpPost("warehouse/orders")]
        public IActionResult WhCreateOrder([FromBody] WhOrderDto o)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmd = conn.CreateCommand(); cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO wh_orders (client_id, client_name, status, notes, total_amount) VALUES (@ci, @cn, 'pending', @n, 0) RETURNING id";
                cmd.Parameters.AddWithValue("ci", o.ClientId); cmd.Parameters.AddWithValue("cn", o.ClientName ?? ""); cmd.Parameters.AddWithValue("n", o.Notes ?? "");
                var orderId = Convert.ToInt32(cmd.ExecuteScalar());
                decimal total = 0;
                if (o.Items != null)
                {
                    foreach (var item in o.Items)
                    {
                        var baseQty = (item.BaseQty > 0) ? item.BaseQty : (item.Qty * (item.BoxQtyPerUnit > 0 ? item.BoxQtyPerUnit : 1));
                        var baseUnit = !string.IsNullOrEmpty(item.BaseUnitName) ? item.BaseUnitName : "Piece";

                        using var icmd = new NpgsqlCommand("INSERT INTO wh_order_items (order_id, product_id, product_name, unit_type, qty, price, total_price, base_qty, base_unit_name) VALUES (@oi, @pi, @pn, @ut, @q, @pr, @tp, @bq, @bun)", conn, tx);
                        icmd.Parameters.AddWithValue("oi", orderId); icmd.Parameters.AddWithValue("pi", item.ProductId); icmd.Parameters.AddWithValue("pn", item.ProductName); icmd.Parameters.AddWithValue("ut", item.UnitType ?? "box"); icmd.Parameters.AddWithValue("q", item.Qty); icmd.Parameters.AddWithValue("pr", item.Price); icmd.Parameters.AddWithValue("tp", item.TotalPrice); icmd.Parameters.AddWithValue("bq", baseQty); icmd.Parameters.AddWithValue("bun", baseUnit);
                        icmd.ExecuteNonQuery();
                        total += item.TotalPrice;
                    }
                }
                using var upCmd = new NpgsqlCommand("UPDATE wh_orders SET total_amount = @ta WHERE id = @id", conn, tx);
                upCmd.Parameters.AddWithValue("ta", total); upCmd.Parameters.AddWithValue("id", orderId);
                upCmd.ExecuteNonQuery();
                tx.Commit();
                return Ok(new { id = orderId, totalAmount = total });
            }
            catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPut("warehouse/orders/{id}/status")]
        public IActionResult WhUpdateOrderStatus(int id, [FromBody] WhStatusDto s)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE wh_orders SET status = @st, updated_at = NOW() WHERE id = @id";
            cmd.Parameters.AddWithValue("st", s.Status); cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();

            if (s.Status == "shipped")
            {
                using var get = conn.CreateCommand();
                get.CommandText = "SELECT product_id, product_name, base_qty FROM wh_order_items WHERE order_id = @oid";
                get.Parameters.AddWithValue("oid", id);
                var shipItems = new List<(int pid, string pn, int qty)>();
                using (var r = get.ExecuteReader())
                    while (r.Read()) shipItems.Add((r.GetInt32(0), r.GetString(1), r.GetInt32(2)));

                foreach (var (pid, pn, qty) in shipItems)
                {
                    using var ded = conn.CreateCommand();
                    ded.CommandText = "UPDATE wh_products SET stock_qty = stock_qty - @q WHERE id = @pid";
                    ded.Parameters.AddWithValue("q", qty);
                    ded.Parameters.AddWithValue("pid", pid);
                    ded.ExecuteNonQuery();

                    using var trail = conn.CreateCommand();
                    trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, barcode, qty_change, reference, reference_type) VALUES (@pid, @pn, '', @q, @ref, 'order_shipped')";
                    trail.Parameters.AddWithValue("pid", pid);
                    trail.Parameters.AddWithValue("pn", pn);
                    trail.Parameters.AddWithValue("q", -qty);
                    trail.Parameters.AddWithValue("ref", $"Order #{id} shipped");
                    trail.ExecuteNonQuery();
                }
            }

            return Ok(new { success = true });
        }

        [HttpGet("warehouse/transfers/pending")]
        public IActionResult WhGetPendingTransfers([FromQuery] string? storeId = null, [FromQuery] int? clientId = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var filters = "t.status = 'pending'";
            if (!string.IsNullOrEmpty(storeId)) { filters += " AND t.store_id = @sid"; cmd.Parameters.AddWithValue("sid", storeId); }
            if (clientId.HasValue) { filters += " AND t.client_id = @ci"; cmd.Parameters.AddWithValue("ci", clientId.Value); }
            cmd.CommandText = $@"
                SELECT t.id, t.client_name, t.created_at,
                       COALESCE((SELECT STRING_AGG(ti.product_name, ', ') FROM wh_transfer_items ti WHERE ti.transfer_id = t.id), '') AS items_summary,
                       COALESCE((SELECT c.name FROM wh_clients c WHERE c.store_type = 'warehouse' LIMIT 1), 'Head Office') AS warehouse_name
                FROM wh_transfers t
                WHERE {filters} ORDER BY t.created_at DESC LIMIT 50";
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) data.Add(new {
                orderId = r.GetInt32(0), clientName = r.GetString(1),
                createdAt = r.GetDateTime(2), itemsSummary = r.GetString(3),
                warehouseName = r.IsDBNull(4) ? "Head Office" : r.GetString(4)
            });
            return Ok(data);
        }

        [HttpPut("warehouse/orders/{id}/receive")]
        public IActionResult WhReceiveOrder(int id, [FromBody] WhReceiveRequest? body = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmd = conn.CreateCommand(); cmd.Transaction = tx;
                cmd.CommandText = "UPDATE wh_orders SET status = @status, updated_at = NOW() WHERE id = @id AND status = 'shipped'";
                cmd.Parameters.AddWithValue("id", id);
                cmd.Parameters.AddWithValue("status", "received");
                var rows = cmd.ExecuteNonQuery();
                if (rows == 0) { tx.Rollback(); return BadRequest(new { error = "Order not found or not in shipped status" }); }

                // Build a set of received productIds for quick lookup
                var receivedIds = new HashSet<int>();
                if (body?.Items != null)
                    foreach (var ri in body.Items)
                        if (ri.ProductId > 0) receivedIds.Add(ri.ProductId);

                // Update received_qty per item, restock shortages
                using var allItems = conn.CreateCommand(); allItems.Transaction = tx;
                allItems.CommandText = @"
                    SELECT oi.product_id, oi.product_name, oi.base_qty, oi.base_unit_name,
                           COALESCE(wp.barcode, '') AS barcode, wp.master_product_id
                    FROM wh_order_items oi
                    LEFT JOIN wh_products wp ON oi.product_id = wp.id
                    WHERE oi.order_id = @oid ORDER BY oi.product_name";
                allItems.Parameters.AddWithValue("oid", id);
                var returnedItems = new List<object>();
                var shortages = new List<object>();
                using var r2 = allItems.ExecuteReader();
                while (r2.Read())
                {
                    var productId = r2.GetInt32(0);
                    var productName = r2.GetString(1);
                    var baseQty = r2.GetInt32(2);
                    var baseUnitName = r2.GetString(3);
                    var barcode = r2.GetString(4);
                    var masterProductId = r2.IsDBNull(5) ? 0 : r2.GetInt32(5);

                    if (receivedIds.Contains(productId) || body == null || body.Items == null)
                    {
                        // This item was received
                        using var upd = conn.CreateCommand(); upd.Transaction = tx;
                        upd.CommandText = "UPDATE wh_order_items SET received_qty = @rq WHERE order_id = @oid AND product_id = @pid";
                        upd.Parameters.AddWithValue("rq", baseQty);
                        upd.Parameters.AddWithValue("oid", id);
                        upd.Parameters.AddWithValue("pid", productId);
                        upd.ExecuteNonQuery();

                        returnedItems.Add(new { productId, productName, baseQty, baseUnitName, barcode, masterProductId });
                    }
                    else
                    {
                        // Shortage ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â restock warehouse
                        using var restock = conn.CreateCommand(); restock.Transaction = tx;
                        restock.CommandText = "UPDATE wh_products SET stock_qty = stock_qty + @qty WHERE id = @pid";
                        restock.Parameters.AddWithValue("qty", baseQty);
                        restock.Parameters.AddWithValue("pid", productId);
                        restock.ExecuteNonQuery();

                        using var trail = conn.CreateCommand(); trail.Transaction = tx;
                        trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, barcode, qty_change, reference, reference_type) VALUES (@pid, @pn, @bc, @qty, @ref, 'shortage_return')";
                        trail.Parameters.AddWithValue("pid", productId);
                        trail.Parameters.AddWithValue("pn", productName);
                        trail.Parameters.AddWithValue("bc", barcode);
                        trail.Parameters.AddWithValue("qty", baseQty);
                        trail.Parameters.AddWithValue("ref", $"Order #{id} shortage restock");
                        trail.ExecuteNonQuery();

                        shortages.Add(new { productId, productName, baseQty });

                        using var upd = conn.CreateCommand(); upd.Transaction = tx;
                        upd.CommandText = "UPDATE wh_order_items SET received_qty = 0 WHERE order_id = @oid AND product_id = @pid";
                        upd.Parameters.AddWithValue("oid", id);
                        upd.Parameters.AddWithValue("pid", productId);
                        upd.ExecuteNonQuery();
                    }
                }

                // If there were shortages, mark order as partial
                if (shortages.Count > 0)
                {
                    using var partialCmd = conn.CreateCommand(); partialCmd.Transaction = tx;
                    partialCmd.CommandText = "UPDATE wh_orders SET status = 'partial', updated_at = NOW() WHERE id = @id";
                    partialCmd.Parameters.AddWithValue("id", id);
                    partialCmd.ExecuteNonQuery();
                }

                tx.Commit();
                return Ok(new { success = true, orderId = id, items = returnedItems, shortages });
            }
            catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
        }

        // ÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚Â
        // WAREHOUSE TRANSFERS (warehouse ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ POS store stock transfers)
        // ÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚Â

        [HttpGet("warehouse/transfers")]
        public IActionResult WhGetTransfers([FromQuery] string? search = null, [FromQuery] string? date = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var where = new List<string>();
            if (!string.IsNullOrEmpty(date))
            {
                where.Add("t.created_at::date = @date");
                cmd.Parameters.AddWithValue("date", date);
            }
            if (!string.IsNullOrEmpty(search))
            {
                where.Add("(t.id::text ILIKE @s OR t.client_name ILIKE @s OR t.notes ILIKE @s)");
                cmd.Parameters.AddWithValue("s", $"%{search}%");
            }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 30;
            var offset = (page - 1) * pageSize;
            cmd.CommandText = $@"
                SELECT t.id, t.client_id, t.client_name, t.status, t.notes, t.store_id,
                       t.created_at, t.updated_at,
                       COALESCE(SUM(CASE WHEN ti.received_qty < ti.qty THEN 1 ELSE 0 END), 0) > 0 AS has_shortage,
                       COUNT(*) OVER() AS total_count
                FROM wh_transfers t
                LEFT JOIN wh_transfer_items ti ON ti.transfer_id = t.id
                {whereSql}
                GROUP BY t.id
                ORDER BY t.created_at DESC
                LIMIT {pageSize} OFFSET {offset}";
            var data = new List<object>();
            int total = 0;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (total == 0) total = r.GetInt32(9);
                data.Add(new {
                    id = r.GetInt32(0), clientId = r.GetInt32(1), clientName = r.GetString(2),
                    status = r.GetString(3), notes = r.IsDBNull(4) ? "" : r.GetString(4),
                    storeId = r.IsDBNull(5) ? "" : r.GetString(5),
                    createdAt = r.GetDateTime(6), updatedAt = r.GetDateTime(7),
                    hasShortage = r.GetBoolean(8)
                });
            }
            return Ok(new { items = data, total });
        }

        [HttpPost("warehouse/transfers")]
        public IActionResult WhCreateTransfer([FromBody] WhTransferDto t)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmd = conn.CreateCommand(); cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO wh_transfers (client_id, client_name, status, notes, store_id) VALUES (@ci, @cn, 'pending', @n, @sid) RETURNING id";
                cmd.Parameters.AddWithValue("ci", t.ClientId);
                cmd.Parameters.AddWithValue("cn", t.ClientName ?? "");
                cmd.Parameters.AddWithValue("n", t.Notes ?? "");
                cmd.Parameters.AddWithValue("sid", t.StoreId ?? "");
                var transferId = Convert.ToInt32(cmd.ExecuteScalar());

                if (t.Items != null)
                {
                    // Merge duplicate items by productId
                    var merged = t.Items.GroupBy(x => x.ProductId)
                        .Select(g => new { g.First().ProductId, g.First().ProductName, g.First().Barcode, Qty = g.Sum(x => x.Qty) })
                        .ToList();

                    foreach (var item in merged)
                    {
                        // Validate stock exists and is sufficient (don't deduct ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â held in pending until POS accepts)
                        using var checkCmd = new NpgsqlCommand(
                            "SELECT stock_qty FROM wh_products WHERE id = @pid AND is_active = true", conn, tx);
                        checkCmd.Parameters.AddWithValue("pid", item.ProductId);
                        var available = checkCmd.ExecuteScalar();
                        if (available == null)
                        {
                            tx.Rollback();
                            return BadRequest(new { error = $"Product not found: {item.ProductName}" });
                        }
                        var stockOnHand = Convert.ToInt32(available);
                        if (stockOnHand < item.Qty)
                        {
                            tx.Rollback();
                            return BadRequest(new { error = $"Insufficient stock for {item.ProductName}: only {stockOnHand} available, {item.Qty} requested. Receive stock first." });
                        }

                        using var icmd = new NpgsqlCommand(
                            "INSERT INTO wh_transfer_items (transfer_id, product_id, product_name, barcode, qty) VALUES (@ti, @pi, @pn, @bc, @q)", conn, tx);
                        icmd.Parameters.AddWithValue("ti", transferId);
                        icmd.Parameters.AddWithValue("pi", item.ProductId);
                        icmd.Parameters.AddWithValue("pn", item.ProductName);
                        icmd.Parameters.AddWithValue("bc", item.Barcode ?? "");
                        icmd.Parameters.AddWithValue("q", item.Qty);
                        icmd.ExecuteNonQuery();
                    }
                }

                tx.Commit();
                return Ok(new { id = transferId });
            }
            catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("warehouse/transfers/{id}/items")]
        public IActionResult WhGetTransferItems(int id)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ti.product_id, ti.product_name, ti.barcode, ti.qty, ti.received_qty,
                       COALESCE(wp.stock_qty, 0) AS current_stock
                FROM wh_transfer_items ti
                LEFT JOIN wh_products wp ON ti.product_id = wp.id
                WHERE ti.transfer_id = @tid ORDER BY ti.product_name";
            cmd.Parameters.AddWithValue("tid", id);
            var items = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                items.Add(new {
                    productId = r.GetInt32(0), productName = r.GetString(1),
                    barcode = r.GetString(2), qty = r.GetInt32(3),
                    receivedQty = r.GetInt32(4), currentStock = r.GetInt32(5)
                });
            return Ok(items);
        }

        [HttpPut("warehouse/transfers/{id}/receive")]
        public IActionResult WhReceiveTransfer(int id, [FromBody] WhTransferReceiveRequest? body = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var checkCmd = conn.CreateCommand(); checkCmd.Transaction = tx;
                checkCmd.CommandText = "SELECT t.status, c.name FROM wh_transfers t LEFT JOIN wh_clients c ON c.id = t.client_id WHERE t.id = @id";
                checkCmd.Parameters.AddWithValue("id", id);
                string? clientName = null;
                using (var r = checkCmd.ExecuteReader())
                {
                    if (!r.Read()) return BadRequest(new { error = "Transfer not found" });
                    var status = r.GetString(0);
                    if (status != "pending") return BadRequest(new { error = "Transfer not found or not pending" });
                    clientName = r.IsDBNull(1) ? null : r.GetString(1);
                }

                var receivedIds = new HashSet<int>();
                if (body?.Items != null)
                    foreach (var ri in body.Items)
                        if (ri.ProductId > 0) receivedIds.Add(ri.ProductId);

                // Read all items first (Npgsql does not support concurrent readers)
                var itemsList = new List<(int ProductId, string ProductName, int Qty, string Barcode)>();
                using (var allItems = conn.CreateCommand()) { allItems.Transaction = tx;
                    allItems.CommandText = "SELECT ti.product_id, ti.product_name, ti.qty, ti.barcode FROM wh_transfer_items ti WHERE ti.transfer_id = @tid ORDER BY ti.product_name";
                    allItems.Parameters.AddWithValue("tid", id);
                    using var r = allItems.ExecuteReader();
                    while (r.Read())
                        itemsList.Add((r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetString(3)));
                }

                var shortages = new List<object>();
                foreach (var (productId, productName, baseQty, barcode) in itemsList)
                {
                    var accepted = body?.Items == null || receivedIds.Contains(productId);
                    var receivedQty = 0;

                    if (accepted)
                    {
                        // Deduct stock from warehouse NOW (was held pending until POS accepts).
                        // Guarded UPDATE ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â if 0 rows affected, stock is insufficient ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ treat as shortage.
                        using var deduct = conn.CreateCommand(); deduct.Transaction = tx;
                        deduct.CommandText = "UPDATE wh_products SET stock_qty = stock_qty - @bq WHERE id = @pid AND stock_qty >= @bq";
                        deduct.Parameters.AddWithValue("bq", baseQty);
                        deduct.Parameters.AddWithValue("pid", productId);
                        if (deduct.ExecuteNonQuery() > 0)
                        {
                            receivedQty = baseQty;

                            // Log transfer_out trail (only when deduction actually succeeded)
                            using var trail = conn.CreateCommand(); trail.Transaction = tx;
                            trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, barcode, qty_change, reference, reference_type) VALUES (@pid, @pn, @bc, @qty, @ref, 'transfer_out')";
                            trail.Parameters.AddWithValue("pid", productId);
                            trail.Parameters.AddWithValue("pn", productName);
                            trail.Parameters.AddWithValue("bc", barcode);
                            trail.Parameters.AddWithValue("qty", -baseQty);
                            trail.Parameters.AddWithValue("ref", $"Transfer #{id} ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ {clientName}");
                            trail.ExecuteNonQuery();
                        }
                        else
                        {
                            // Insufficient stock ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â nothing deducted, item stays in warehouse
                            shortages.Add(new { productId, productName, baseQty });
                        }
                    }
                    else
                    {
                        // Unchecked item ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â stock stays in warehouse
                        shortages.Add(new { productId, productName, baseQty });
                    }

                    using var upd = conn.CreateCommand(); upd.Transaction = tx;
                    upd.CommandText = "UPDATE wh_transfer_items SET received_qty = @rq WHERE transfer_id = @tid AND product_id = @pid";
                    upd.Parameters.AddWithValue("rq", receivedQty);
                    upd.Parameters.AddWithValue("tid", id);
                    upd.Parameters.AddWithValue("pid", productId);
                    upd.ExecuteNonQuery();
                }

                var finalStatus = shortages.Count > 0 ? "partial" : "completed";
                using var updateCmd = conn.CreateCommand(); updateCmd.Transaction = tx;
                updateCmd.CommandText = "UPDATE wh_transfers SET status = @st, updated_at = NOW() WHERE id = @id";
                updateCmd.Parameters.AddWithValue("st", finalStatus);
                updateCmd.Parameters.AddWithValue("id", id);
                updateCmd.ExecuteNonQuery();

                tx.Commit();
                return Ok(new { success = true, orderId = id, status = finalStatus, shortages });
            }
            catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPut("warehouse/transfers/{id}/cancel")]
        public IActionResult WhCancelTransfer(int id)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var checkCmd = conn.CreateCommand(); checkCmd.Transaction = tx;
                checkCmd.CommandText = "SELECT status FROM wh_transfers WHERE id = @id";
                checkCmd.Parameters.AddWithValue("id", id);
                var status = checkCmd.ExecuteScalar()?.ToString();
                if (status != "pending") return BadRequest(new { error = "Only pending transfers can be cancelled" });

                // Just mark as cancelled ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â stock was never deducted (held pending until POS receives)
                using var updateCmd = conn.CreateCommand(); updateCmd.Transaction = tx;
                updateCmd.CommandText = "UPDATE wh_transfers SET status = 'cancelled', updated_at = NOW() WHERE id = @id";
                updateCmd.Parameters.AddWithValue("id", id);
                updateCmd.ExecuteNonQuery();

                tx.Commit();
                return Ok(new { success = true });
            }
            catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("warehouse/stock-trails")]
    public IActionResult WhGetStockTrails([FromQuery] int productId)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT qty_change, reference, reference_type, created_at,
                   SUM(qty_change) OVER (PARTITION BY product_id ORDER BY created_at) - qty_change AS stock_before,
                   SUM(qty_change) OVER (PARTITION BY product_id ORDER BY created_at) AS stock_after,
                   COALESCE(invoice_no,'')
            FROM wh_stock_trails WHERE product_id = @pid ORDER BY created_at DESC LIMIT 200";
        cmd.Parameters.AddWithValue("pid", productId);
        var list = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new { qtyChange = r.GetInt32(0), reference = r.GetString(1), type = r.GetString(2), createdAt = r.GetDateTime(3), stockBefore = r.GetInt32(4), stockAfter = r.GetInt32(5), invoiceNo = r.IsDBNull(6) ? "" : r.GetString(6) });
        return Ok(list);
    }

        [HttpPost("warehouse/stock-snapshot")]
        public IActionResult WhStockSnapshot([FromBody] WhStockSnapshotRequest req)
        {
            if (req?.Items == null || req.Items.Count == 0)
                return Ok(new { ok = true, updated = 0 });
            if (string.IsNullOrEmpty(req.StoreId))
                return Ok(new { ok = true, updated = 0, skipped = "storeId missing (old POS client - update app)" });

            using var conn = Data.PgDatabaseHelper.GetConnection();
            int updated = 0;
            using (var tx = conn.BeginTransaction())
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"UPDATE products
                    SET stock_qty = @q, name = @n, barcode = @b, synced_at = NOW()
                    WHERE store_id = @sid AND pos_id = @pid";
                var pSid = cmd.Parameters.AddWithValue("sid", req.StoreId);
                var pPid = cmd.Parameters.AddWithValue("pid", 0);
                var pQ = cmd.Parameters.AddWithValue("q", 0);
                var pN = cmd.Parameters.AddWithValue("n", "");
                var pB = cmd.Parameters.AddWithValue("b", "");
                foreach (var it in req.Items)
                {
                    pPid.Value = it.ProductId;
                    pQ.Value = it.CurrentStock;
                    pN.Value = it.ProductName ?? "";
                    pB.Value = it.Barcode ?? "";
                    updated += cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
            return Ok(new { ok = true, updated });
        }

        [HttpGet("warehouse/inventory-activity")]
        public IActionResult WhGetInventoryActivity(
            [FromQuery] string? search = null,
            [FromQuery] string? from = null,
            [FromQuery] string? to = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();

            var sql = "SELECT product_name, barcode, qty_change, reference, reference_type, created_at, " +
                       "SUM(qty_change) OVER (PARTITION BY product_id ORDER BY created_at ASC ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS running_balance " +
                       "FROM wh_stock_trails WHERE 1=1";

            if (!string.IsNullOrEmpty(search))
            {
                sql += " AND (product_name ILIKE @q OR barcode ILIKE @q)";
                cmd.Parameters.AddWithValue("q", $"%{search}%");
            }

            if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var fromDate))
            {
                sql += " AND created_at >= @from";
                cmd.Parameters.AddWithValue("from", fromDate);
            }

            if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var toDate))
            {
                sql += " AND created_at <= @to";
                cmd.Parameters.AddWithValue("to", toDate.Date.AddDays(1));
            }

            sql += " ORDER BY created_at DESC LIMIT 500";

            cmd.CommandText = sql;
            var list = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new
                {
                    productName = r.GetString(0),
                    barcode = r.IsDBNull(1) ? "" : r.GetString(1),
                    stockBefore = (int?)null,
                    stockAfter = (int?)null,
                    qtyChange = r.GetInt32(2),
                    reference = r.IsDBNull(3) ? "" : r.GetString(3),
                    referenceType = r.IsDBNull(4) ? "" : r.GetString(4),
                    createdAt = r.GetDateTime(5),
                    runningBalance = r.IsDBNull(6) ? 0 : r.GetInt32(6)
                });
            return Ok(list);
        }

        [HttpGet("warehouse/stock-trails/backfill-all")]
    public IActionResult WhBackfillStockTrails()
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        var trailCount = 0;
        var deductCount = 0;
        using var tx = conn.BeginTransaction();
        try
        {
            // Step 1: Insert missing stock trails with destination name
            using var trailCmd = conn.CreateCommand(); trailCmd.Transaction = tx;
            trailCmd.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, barcode, qty_change, reference, reference_type) " +
                "SELECT ti.product_id, ti.product_name, ti.barcode, -ti.qty, 'Transfer #' || ti.transfer_id || CASE WHEN c.name IS NOT NULL THEN ' ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ ' || c.name ELSE '' END, 'transfer_out' " +
                "FROM wh_transfer_items ti JOIN wh_transfers t ON t.id = ti.transfer_id LEFT JOIN wh_clients c ON c.id = t.client_id " +
                "WHERE t.status IN ('completed','partial') AND NOT EXISTS (SELECT 1 FROM wh_stock_trails st WHERE st.reference LIKE 'Transfer #' || ti.transfer_id || '%' AND st.product_id = ti.product_id)";
            trailCount = trailCmd.ExecuteNonQuery();

            // Step 2: Deduct stock from wh_products for completed transfers that haven't been deducted yet
            // We check by looking at transfer items whose stock deduction hasn't been recorded
            // using the intersection of transfer_out trail records
            using var deductCmd = conn.CreateCommand(); deductCmd.Transaction = tx;
            deductCmd.CommandText = @"
                UPDATE wh_products wp SET stock_qty = wp.stock_qty - ti.total_qty
                FROM (
                    SELECT ti.product_id, SUM(ti.qty) as total_qty
                    FROM wh_transfer_items ti
                    JOIN wh_transfers t ON t.id = ti.transfer_id
                    WHERE t.status IN ('completed','partial')
                    GROUP BY ti.product_id
                ) ti
                WHERE wp.id = ti.product_id
                AND ti.total_qty > 0
                AND wp.stock_qty >= ti.total_qty
                AND EXISTS (
                    SELECT 1 FROM wh_stock_trails st
                    WHERE st.product_id = ti.product_id
                    AND st.reference_type = 'transfer_out'
                    AND st.created_at >= NOW() - INTERVAL '5 minutes'
                )";
            deductCount = deductCmd.ExecuteNonQuery();

            tx.Commit();
        }
        catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = "Backfill failed: " + ex.Message }); }
        return Ok(new { trailsInserted = trailCount, stockDeducted = deductCount });
    }

    [HttpGet("customers/count")]
    public IActionResult GetCustomerCount([FromQuery] string? since = null)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM customers WHERE is_active = true";
        if (!string.IsNullOrEmpty(since))
            cmd.CommandText += " AND synced_at > @since";
        if (!string.IsNullOrEmpty(since))
            cmd.Parameters.AddWithValue("since", DateTime.Parse(since).ToUniversalTime());
        return Ok(new { count = Convert.ToInt32(cmd.ExecuteScalar()) });
    }

    [HttpGet("warehouse/customers")]
    public IActionResult WhGetCustomers([FromQuery] string? search = null, [FromQuery] bool all = false)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, phone, loyalty_points, email, address, credit_balance FROM customers WHERE is_active = true";
        if (!string.IsNullOrEmpty(search))
            cmd.CommandText += " AND (name ILIKE @s OR phone ILIKE @s)";
        cmd.CommandText += " ORDER BY name";
        if (!all) cmd.CommandText += " LIMIT 200";
        if (!string.IsNullOrEmpty(search))
            cmd.Parameters.AddWithValue("s", $"%{search}%");
        var list = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new { id = r.GetInt32(0), name = r.GetString(1), phone = r.IsDBNull(2) ? "" : r.GetString(2), points = r.IsDBNull(3) ? 0 : r.GetInt32(3), email = r.IsDBNull(4) ? "" : r.GetString(4), address = r.IsDBNull(5) ? "" : r.GetString(5), creditBalance = r.GetDecimal(6) });
        return Ok(list);
    }

    [HttpPost("warehouse/credit-pay")]
    public IActionResult WhCreditPay([FromBody] WhCreditPayRequest? req)
    {
        if (req == null || req.CustomerId <= 0 || req.Amount <= 0)
            return BadRequest(new { error = "Customer and amount required" });
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            var currentBalance = 0m; var customerName = "";
            using (var cu = conn.CreateCommand()) { cu.Transaction = tx;
                cu.CommandText = "SELECT name, COALESCE(credit_balance, 0) FROM customers WHERE id = @cid";
                cu.Parameters.AddWithValue("cid", req.CustomerId);
                using var cr = cu.ExecuteReader();
                if (!cr.Read()) return BadRequest(new { error = "Customer not found" });
                customerName = cr.GetString(0); currentBalance = cr.GetDecimal(1);
            }

            string? invoiceNo = string.IsNullOrWhiteSpace(req.InvoiceNo) ? null : req.InvoiceNo.Trim();
            if (invoiceNo != null)
            {
                // Validate the receipt belongs to this customer and amount is within the receipt's current remaining
                var amount = 0m;
                using (var rc = conn.CreateCommand()) { rc.Transaction = tx;
                    rc.CommandText = "SELECT total_amount, created_at FROM wh_walkin_sales WHERE invoice_no = @inv AND customer_id = @cid AND payment_method = 'Credit' AND COALESCE(is_voided, FALSE) = FALSE";
                    rc.Parameters.AddWithValue("inv", invoiceNo);
                    rc.Parameters.AddWithValue("cid", req.CustomerId);
                    using var rr = rc.ExecuteReader();
                    if (!rr.Read()) return BadRequest(new { error = $"Receipt {invoiceNo} not found for this customer" });
                    amount = rr.GetDecimal(0);
                }

                // paid against this receipt = allocated payments (invoice_no match) + FIFO pool share that reaches it.
                // Must mirror WhCreditBreakdown's sweep (pool consumes balances oldest-first) so the displayed
                // remaining == the validated remaining.
                decimal pool = 0m;
                using (var pp = conn.CreateCommand()) { pp.Transaction = tx;
                    pp.CommandText = "SELECT COALESCE(SUM(credit), 0) FROM credit_transactions WHERE customer_id = @cid AND type = 'Payment' AND store_id = '' AND invoice_no = ''";
                    pp.Parameters.AddWithValue("cid", req.CustomerId);
                    pool = Convert.ToDecimal(pp.ExecuteScalar());
                }

                decimal remaining = 0m;
                using (var all = conn.CreateCommand()) { all.Transaction = tx;
                    all.CommandText = @"WITH alloc AS (
                            SELECT ct.invoice_no, SUM(ct.credit) AS pa FROM credit_transactions ct
                            WHERE ct.customer_id = @cid AND ct.type = 'Payment' AND ct.store_id = '' AND ct.invoice_no <> ''
                            GROUP BY ct.invoice_no
                        )
                        SELECT s.invoice_no, s.total_amount, COALESCE(a.pa, 0) FROM wh_walkin_sales s
                        LEFT JOIN alloc a ON a.invoice_no = s.invoice_no
                        WHERE s.customer_id = @cid AND s.payment_method = 'Credit' AND COALESCE(s.is_voided, FALSE) = FALSE
                        ORDER BY s.created_at ASC, s.id ASC";
                    all.Parameters.AddWithValue("cid", req.CustomerId);
                    var poolCopy = pool;
                    using var ar = all.ExecuteReader();
                    while (ar.Read())
                    {
                        var inv = ar.GetString(0);
                        var amt = ar.GetDecimal(1);
                        var alloc = ar.GetDecimal(2);
                        var bal = amt - Math.Min(alloc, amt);
                        var share = Math.Min(poolCopy, Math.Max(0m, bal));
                        poolCopy -= share;
                        if (inv == invoiceNo) { remaining = Math.Max(0m, bal - share); break; }
                    }
                }
                if (remaining <= 0) return BadRequest(new { error = $"Receipt {invoiceNo} is already fully paid" });
                if (req.Amount > remaining)
                    return BadRequest(new { error = $"Amount exceeds remaining of {invoiceNo} ({remaining:N2})" });
            }

            if (req.Amount > currentBalance)
                return BadRequest(new { error = $"Amount exceeds balance ({currentBalance:N2})" });

            var newBalance = currentBalance - req.Amount;
            using var upd = conn.CreateCommand(); upd.Transaction = tx;
            upd.CommandText = "UPDATE customers SET credit_balance = @nb WHERE id = @cid";
            upd.Parameters.AddWithValue("nb", newBalance);
            upd.Parameters.AddWithValue("cid", req.CustomerId);
            upd.ExecuteNonQuery();

            using var ct = conn.CreateCommand(); ct.Transaction = tx;
            ct.CommandText = @"INSERT INTO credit_transactions (pos_id, store_id, customer_id, sale_id, type, description, debit, credit, balance, payment_method, user_name, invoice_no, created_at, synced_at)
                VALUES (-NEXTVAL('credit_transactions_id_seq'), '', @cid, NULL, 'Payment', @desc, 0, @amt, @nb, @pm, @un, @inv, NOW(), NOW()) RETURNING id";
            ct.Parameters.AddWithValue("cid", req.CustomerId);
            ct.Parameters.AddWithValue("desc", invoiceNo != null ? $"Payment - {customerName} ({invoiceNo})" : $"Payment - {customerName}");
            ct.Parameters.AddWithValue("amt", req.Amount);
            ct.Parameters.AddWithValue("nb", newBalance);
            ct.Parameters.AddWithValue("pm", string.IsNullOrEmpty(req.Method) ? "Cash" : req.Method);
            ct.Parameters.AddWithValue("un", req.CashierName ?? "");
            ct.Parameters.AddWithValue("inv", invoiceNo ?? "");
            var txnId = Convert.ToInt32(ct.ExecuteScalar());

            tx.Commit();
            return Ok(new { id = txnId, customerId = req.CustomerId, customerName, amount = req.Amount, method = string.IsNullOrEmpty(req.Method) ? "Cash" : req.Method, balance = newBalance, invoiceNo = invoiceNo ?? "" });
        }
        catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpGet("warehouse/credit-billing")]
    public IActionResult WhCreditBilling()
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT c.id, c.name, c.phone,
                COALESCE((SELECT SUM(s.total_amount) FROM wh_walkin_sales s WHERE s.customer_id = c.id AND s.payment_method = 'Credit' AND COALESCE(s.is_voided, FALSE) = FALSE), 0) AS billed,
                COALESCE((SELECT SUM(ct.credit) FROM credit_transactions ct WHERE ct.customer_id = c.id AND ct.type = 'Payment' AND ct.store_id = ''), 0) AS paid
            FROM customers c
            WHERE c.is_active = true
            ORDER BY billed DESC LIMIT 300";
        var list = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var billed = r.GetDecimal(3); var paid = r.GetDecimal(4);
            var balance = Math.Max(0m, billed - paid);
            if (balance <= 0m) continue;
            list.Add(new { id = r.GetInt32(0), name = r.GetString(1), phone = r.IsDBNull(2) ? "" : r.GetString(2), wholesaleBalance = balance, billed, paid });
        }
        return Ok(list);
    }

    [HttpGet("warehouse/credit-breakdown")]
    public IActionResult WhCreditBreakdown([FromQuery] int customerId)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT s.invoice_no, s.created_at, s.total_amount FROM wh_walkin_sales s
            WHERE s.customer_id = @cid AND s.payment_method = 'Credit' AND COALESCE(s.is_voided, FALSE) = FALSE
            ORDER BY s.created_at ASC, s.id ASC";
        cmd.Parameters.AddWithValue("cid", customerId);
        var receipts = new List<(string invoiceNo, DateTime created, decimal amount)>();
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                receipts.Add((r.GetString(0), r.GetDateTime(1), r.GetDecimal(2)));

        // allocated payments per receipt (invoice_no linked)
        var allocated = new Dictionary<string, decimal>();
        using (var p = conn.CreateCommand())
        {
            p.CommandText = "SELECT invoice_no, COALESCE(SUM(credit), 0) FROM credit_transactions WHERE customer_id = @cid AND type = 'Payment' AND store_id = '' AND invoice_no <> '' GROUP BY invoice_no";
            p.Parameters.AddWithValue("cid", customerId);
            using var r = p.ExecuteReader();
            while (r.Read()) allocated[r.GetString(0)] = r.GetDecimal(1);
        }

        // unallocated pool (general payments, no receipt) applied FIFO over remaining balances
        decimal pool = 0m;
        using (var pp = conn.CreateCommand())
        {
            pp.CommandText = "SELECT COALESCE(SUM(credit), 0) FROM credit_transactions WHERE customer_id = @cid AND type = 'Payment' AND store_id = '' AND invoice_no = ''";
            pp.Parameters.AddWithValue("cid", customerId);
            pool = Convert.ToDecimal(pp.ExecuteScalar());
        }

        // payment trail per receipt
        var trail = new Dictionary<string, List<object>>();
        using (var tr = conn.CreateCommand())
        {
            tr.CommandText = @"SELECT invoice_no, id, COALESCE(credit, 0), COALESCE(payment_method, ''), COALESCE(user_name, ''), created_at
                FROM credit_transactions WHERE customer_id = @cid AND type = 'Payment' AND store_id = '' AND invoice_no <> ''
                ORDER BY created_at ASC";
            tr.Parameters.AddWithValue("cid", customerId);
            using var r = tr.ExecuteReader();
            while (r.Read())
            {
                var inv = r.GetString(0);
                if (!trail.TryGetValue(inv, out var list)) { list = new List<object>(); trail[inv] = list; }
                list.Add(new { id = r.GetInt32(1), amount = r.GetDecimal(2), method = r.GetString(3), cashier = r.GetString(4), date = r.GetDateTime(5) });
            }
        }

        var name = "";
        using (var c = conn.CreateCommand())
        {
            c.CommandText = "SELECT name FROM customers WHERE id = @cid";
            c.Parameters.AddWithValue("cid", customerId);
            name = Convert.ToString(c.ExecuteScalar()) ?? "";
        }

        // pass 1: apply allocated payments per receipt
        var balances = receipts.Select(rc => new
        {
            rc.invoiceNo,
            rc.created,
            rc.amount,
            allocatedPaid = allocated.TryGetValue(rc.invoiceNo, out var ap) ? Math.Min(ap, rc.amount) : 0m
        }).ToList();

        // pass 2: FIFO pool sweeps remaining balances oldest-first
        var poolCopy = pool;
        var remainingAlloc = new Dictionary<string, decimal>();
        foreach (var b in balances)
        {
            var bal = b.amount - b.allocatedPaid;
            var poolShare = Math.Min(poolCopy, Math.Max(0, bal));
            poolCopy -= poolShare;
            remainingAlloc[b.invoiceNo] = Math.Max(0, bal - poolShare);
        }

        var detail = new List<object>();
        decimal totalBalance = 0m;
        foreach (var b in balances)
        {
            var remaining = remainingAlloc[b.invoiceNo];
            totalBalance += remaining;
            detail.Add(new {
                invoiceNo = b.invoiceNo,
                date = b.created,
                amount = b.amount,
                remaining,
                paid = b.amount - remaining,
                trail = trail.TryGetValue(b.invoiceNo, out var t) ? t : new List<object>()
            });
        }
        return Ok(new { customerId, name, totalBalance, paidTotal = pool, receipts = detail });
    }

    [HttpPost("warehouse/sell")]
    public IActionResult WhSell([FromBody] WhWalkinSellRequest req)
    {
        if (req == null || req.Items == null || req.Items.Count == 0)
            return BadRequest(new { error = "No items" });
        if (string.IsNullOrWhiteSpace(req.CustomerName))
            return BadRequest(new { error = "Customer name required" });

        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            // Generate invoice number: WH-YYYYMMDD-NNNN
            var today = DateTime.Now.ToString("yyyyMMdd");
            var seq = 0;
            using (var genInv = conn.CreateCommand()) { genInv.Transaction = tx;
                genInv.CommandText = @"
                    INSERT INTO wh_invoice_counter (date_key, last_seq) VALUES (@d, 0)
                    ON CONFLICT (date_key) DO UPDATE SET last_seq = wh_invoice_counter.last_seq + 1
                    RETURNING last_seq";
                genInv.Parameters.AddWithValue("d", today);
                seq = Convert.ToInt32(genInv.ExecuteScalar());
            }
            var invoiceNo = $"WH-{today}-{seq:D4}";

            // Create sale header
            int saleId;
            using (var hdr = conn.CreateCommand()) { hdr.Transaction = tx;
                hdr.CommandText = "INSERT INTO wh_walkin_sales (customer_id, customer_name, total_amount, item_count, invoice_no, payment_method) VALUES (@cid, @cn, 0, @ic, @inv, @pm) RETURNING id";
                hdr.Parameters.AddWithValue("cid", req.CustomerId > 0 ? req.CustomerId : 0);
                hdr.Parameters.AddWithValue("cn", req.CustomerName.Trim());
                hdr.Parameters.AddWithValue("ic", req.Items.Count);
                hdr.Parameters.AddWithValue("inv", invoiceNo);
                hdr.Parameters.AddWithValue("pm", req.PaymentMethod ?? "Cash");
                saleId = Convert.ToInt32(hdr.ExecuteScalar());
            }

            decimal grandTotal = 0;
            int totalPoints = 0;

            foreach (var item in req.Items)
            {
                // Get product info + units
                string productName = "", barcode = "";
                int stockQty = 0, boxQty = 1;
                using (var get = conn.CreateCommand()) { get.Transaction = tx;
                    get.CommandText = "SELECT name, barcode, stock_qty, box_qty FROM wh_products WHERE id = @pid";
                    get.Parameters.AddWithValue("pid", item.ProductId);
                    using var r = get.ExecuteReader();
                    if (!r.Read()) return BadRequest(new { error = "Product not found: " + item.ProductId });
                    productName = r.GetString(0);
                    barcode = r.IsDBNull(1) ? "" : r.GetString(1);
                    stockQty = r.GetInt32(2);
                    boxQty = r.IsDBNull(3) ? 1 : Math.Max(1, r.GetInt32(3));
                }

                // Find unit by index (0 = default piece)
                string unitName = "Piece";
                decimal unitPrice = 0;
                int qtyPerUnit = 1;
                int pointsPerUnit = 0;

                // Get units from master_product_units via master_product_id
                using (var get = conn.CreateCommand()) { get.Transaction = tx;
                    get.CommandText = @"
                        SELECT mu.unit_name, mu.price, mu.qty_per_unit, mu.points_per_unit
                        FROM master_product_units mu
                        JOIN wh_products wp ON wp.master_product_id = mu.product_id
                        WHERE wp.id = @pid ORDER BY mu.is_default DESC, mu.id LIMIT 20";
                    get.Parameters.AddWithValue("pid", item.ProductId);
                    var units = new List<(string name, decimal price, int qty, int pts)>();
                    {
                        using var r = get.ExecuteReader();
                        while (r.Read())
                            units.Add((r.GetString(0), r.GetDecimal(1), r.GetInt32(2), r.GetInt32(3)));
                    }

                    if (units.Count > 0)
                    {
                        var idx = item.UnitIndex >= 0 && item.UnitIndex < units.Count ? item.UnitIndex : 0;
                        unitName = units[idx].name;
                        unitPrice = units[idx].price;
                        qtyPerUnit = units[idx].qty;
                        pointsPerUnit = units[idx].pts;
                    }
                    else
                    {
                        qtyPerUnit = boxQty;
                        using var fp = conn.CreateCommand(); fp.Transaction = tx;
                        fp.CommandText = "SELECT piece_price FROM wh_products WHERE id = @pid";
                        fp.Parameters.AddWithValue("pid", item.ProductId);
                        unitPrice = Convert.ToDecimal(fp.ExecuteScalar());
                    }
                }

                var stockDeduction = item.Qty * qtyPerUnit;
                if (stockQty < stockDeduction)
                    return BadRequest(new { error = $"Not enough stock for {productName} (have {stockQty}, need {stockDeduction})" });

                var subtotal = item.Qty * unitPrice;
                var points = pointsPerUnit > 0 ? item.Qty * pointsPerUnit : 0;

                // Deduct stock
                using var deduct = conn.CreateCommand(); deduct.Transaction = tx;
                deduct.CommandText = "UPDATE wh_products SET stock_qty = stock_qty - @sd WHERE id = @pid";
                deduct.Parameters.AddWithValue("sd", stockDeduction);
                deduct.Parameters.AddWithValue("pid", item.ProductId);
                deduct.ExecuteNonQuery();

                // Log stock trail
                var isMobile = string.Equals(req.Source, "mobile", StringComparison.OrdinalIgnoreCase);
                var trailSource = isMobile ? "mobile" : "desktop";
                using var trail = conn.CreateCommand(); trail.Transaction = tx;
                trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, barcode, qty_change, reference, reference_type, source) VALUES (@pid, @pn, @bc, @qc, @ref, 'walkin_sale', @src)";
                trail.Parameters.AddWithValue("pid", item.ProductId);
                trail.Parameters.AddWithValue("pn", productName);
                trail.Parameters.AddWithValue("bc", barcode);
                trail.Parameters.AddWithValue("qc", -stockDeduction);
                trail.Parameters.AddWithValue("ref", $"{invoiceNo} | {req.CustomerName.Trim()} | {unitName} x {item.Qty}{(isMobile ? " | Mobile" : "")}");
                trail.Parameters.AddWithValue("src", trailSource);
                trail.ExecuteNonQuery();

                // Insert sale item
                using var si = conn.CreateCommand(); si.Transaction = tx;
                si.CommandText = "INSERT INTO wh_walkin_sale_items (sale_id, product_id, product_name, barcode, unit_name, qty, price, subtotal, stock_deduction, points_earned) VALUES (@sid, @pid, @pn, @bc, @un, @q, @p, @st, @sd, @pts)";
                si.Parameters.AddWithValue("sid", saleId);
                si.Parameters.AddWithValue("pid", item.ProductId);
                si.Parameters.AddWithValue("pn", productName);
                si.Parameters.AddWithValue("bc", barcode);
                si.Parameters.AddWithValue("un", unitName);
                si.Parameters.AddWithValue("q", item.Qty);
                si.Parameters.AddWithValue("p", unitPrice);
                si.Parameters.AddWithValue("st", subtotal);
                si.Parameters.AddWithValue("sd", stockDeduction);
                si.Parameters.AddWithValue("pts", points);
                si.ExecuteNonQuery();

                grandTotal += subtotal;
                totalPoints += points;
            }

            // Update sale header total
            using var upd = conn.CreateCommand(); upd.Transaction = tx;
            upd.CommandText = "UPDATE wh_walkin_sales SET total_amount = @ta WHERE id = @id";
            upd.Parameters.AddWithValue("ta", grandTotal);
            upd.Parameters.AddWithValue("id", saleId);
            upd.ExecuteNonQuery();

            // Update customer loyalty points
            if (totalPoints > 0 && req.CustomerId > 0)
            {
                using var pts = conn.CreateCommand(); pts.Transaction = tx;
                pts.CommandText = "UPDATE customers SET loyalty_points = COALESCE(loyalty_points, 0) + @pts WHERE id = @cid";
                pts.Parameters.AddWithValue("pts", totalPoints);
                pts.Parameters.AddWithValue("cid", req.CustomerId);
                pts.ExecuteNonQuery();
            }

            if (req.PaymentMethod == "Credit" && req.CustomerId > 0)
            {
                using var ct = conn.CreateCommand(); ct.Transaction = tx;
                ct.CommandText = @"INSERT INTO credit_transactions (pos_id, store_id, customer_id, sale_id, type, description, debit, balance, payment_method, user_name, created_at, synced_at)
                    VALUES (@pos, '', @cid, @sid, 'Sale', @desc, @amt, (SELECT COALESCE(credit_balance, 0) FROM customers WHERE id = @cid) + @amt, 'Credit', @un, NOW(), NOW())";
                ct.Parameters.AddWithValue("pos", saleId);
                ct.Parameters.AddWithValue("cid", req.CustomerId);
                ct.Parameters.AddWithValue("sid", saleId);
                ct.Parameters.AddWithValue("desc", $"Invoice {invoiceNo} - {req.Items.Count} item(s)");
                ct.Parameters.AddWithValue("amt", grandTotal);
                ct.Parameters.AddWithValue("un", req.CustomerName ?? "");
                ct.ExecuteNonQuery();

                using var cb = conn.CreateCommand(); cb.Transaction = tx;
                cb.CommandText = "UPDATE customers SET credit_balance = COALESCE(credit_balance, 0) + @amt WHERE id = @cid";
                cb.Parameters.AddWithValue("amt", grandTotal);
                cb.Parameters.AddWithValue("cid", req.CustomerId);
                cb.ExecuteNonQuery();
            }

            tx.Commit();

            // Return receipt data
            return Ok(new { saleId, grandTotal, invoiceNo, totalPoints });
        }
        catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpGet("warehouse/sales")]
    public IActionResult WhGetSales([FromQuery] string? from, [FromQuery] string? to, [FromQuery] int limit = 500)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT s.id, s.customer_name, s.total_amount, s.item_count, s.created_at, COALESCE(s.is_voided, FALSE), COALESCE(s.invoice_no, ''), COALESCE(s.payment_method, 'Cash') FROM wh_walkin_sales s WHERE 1=1";
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var fromDate)) { cmd.CommandText += " AND s.created_at >= @from"; cmd.Parameters.AddWithValue("from", fromDate); }
        if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var toDate)) { cmd.CommandText += " AND s.created_at <= @to"; cmd.Parameters.AddWithValue("to", toDate.Date.AddDays(1)); }
        cmd.CommandText += " ORDER BY s.created_at DESC LIMIT " + limit;

            var list = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var saleId = r.GetInt32(0);
            list.Add(new { id = saleId, customerName = r.GetString(1), total = r.GetDecimal(2), itemCount = r.GetInt32(3), createdAt = r.GetDateTime(4), isVoided = r.GetBoolean(5), invoiceNo = r.IsDBNull(6) ? "" : r.GetString(6), paymentMethod = r.IsDBNull(7) ? "Cash" : r.GetString(7) });
        }
        return Ok(list);
    }

    [HttpGet("warehouse/sales/summary")]
    public IActionResult WhGetSalesSummary([FromQuery] string? from, [FromQuery] string? to)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        var where = "COALESCE(s.is_voided, FALSE) = FALSE";
        var filter = " COALESCE(S.is_voided, FALSE) = FALSE";
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var fromDate)) { where += " AND s.created_at >= @from"; filter += " AND S.created_at >= @from"; cmd.Parameters.AddWithValue("from", fromDate); }
        if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var toDate)) { where += " AND s.created_at <= @to"; filter += " AND S.created_at <= @to"; cmd.Parameters.AddWithValue("to", toDate.Date.AddDays(1)); }
        // Total sales + transaction count come from the header ONLY (one row per sale) via
        // scalar subqueries; the item join below is used exclusively for gross inventory
        // cost. Previously COUNT(*) and SUM(s.total_amount) ran over the joined rows,
        // so a sale with N items was counted N times (e.g. 16 sales -> 28 rows -> inflated
        // 465703.00 instead of 193087.00).
        cmd.CommandText = @"
            SELECT
                (SELECT COALESCE(SUM(S.total_amount), 0) FROM wh_walkin_sales S WHERE " + filter + @"),
                (SELECT COUNT(*) FROM wh_walkin_sales S WHERE " + filter + @"),
                COALESCE(SUM(si.stock_deduction * COALESCE(mp.cost, wp.box_cost / NULLIF(wp.box_qty, 0), 0)), 0) AS gross_cost
            FROM wh_walkin_sales s
            LEFT JOIN wh_walkin_sale_items si ON si.sale_id = s.id AND COALESCE(si.is_voided, FALSE) = FALSE
            LEFT JOIN wh_products wp ON wp.id = si.product_id
            LEFT JOIN master_products mp ON mp.id = wp.master_product_id
            WHERE " + where;
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return Ok(new { totalSales = 0m, transactionCount = 0, grossInventoryCost = 0m });
        return Ok(new {
            totalSales = r.GetDecimal(0),
            transactionCount = r.GetInt32(1),
            grossInventoryCost = r.GetDecimal(2)
        });
    }

    [HttpGet("warehouse/sales/{id}/items")]
    public IActionResult WhGetSaleItems(int id)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, product_name, unit_name, qty, price, subtotal, points_earned, COALESCE(is_voided, FALSE) FROM wh_walkin_sale_items WHERE sale_id = @sid ORDER BY id";
        cmd.Parameters.AddWithValue("sid", id);
        var list = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new { id = r.GetInt32(0), productName = r.GetString(1), unitName = r.GetString(2), qty = r.GetInt32(3), price = r.GetDecimal(4), subtotal = r.GetDecimal(5), points = r.GetInt32(6), isVoided = r.GetBoolean(7) });
        return Ok(list);
    }

    [HttpGet("warehouse/sales/{id}/receipt")]
    public IActionResult WhGetSaleReceipt(int id)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        object? header = null;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, COALESCE(invoice_no, ''), COALESCE(customer_name, ''), total_amount, COALESCE(payment_method, 'Cash'), created_at, COALESCE(is_voided, FALSE) FROM wh_walkin_sales WHERE id = @sid";
            cmd.Parameters.AddWithValue("sid", id);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return NotFound(new { error = "Sale not found" });
            header = new { id = r.GetInt32(0), invoiceNo = r.GetString(1), customerName = r.GetString(2), total = r.GetDecimal(3), paymentMethod = r.GetString(4), createdAt = r.GetDateTime(5), isVoided = r.GetBoolean(6) };
        }
        var items = new List<object>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT product_name, unit_name, qty, price, subtotal, COALESCE(is_voided, FALSE) FROM wh_walkin_sale_items WHERE sale_id = @sid ORDER BY id";
            cmd.Parameters.AddWithValue("sid", id);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                items.Add(new { productName = r.GetString(0), unitName = r.GetString(1), qty = r.GetInt32(2), price = r.GetDecimal(3), subtotal = r.GetDecimal(4), isVoided = r.GetBoolean(5) });
        }
        return Ok(new { header, items });
    }

    [HttpPost("warehouse/sales/{id}/void")]
    public IActionResult WhVoidSale(int id, [FromBody] WhVoidRequest? req)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            string invoiceNo = "";
            using var chk = conn.CreateCommand(); chk.Transaction = tx;
            chk.CommandText = "SELECT is_voided, COALESCE(invoice_no, ''), customer_name, total_amount, COALESCE(payment_method, 'Cash'), COALESCE(customer_id, 0) FROM wh_walkin_sales WHERE id = @id";
            chk.Parameters.AddWithValue("id", id);
            using var cr = chk.ExecuteReader();
            if (!cr.Read()) return NotFound(new { error = "Sale not found" });
            if (cr.GetBoolean(0)) return BadRequest(new { error = "Sale already voided" });
            invoiceNo = cr.GetString(1);
            var custName = cr.IsDBNull(2) ? "" : cr.GetString(2);
            var totalAmt = cr.GetDecimal(3);
            var paymentMethod = cr.IsDBNull(4) ? "Cash" : cr.GetString(4);
            var customerId = cr.IsDBNull(5) ? 0 : cr.GetInt32(5);
            cr.Close();

            var isPartial = req?.Items != null && req.Items.Count > 0;
            var voidItemIds = new HashSet<int>();
            if (isPartial)
                foreach (var vi in req!.Items!)
                    if (vi.ItemId > 0) voidItemIds.Add(vi.ItemId);

            using var items = conn.CreateCommand(); items.Transaction = tx;
            items.CommandText = "SELECT id, product_id, product_name, stock_deduction, qty, price, COALESCE(points_earned, 0) FROM wh_walkin_sale_items WHERE sale_id = @sid AND COALESCE(is_voided, FALSE) = FALSE";
            items.Parameters.AddWithValue("sid", id);
            using var r = items.ExecuteReader();
            var allItems = new List<(int itemId, int pid, string pname, int dedQty, int qty, decimal price, int pts)>();
            while (r.Read())
                allItems.Add((r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4), r.GetDecimal(5), r.GetInt32(6)));
            r.Close();

            int voidedCount = 0;
            decimal voidedAmt = 0;
            int voidedPoints = 0;
            foreach (var (itemId, pid, pname, dedQty, qty, price, pts) in allItems)
            {
                if (isPartial && !voidItemIds.Contains(itemId)) continue;

                using var upd = conn.CreateCommand(); upd.Transaction = tx;
                upd.CommandText = "UPDATE wh_products SET stock_qty = stock_qty + @qty WHERE id = @pid";
                upd.Parameters.AddWithValue("pid", pid);
                upd.Parameters.AddWithValue("qty", dedQty);
                upd.ExecuteNonQuery();

                using var trail = conn.CreateCommand(); trail.Transaction = tx;
                trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, qty_change, reference, reference_type) VALUES (@pid, @pn, @qty, @ref, 'void_return')";
                trail.Parameters.AddWithValue("pid", pid);
                trail.Parameters.AddWithValue("pn", pname);
                trail.Parameters.AddWithValue("qty", dedQty);
                trail.Parameters.AddWithValue("ref", isPartial ? $"Wholesale Partial Void #{id}" : $"Wholesale Void #{id}");
                trail.ExecuteNonQuery();

                using var mi = conn.CreateCommand(); mi.Transaction = tx;
                mi.CommandText = "UPDATE wh_walkin_sale_items SET is_voided = TRUE WHERE id = @iid";
                mi.Parameters.AddWithValue("iid", itemId);
                mi.ExecuteNonQuery();

                var action = isPartial ? "VoidItem" : "VoidSale";
                using var vl = conn.CreateCommand(); vl.Transaction = tx;
                vl.CommandText = "INSERT INTO wh_void_logs (sale_id, invoice_no, action, reason, product_name, quantity, amount, user_name) VALUES (@sid, @inv, @act, @rsn, @pn, @qty, @amt, @un)";
                vl.Parameters.AddWithValue("sid", id);
                vl.Parameters.AddWithValue("inv", invoiceNo);
                vl.Parameters.AddWithValue("act", action);
                vl.Parameters.AddWithValue("rsn", req?.Reason ?? "");
                vl.Parameters.AddWithValue("pn", pname);
                vl.Parameters.AddWithValue("qty", qty);
                vl.Parameters.AddWithValue("amt", price * qty);
                vl.Parameters.AddWithValue("un", req?.UserName ?? "");
                vl.ExecuteNonQuery();

                voidedCount++;
                voidedAmt += price * qty;
                voidedPoints += pts;
            }

            if (isPartial && voidedAmt > 0)
            {
                using var updH = conn.CreateCommand(); updH.Transaction = tx;
                updH.CommandText = "UPDATE wh_walkin_sales SET total_amount = total_amount - @amt WHERE id = @id";
                updH.Parameters.AddWithValue("amt", voidedAmt);
                updH.Parameters.AddWithValue("id", id);
                updH.ExecuteNonQuery();
            }

            if (customerId > 0 && voidedAmt > 0 && string.Equals(paymentMethod, "Credit", StringComparison.OrdinalIgnoreCase))
            {
                using var cb = conn.CreateCommand(); cb.Transaction = tx;
                cb.CommandText = "UPDATE customers SET credit_balance = COALESCE(credit_balance, 0) - @amt WHERE id = @cid";
                cb.Parameters.AddWithValue("amt", voidedAmt);
                cb.Parameters.AddWithValue("cid", customerId);
                cb.ExecuteNonQuery();

                using var ct = conn.CreateCommand(); ct.Transaction = tx;
                ct.CommandText = @"INSERT INTO credit_transactions (pos_id, store_id, customer_id, sale_id, type, description, debit, credit, balance, payment_method, user_name, created_at, synced_at)
                    VALUES (-NEXTVAL('credit_transactions_id_seq'), '', @cid, @sid, 'Void', @desc, 0, @amt, (SELECT COALESCE(credit_balance, 0) FROM customers WHERE id = @cid), 'Credit', @un, NOW(), NOW())";
                ct.Parameters.AddWithValue("cid", customerId);
                ct.Parameters.AddWithValue("cid", customerId);
                ct.Parameters.AddWithValue("sid", id);
                ct.Parameters.AddWithValue("desc", $"Void Invoice {invoiceNo} - {voidedCount} item(s)");
                ct.Parameters.AddWithValue("amt", voidedAmt);
                ct.Parameters.AddWithValue("un", req?.UserName ?? "");
                ct.ExecuteNonQuery();
            }

            if (customerId > 0 && voidedPoints > 0)
            {
                using var pt = conn.CreateCommand(); pt.Transaction = tx;
                pt.CommandText = "UPDATE customers SET loyalty_points = COALESCE(loyalty_points, 0) - @pts WHERE id = @cid";
                pt.Parameters.AddWithValue("pts", voidedPoints);
                pt.Parameters.AddWithValue("cid", customerId);
                pt.ExecuteNonQuery();
            }

            if (!isPartial)
            {
                using var mark = conn.CreateCommand(); mark.Transaction = tx;
                mark.CommandText = "UPDATE wh_walkin_sales SET is_voided = TRUE WHERE id = @id";
                mark.Parameters.AddWithValue("id", id);
                mark.ExecuteNonQuery();
            }

            tx.Commit();
            return Ok(new { ok = true, voidedCount, voidedAmt, isPartial });
        }
        catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPut("warehouse/sales/{id}")]
    public IActionResult WhEditSale(int id, [FromBody] WhWalkinSellRequest req)
    {
        if (req == null || req.Items == null || req.Items.Count == 0)
            return BadRequest(new { error = "No items" });

        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            // Restore all original stock deductions
            using var orig = conn.CreateCommand(); orig.Transaction = tx;
            orig.CommandText = "SELECT product_id, product_name, stock_deduction FROM wh_walkin_sale_items WHERE sale_id = @sid";
            orig.Parameters.AddWithValue("sid", id);
            using var rOrig = orig.ExecuteReader();
            var restores = new List<(int pid, string pn, int qty)>();
            while (rOrig.Read()) restores.Add((rOrig.GetInt32(0), rOrig.GetString(1), rOrig.GetInt32(2)));
            rOrig.Close();
            foreach (var (pid, pn, qty) in restores)
            {
                using var res = conn.CreateCommand(); res.Transaction = tx;
                res.CommandText = "UPDATE wh_products SET stock_qty = stock_qty + @qty WHERE id = @pid";
                res.Parameters.AddWithValue("pid", pid);
                res.Parameters.AddWithValue("qty", qty);
                res.ExecuteNonQuery();

                using var trail = conn.CreateCommand(); trail.Transaction = tx;
                trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, barcode, qty_change, reference, reference_type) VALUES (@pid, @pn, '', @qty, @ref, 'walkin_sale_edit_restore')";
                trail.Parameters.AddWithValue("pid", pid);
                trail.Parameters.AddWithValue("pn", pn);
                trail.Parameters.AddWithValue("qty", qty);
                trail.Parameters.AddWithValue("ref", $"Wholesale Edit #{id} restore");
                trail.ExecuteNonQuery();
            }

            // Delete old items
            using var del = conn.CreateCommand(); del.Transaction = tx;
            del.CommandText = "DELETE FROM wh_walkin_sale_items WHERE sale_id = @sid";
            del.Parameters.AddWithValue("sid", id);
            del.ExecuteNonQuery();

            // Insert new items + deduct stock
            decimal grandTotal = 0;
            foreach (var item in req.Items)
            {
                // Get product info
                string pn = ""; int boxQty = 1; decimal unitPrice = 0; int qtyPerUnit = 1;
                using (var get = conn.CreateCommand()) { get.Transaction = tx;
                    get.CommandText = "SELECT name, box_qty, piece_price FROM wh_products WHERE id = @pid";
                    get.Parameters.AddWithValue("pid", item.ProductId);
                    using var r = get.ExecuteReader();
                    if (!r.Read()) return BadRequest(new { error = "Product not found: " + item.ProductId });
                    pn = r.GetString(0); boxQty = Math.Max(1, r.IsDBNull(1) ? 1 : r.GetInt32(1));
                }

                // Get unit by index
                string unitName = "Piece"; unitPrice = 0; qtyPerUnit = 1;
                using (var get = conn.CreateCommand()) { get.Transaction = tx;
                    get.CommandText = @"SELECT mu.unit_name, mu.price, mu.qty_per_unit FROM master_product_units mu JOIN wh_products wp ON wp.master_product_id = mu.product_id WHERE wp.id = @pid ORDER BY mu.is_default DESC, mu.id LIMIT 20";
                    get.Parameters.AddWithValue("pid", item.ProductId);
                    var units = new List<(string n, decimal p, int q)>();
                    using var r = get.ExecuteReader(); while (r.Read()) units.Add((r.GetString(0), r.GetDecimal(1), r.GetInt32(2)));
                    if (units.Count > 0) { var idx = item.UnitIndex >= 0 && item.UnitIndex < units.Count ? item.UnitIndex : 0; unitName = units[idx].n; unitPrice = units[idx].p; qtyPerUnit = units[idx].q; }
                }

                var stockDeduction = item.Qty * qtyPerUnit;
                var subtotal = item.Qty * unitPrice;

                using var ins = conn.CreateCommand(); ins.Transaction = tx;
                ins.CommandText = "INSERT INTO wh_walkin_sale_items (sale_id, product_id, product_name, barcode, unit_name, qty, price, subtotal, stock_deduction) VALUES (@sid, @pid, @pn, '', @un, @qty, @pr, @st, @sd)";
                ins.Parameters.AddWithValue("sid", id); ins.Parameters.AddWithValue("pid", item.ProductId);
                ins.Parameters.AddWithValue("pn", pn); ins.Parameters.AddWithValue("un", unitName);
                ins.Parameters.AddWithValue("qty", item.Qty); ins.Parameters.AddWithValue("pr", unitPrice);
                ins.Parameters.AddWithValue("st", subtotal); ins.Parameters.AddWithValue("sd", stockDeduction);
                ins.ExecuteNonQuery();

                using var ded = conn.CreateCommand(); ded.Transaction = tx;
                ded.CommandText = "UPDATE wh_products SET stock_qty = stock_qty - @sd WHERE id = @pid";
                ded.Parameters.AddWithValue("pid", item.ProductId); ded.Parameters.AddWithValue("sd", stockDeduction);
                ded.ExecuteNonQuery();

                using var trail = conn.CreateCommand(); trail.Transaction = tx;
                trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, barcode, qty_change, reference, reference_type) VALUES (@pid, @pn, '', @qty, @ref, 'walkin_sale')";
                trail.Parameters.AddWithValue("pid", item.ProductId);
                trail.Parameters.AddWithValue("pn", pn);
                trail.Parameters.AddWithValue("qty", -stockDeduction);
                trail.Parameters.AddWithValue("ref", $"Wholesale Edit #{id} | {unitName} x {item.Qty}");
                trail.ExecuteNonQuery();

                grandTotal += subtotal;
            }

            using var upd = conn.CreateCommand(); upd.Transaction = tx;
            upd.CommandText = "UPDATE wh_walkin_sales SET total_amount = @t, item_count = @ic WHERE id = @sid";
            upd.Parameters.AddWithValue("t", grandTotal); upd.Parameters.AddWithValue("ic", req.Items.Count);
            upd.Parameters.AddWithValue("sid", id);
            upd.ExecuteNonQuery();

            tx.Commit();
            return Ok(new { ok = true, grandTotal });
        }
        catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpGet("warehouse/void-logs")]
    public IActionResult WhGetVoidLogs([FromQuery] int limit = 200)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, sale_id, invoice_no, action, reason, product_name, quantity, amount, user_name, created_at FROM wh_void_logs ORDER BY created_at DESC LIMIT @lmt";
        cmd.Parameters.AddWithValue("lmt", limit);
        var list = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new { id = r.GetInt32(0), saleId = r.IsDBNull(1) ? 0 : r.GetInt32(1), invoiceNo = r.IsDBNull(2) ? "" : r.GetString(2), action = r.GetString(3), reason = r.IsDBNull(4) ? "" : r.GetString(4), productName = r.IsDBNull(5) ? "" : r.GetString(5), qty = r.GetInt32(6), amount = r.GetDecimal(7), userName = r.IsDBNull(8) ? "" : r.GetString(8), createdAt = r.GetDateTime(9) });
        return Ok(list);
    }

    [HttpPost("receipt-audit")]
    public IActionResult PostReceiptAudit([FromBody] ReceiptAuditRequest req)
    {
        try
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO receipt_audits (store_id, store_name, shift_date, total_receipts, voided_count, deleted_count, lost_value, voided_invoices, missing_invoices)
                VALUES (@sid, @sn, @sd, @tr, @vc, @dc, @lv, @vi::json, @mi::json) RETURNING id";
            cmd.Parameters.AddWithValue("sid", req.StoreId ?? "");
            cmd.Parameters.AddWithValue("sn", req.StoreName ?? "");
            cmd.Parameters.AddWithValue("sd", string.IsNullOrEmpty(req.ShiftDate) ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(req.ShiftDate));
            cmd.Parameters.AddWithValue("tr", req.TotalReceipts);
            cmd.Parameters.AddWithValue("vc", req.VoidedCount);
            cmd.Parameters.AddWithValue("dc", req.DeletedCount);
            cmd.Parameters.AddWithValue("lv", req.LostValue);
            cmd.Parameters.AddWithValue("vi", NpgsqlTypes.NpgsqlDbType.Jsonb, System.Text.Json.JsonSerializer.Serialize(req.VoidedInvoices ?? new List<string>()));
            cmd.Parameters.AddWithValue("mi", NpgsqlTypes.NpgsqlDbType.Jsonb, System.Text.Json.JsonSerializer.Serialize(req.MissingInvoices ?? new List<string>()));
            var id = cmd.ExecuteScalar();
            return Ok(new { id });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpGet("receipt-audit")]
    public IActionResult GetReceiptAudits([FromQuery] int limit = 50)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, store_id, store_name, shift_date, total_receipts, voided_count, deleted_count, lost_value, voided_invoices, missing_invoices, created_at FROM receipt_audits ORDER BY shift_date DESC LIMIT @lmt";
        cmd.Parameters.AddWithValue("lmt", limit);
        var list = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new {
                id = r.GetInt32(0), storeId = r.GetString(1), storeName = r.GetString(2),
                shiftDate = r.GetDateTime(3), totalReceipts = r.GetInt32(4), voidedCount = r.GetInt32(5),
                deletedCount = r.GetInt32(6), lostValue = r.GetDecimal(7),
                voidedInvoices = r.GetString(8), missingInvoices = r.GetString(9),
                createdAt = r.GetDateTime(10)
            });
        return Ok(list);
    }

    [HttpPost("warehouse/end-shift")]
    public IActionResult WhEndShift([FromBody] WhEndShiftRequest? req)
    {
        if (req == null) req = new WhEndShiftRequest();
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            var since = DateTime.MinValue;
            using (var last = conn.CreateCommand()) { last.Transaction = tx;
                last.CommandText = "SELECT COALESCE(MAX(close_date), @min) FROM wh_daily_closes";
                last.Parameters.AddWithValue("min", since);
                since = Convert.ToDateTime(last.ExecuteScalar());
            }
            if (since == DateTime.MinValue)
            {
                var ph = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
                var todayPh = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ph).Date;
                since = TimeZoneInfo.ConvertTimeToUtc(todayPh, ph);
            }

            using var totals = conn.CreateCommand(); totals.Transaction = tx;
            totals.CommandText = $@"SELECT
                COALESCE(SUM(CASE WHEN COALESCE(is_voided, FALSE) = FALSE THEN total_amount ELSE 0 END), 0) AS total_sales,
                COALESCE(SUM(CASE WHEN COALESCE(is_voided, FALSE) = FALSE AND payment_method = 'Cash' THEN total_amount ELSE 0 END), 0) AS total_cash,
                COALESCE(SUM(CASE WHEN COALESCE(is_voided, FALSE) = FALSE AND payment_method = 'E-Wallet' THEN total_amount ELSE 0 END), 0) AS total_ewallet,
                COALESCE(SUM(CASE WHEN COALESCE(is_voided, FALSE) = FALSE AND payment_method = 'Credit' THEN total_amount ELSE 0 END), 0) AS total_credit,
                COALESCE((SELECT SUM(si.subtotal) FROM wh_walkin_sale_items si JOIN wh_walkin_sales sv ON sv.id = si.sale_id WHERE sv.created_at >= @since AND si.is_voided = TRUE), 0) AS total_voided,
                COUNT(*) FILTER (WHERE COALESCE(is_voided, FALSE) = FALSE) AS sale_count,
                COALESCE((SELECT SUM(ct.credit) FROM credit_transactions ct WHERE ct.type = 'Payment' AND ct.created_at >= @since), 0) AS credit_collected,
                COALESCE((SELECT SUM(ct.credit) FROM credit_transactions ct WHERE ct.type = 'Payment' AND ct.payment_method = 'Cash' AND ct.created_at >= @since), 0) AS credit_collected_cash
                FROM wh_walkin_sales WHERE created_at >= @since";
            totals.Parameters.AddWithValue("since", since);

            using var tr = totals.ExecuteReader();
            tr.Read();
            var totalSales = tr.GetDecimal(0); var totalCash = tr.GetDecimal(1); var totalEw = tr.GetDecimal(2);
            var totalCredit = tr.GetDecimal(3); var totalVoided = tr.GetDecimal(4); var saleCount = tr.GetInt32(5);
            var creditCollected = tr.GetDecimal(6); var creditCollectedCash = tr.GetDecimal(7);
            tr.Close();

            if (req.Preview)
                return Ok(new { preview = true, since, totalSales, totalCash, totalEw, totalCredit, totalVoided, saleCount, creditCollected, expectedCash = totalCash + creditCollectedCash });

            var cashOnHand = req.Denom1000 * 1000m + req.Denom500 * 500m + req.Denom200 * 200m + req.Denom100 * 100m
                + req.Denom50 * 50m + req.Denom20 * 20m + req.DenomCoins;
            var expectedCash = totalCash + creditCollectedCash;
            var diff = cashOnHand - expectedCash;

            if (cashOnHand <= 0 && expectedCash > 0)
                return BadRequest(new { error = "Cash on hand is 0 while expected cash is " + expectedCash.ToString("N2") + ". Enter the actual denominations (or confirm) before saving the shift." });

            using var ins = conn.CreateCommand(); ins.Transaction = tx;
            ins.CommandText = @"INSERT INTO wh_daily_closes (close_date, total_sales, total_cash, total_ewallet, total_credit, total_voided, sale_count, credit_collected, cash_on_hand, difference, expenses, cashier_name, denom1000, denom500, denom200, denom100, denom50, denom20, denom_coins)
                VALUES (NOW(), @ts, @tc, @te, @tcr, @tv, @sc, @cc, @ch, @d, @ex, @cn, @d1000, @d500, @d200, @d100, @d50, @d20, @dcoins) RETURNING id";
            ins.Parameters.AddWithValue("ts", totalSales);
            ins.Parameters.AddWithValue("tc", totalCash);
            ins.Parameters.AddWithValue("te", totalEw);
            ins.Parameters.AddWithValue("tcr", totalCredit);
            ins.Parameters.AddWithValue("tv", totalVoided);
            ins.Parameters.AddWithValue("sc", saleCount);
            ins.Parameters.AddWithValue("cc", creditCollected);
            ins.Parameters.AddWithValue("ch", cashOnHand);
            ins.Parameters.AddWithValue("d", diff);
            ins.Parameters.AddWithValue("ex", req.Expenses ?? 0);
            ins.Parameters.AddWithValue("cn", req.CashierName ?? "");
            ins.Parameters.AddWithValue("d1000", req.Denom1000);
            ins.Parameters.AddWithValue("d500", req.Denom500);
            ins.Parameters.AddWithValue("d200", req.Denom200);
            ins.Parameters.AddWithValue("d100", req.Denom100);
            ins.Parameters.AddWithValue("d50", req.Denom50);
            ins.Parameters.AddWithValue("d20", req.Denom20);
            ins.Parameters.AddWithValue("dcoins", req.DenomCoins);
            var dcId = Convert.ToInt32(ins.ExecuteScalar());

            tx.Commit();
            return Ok(new { id = dcId, totalSales, totalCash, totalEw, totalCredit, totalVoided, saleCount, creditCollected, expectedCash, cashOnHand, difference = diff, expenses = req.Expenses ?? 0, denom1000 = req.Denom1000, denom500 = req.Denom500, denom200 = req.Denom200, denom100 = req.Denom100, denom50 = req.Denom50, denom20 = req.Denom20, denomCoins = req.DenomCoins });
        }
        catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpGet("warehouse/shifts")]
    public IActionResult WhGetShifts([FromQuery] int limit = 50)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, close_date, total_sales, total_cash, total_ewallet, total_credit, total_voided, sale_count, credit_collected, cash_on_hand, difference, expenses, cashier_name, created_at, denom1000, denom500, denom200, denom100, denom50, denom20, denom_coins FROM wh_daily_closes ORDER BY close_date DESC LIMIT @lmt";
        cmd.Parameters.AddWithValue("lmt", limit);
        var list = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new { id = r.GetInt32(0), closeDate = r.GetDateTime(1), totalSales = r.GetDecimal(2), totalCash = r.GetDecimal(3), totalEw = r.GetDecimal(4), totalCredit = r.GetDecimal(5), totalVoided = r.GetDecimal(6), saleCount = r.GetInt32(7), creditCollected = r.GetDecimal(8), cashOnHand = r.GetDecimal(9), difference = r.GetDecimal(10), expenses = r.GetDecimal(11), cashierName = r.IsDBNull(12) ? "" : r.GetString(12), createdAt = r.GetDateTime(13), denom1000 = r.GetDecimal(14), denom500 = r.GetDecimal(15), denom200 = r.GetDecimal(16), denom100 = r.GetDecimal(17), denom50 = r.GetDecimal(18), denom20 = r.GetDecimal(19), denomCoins = r.GetDecimal(20) });
        return Ok(list);
    }

    [HttpGet("shop/catalog")]
    public IActionResult ShopCatalog([FromQuery] string storeId = "STORE-20260602-7159", [FromQuery] string? category = null, [FromQuery] bool withImages = true)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT mp.id, mp.name, mp.barcode, mp.category, mp.price, mp.cost, " +
            (withImages ? "mp.image_data," : "'' AS image_data,") + @"
                   COALESCE(p.stock_qty, 0) AS hq_stock,
                   COALESCE(w.stock_qty, 0) AS wh_stock,
                   COALESCE(w.box_qty, 0) AS wh_box_qty,
                   COALESCE(w.box_price, 0) AS wh_box_price,
                   COALESCE((SELECT json_agg(json_build_object('id', mpu.id, 'unitName', mpu.unit_name, 'price', mpu.price, 'qtyPerUnit', mpu.qty_per_unit, 'isDefault', mpu.is_default))
                             FROM master_product_units mpu WHERE mpu.product_id = mp.id), '[]'::json) AS units
            FROM master_products mp
            LEFT JOIN LATERAL (SELECT stock_qty FROM products
                               WHERE store_id = @sid AND barcode = mp.barcode AND is_active = true
                               ORDER BY pos_id LIMIT 1) p ON true
            LEFT JOIN wh_products w ON w.master_product_id = mp.id AND w.is_active = true
            WHERE mp.is_active = true AND mp.sell_online = true";
        if (!string.IsNullOrEmpty(category)) { cmd.CommandText += " AND mp.category = @cat"; cmd.Parameters.AddWithValue("cat", category); }
        cmd.CommandText += @"
            ORDER BY mp.name";
        cmd.Parameters.AddWithValue("sid", storeId);
        using var reader = cmd.ExecuteReader();
        var list = new List<object>();
        while (reader.Read())
            list.Add(new {
                id = reader.GetInt32(0),
                name = reader.GetString(1),
                barcode = reader.IsDBNull(2) ? "" : reader.GetString(2),
                category = reader.IsDBNull(3) ? "" : reader.GetString(3),
                price = reader.GetDecimal(4),
                cost = reader.GetDecimal(5),
                imageData = reader.IsDBNull(6) ? "" : reader.GetString(6),
                hqStock = reader.GetInt32(7),
                whStock = reader.GetInt32(8),
                whBoxQty = reader.GetInt32(9),
                whBoxPrice = reader.GetDecimal(10),
                units = reader.IsDBNull(11) ? new object[0] : JsonSerializer.Deserialize<object[]>(reader.GetString(11)) ?? new object[0]
            });
        return Ok(list);
    }

    [HttpGet("shop/product/{id}")]
    public IActionResult ShopProduct(int id, [FromQuery] string storeId = "STORE-20260602-7159")
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT mp.id, mp.name, mp.barcode, mp.category, mp.price, mp.cost, mp.image_data,
                   COALESCE(p.stock_qty, 0) AS hq_stock,
                   COALESCE(w.stock_qty, 0) AS wh_stock,
                   COALESCE(w.box_qty, 0) AS wh_box_qty,
                   COALESCE(w.box_price, 0) AS wh_box_price,
                   COALESCE((SELECT json_agg(json_build_object('id', mpu.id, 'unitName', mpu.unit_name, 'price', mpu.price, 'qtyPerUnit', mpu.qty_per_unit, 'isDefault', mpu.is_default))
                             FROM master_product_units mpu WHERE mpu.product_id = mp.id), '[]'::json) AS units
            FROM master_products mp
            LEFT JOIN LATERAL (SELECT stock_qty FROM products
                               WHERE store_id = @sid AND barcode = mp.barcode AND is_active = true
                               ORDER BY pos_id LIMIT 1) p ON true
            LEFT JOIN wh_products w ON w.master_product_id = mp.id AND w.is_active = true
            WHERE mp.id = @id AND mp.is_active = true AND mp.sell_online = true";
        cmd.Parameters.AddWithValue("sid", storeId);
        cmd.Parameters.AddWithValue("id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return NotFound(new { message = "Product not found" });
        var p = new {
            id = reader.GetInt32(0),
            name = reader.GetString(1),
            barcode = reader.IsDBNull(2) ? "" : reader.GetString(2),
            category = reader.IsDBNull(3) ? "" : reader.GetString(3),
            price = reader.GetDecimal(4),
            cost = reader.GetDecimal(5),
            imageData = reader.IsDBNull(6) ? "" : reader.GetString(6),
            hqStock = reader.GetInt32(7),
            whStock = reader.GetInt32(8),
            whBoxQty = reader.GetInt32(9),
            whBoxPrice = reader.GetDecimal(10),
            units = reader.IsDBNull(11) ? new object[0] : JsonSerializer.Deserialize<object[]>(reader.GetString(11)) ?? new object[0]
        };
        return Ok(p);
    }

    [HttpGet("shop/catalog/search")]
    public IActionResult ShopCatalogSearch([FromQuery] string? q = null, [FromQuery] int limit = 30)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, name, barcode, category, price
            FROM master_products
            WHERE is_active = true AND sell_online = true
              AND (@q IS NULL OR @q = '' OR name ILIKE '%' || @q || '%' OR barcode ILIKE '%' || @q || '%')
            ORDER BY name LIMIT @lmt";
        cmd.Parameters.AddWithValue("q", (object?)q ?? DBNull.Value);
        cmd.Parameters.AddWithValue("lmt", limit);
        using var reader = cmd.ExecuteReader();
        var list = new List<object>();
        while (reader.Read())
            list.Add(new {
                id = reader.GetInt32(0),
                name = reader.GetString(1),
                barcode = reader.IsDBNull(2) ? "" : reader.GetString(2),
                category = reader.IsDBNull(3) ? "" : reader.GetString(3),
                price = reader.GetDecimal(4)
            });
        return Ok(list);
    }

    [HttpGet("shop/categories")]
    public IActionResult ShopCategories()
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT category FROM master_products WHERE is_active = true AND sell_online = true AND category <> '' ORDER BY category";
        var list = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(reader.GetString(0));
        return Ok(new { categories = list });
    }

    [HttpPost("shop/orders")]
    public IActionResult ShopCreateOrder([FromBody] ShopOrderRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.CustomerName) || req.Items == null || req.Items.Count == 0)
            return BadRequest(new { message = "Customer name and at least one item are required" });
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            decimal total = 0;
            foreach (var it in req.Items) total += it.Total;
            var deliveryFee = req.DeliveryFee ?? 0;
            if (deliveryFee < 0) return BadRequest(new { message = "Delivery fee cannot be negative" });
            total += deliveryFee;

            int orderId;
            using (var insCmd = conn.CreateCommand())
            {
                insCmd.Transaction = tx;
                insCmd.CommandText = @"
                    INSERT INTO online_orders (order_no, customer_name, phone, address, payment_method, gcash_ref, delivery_note, status, total, delivery_fee)
                    VALUES ('', @cn, @ph, @ad, @pm, @gr, @dn, 'pending', @tot, @df) RETURNING id";
                insCmd.Parameters.AddWithValue("cn", req.CustomerName.Trim());
                insCmd.Parameters.AddWithValue("ph", req.Phone ?? "");
                insCmd.Parameters.AddWithValue("ad", req.Address ?? "");
                insCmd.Parameters.AddWithValue("pm", (req.PaymentMethod ?? "COD").ToUpperInvariant() == "GCASH" ? "GCash" : "COD");
                insCmd.Parameters.AddWithValue("gr", req.GcashRef ?? "");
                insCmd.Parameters.AddWithValue("dn", req.DeliveryNote ?? "");
                insCmd.Parameters.AddWithValue("tot", total);
                insCmd.Parameters.AddWithValue("df", deliveryFee);
                orderId = Convert.ToInt32(insCmd.ExecuteScalar());
            }

            var no = $"SHOP-{DateTime.Now:yyyyMMdd}-{orderId:D4}";
            using (var upCmd = conn.CreateCommand())
            {
                upCmd.Transaction = tx;
                upCmd.CommandText = "UPDATE online_orders SET order_no = @no WHERE id = @id";
                upCmd.Parameters.AddWithValue("no", no);
                upCmd.Parameters.AddWithValue("id", orderId);
                upCmd.ExecuteNonQuery();
            }

            foreach (var it in req.Items)
            {
                using var itemCmd = conn.CreateCommand();
                itemCmd.Transaction = tx;
                itemCmd.CommandText = @"
                    INSERT INTO online_order_items (order_id, product_id, product_name, unit_name, qty, price, total)
                    VALUES (@oid, @pid, @pn, @un, @q, @pr, @tot)";
                itemCmd.Parameters.AddWithValue("oid", orderId);
                itemCmd.Parameters.AddWithValue("pid", it.ProductId);
                itemCmd.Parameters.AddWithValue("pn", it.ProductName ?? "");
                itemCmd.Parameters.AddWithValue("un", it.UnitName ?? "PC");
                itemCmd.Parameters.AddWithValue("q", it.Qty);
                itemCmd.Parameters.AddWithValue("pr", it.Price);
                itemCmd.Parameters.AddWithValue("tot", it.Total);
                itemCmd.ExecuteNonQuery();
            }

            tx.Commit();
            return Ok(new { id = orderId, orderNo = no, status = "pending", total });
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("shop/orders")]
    public IActionResult ShopGetOrders([FromQuery] string? phone = null, [FromQuery] string? status = null, [FromQuery] int limit = 50)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, order_no, customer_name, phone, address, payment_method, gcash_ref, delivery_note, status, total, delivery_fee, created_at
            FROM online_orders
            WHERE 1 = 1";
        if (!string.IsNullOrEmpty(phone)) { cmd.CommandText += " AND phone = @ph"; cmd.Parameters.AddWithValue("ph", phone); }
        if (!string.IsNullOrEmpty(status)) { cmd.CommandText += " AND status = @st"; cmd.Parameters.AddWithValue("st", status); }
        cmd.CommandText += " ORDER BY id DESC LIMIT @lmt";
        cmd.Parameters.AddWithValue("lmt", limit);
        using var reader = cmd.ExecuteReader();
        var list = new List<object>();
        while (reader.Read())
            list.Add(new {
                id = reader.GetInt32(0),
                orderNo = reader.GetString(1),
                customerName = reader.GetString(2),
                phone = reader.GetString(3),
                address = reader.IsDBNull(4) ? "" : reader.GetString(4),
                paymentMethod = reader.GetString(5),
                gcashRef = reader.IsDBNull(6) ? "" : reader.GetString(6),
                deliveryNote = reader.IsDBNull(7) ? "" : reader.GetString(7),
                status = reader.GetString(8),
                total = reader.GetDecimal(9),
                deliveryFee = reader.GetDecimal(10),
                createdAt = reader.GetDateTime(11)
            });
        return Ok(list);
    }

    [HttpGet("shop/orders/{id}")]
    public IActionResult ShopGetOrder(int id)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, order_no, customer_name, phone, address, payment_method, gcash_ref, delivery_note, status, total, delivery_fee, created_at FROM online_orders WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return NotFound(new { message = "Order not found" });
        var order = new {
            id = reader.GetInt32(0),
            orderNo = reader.GetString(1),
            customerName = reader.GetString(2),
            phone = reader.GetString(3),
            address = reader.IsDBNull(4) ? "" : reader.GetString(4),
            paymentMethod = reader.GetString(5),
            gcashRef = reader.IsDBNull(6) ? "" : reader.GetString(6),
            deliveryNote = reader.IsDBNull(7) ? "" : reader.GetString(7),
            status = reader.GetString(8),
            total = reader.GetDecimal(9),
            deliveryFee = reader.GetDecimal(10),
            createdAt = reader.GetDateTime(11)
        };
        reader.Close();

        using var itemsCmd = conn.CreateCommand();
        itemsCmd.CommandText = "SELECT id, product_id, product_name, unit_name, qty, price, total FROM online_order_items WHERE order_id = @oid";
        itemsCmd.Parameters.AddWithValue("oid", id);
        var items = new List<object>();
        using var itemReader = itemsCmd.ExecuteReader();
        while (itemReader.Read())
            items.Add(new {
                id = itemReader.GetInt32(0),
                productId = itemReader.GetInt32(1),
                productName = itemReader.GetString(2),
                unitName = itemReader.GetString(3),
                qty = itemReader.GetInt32(4),
                price = itemReader.GetDecimal(5),
                total = itemReader.GetDecimal(6)
            });
        return Ok(new { order, items });
    }

    [HttpPut("shop/orders/{id}/status")]
    public IActionResult ShopUpdateOrderStatus(int id, [FromBody] ShopStatusRequest req)
    {
        var allowed = new[] { "pending", "confirmed", "shipped", "delivered", "cancelled" };
        var status = (req?.Status ?? "").ToLowerInvariant();
        if (!allowed.Contains(status)) return BadRequest(new { message = "Invalid status" });
        const string hqStore = "STORE-20260602-7159";
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            using var curCmd = conn.CreateCommand();
            curCmd.Transaction = tx;
            curCmd.CommandText = "SELECT status FROM online_orders WHERE id = @id";
            curCmd.Parameters.AddWithValue("id", id);
            var curStatus = curCmd.ExecuteScalar() as string;
            if (curStatus == null) return NotFound(new { message = "Order not found" });
            if (curStatus == status) return Ok(new { id, status });

            if (status == "confirmed" && curStatus != "pending")
                return BadRequest(new { message = "Only pending orders can be confirmed" });
            if (status == "cancelled" && curStatus == "delivered")
                return BadRequest(new { message = "Delivered orders cannot be cancelled" });

            var pcsChange = 0; // +reserve, -restore
            if (status == "confirmed") pcsChange = 1;
            else if (status == "cancelled" && curStatus != "pending") pcsChange = -1;

            if (pcsChange != 0)
            {
                using var itemsCmd = conn.CreateCommand();
                itemsCmd.Transaction = tx;
                itemsCmd.CommandText = "SELECT product_id, unit_name, qty FROM online_order_items WHERE order_id = @oid";
                itemsCmd.Parameters.AddWithValue("oid", id);
                using var itemReader = itemsCmd.ExecuteReader();
                var shortages = new List<string>();
                var itemRows = new List<(int pid, string unit, int qty)>();
                while (itemReader.Read())
                    itemRows.Add((itemReader.GetInt32(0), itemReader.GetString(1), itemReader.GetInt32(2)));
                itemReader.Close();

                foreach (var (pid, unit, qty) in itemRows)
                {
                    using var prodCmd = conn.CreateCommand();
                    prodCmd.Transaction = tx;
                    prodCmd.CommandText = "SELECT barcode FROM master_products WHERE id = @pid";
                    prodCmd.Parameters.AddWithValue("pid", pid);
                    var barcode = prodCmd.ExecuteScalar() as string;
                    if (string.IsNullOrEmpty(barcode)) continue;

                    int qtyPerUnit = 1;
                    using var unitCmd = conn.CreateCommand();
                    unitCmd.Transaction = tx;
                    unitCmd.CommandText = "SELECT qty_per_unit FROM master_product_units WHERE product_id = @pid AND unit_name = @un ORDER BY is_default DESC LIMIT 1";
                    unitCmd.Parameters.AddWithValue("pid", pid);
                    unitCmd.Parameters.AddWithValue("un", unit);
                    var qpu = unitCmd.ExecuteScalar();
                    if (qpu != null && Convert.ToInt32(qpu) > 0) qtyPerUnit = Convert.ToInt32(qpu);
                    var pcs = qty * qtyPerUnit;

                    if (pcsChange > 0)
                    {
                        using var updCmd = conn.CreateCommand();
                        updCmd.Transaction = tx;
                        updCmd.CommandText = @"
                            UPDATE products SET stock_qty = stock_qty - @pcs
                            WHERE id = (SELECT id FROM products
                                WHERE store_id = @sid AND barcode = @b AND is_active = true AND stock_qty >= @pcs
                                ORDER BY pos_id LIMIT 1)";
                        updCmd.Parameters.AddWithValue("pcs", pcs);
                        updCmd.Parameters.AddWithValue("sid", hqStore);
                        updCmd.Parameters.AddWithValue("b", barcode);
                        if (updCmd.ExecuteNonQuery() == 0)
                            shortages.Add($"{unit} x{qty} ({barcode})");
                    }
                    else
                    {
                        using var updCmd = conn.CreateCommand();
                        updCmd.Transaction = tx;
                        updCmd.CommandText = @"
                            UPDATE products SET stock_qty = stock_qty + @pcs
                            WHERE id = (SELECT id FROM products
                                WHERE store_id = @sid AND barcode = @b AND is_active = true
                                ORDER BY pos_id LIMIT 1)";
                        updCmd.Parameters.AddWithValue("pcs", pcs);
                        updCmd.Parameters.AddWithValue("sid", hqStore);
                        updCmd.Parameters.AddWithValue("b", barcode);
                        updCmd.ExecuteNonQuery();
                    }
                }
                if (shortages.Count > 0)
                    return BadRequest(new { message = "Insufficient HQ stock: " + string.Join(", ", shortages) });
            }

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE online_orders SET status = @st, updated_at = NOW() WHERE id = @id";
            cmd.Parameters.AddWithValue("st", status);
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
            tx.Commit();
            return Ok(new { id, status });
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("shop/orders/new-count")]
    public IActionResult ShopNewOrderCount([FromQuery] string? since = null)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM online_orders WHERE status = 'pending'";
        if (!string.IsNullOrEmpty(since) && DateTime.TryParse(since, out var sinceDt))
        {
            cmd.CommandText += " AND created_at > @since";
            cmd.Parameters.AddWithValue("since", sinceDt);
        }
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        return Ok(new { pending = count });
    }

    [HttpGet("shop/settings")]
    public IActionResult ShopGetSettings()
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT delivery_fee, free_delivery_min FROM shop_settings WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Ok(new { deliveryFee = 0m, freeDeliveryMin = 0m });
        return Ok(new { deliveryFee = reader.GetDecimal(0), freeDeliveryMin = reader.GetDecimal(1) });
    }

    [HttpPost("shop/settings")]
    public IActionResult ShopSaveSettings([FromBody] ShopSettingsRequest req)
    {
        if (req == null) return BadRequest(new { message = "Body is required" });
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO shop_settings (id, delivery_fee, free_delivery_min, updated_at)
            VALUES (1, @df, @fd, NOW())
            ON CONFLICT (id) DO UPDATE SET delivery_fee = @df, free_delivery_min = @fd, updated_at = NOW()";
        cmd.Parameters.AddWithValue("df", Math.Max(0, req.DeliveryFee ?? 0));
        cmd.Parameters.AddWithValue("fd", Math.Max(0, req.FreeDeliveryMin ?? 0));
        cmd.ExecuteNonQuery();
        return Ok(new { success = true });
    }

    [HttpPost("shop/notify")]
    public IActionResult ShopNotify([FromBody] ShopNotifyRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { message = "Message is required" });
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO shop_notifications (store_id, message) VALUES (@sid, @msg)";
        cmd.Parameters.AddWithValue("sid", req.StoreId ?? "");
        cmd.Parameters.AddWithValue("msg", req.Message.Trim());
        cmd.ExecuteNonQuery();
        return Ok(new { success = true });
    }

    [HttpGet("shop/notifications/{storeId}")]
    public IActionResult ShopGetNotifications(string storeId, [FromQuery] string? since = null)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, message, created_at FROM shop_notifications
            WHERE (store_id = @sid OR store_id = '')";
        cmd.Parameters.AddWithValue("sid", storeId);
        if (!string.IsNullOrEmpty(since) && DateTime.TryParse(since, out var sinceDt))
        {
            cmd.CommandText += " AND created_at > @since";
            cmd.Parameters.AddWithValue("since", sinceDt);
        }
        cmd.CommandText += " ORDER BY id DESC LIMIT 20";
        var list = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new { id = reader.GetInt32(0), message = reader.GetString(1), createdAt = reader.GetDateTime(2) });
        return Ok(list);
    }

    [HttpGet("warehouse/transfers/pending-count")]
    public IActionResult WhGetPendingTransferCount()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM wh_transfers WHERE status = 'pending'";
            var count = Convert.ToInt32(cmd.ExecuteScalar());
            return Ok(new { pending = count });
        }

        [HttpGet("missing-shifts")]
        public IActionResult GetMissingShifts()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                WITH store_list AS (
                    SELECT DISTINCT s.store_id, COALESCE(st.store_name, '') AS store_name
                    FROM sales s
                    LEFT JOIN stores st ON s.store_id = st.store_id
                    WHERE s.store_id != ''
                ),
                today_closes AS (
                    SELECT DISTINCT store_id FROM daily_closes WHERE close_date::date = CURRENT_DATE
                ),
                today_sales AS (
                    SELECT store_id, COUNT(*) AS sale_count FROM sales
                    WHERE is_voided = false AND sale_date::date = CURRENT_DATE AND store_id != ''
                    GROUP BY store_id
                )
                SELECT sl.store_id, sl.store_name,
                       COALESCE(ts.sale_count, 0) AS today_sale_count,
                       CASE WHEN tc.store_id IS NOT NULL THEN true ELSE false END AS has_close
                FROM store_list sl
                LEFT JOIN today_closes tc ON sl.store_id = tc.store_id
                LEFT JOIN today_sales ts ON sl.store_id = ts.store_id
                ORDER BY sl.store_id";
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var storeId = reader.GetString(0);
                var storeName = reader.GetString(1);
                var saleCount = reader.GetInt32(2);
                var hasClose = reader.GetBoolean(3);
                data.Add(new {
                    storeId,
                    storeName,
                    todaySaleCount = saleCount,
                    hasClose,
                    missing = !hasClose
                });
            }
            return Ok(data);
        }

    [HttpGet("pos-promo")]
    public IActionResult GetPosPromo()
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = new NpgsqlCommand("SELECT message FROM pos_promo WHERE id = 1", conn);
        var msg = cmd.ExecuteScalar()?.ToString() ?? "";
        return Ok(new { message = msg });
    }

    [HttpPost("pos-promo")]
    public IActionResult SetPosPromo([FromBody] PosPromoRequest req)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = new NpgsqlCommand("INSERT INTO pos_promo (id, message, updated_at) VALUES (1, @m, NOW()) ON CONFLICT (id) DO UPDATE SET message = @m, updated_at = NOW()", conn);
        cmd.Parameters.AddWithValue("m", req.Message ?? "");
        cmd.ExecuteNonQuery();
        return Ok(new { ok = true });
    }

    [HttpGet("branding")]
    public IActionResult GetBranding()
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = new NpgsqlCommand("SELECT app_title, logo_url, splash_bg, login_bg, primary_color, icon_key FROM branding WHERE id = 1", conn);
        using var rdr = cmd.ExecuteReader();
        if (!rdr.Read()) return Ok(new BrandingConfig());
        return Ok(new BrandingConfig
        {
            AppTitle = rdr.GetString(0),
            LogoUrl = rdr.GetString(1),
            SplashBg = rdr.GetString(2),
            LoginBg = rdr.GetString(3),
            PrimaryColor = rdr.GetString(4),
            IconKey = rdr.GetString(5)
        });
    }

    [HttpPost("branding")]
    public IActionResult SetBranding([FromBody] BrandingConfig req)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO branding (id, app_title, logo_url, splash_bg, login_bg, primary_color, icon_key, updated_at)
            VALUES (1, @t, @l, @s, @lb, @p, @ik, NOW())
            ON CONFLICT (id) DO UPDATE SET
                app_title = @t, logo_url = @l, splash_bg = @s, login_bg = @lb,
                primary_color = @p, icon_key = @ik, updated_at = NOW()", conn);
        cmd.Parameters.AddWithValue("t", req.AppTitle ?? "");
        cmd.Parameters.AddWithValue("l", req.LogoUrl ?? "");
        cmd.Parameters.AddWithValue("s", req.SplashBg ?? "");
        cmd.Parameters.AddWithValue("lb", req.LoginBg ?? "");
        cmd.Parameters.AddWithValue("p", req.PrimaryColor ?? "");
        cmd.Parameters.AddWithValue("ik", req.IconKey ?? "");
        cmd.ExecuteNonQuery();
        return Ok(new { ok = true });
    }

    [HttpPost("branding/logo")]
    public async Task<IActionResult> UploadBrandingLogo(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".png", ".jpg", ".jpeg", ".webp", ".svg" }.Contains(ext)) return BadRequest("Invalid image type");
        var fileName = "brand_logo" + ext;
        var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);
        return Ok(new { url = "/assets/" + fileName, fullUrl = "https://admin.jumongdev.com/assets/" + fileName });
    }

    // ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ Agent (remote diagnostic) ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬

    private static readonly ConcurrentDictionary<string, Queue<AgentCommand>> _cmdQueues = new();
    private static readonly ConcurrentDictionary<string, List<AgentResult>> _results = new();
    private static readonly ConcurrentDictionary<string, (DateTime lastSeen, string ip, string machine, string appVersion, bool hasError, string errorSummary)> _agents = new();
    private static readonly ConcurrentDictionary<string, (DateTime updatedAt, Dictionary<string, int> pending)> _posStatus = new();
    private static int _cmdCounter = 0;

        [HttpPost("agent/heartbeat")]
        public IActionResult AgentHeartbeat([FromBody] AgentHeartbeat hb)
        {
            if (string.IsNullOrEmpty(hb.StoreId)) return BadRequest();
            var ver = !string.IsNullOrEmpty(hb.AppVersion) ? hb.AppVersion : hb.Version;
            _agents[hb.StoreId] = (DateTime.UtcNow, hb.LocalIp ?? "", hb.MachineName ?? "", ver ?? "", hb.HasError, hb.ErrorSummary ?? "");
            return Ok(new { ok = true });
        }

        [HttpGet("agent/status")]
        public IActionResult AgentStatus()
        {
            var latestVer = "1.1.42";
            var list = _agents.Select(a => new { storeId = a.Key, lastSeen = a.Value.lastSeen, ip = a.Value.ip, machine = a.Value.machine, appVersion = a.Value.appVersion, outdated = string.Compare(latestVer, a.Value.appVersion ?? "", StringComparison.Ordinal) > 0, hasError = a.Value.hasError, errorSummary = a.Value.errorSummary }).OrderBy(a => a.storeId);
            return Ok(list);
        }

    [HttpPost("agent/send/{storeId}")]
    public IActionResult AgentSendCommand(string storeId, [FromBody] AgentCommand cmd)
    {
        var queue = _cmdQueues.GetOrAdd(storeId, _ => new Queue<AgentCommand>());
        cmd.Id = Interlocked.Increment(ref _cmdCounter);
        queue.Enqueue(cmd);
        return Ok(new { commandId = cmd.Id });
    }

    [HttpGet("agent/poll/{storeId}")]
    public IActionResult AgentPoll(string storeId)
    {
        if (!_cmdQueues.TryGetValue(storeId, out var queue) || queue.Count == 0) return Ok("{}");
        if (queue.TryDequeue(out var cmd)) return Ok(cmd);
        return Ok("{}");
    }

    [HttpPost("agent/result")]
    public IActionResult AgentResult([FromBody] AgentResult ar)
    {
        var list = _results.GetOrAdd(ar.StoreId, _ => new List<AgentResult>());
        list.Add(ar);
        if (list.Count > 50) list.RemoveAt(0);
        return Ok(new { ok = true });
    }

        [HttpGet("agent/results/{storeId}")]
        public IActionResult AgentResults(string storeId)
        {
            return Ok(_results.TryGetValue(storeId, out var list) ? list : new List<AgentResult>());
        }

        [HttpPost("agent/upload-file")]
        public async Task<IActionResult> AgentUploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file");
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", file.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);
            return Ok(new { url = "/assets/" + file.FileName, fullUrl = "https://admin.jumongdev.com/assets/" + file.FileName });
        }

        [HttpPost("pos-status")]
        public IActionResult PosStatus([FromBody] PosStatusRequest req)
        {
            if (string.IsNullOrEmpty(req.StoreId)) return BadRequest("StoreId required");
            _posStatus[req.StoreId] = (DateTime.UtcNow, req.Pending ?? new Dictionary<string, int>());
            return Ok(new { ok = true });
        }

        [HttpGet("pos-status")]
        public IActionResult PosStatusAll()
        {
            var list = _posStatus.Select(kv => new { storeId = kv.Key, pending = kv.Value.pending, updatedAt = kv.Value.updatedAt }).OrderBy(x => x.storeId);
            return Ok(list);
        }

        [HttpPost("suspect-1pc")]
        public IActionResult PushSuspect1Pc([FromBody] Suspect1PcRequest req)
        {
            if (string.IsNullOrEmpty(req.InvoiceNo)) return BadRequest("InvoiceNo required");
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO suspect_1pc_sales (store_id, store_name, invoice_no, sale_date, cashier, items_json)
                VALUES (@sid, @sn, @inv, CAST(@dt AS TIMESTAMPTZ), @csh, @items::json)
                RETURNING id";
            cmd.Parameters.AddWithValue("sid", req.StoreId ?? "");
            cmd.Parameters.AddWithValue("sn", req.StoreName ?? "");
            cmd.Parameters.AddWithValue("inv", req.InvoiceNo);
            cmd.Parameters.AddWithValue("dt", string.IsNullOrEmpty(req.SaleDate) ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(req.SaleDate));
            cmd.Parameters.AddWithValue("csh", req.Cashier ?? "");
            cmd.Parameters.AddWithValue("items", NpgsqlTypes.NpgsqlDbType.Jsonb, System.Text.Json.JsonSerializer.Serialize(req.Items ?? new List<Suspect1PcItem>()));
            var id = cmd.ExecuteScalar();
            return Ok(new { id });
        }

        [HttpGet("suspect-1pc")]
        public IActionResult GetSuspect1Pc([FromQuery] string? store, [FromQuery] string? status)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var where = new List<string>();
            if (!string.IsNullOrEmpty(store)) { where.Add("store_id = @store"); cmd.Parameters.AddWithValue("store", store); }
            if (!string.IsNullOrEmpty(status)) { where.Add("status = @status"); cmd.Parameters.AddWithValue("status", status); }
            var filter = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            cmd.CommandText = $"SELECT id, store_id, store_name, invoice_no, sale_date, cashier, items_json, status, checker, notes, created_at FROM suspect_1pc_sales {filter} ORDER BY created_at DESC LIMIT 200";
            using var rdr = cmd.ExecuteReader();
            var list = new List<object>();
            while (rdr.Read())
            {
                list.Add(new
                {
                    Id = rdr.GetInt32(0),
                    StoreId = rdr.GetString(1),
                    StoreName = rdr.GetString(2),
                    InvoiceNo = rdr.GetString(3),
                    SaleDate = rdr.GetDateTime(4),
                    Cashier = rdr.GetString(5),
                    Items = rdr.GetString(6),
                    Status = rdr.GetString(7),
                    Checker = rdr.GetString(8),
                    Notes = rdr.GetString(9),
                    CreatedAt = rdr.GetDateTime(10)
                });
            }
            return Ok(list);
        }

        [HttpPut("suspect-1pc/{id}/assign")]
        public IActionResult AssignSuspect1Pc(int id, [FromBody] Suspect1PcAssignRequest req)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE suspect_1pc_sales SET checker = @c, status = 'checking' WHERE id = @id";
            cmd.Parameters.AddWithValue("c", req.Checker ?? "");
            cmd.Parameters.AddWithValue("id", id);
            var rows = cmd.ExecuteNonQuery();
            return rows > 0 ? Ok(new { ok = true }) : NotFound();
        }

        [HttpPut("suspect-1pc/{id}/resolve")]
        public IActionResult ResolveSuspect1Pc(int id, [FromBody] Suspect1PcResolveRequest req)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE suspect_1pc_sales SET status = 'resolved', notes = @n WHERE id = @id";
            cmd.Parameters.AddWithValue("n", req.Notes ?? "");
            cmd.Parameters.AddWithValue("id", id);
            var rows = cmd.ExecuteNonQuery();
            return rows > 0 ? Ok(new { ok = true }) : NotFound();
        }
    }

    public class PosPromoRequest { public string Message { get; set; } = ""; }
    public class CrashReportRequest { public string App { get; set; } = ""; public string? Version { get; set; } public string? Device { get; set; } public string Type { get; set; } = "crash"; public string? Log { get; set; } }

    public class ShopOrderRequest
    {
        public string CustomerName { get; set; } = "";
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? PaymentMethod { get; set; }
        public string? GcashRef { get; set; }
        public string? DeliveryNote { get; set; }
        public decimal? DeliveryFee { get; set; }
        public List<ShopOrderItemRequest> Items { get; set; } = new();
    }

    public class ShopOrderItemRequest
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? UnitName { get; set; }
        public int Qty { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
    }

    public class ShopStatusRequest { public string? Status { get; set; } }
    public class ShopSettingsRequest { public decimal? DeliveryFee { get; set; } public decimal? FreeDeliveryMin { get; set; } }

    public class ShopNotifyRequest { public string? StoreId { get; set; } public string? Message { get; set; } }
    public class ChatRequest { public string Message { get; set; } = ""; public List<ChatMessage>? History { get; set; } }
    public class ChatMessage { public string Role { get; set; } = "user"; public string Content { get; set; } = ""; }
    public class ChatLogEntry { public DateTime At { get; set; } public long Ms { get; set; } public bool Ok { get; set; } public int ReplyLen { get; set; } public string Err { get; set; } = ""; public string Backend { get; set; } = ""; }
    public class BrandingConfig { public string AppTitle { get; set; } = ""; public string LogoUrl { get; set; } = ""; public string SplashBg { get; set; } = ""; public string LoginBg { get; set; } = ""; public string PrimaryColor { get; set; } = ""; public string IconKey { get; set; } = ""; }
    public class Suspect1PcRequest { public string? StoreId { get; set; } public string? StoreName { get; set; } public string InvoiceNo { get; set; } = ""; public string? Cashier { get; set; } public string? SaleDate { get; set; } public List<Suspect1PcItem>? Items { get; set; } }
    public class Suspect1PcItem { public string ProductName { get; set; } = ""; public string UnitName { get; set; } = ""; public decimal Price { get; set; } public int Quantity { get; set; } }
    public class Suspect1PcAssignRequest { public string Checker { get; set; } = ""; }
    public class Suspect1PcResolveRequest { public string Notes { get; set; } = ""; }
    }

    public class WhProductDto { public string Name { get; set; } = ""; public string? Barcode { get; set; } public string? Category { get; set; } public decimal BoxPrice { get; set; } public decimal BoxCost { get; set; } public int BoxQty { get; set; } = 1; public decimal PiecePrice { get; set; } }
    public class WhStockDto { public int StockQty { get; set; } }
    public class WhStockMoveDto { public int QtyChange { get; set; } public string Reason { get; set; } = ""; public string? Source { get; set; } }

    public class WhReceivingDto { public string Source { get; set; } = ""; public string? Source2 { get; set; } public List<WhReceivingItemDto> Items { get; set; } = new(); }
    public class WhReceivingItemDto { public int ProductId { get; set; } public int Qty { get; set; } }
    public class WhClientDto { public string Name { get; set; } = ""; public string? Contact { get; set; } public string? Address { get; set; } public string? StoreType { get; set; } public string? StoreId { get; set; } }
    public class WhOrderDto { public int ClientId { get; set; } public string? ClientName { get; set; } public string? Notes { get; set; } public List<WhOrderItemDto>? Items { get; set; } }
    public class WhOrderItemDto { public int ProductId { get; set; } public string ProductName { get; set; } = ""; public string? UnitType { get; set; } public int Qty { get; set; } public decimal Price { get; set; } public decimal TotalPrice { get; set; } public int BaseQty { get; set; } public string? BaseUnitName { get; set; } public int BoxQtyPerUnit { get; set; } = 1; }
    public class WhStatusDto { public string Status { get; set; } = ""; }
    public class WhReceiveRequest
    {
        public List<WhReceivedItemDto>? Items { get; set; }
    }
    public class WhReceivedItemDto
    {
        public int ProductId { get; set; }
        public int BaseQty { get; set; }
        public string ProductName { get; set; } = "";
        public string? Barcode { get; set; }
    }

    public class WhTransferDto { public int ClientId { get; set; } public string? ClientName { get; set; } public string? Notes { get; set; } public string? StoreId { get; set; } public List<WhTransferItemDto>? Items { get; set; } }
    public class WhTransferItemDto { public int ProductId { get; set; } public string ProductName { get; set; } = ""; public string? Barcode { get; set; } public int Qty { get; set; } }
    public class WhTransferReceiveRequest { public List<WhTransferReceivedItemDto>? Items { get; set; } }
    public class WhTransferReceivedItemDto { public int ProductId { get; set; } public string ProductName { get; set; } = ""; }

    public class WhStockSnapshotRequest
    {
        public string? StoreId { get; set; }
        public List<WhStockSnapshotItem>? Items { get; set; }
    }
    public class WhStockSnapshotItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int CurrentStock { get; set; }
        public string Barcode { get; set; } = "";
    }

    public class WhWalkinSellRequest
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string PaymentMethod { get; set; } = "Cash";
        public decimal CashReceived { get; set; }
        public string? Source { get; set; }
        public List<WhWalkinSellItem> Items { get; set; } = new();
    }
    public class WhVoidRequest { public string Reason { get; set; } = ""; public string? UserName { get; set; } public List<WhVoidItemDto>? Items { get; set; } }
    public class WhVoidItemDto { public int ItemId { get; set; } }
    public class WhEndShiftRequest
    {
        public bool Preview { get; set; }
        public decimal? Expenses { get; set; }
        public string? CashierName { get; set; }
        public decimal Denom1000 { get; set; }
        public decimal Denom500 { get; set; }
        public decimal Denom200 { get; set; }
        public decimal Denom100 { get; set; }
        public decimal Denom50 { get; set; }
        public decimal Denom20 { get; set; }
        public decimal DenomCoins { get; set; }
    }
    public class WhCreditPayRequest { public int CustomerId { get; set; } public decimal Amount { get; set; } public string? Method { get; set; } public string? CashierName { get; set; } public string? InvoiceNo { get; set; } }
    public class ReceiptAuditRequest { public string? StoreId { get; set; } public string? StoreName { get; set; } public string? ShiftDate { get; set; } public int TotalReceipts { get; set; } public int VoidedCount { get; set; } public int DeletedCount { get; set; } public decimal LostValue { get; set; } public List<string>? VoidedInvoices { get; set; } public List<string>? MissingInvoices { get; set; } }
    public class WhWalkinSellItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int UnitIndex { get; set; }
        public int Qty { get; set; }
    }

    public class SeedProductDto
    {
        public string Name { get; set; } = "";
        public string? Barcode { get; set; }
        public string? Category { get; set; }
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public int StockQty { get; set; }
        public string? ImageData { get; set; }
        public bool PointsExempt { get; set; }
        public int PointsPerUnit { get; set; }
        public bool IsActive { get; set; } = true;
        public bool SellOnline { get; set; } = true;
        public List<SeedProductUnitDto>? Units { get; set; }
    }

    public class SeedProductUnitDto
    {
        public string UnitName { get; set; } = "Piece";
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public int QtyPerUnit { get; set; } = 1;
        public bool IsDefault { get; set; }
        public int PointsPerUnit { get; set; }
    }

    public class MasterProductFlagsDto
    {
        public bool? SellOnline { get; set; }
        public bool? IsActive { get; set; }
        public bool? PointsExempt { get; set; }
    }

    public class RenameStoreRequest
    {
        public string StoreId { get; set; } = "";
        public string StoreName { get; set; } = "";
    }

    public class AgentHeartbeat
    {
        public string StoreId { get; set; } = "";
        public string AppVersion { get; set; } = "";
        public string Version { get; set; } = "";
        public string LocalIp { get; set; } = "";
        public string MachineName { get; set; } = "";
        public bool HasError { get; set; }
        public string ErrorSummary { get; set; } = "";
    }

    public class AgentCommand
    {
        public int Id { get; set; }
        public string Type { get; set; } = "sql";
        public string Payload { get; set; } = "";
    }

    public class AgentResult
    {
        public string StoreId { get; set; } = "";
        public int CommandId { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
    }

    public class PosStatusRequest
    {
        public string StoreId { get; set; } = "";
        public Dictionary<string, int>? Pending { get; set; }
    }

