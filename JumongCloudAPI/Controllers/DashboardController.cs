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
        return range switch
        {
            "today"    => $" AND {col}::date = CURRENT_DATE",
            "yesterday"=> $" AND {col}::date = CURRENT_DATE - INTERVAL '1 day'",
            "week"     => $" AND {col} >= CURRENT_DATE - INTERVAL '7 days'",
            "month"    => $" AND {col} >= CURRENT_DATE - INTERVAL '30 days'",
            _          => $" AND {col}::date = @date"
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
            if (!string.IsNullOrEmpty(date) && (string.IsNullOrEmpty(range) || range == "custom")) cmd.Parameters.AddWithValue("date", DateTime.Parse(date));

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
                    (SELECT COALESCE(SUM(si.total_price),0) {slj} WHERE s.is_voided = false AND si.is_voided = true {StoreFilter(storeId, "s")}{tfSales.Replace("sale_date","s.sale_date")}) AS total_voided
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
                totalVoided = row.GetDecimal(10)
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
        public IActionResult GetTopProducts([FromQuery] int limit = 10, [FromQuery] string? storeId = null, [FromQuery] string? range = null)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var tf = TimeframeClause(range, "s.sale_date", cmd);
            if (string.IsNullOrEmpty(range) || range == "all") tf = "";
            cmd.CommandText = $@"
                SELECT si.product_name, SUM(si.quantity) AS total_qty, SUM(si.total_price) AS total_amount
                FROM sale_items si
                JOIN sales s ON si.sale_id = s.pos_id
                WHERE s.is_voided = false {StoreFilter(storeId, "s")}{tf}
                GROUP BY si.product_name
                ORDER BY total_qty DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("limit", limit);
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new { productName = reader.GetString(0), totalQty = reader.GetInt32(1), totalAmount = reader.GetDecimal(2) });
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
            cmd.CommandText = $@"SELECT u.pos_id, u.username, u.role, u.full_name, u.is_active, u.store_id
                FROM users u WHERE 1=1 {StoreFilter(storeId, "u")} ORDER BY u.username LIMIT 200";
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) data.Add(new { posId = r.GetInt32(0), username = r.GetString(1), role = r.GetString(2), fullName = r.IsDBNull(3) ? "" : r.GetString(3), isActive = r.GetBoolean(4), storeId = r.GetString(5) });
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
                       total_expenses, cash_on_hand, difference, user_name, notes, store_id
                FROM daily_closes d
                WHERE close_date >= CURRENT_DATE - @days {StoreFilter(storeId, "d")}
                ORDER BY close_date DESC";
            cmd.Parameters.AddWithValue("days", days);
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new { closeDate = reader.GetDateTime(0), totalSales = reader.GetDecimal(1), totalCash = reader.GetDecimal(2), totalEwallet = reader.GetDecimal(3), totalCredit = reader.GetDecimal(4), totalVoided = reader.GetDecimal(5), totalExpenses = reader.GetDecimal(6), cashOnHand = reader.GetDecimal(7), difference = reader.GetDecimal(8), userName = reader.GetString(9), notes = reader.IsDBNull(10) ? "" : reader.GetString(10), storeId = reader.GetString(11) });
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
                WHERE st.quantity_added > 0 {StoreFilter(storeId, "st")}{tf}
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
                SELECT name, barcode, category, stock_qty, price, cost
                FROM products p
                WHERE is_active = true {StoreFilter(storeId, "p")}
                ORDER BY stock_qty ASC";
            if (!string.IsNullOrEmpty(storeId)) cmd.Parameters.AddWithValue("storeId", storeId);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new { name = reader.GetString(0), barcode = reader.IsDBNull(1) ? "" : reader.GetString(1), category = reader.IsDBNull(2) ? "" : reader.GetString(2), stockQty = reader.GetInt32(3), price = reader.GetDecimal(4), cost = reader.GetDecimal(5) });
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
            cmd.CommandText = @"
                SELECT si.product_name, si.barcode, si.quantity, si.price, si.total_price,
                       COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0) AS unit_cost, si.qty_per_unit,
                       si.quantity * COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0) AS total_cost,
                       si.total_price - (si.quantity * COALESCE(NULLIF(si.unit_cost, 0), p.cost, 0)) AS profit,
                       p.pos_id AS product_pos_id, si.store_id
                FROM sale_items si
                JOIN sales s ON si.sale_id = s.pos_id AND si.store_id = s.store_id
                LEFT JOIN products p ON si.product_id = p.pos_id AND si.store_id = p.store_id
                WHERE s.invoice_no = @inv AND si.is_voided = false
                ORDER BY si.product_name";
            cmd.Parameters.AddWithValue("inv", invoiceNo);
            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Add(new {
                    productName = reader.GetString(0),
                    barcode = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    quantity = reader.GetInt32(2),
                    price = reader.GetDecimal(3),
                    totalPrice = reader.GetDecimal(4),
                    unitCost = reader.GetDecimal(5),
                    qtyPerUnit = reader.GetInt32(6),
                    totalCost = reader.GetDecimal(7),
                    profit = reader.GetDecimal(8),
                    productPosId = reader.IsDBNull(9) ? 0 : reader.GetInt32(9)
                });
            return Ok(data);
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

        [HttpGet("version")]
        public IActionResult GetVersion()
        {
            return Ok(new { version = "1.0.19", buildDate = "2026-06-10", changes = "Fix profit calc: fallback to p.cost when unit_cost=0", downloadUrl = "https://github.com/jumongdev/JumongPosV1.01/releases/download/v1.0.19/JumongPosV1.01.exe" });
        }

        [HttpGet("products/master")]
        public IActionResult GetMasterProducts()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, name, barcode, category, price, cost, stock_qty, is_active
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
                    stockQty = reader.GetInt32(6)
                });
            return Ok(products);
        }

        [HttpGet("products/master/{id}/units")]
        public IActionResult GetMasterProductUnits(int id)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, product_id, unit_name, price, cost, qty_per_unit, is_default
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
                    isDefault = reader.GetBoolean(6)
                });
            return Ok(units);
        }

        [HttpGet("products/master/download")]
        public IActionResult DownloadMasterCatalog()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT mp.id, mp.name, mp.barcode, mp.category, mp.price, mp.cost, mp.stock_qty,
                       COALESCE(json_agg(
                           json_build_object('unitName', mpu.unit_name, 'price', mpu.price, 'cost', mpu.cost, 'qtyPerUnit', mpu.qty_per_unit, 'isDefault', mpu.is_default)
                           ORDER BY mpu.is_default DESC, mpu.unit_name
                       ) FILTER (WHERE mpu.id IS NOT NULL), '[]') AS units
                FROM master_products mp
                LEFT JOIN master_product_units mpu ON mpu.product_id = mp.id
                WHERE mp.is_active = true
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
                    units = reader.IsDBNull(7) ? null : System.Text.Json.JsonSerializer.Deserialize<object>(reader.GetString(7))
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
                foreach (var p in products)
                {
                    using var cmd = new NpgsqlCommand(@"
                        INSERT INTO master_products (name, barcode, category, price, cost, stock_qty)
                        VALUES (@name, @barcode, @cat, @price, @cost, @qty) RETURNING id", conn, tx);
                    cmd.Parameters.AddWithValue("name", p.Name);
                    cmd.Parameters.AddWithValue("barcode", (object?)p.Barcode ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("cat", p.Category ?? "");
                    cmd.Parameters.AddWithValue("price", p.Price);
                    cmd.Parameters.AddWithValue("cost", p.Cost);
                    cmd.Parameters.AddWithValue("qty", p.StockQty);
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
                return Ok(new { success = true, count = products.Count });
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
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO master_products (name, barcode, category, price, cost, stock_qty)
                    VALUES (@n, @b, @c, @p, @co, 0) RETURNING id", conn, tx);
                cmd.Parameters.AddWithValue("n", p.Name);
                cmd.Parameters.AddWithValue("b", (object?)p.Barcode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("c", p.Category ?? "");
                cmd.Parameters.AddWithValue("p", p.Price);
                cmd.Parameters.AddWithValue("co", p.Cost);
                var id = Convert.ToInt32(cmd.ExecuteScalar());

                if (p.Units != null)
                {
                    foreach (var u in p.Units)
                    {
                        using var ucmd = new NpgsqlCommand(@"
                            INSERT INTO master_product_units (product_id, unit_name, price, cost, qty_per_unit, is_default)
                            VALUES (@pid, @un, @pr, @co, @qpu, @def)", conn, tx);
                        ucmd.Parameters.AddWithValue("pid", id);
                        ucmd.Parameters.AddWithValue("un", u.UnitName);
                        ucmd.Parameters.AddWithValue("pr", u.Price);
                        ucmd.Parameters.AddWithValue("co", u.Cost);
                        ucmd.Parameters.AddWithValue("qpu", u.QtyPerUnit);
                        ucmd.Parameters.AddWithValue("def", u.IsDefault);
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
                using var cmd = new NpgsqlCommand(@"
                    UPDATE master_products SET name=@n, barcode=@b, category=@c, price=@p, cost=@co
                    WHERE id=@id", conn, tx);
                cmd.Parameters.AddWithValue("n", p.Name);
                cmd.Parameters.AddWithValue("b", (object?)p.Barcode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("c", p.Category ?? "");
                cmd.Parameters.AddWithValue("p", p.Price);
                cmd.Parameters.AddWithValue("co", p.Cost);
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
                            INSERT INTO master_product_units (product_id, unit_name, price, cost, qty_per_unit, is_default)
                            VALUES (@pid, @un, @pr, @co, @qpu, @def)", conn, tx);
                        ucmd.Parameters.AddWithValue("pid", id);
                        ucmd.Parameters.AddWithValue("un", u.UnitName);
                        ucmd.Parameters.AddWithValue("pr", u.Price);
                        ucmd.Parameters.AddWithValue("co", u.Cost);
                        ucmd.Parameters.AddWithValue("qpu", u.QtyPerUnit);
                        ucmd.Parameters.AddWithValue("def", u.IsDefault);
                        ucmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
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
        public IActionResult WhGetProducts([FromQuery] bool activeOnly = true)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT id, name, barcode, category, box_price, box_cost, box_qty, piece_price, stock_qty FROM wh_products {(activeOnly ? "WHERE is_active = true" : "")} ORDER BY name";
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) data.Add(new { id = r.GetInt32(0), name = r.GetString(1), barcode = r.IsDBNull(2) ? "" : r.GetString(2), category = r.IsDBNull(3) ? "" : r.GetString(3), boxPrice = r.GetDecimal(4), boxCost = r.GetDecimal(5), boxQty = r.GetInt32(6), piecePrice = r.GetDecimal(7), stockQty = r.GetInt32(8) });
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

        [HttpPut("warehouse/products/{id}/stock")]
        public IActionResult WhUpdateStock(int id, [FromBody] WhStockDto s)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE wh_products SET stock_qty = @qty WHERE id = @id";
            cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("qty", s.StockQty);
            cmd.ExecuteNonQuery();
            return Ok(new { success = true });
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
        public IActionResult WhGetClients()
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, contact, address, store_type FROM wh_clients WHERE is_active = true ORDER BY name";
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) data.Add(new { id = r.GetInt32(0), name = r.GetString(1), contact = r.IsDBNull(2) ? "" : r.GetString(2), address = r.IsDBNull(3) ? "" : r.GetString(3), storeType = r.GetString(4) });
            return Ok(data);
        }

        [HttpPost("warehouse/clients")]
        public IActionResult WhCreateClient([FromBody] WhClientDto c)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO wh_clients (name, contact, address, store_type) VALUES (@n, @ct, @a, @st) RETURNING id";
            cmd.Parameters.AddWithValue("n", c.Name); cmd.Parameters.AddWithValue("ct", c.Contact ?? ""); cmd.Parameters.AddWithValue("a", c.Address ?? ""); cmd.Parameters.AddWithValue("st", c.StoreType ?? "pos");
            return Ok(new { id = Convert.ToInt32(cmd.ExecuteScalar()) });
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
            cmd.CommandText = $"SELECT o.id, o.client_id, o.client_name, o.status, o.notes, o.total_amount, o.created_at, o.updated_at FROM wh_orders o{where} ORDER BY o.created_at DESC LIMIT 200";
            var data = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) data.Add(new { id = r.GetInt32(0), clientId = r.GetInt32(1), clientName = r.GetString(2), status = r.GetString(3), notes = r.IsDBNull(4) ? "" : r.GetString(4), totalAmount = r.GetDecimal(5), createdAt = r.GetDateTime(6), updatedAt = r.GetDateTime(7) });
            return Ok(data);
        }

        [HttpGet("warehouse/orders/{id}")]
        public IActionResult WhGetOrder(int id)
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT product_name, unit_type, qty, price, total_price FROM wh_order_items WHERE order_id = @oid ORDER BY product_name";
            cmd.Parameters.AddWithValue("oid", id);
            var items = new List<object>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) items.Add(new { productName = r.GetString(0), unitType = r.GetString(1), qty = r.GetInt32(2), price = r.GetDecimal(3), totalPrice = r.GetDecimal(4) });
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
                        using var icmd = new NpgsqlCommand("INSERT INTO wh_order_items (order_id, product_id, product_name, unit_type, qty, price, total_price) VALUES (@oi, @pi, @pn, @ut, @q, @pr, @tp)", conn, tx);
                        icmd.Parameters.AddWithValue("oi", orderId); icmd.Parameters.AddWithValue("pi", item.ProductId); icmd.Parameters.AddWithValue("pn", item.ProductName); icmd.Parameters.AddWithValue("ut", item.UnitType ?? "box"); icmd.Parameters.AddWithValue("q", item.Qty); icmd.Parameters.AddWithValue("pr", item.Price); icmd.Parameters.AddWithValue("tp", item.TotalPrice);
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
            return Ok(new { success = true });
        }
    }

    public class WhProductDto { public string Name { get; set; } = ""; public string? Barcode { get; set; } public string? Category { get; set; } public decimal BoxPrice { get; set; } public decimal BoxCost { get; set; } public int BoxQty { get; set; } = 1; public decimal PiecePrice { get; set; } }
    public class WhStockDto { public int StockQty { get; set; } }
    public class WhClientDto { public string Name { get; set; } = ""; public string? Contact { get; set; } public string? Address { get; set; } public string? StoreType { get; set; } }
    public class WhOrderDto { public int ClientId { get; set; } public string? ClientName { get; set; } public string? Notes { get; set; } public List<WhOrderItemDto>? Items { get; set; } }
    public class WhOrderItemDto { public int ProductId { get; set; } public string ProductName { get; set; } = ""; public string? UnitType { get; set; } public int Qty { get; set; } public decimal Price { get; set; } public decimal TotalPrice { get; set; } }
    public class WhStatusDto { public string Status { get; set; } = ""; }

    public class SeedProductDto
    {
        public string Name { get; set; } = "";
        public string? Barcode { get; set; }
        public string? Category { get; set; }
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public int StockQty { get; set; }
        public List<SeedProductUnitDto>? Units { get; set; }
    }

    public class SeedProductUnitDto
    {
        public string UnitName { get; set; } = "Piece";
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public int QtyPerUnit { get; set; } = 1;
        public bool IsDefault { get; set; }
    }

    public class RenameStoreRequest
    {
        public string StoreId { get; set; } = "";
        public string StoreName { get; set; } = "";
    }
}
