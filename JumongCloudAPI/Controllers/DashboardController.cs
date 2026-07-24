using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;

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
            cmd.CommandText = @"SELECT s.store_id, COALESCE(st.store_name, '') AS store_name
                FROM (SELECT DISTINCT store_id FROM sales WHERE store_id != '') s
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
                       COALESCE(NULLIF(s.cashier_name,''), NULLIF(u.full_name,''), NULLIF(u.username,''), 'Cashier #' || COALESCE(s.user_id::text,'')) AS cashier, s.store_id
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
                SELECT u.pos_id, u.username, u.role, u.full_name, u.is_active, u.password_hash,
                       COALESCE((SELECT json_agg(us.store_id) FROM user_stores us WHERE us.user_pos_id = u.pos_id), '[]'::json) AS store_ids
                FROM users u
                WHERE u.is_active = true {StoreFilter(storeId, "u")}
                GROUP BY u.pos_id, u.username, u.role, u.full_name, u.is_active, u.password_hash
                ORDER BY u.username LIMIT 500";
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var storeIds = r.IsDBNull(6) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.GetString(6)) ?? new();
                data.Add(new { posId = r.GetInt32(0), username = r.GetString(1), role = r.GetString(2), fullName = r.IsDBNull(3) ? "" : r.GetString(3), isActive = r.GetBoolean(4), passwordHash = r.IsDBNull(5) ? "12345" : r.GetString(5), storeIds });
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
            insCmd.CommandText = @"INSERT INTO users (pos_id, store_id, username, role, full_name, is_active, password_hash, synced_at)
                VALUES (@p, '', @u, @r, @fn, true, @ph, NOW()) RETURNING id";
            insCmd.Parameters.AddWithValue("p", newPosId);
            insCmd.Parameters.AddWithValue("u", username);
            insCmd.Parameters.AddWithValue("r", role);
            insCmd.Parameters.AddWithValue("fn", fullName);
            insCmd.Parameters.AddWithValue("ph", passwordHash);
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
            var storeIds = body.TryGetProperty("storeIds", out var si) && si.ValueKind == JsonValueKind.Array
                ? si.EnumerateArray().Select(x => x.GetString()).Where(s => !string.IsNullOrEmpty(s)).ToList()
                : new List<string?>();

            if (string.IsNullOrEmpty(username)) return BadRequest(new { error = "Username is required" });

            using var conn = Data.PgDatabaseHelper.GetConnection();

            using var cmd = conn.CreateCommand();
            var setClause = "username = @u, role = @r, full_name = @fn, is_active = @ia, synced_at = NOW()";
            if (passwordHash != null) setClause += ", password_hash = @ph";

            cmd.CommandText = $"UPDATE users SET {setClause} WHERE pos_id = @pid";
            cmd.Parameters.AddWithValue("pid", posId);
            cmd.Parameters.AddWithValue("u", username);
            cmd.Parameters.AddWithValue("r", role);
            cmd.Parameters.AddWithValue("fn", fullName);
            cmd.Parameters.AddWithValue("ia", isActive);
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
                SELECT DISTINCT u.pos_id, u.username, u.role, u.full_name, u.is_active, u.password_hash
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
                data.Add(new { posId = r.GetInt32(0), username = r.GetString(1), role = r.GetString(2), fullName = r.IsDBNull(3) ? "" : r.GetString(3), isActive = r.GetBoolean(4), passwordHash = r.IsDBNull(5) ? "12345" : r.GetString(5) });
            return Ok(data);
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
            cmd.CommandText = $@"
                SELECT name, barcode, category, stock_qty, price, cost, store_id
                FROM products p
                WHERE is_active = true {StoreFilter(storeId, "p")}
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
                SELECT COALESCE(NULLIF(s.cashier_name,''), NULLIF(u.full_name,''), NULLIF(u.username,''), 'Cashier #' || COALESCE(s.user_id::text,'Unknown')) AS cashier,
                       COUNT(*) AS total_sales,
                       COALESCE(SUM(s.grand_total),0) AS total_revenue,
                       COALESCE(AVG(s.grand_total),0) AS avg_transaction,
                       COUNT(*) FILTER (WHERE s.payment_method = 'Cash') AS cash_count,
                       COUNT(*) FILTER (WHERE s.payment_method = 'E-Wallet') AS ewallet_count,
                       COUNT(*) FILTER (WHERE s.payment_method = 'Credit') AS credit_count
                FROM sales s
                LEFT JOIN users u ON s.user_id = u.pos_id AND s.store_id = u.store_id
                WHERE s.is_voided = false {StoreFilter(storeId, "s")}{tf}
                GROUP BY COALESCE(NULLIF(s.cashier_name,''), NULLIF(u.full_name,''), NULLIF(u.username,''), 'Cashier #' || COALESCE(s.user_id::text,'Unknown'))
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
                    COALESCE(NULLIF(s.cashier_name,''), NULLIF(u.full_name,''), NULLIF(u.username,''), 'Cashier #' || COALESCE(s.user_id::text,'')) AS cashier,
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
            return Ok(new { version = "1.0.9" });
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
            return Ok(new { @fixed = total, message = $"Fixed {total} records across both stores — added 8h to timestamps" });
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
            return Ok(new { @fixed = total, message = $"Fixed {total} records across both stores — added 8h to timestamps where Manila hour < 8" });
        }

        [HttpGet("fix-sync-table-times")]
        public IActionResult FixSyncTableTimes()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var total = 0;
            // SyncController.SyncTable used DateTime.TryParse with default styles,
            // which converted offset strings (+08:00) to server local time (UTC),
            // then Npgsql (session Asia/Manila) double-converted them — stored 8h behind.
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
            return Ok(new { @fixed = total, message = $"Fixed {total} records — added 8h to all Maria hour >= 8 timestamps (SyncTable double-conversion fix)" });
        }

        [HttpGet("products/master")]
        public IActionResult GetMasterProducts()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, name, barcode, category, price, cost, stock_qty, image_data, is_active, points_exempt, points_per_unit
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
                    pointsExempt = reader.GetBoolean(9),
                    pointsPerUnit = reader.GetInt32(10)
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
                       mp.points_exempt, mp.points_per_unit, mp.is_active,
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
                    units = reader.IsDBNull(11) ? null : System.Text.Json.JsonSerializer.Deserialize<object>(reader.GetString(11))
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
                    INSERT INTO master_products (name, barcode, category, price, cost, stock_qty, image_data, points_exempt, points_per_unit, updated_at)
                    VALUES (@n, @b, @c, @p, @co, 0, @img, @pe, @ppu, NOW()) RETURNING id", conn, tx);
                cmd.Parameters.AddWithValue("n", p.Name);
                cmd.Parameters.AddWithValue("b", (object?)p.Barcode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("c", p.Category ?? "");
                cmd.Parameters.AddWithValue("p", p.Price);
                cmd.Parameters.AddWithValue("co", p.Cost);
                cmd.Parameters.AddWithValue("img", p.ImageData ?? "");
                cmd.Parameters.AddWithValue("pe", p.PointsExempt);
                cmd.Parameters.AddWithValue("ppu", p.PointsPerUnit);
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
                    UPDATE master_products SET name=@n, barcode=@b, category=@c, price=@p, cost=@co, image_data=@img, points_exempt=@pe, points_per_unit=@ppu, is_active=@ia, updated_at=NOW()
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

        // ── Warehouse API ──
        [HttpGet("warehouse/products")]
        public IActionResult WhGetProducts([FromQuery] bool activeOnly = true, [FromQuery] string? search = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var where = activeOnly ? "wp.is_active = true" : "1=1";
            if (!string.IsNullOrEmpty(search))
                where += $" AND (wp.name ILIKE @s OR wp.barcode ILIKE @s)";
            cmd.CommandText = $@"
                SELECT wp.id, wp.name, wp.barcode, wp.category, wp.box_price, wp.box_cost, wp.box_qty, wp.piece_price, wp.stock_qty,
                       CASE WHEN wp.master_product_id IS NOT NULL THEN
                           (SELECT COALESCE(json_agg(json_build_object('unitName', mpu.unit_name, 'price', mpu.price, 'cost', mpu.cost, 'qtyPerUnit', mpu.qty_per_unit, 'isDefault', mpu.is_default) ORDER BY mpu.is_default DESC, mpu.unit_name), '[]'::json)
                            FROM master_product_units mpu WHERE mpu.product_id = wp.master_product_id)
                       ELSE '[]'::json END AS units
                FROM wh_products wp WHERE {where} ORDER BY wp.name {(string.IsNullOrEmpty(search) ? "" : "LIMIT 100")}";
            if (!string.IsNullOrEmpty(search))
                cmd.Parameters.AddWithValue("s", $"%{search}%");
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var unitsJson = r.GetString(9);
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
                    units = unitsJson != "[]" ? System.Text.Json.JsonSerializer.Deserialize<object>(unitsJson) : null
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
                    COALESCE(SUM(stock_qty), 0)::bigint AS total_stock_qty,
                    COALESCE(SUM((box_cost / NULLIF(box_qty, 0)) * stock_qty), 0) AS total_cost,
                    COALESCE(SUM(piece_price * stock_qty), 0) AS total_price,
                    COUNT(*) FILTER (WHERE box_cost = 0 OR box_cost IS NULL)::bigint AS zero_cost_items
                FROM wh_products WHERE is_active = true";
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
                using var trail = conn.CreateCommand(); trail.Transaction = tx;
                trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, barcode, qty_change, reference, reference_type) VALUES (@pid, @pn, @bc, @qty, @ref, @rt)";
                trail.Parameters.AddWithValue("pid", id);
                trail.Parameters.AddWithValue("pn", name ?? "");
                trail.Parameters.AddWithValue("bc", barcode ?? "");
                trail.Parameters.AddWithValue("qty", s.QtyChange);
                trail.Parameters.AddWithValue("ref", refText);
                trail.Parameters.AddWithValue("rt", refType);
                trail.ExecuteNonQuery();

                tx.Commit();
                return Ok(new { success = true });
            }
            catch (Exception ex) { tx.Rollback(); return StatusCode(500, new { error = ex.Message }); }
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

            // Check if already imported — if so, update instead of duplicate
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
                using var ded = conn.CreateCommand();
                ded.CommandText = @"
                    UPDATE wh_products SET stock_qty = stock_qty - COALESCE((
                        SELECT SUM(oi.base_qty) FROM wh_order_items oi WHERE oi.order_id = @oid AND oi.product_id = wh_products.id
                    ), 0)
                    WHERE id IN (SELECT DISTINCT product_id FROM wh_order_items WHERE order_id = @oid)";
                ded.Parameters.AddWithValue("oid", id);
                ded.ExecuteNonQuery();
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
                        // Shortage — restock warehouse
                        using var restock = conn.CreateCommand(); restock.Transaction = tx;
                        restock.CommandText = "UPDATE wh_products SET stock_qty = stock_qty + @qty WHERE id = @pid";
                        restock.Parameters.AddWithValue("qty", baseQty);
                        restock.Parameters.AddWithValue("pid", productId);
                        restock.ExecuteNonQuery();

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

        // ══════════════════════════════════════════════════════════════
        // WAREHOUSE TRANSFERS (warehouse → POS store stock transfers)
        // ══════════════════════════════════════════════════════════════

        [HttpGet("warehouse/transfers")]
        public IActionResult WhGetTransfers()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT t.id, t.client_id, t.client_name, t.status, t.notes, t.store_id,
                       t.created_at, t.updated_at,
                       COALESCE(SUM(CASE WHEN ti.received_qty < ti.qty THEN 1 ELSE 0 END), 0) > 0 AS has_shortage
                FROM wh_transfers t
                LEFT JOIN wh_transfer_items ti ON ti.transfer_id = t.id
                GROUP BY t.id
                ORDER BY t.created_at DESC LIMIT 200";
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                data.Add(new {
                    id = r.GetInt32(0), clientId = r.GetInt32(1), clientName = r.GetString(2),
                    status = r.GetString(3), notes = r.IsDBNull(4) ? "" : r.GetString(4),
                    storeId = r.IsDBNull(5) ? "" : r.GetString(5),
                    createdAt = r.GetDateTime(6), updatedAt = r.GetDateTime(7),
                    hasShortage = r.GetBoolean(8)
                });
            return Ok(data);
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
                        // Deduct stock from warehouse immediately (prevents overselling)
                        using var deductCmd = new NpgsqlCommand(
                            "UPDATE wh_products SET stock_qty = stock_qty - @qty WHERE id = @pid AND stock_qty >= @qty", conn, tx);
                        deductCmd.Parameters.AddWithValue("pid", item.ProductId);
                        deductCmd.Parameters.AddWithValue("qty", item.Qty);
                        var affected = deductCmd.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            tx.Rollback();
                            return BadRequest(new { error = $"Not enough stock for {item.ProductName}" });
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
                    barcode = r.GetString(2), baseQty = r.GetInt32(3),
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
                var transferOut = new List<object>();
                foreach (var (productId, productName, baseQty, barcode) in itemsList)
                {
                    var accepted = body?.Items == null || receivedIds.Contains(productId);
                    var receivedQty = accepted ? baseQty : 0;

                    using var upd = conn.CreateCommand(); upd.Transaction = tx;
                    upd.CommandText = "UPDATE wh_transfer_items SET received_qty = @rq WHERE transfer_id = @tid AND product_id = @pid";
                    upd.Parameters.AddWithValue("rq", receivedQty);
                    upd.Parameters.AddWithValue("tid", id);
                    upd.Parameters.AddWithValue("pid", productId);
                    upd.ExecuteNonQuery();

                    if (accepted)
                    {
                        // Stock already deducted on transfer creation
                    }
                    else
                    {
                        // Restock shortages back to warehouse
                        using var restock = conn.CreateCommand(); restock.Transaction = tx;
                        restock.CommandText = "UPDATE wh_products SET stock_qty = stock_qty + @bq WHERE id = @pid";
                        restock.Parameters.AddWithValue("bq", baseQty);
                        restock.Parameters.AddWithValue("pid", productId);
                        restock.ExecuteNonQuery();

                        // Log trail
                        using var trail = conn.CreateCommand(); trail.Transaction = tx;
                        trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, barcode, qty_change, reference, reference_type) VALUES (@pid, @pn, @bc, @qty, @ref, 'shortage_return')";
                        trail.Parameters.AddWithValue("pid", productId);
                        trail.Parameters.AddWithValue("pn", productName);
                        trail.Parameters.AddWithValue("bc", barcode);
                        trail.Parameters.AddWithValue("qty", baseQty);
                        trail.Parameters.AddWithValue("ref", $"Transfer #{id}{(clientName != null ? " → " + clientName : "")}");
                        trail.ExecuteNonQuery();

                        shortages.Add(new { productId, productName, baseQty });
                    }
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

                // Restore stock to warehouse
                using var itemsCmd = conn.CreateCommand(); itemsCmd.Transaction = tx;
                itemsCmd.CommandText = "SELECT product_id, qty FROM wh_transfer_items WHERE transfer_id = @tid";
                itemsCmd.Parameters.AddWithValue("tid", id);
                using var r = itemsCmd.ExecuteReader();
                var restoreList = new List<(int pid, int qty)>();
                while (r.Read()) restoreList.Add((r.GetInt32(0), r.GetInt32(1)));
                r.Close();

                foreach (var (pid, qty) in restoreList)
                {
                    using var upd = conn.CreateCommand(); upd.Transaction = tx;
                    upd.CommandText = "UPDATE wh_products SET stock_qty = stock_qty + @qty WHERE id = @pid";
                    upd.Parameters.AddWithValue("pid", pid);
                    upd.Parameters.AddWithValue("qty", qty);
                    upd.ExecuteNonQuery();
                }

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
        cmd.CommandText = "SELECT qty_change, reference, reference_type, created_at FROM wh_stock_trails WHERE product_id = @pid ORDER BY created_at DESC LIMIT 200";
        cmd.Parameters.AddWithValue("pid", productId);
        var list = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new { qtyChange = r.GetInt32(0), reference = r.GetString(1), type = r.GetString(2), createdAt = r.GetDateTime(3) });
        return Ok(list);
    }

        [HttpPost("warehouse/stock-snapshot")]
        public IActionResult WhStockSnapshot([FromBody] WhStockSnapshotRequest req)
        {
            if (req?.Items == null) return Ok(new { ok = true });
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                foreach (var item in req.Items)
                {
                    using var cmd = conn.CreateCommand(); cmd.Transaction = tx;
                    cmd.CommandText = "UPDATE wh_products SET stock_qty = @sq WHERE id = @pid";
                    cmd.Parameters.AddWithValue("pid", item.ProductId);
                    cmd.Parameters.AddWithValue("sq", item.CurrentStock);
                    cmd.ExecuteNonQuery();

                    using var trail = conn.CreateCommand(); trail.Transaction = tx;
                    trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, qty_change, reference, reference_type) VALUES (@pid, @pn, 0, 'Stock snapshot from POS', 'snapshot')";
                    trail.Parameters.AddWithValue("pid", item.ProductId);
                    trail.Parameters.AddWithValue("pn", item.ProductName);
                    trail.ExecuteNonQuery();
                }
                tx.Commit();
                return Ok(new { ok = true });
            }
            catch { tx.Rollback(); return Ok(new { ok = false }); }
        }

        [HttpGet("warehouse/inventory-activity")]
        public IActionResult WhGetInventoryActivity(
            [FromQuery] string? search = null,
            [FromQuery] string? from = null,
            [FromQuery] string? to = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();

            var sql = "SELECT product_name, barcode, qty_change, reference, reference_type, created_at FROM wh_stock_trails WHERE 1=1";

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
                    createdAt = r.GetDateTime(5)
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
                "SELECT ti.product_id, ti.product_name, ti.barcode, -ti.qty, 'Transfer #' || ti.transfer_id || CASE WHEN c.name IS NOT NULL THEN ' → ' || c.name ELSE '' END, 'transfer_out' " +
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
                hdr.CommandText = "INSERT INTO wh_walkin_sales (customer_id, customer_name, total_amount, item_count, invoice_no) VALUES (@cid, @cn, 0, @ic, @inv) RETURNING id";
                hdr.Parameters.AddWithValue("cid", req.CustomerId > 0 ? req.CustomerId : 0);
                hdr.Parameters.AddWithValue("cn", req.CustomerName.Trim());
                hdr.Parameters.AddWithValue("ic", req.Items.Count);
                hdr.Parameters.AddWithValue("inv", invoiceNo);
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
                    using var r = get.ExecuteReader();
                    while (r.Read())
                        units.Add((r.GetString(0), r.GetDecimal(1), r.GetInt32(2), r.GetInt32(3)));

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
                        // Fallback: use wh_products.box_qty and piece_price
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
                using var trail = conn.CreateCommand(); trail.Transaction = tx;
                trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, barcode, qty_change, reference, reference_type) VALUES (@pid, @pn, @bc, @qc, @ref, 'walkin_sale')";
                trail.Parameters.AddWithValue("pid", item.ProductId);
                trail.Parameters.AddWithValue("pn", productName);
                trail.Parameters.AddWithValue("bc", barcode);
                trail.Parameters.AddWithValue("qc", -stockDeduction);
                trail.Parameters.AddWithValue("ref", $"Walk-in: {req.CustomerName.Trim()} | {unitName} x {item.Qty}");
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
        cmd.CommandText = "SELECT s.id, s.customer_name, s.total_amount, s.item_count, s.created_at, COALESCE(s.is_voided, FALSE), COALESCE(s.invoice_no, '') FROM wh_walkin_sales s WHERE 1=1";
        if (!string.IsNullOrEmpty(from)) { cmd.CommandText += " AND s.created_at >= @from"; cmd.Parameters.AddWithValue("from", from); }
        if (!string.IsNullOrEmpty(to)) { cmd.CommandText += " AND s.created_at <= @to"; cmd.Parameters.AddWithValue("to", to + " 23:59:59"); }
        cmd.CommandText += " ORDER BY s.created_at DESC LIMIT " + limit;

        var list = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var saleId = r.GetInt32(0);
            list.Add(new { id = saleId, customerName = r.GetString(1), total = r.GetDecimal(2), itemCount = r.GetInt32(3), createdAt = r.GetDateTime(4), isVoided = r.GetBoolean(5), invoiceNo = r.IsDBNull(6) ? "" : r.GetString(6) });
        }
        return Ok(list);
    }

    [HttpGet("warehouse/sales/{id}/items")]
    public IActionResult WhGetSaleItems(int id)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT product_name, unit_name, qty, price, subtotal, points_earned FROM wh_walkin_sale_items WHERE sale_id = @sid ORDER BY id";
        cmd.Parameters.AddWithValue("sid", id);
        var list = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new { productName = r.GetString(0), unitName = r.GetString(1), qty = r.GetInt32(2), price = r.GetDecimal(3), subtotal = r.GetDecimal(4), points = r.GetInt32(5) });
        return Ok(list);
    }

    [HttpPost("warehouse/sales/{id}/void")]
    public IActionResult WhVoidSale(int id, [FromBody] WhVoidRequest? req)
    {
        using var conn = Data.PgDatabaseHelper.GetConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            using var chk = conn.CreateCommand(); chk.Transaction = tx;
            chk.CommandText = "SELECT is_voided FROM wh_walkin_sales WHERE id = @id";
            chk.Parameters.AddWithValue("id", id);
            var existing = chk.ExecuteScalar();
            if (existing == null) return NotFound(new { error = "Sale not found" });
            if (existing is bool b && b) return BadRequest(new { error = "Sale already voided" });

            // Restore stock
            using var items = conn.CreateCommand(); items.Transaction = tx;
            items.CommandText = "SELECT product_id, stock_deduction FROM wh_walkin_sale_items WHERE sale_id = @sid";
            items.Parameters.AddWithValue("sid", id);
            using var r = items.ExecuteReader();
            var restores = new List<(int pid, int qty)>();
            while (r.Read()) restores.Add((r.GetInt32(0), r.GetInt32(1)));
            r.Close();

            foreach (var (pid, qty) in restores)
            {
                using var upd = conn.CreateCommand(); upd.Transaction = tx;
                upd.CommandText = "UPDATE wh_products SET stock_qty = stock_qty + @qty WHERE id = @pid";
                upd.Parameters.AddWithValue("pid", pid);
                upd.Parameters.AddWithValue("qty", qty);
                upd.ExecuteNonQuery();

                using var trail = conn.CreateCommand(); trail.Transaction = tx;
                trail.CommandText = "INSERT INTO wh_stock_trails (product_id, product_name, qty_change, reference, reference_type) " +
                    "SELECT @pid, name, @qty, 'Wholesale Void #' || @sid, 'void_return' FROM wh_products WHERE id = @pid";
                trail.Parameters.AddWithValue("pid", pid);
                trail.Parameters.AddWithValue("qty", qty);
                trail.Parameters.AddWithValue("sid", id);
                trail.ExecuteNonQuery();
            }

            using var mark = conn.CreateCommand(); mark.Transaction = tx;
            mark.CommandText = "UPDATE wh_walkin_sales SET is_voided = TRUE WHERE id = @id";
            mark.Parameters.AddWithValue("id", id);
            mark.ExecuteNonQuery();

            tx.Commit();
            return Ok(new { ok = true });
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
            orig.CommandText = "SELECT product_id, stock_deduction FROM wh_walkin_sale_items WHERE sale_id = @sid";
            orig.Parameters.AddWithValue("sid", id);
            using var rOrig = orig.ExecuteReader();
            var restores = new List<(int pid, int qty)>();
            while (rOrig.Read()) restores.Add((rOrig.GetInt32(0), rOrig.GetInt32(1)));
            rOrig.Close();
            foreach (var (pid, qty) in restores)
            {
                using var res = conn.CreateCommand(); res.Transaction = tx;
                res.CommandText = "UPDATE wh_products SET stock_qty = stock_qty + @qty WHERE id = @pid";
                res.Parameters.AddWithValue("pid", pid);
                res.Parameters.AddWithValue("qty", qty);
                res.ExecuteNonQuery();
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

    public class PosPromoRequest { public string Message { get; set; } = ""; }
    }

    public class WhProductDto { public string Name { get; set; } = ""; public string? Barcode { get; set; } public string? Category { get; set; } public decimal BoxPrice { get; set; } public decimal BoxCost { get; set; } public int BoxQty { get; set; } = 1; public decimal PiecePrice { get; set; } }
    public class WhStockDto { public int StockQty { get; set; } }
    public class WhStockMoveDto { public int QtyChange { get; set; } public string Reason { get; set; } = ""; }
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
        public List<WhStockSnapshotItem>? Items { get; set; }
    }
    public class WhStockSnapshotItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int CurrentStock { get; set; }
    }

    public class WhWalkinSellRequest
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string PaymentMethod { get; set; } = "Cash";
        public decimal CashReceived { get; set; }
        public List<WhWalkinSellItem> Items { get; set; } = new();
    }
    public class WhVoidRequest { public string Reason { get; set; } = ""; }
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

    public class RenameStoreRequest
    {
        public string StoreId { get; set; } = "";
        public string StoreName { get; set; } = "";
    }
}
