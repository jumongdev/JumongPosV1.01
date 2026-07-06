using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Globalization;
using System.Text.Json;

namespace JumongCloudAPI.Controllers;

[ApiController]
[Route("api")]
public class SyncController : ControllerBase
{
    private string StoreId()
    {
        var sid = Request.Query["store_id"].ToString();
        var sname = Request.Query["store_name"].ToString();
        if (!string.IsNullOrEmpty(sid) && !string.IsNullOrEmpty(sname))
            UpsertStoreName(sid, sname);
        return sid;
    }

    private static void UpsertStoreName(string storeId, string storeName)
    {
        try
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO stores (store_id, store_name) VALUES (@id, @name) ON CONFLICT (store_id) DO UPDATE SET store_name = @name";
            cmd.Parameters.AddWithValue("id", storeId);
            cmd.Parameters.AddWithValue("name", storeName);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    [HttpPost("products")]
    public IActionResult SyncProducts([FromBody] List<JsonElement> items)
    {
        var sid = StoreId();
        return SyncTable("products", items, new[] { "pos_id", "name", "barcode", "category", "price", "cost", "stock_qty", "is_active", "created_at", "modified_by" },
            "INSERT INTO products (pos_id, store_id, name, barcode, category, price, cost, stock_qty, is_active, created_at, modified_by, synced_at) " +
            "VALUES (@p0,@sid,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,NOW()) " +
            "ON CONFLICT (store_id, pos_id) DO UPDATE SET name=@p1, barcode=@p2, category=@p3, price=@p4, cost=@p5, stock_qty=@p6, is_active=@p7, modified_by=@p9, synced_at=NOW()", sid);
    }

    [HttpPost("customers")]
    public IActionResult SyncCustomers([FromBody] List<JsonElement> items)
    {
        var sid = StoreId();
        return SyncTable("customers", items, new[] { "pos_id", "name", "phone", "email", "loyalty_points", "is_active", "credit_balance", "credit_limit", "address", "created_at", "modified_by" },
            "INSERT INTO customers (pos_id, store_id, name, phone, email, loyalty_points, is_active, credit_balance, credit_limit, address, created_at, modified_by, synced_at) " +
            "VALUES (@p0,@sid,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,NOW()) " +
            "ON CONFLICT (store_id, pos_id) DO UPDATE SET name=@p1, phone=@p2, email=@p3, loyalty_points=@p4, is_active=@p5, credit_balance=@p6, credit_limit=@p7, address=@p8, modified_by=@p10, synced_at=NOW()", sid);
    }

    [HttpPost("users")]
    public IActionResult SyncUsers([FromBody] List<JsonElement> items)
    {
        var sid = StoreId();
        try
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            foreach (var item in items)
            {
                var posId = item.TryGetProperty("pos_id", out var pidEl) && pidEl.ValueKind == JsonValueKind.Number ? pidEl.GetInt32() : 0;
                var username = item.TryGetProperty("username", out var uEl) ? uEl.GetString() ?? "" : "";
                var role = item.TryGetProperty("role", out var rEl) ? rEl.GetString() ?? "Cashier" : "Cashier";
                var fullName = item.TryGetProperty("full_name", out var fnEl) ? fnEl.GetString() ?? "" : "";
                var isActive = item.TryGetProperty("is_active", out var iaEl) ? iaEl.GetBoolean() : true;
                var passwordHash = item.TryGetProperty("password_hash", out var phEl) ? phEl.GetString() : null;

                if (string.IsNullOrEmpty(username)) continue;

                // Try to find existing user by username (consolidated)
                using var findCmd = conn.CreateCommand();
                findCmd.CommandText = "SELECT id, pos_id FROM users WHERE LOWER(username) = LOWER(@u) LIMIT 1";
                findCmd.Parameters.AddWithValue("u", username);
                var existingId = findCmd.ExecuteScalar();

                if (existingId != null && existingId != DBNull.Value)
                {
                    // User exists — update
                    using var updCmd = conn.CreateCommand();
                    var sql = "UPDATE users SET role=@r, full_name=@fn, is_active=@ia, synced_at=NOW()";
                    if (passwordHash != null) sql += ", password_hash=@ph";
                    sql += " WHERE id=@id";
                    updCmd.CommandText = sql;
                    updCmd.Parameters.AddWithValue("id", existingId);
                    updCmd.Parameters.AddWithValue("r", role);
                    updCmd.Parameters.AddWithValue("fn", fullName);
                    updCmd.Parameters.AddWithValue("ia", isActive);
                    if (passwordHash != null) updCmd.Parameters.AddWithValue("ph", passwordHash);
                    updCmd.ExecuteNonQuery();
                }
                else
                {
                    // New user — insert
                    using var insCmd = conn.CreateCommand();
                    insCmd.CommandText = "INSERT INTO users (pos_id, store_id, username, role, full_name, is_active, password_hash, synced_at) " +
                        "VALUES (@p, @sid, @u, @r, @fn, @ia, @ph, NOW())";
                    insCmd.Parameters.AddWithValue("p", posId);
                    insCmd.Parameters.AddWithValue("sid", sid);
                    insCmd.Parameters.AddWithValue("u", username);
                    insCmd.Parameters.AddWithValue("r", role);
                    insCmd.Parameters.AddWithValue("fn", fullName);
                    insCmd.Parameters.AddWithValue("ia", isActive);
                    insCmd.Parameters.AddWithValue("ph", passwordHash ?? "12345");
                    insCmd.ExecuteNonQuery();
                }

                // Populate user_stores for this user in this store
                if (!string.IsNullOrEmpty(sid) && posId > 0)
                {
                    using var usCmd = conn.CreateCommand();
                    usCmd.CommandText = "INSERT INTO user_stores (user_pos_id, store_id) VALUES (@p, @sid) ON CONFLICT DO NOTHING";
                    usCmd.Parameters.AddWithValue("p", posId);
                    usCmd.Parameters.AddWithValue("sid", sid);
                    usCmd.ExecuteNonQuery();
                }
            }
            return Ok(new { success = true, count = items.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPost("sales")]
    public IActionResult SyncSales([FromBody] SyncSalePayload payload)
    {
        var sid = StoreId();
        try
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();

            using var cmd = new NpgsqlCommand(
                "INSERT INTO sales (pos_id, store_id, invoice_no, sale_date, sub_total, discount, tax, grand_total, amount_paid, change, payment_method, customer_id, user_id, is_voided, reference_no, order_type, cash_paid, ew_paid, cashier_name, synced_at) " +
                "VALUES (@p0,@sid,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14,@p15,@p16,@p17,NOW()) " +
                "ON CONFLICT (store_id, pos_id) DO UPDATE SET invoice_no=@p1, sale_date=@p2, sub_total=@p3, discount=@p4, tax=@p5, grand_total=@p6, amount_paid=@p7, change=@p8, payment_method=@p9, customer_id=@p10, user_id=@p11, is_voided=@p12, reference_no=@p13, order_type=@p14, cash_paid=@p15, ew_paid=@p16, cashier_name=@p17, synced_at=NOW()",
                conn, tx);
            cmd.Parameters.AddWithValue("p0", payload.Sale.PosId);
            cmd.Parameters.AddWithValue("@sid", sid);
            cmd.Parameters.AddWithValue("p1", payload.Sale.InvoiceNo);
            cmd.Parameters.AddWithValue("p2", payload.Sale.SaleDate);
            cmd.Parameters.AddWithValue("p3", payload.Sale.SubTotal);
            cmd.Parameters.AddWithValue("p4", payload.Sale.Discount);
            cmd.Parameters.AddWithValue("p5", payload.Sale.Tax);
            cmd.Parameters.AddWithValue("p6", payload.Sale.GrandTotal);
            cmd.Parameters.AddWithValue("p7", payload.Sale.AmountPaid);
            cmd.Parameters.AddWithValue("p8", payload.Sale.Change);
            cmd.Parameters.AddWithValue("p9", payload.Sale.PaymentMethod);
            cmd.Parameters.AddWithValue("p10", (object?)payload.Sale.CustomerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p11", (object?)payload.Sale.UserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p12", payload.Sale.IsVoided);
            cmd.Parameters.AddWithValue("p13", payload.Sale.ReferenceNo);
            cmd.Parameters.AddWithValue("p14", payload.Sale.OrderType);
            cmd.Parameters.AddWithValue("p15", payload.Sale.CashPaid);
            cmd.Parameters.AddWithValue("p16", payload.Sale.EwPaid);
            cmd.Parameters.AddWithValue("p17", payload.Sale.CashierName ?? "");
            cmd.ExecuteNonQuery();

            foreach (var item in payload.Items)
            {
                using var icmd = new NpgsqlCommand(
                    "INSERT INTO sale_items (pos_id, store_id, sale_id, product_id, product_name, barcode, price, quantity, total_price, is_voided, unit_name, qty_per_unit, unit_cost, synced_at) " +
                    "VALUES (@p0,@sid,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,NOW()) " +
                    "ON CONFLICT (store_id, pos_id) DO UPDATE SET sale_id=@p1, product_id=@p2, product_name=@p3, barcode=@p4, price=@p5, quantity=@p6, total_price=@p7, is_voided=@p8, unit_name=@p9, qty_per_unit=@p10, unit_cost=@p11, synced_at=NOW()",
                    conn, tx);
                icmd.Parameters.AddWithValue("p0", item.PosId);
                icmd.Parameters.AddWithValue("@sid", sid);
                icmd.Parameters.AddWithValue("p1", payload.Sale.PosId);
                icmd.Parameters.AddWithValue("p2", item.ProductId);
                icmd.Parameters.AddWithValue("p3", item.ProductName);
                icmd.Parameters.AddWithValue("p4", (object?)item.Barcode ?? DBNull.Value);
                icmd.Parameters.AddWithValue("p5", item.Price);
                icmd.Parameters.AddWithValue("p6", item.Quantity);
                icmd.Parameters.AddWithValue("p7", item.TotalPrice);
                icmd.Parameters.AddWithValue("p8", item.IsVoided);
                icmd.Parameters.AddWithValue("p9", item.UnitName);
                icmd.Parameters.AddWithValue("p10", item.QtyPerUnit);
                icmd.Parameters.AddWithValue("p11", item.UnitCost);
                icmd.ExecuteNonQuery();
            }

            tx.Commit();
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPost("voidlogs")]
    public IActionResult SyncVoidLogs([FromBody] List<JsonElement> items)
    {
        var sid = StoreId();
        return SyncTable("void_logs", items, new[] { "pos_id", "sale_id", "sale_item_id", "action", "reason", "invoice_no", "product_name", "quantity", "amount", "user_id", "user_name", "created_at" },
            "INSERT INTO void_logs (pos_id, store_id, sale_id, sale_item_id, action, reason, invoice_no, product_name, quantity, amount, user_id, user_name, created_at, synced_at) " +
            "VALUES (@p0,@sid,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,NOW()) " +
            "ON CONFLICT (store_id, pos_id) DO UPDATE SET sale_id=@p1, sale_item_id=@p2, action=@p3, reason=@p4, invoice_no=@p5, product_name=@p6, quantity=@p7, amount=@p8, user_id=@p9, user_name=@p10, created_at=@p11, synced_at=NOW()", sid);
    }

    [HttpPost("stocktrails")]
    public IActionResult SyncStockTrails([FromBody] List<JsonElement> items)
    {
        var sid = StoreId();
        return SyncTable("stock_trails", items, new[] { "pos_id", "product_id", "product_name", "barcode", "quantity_added", "stock_before", "stock_after", "reference", "user_id", "user_name", "created_at" },
            "INSERT INTO stock_trails (pos_id, store_id, product_id, product_name, barcode, quantity_added, stock_before, stock_after, reference, user_id, user_name, created_at, synced_at) " +
            "VALUES (@p0,@sid,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,NOW()) " +
            "ON CONFLICT (store_id, pos_id) DO UPDATE SET product_id=@p1, product_name=@p2, barcode=@p3, quantity_added=@p4, stock_before=@p5, stock_after=@p6, reference=@p7, user_id=@p8, user_name=@p9, created_at=@p10, synced_at=NOW()", sid);
    }

    [HttpPost("credittransactions")]
    public IActionResult SyncCreditTransactions([FromBody] List<JsonElement> items)
    {
        var sid = StoreId();
        return SyncTable("credit_transactions", items, new[] { "pos_id", "customer_id", "sale_id", "type", "description", "debit", "credit", "balance", "payment_method", "reference_no", "user_id", "user_name", "created_at" },
            "INSERT INTO credit_transactions (pos_id, store_id, customer_id, sale_id, type, description, debit, credit, balance, payment_method, reference_no, user_id, user_name, created_at, synced_at) " +
            "VALUES (@p0,@sid,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,NOW()) " +
            "ON CONFLICT (store_id, pos_id) DO UPDATE SET customer_id=@p1, sale_id=@p2, type=@p3, description=@p4, debit=@p5, credit=@p6, balance=@p7, payment_method=@p8, reference_no=@p9, user_id=@p10, user_name=@p11, created_at=@p12, synced_at=NOW()", sid);
    }

    [HttpPost("dailycloses")]
    public IActionResult SyncDailyCloses([FromBody] List<JsonElement> items)
    {
        var sid = StoreId();
        return SyncTable("daily_closes", items, new[] { "pos_id", "close_date", "total_sales", "total_cash", "total_ewallet", "total_credit", "total_voided", "cash_on_hand", "difference", "opening_cash", "total_expenses", "notes", "user_id", "user_name", "created_at" },
            "INSERT INTO daily_closes (pos_id, store_id, close_date, total_sales, total_cash, total_ewallet, total_credit, total_voided, cash_on_hand, difference, opening_cash, total_expenses, notes, user_id, user_name, created_at, synced_at) " +
            "VALUES (@p0,@sid,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14,NOW()) " +
            "ON CONFLICT (store_id, pos_id) DO UPDATE SET close_date=@p1, total_sales=@p2, total_cash=@p3, total_ewallet=@p4, total_credit=@p5, total_voided=@p6, cash_on_hand=@p7, difference=@p8, opening_cash=@p9, total_expenses=@p10, notes=@p11, user_id=@p12, user_name=@p13, created_at=@p14, synced_at=NOW()", sid);
    }

    [HttpPost("expenses")]
    public IActionResult SyncExpenses([FromBody] List<JsonElement> items)
    {
        var sid = StoreId();
        return SyncTable("expenses", items, new[] { "pos_id", "amount", "category", "description", "reference_no", "cashier_username", "timestamp", "receipt_image" },
            "INSERT INTO expenses (pos_id, store_id, amount, category, description, reference_no, cashier_username, timestamp, receipt_image, synced_at) " +
            "VALUES (@p0,@sid,@p1,@p2,@p3,@p4,@p5,@p6,@p7,NOW()) " +
            "ON CONFLICT (store_id, pos_id) DO UPDATE SET amount=@p1, category=@p2, description=@p3, reference_no=@p4, cashier_username=@p5, timestamp=@p6, receipt_image=@p7, synced_at=NOW()", sid);
    }

    private IActionResult SyncTable(string tableName, List<JsonElement> items, string[] fields, string upsertSql, string sid)
    {
        try
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var tx = conn.BeginTransaction();

            foreach (var item in items)
            {
                using var cmd = new NpgsqlCommand(upsertSql, conn, tx);
                cmd.Parameters.AddWithValue("@sid", sid);
                for (var i = 0; i < fields.Length; i++)
                {
                    var key = "";
                    if (item.TryGetProperty(fields[i], out var prop))
                        key = fields[i];
                    else
                    {
                        var camelCase = string.Concat(fields[i].Split('_').Select((w, j) => j == 0 ? w : char.ToUpper(w[0]) + w[1..]));
                        if (item.TryGetProperty(camelCase, out prop))
                            key = camelCase;
                        else
                        {
                            cmd.Parameters.AddWithValue($"p{i}", DBNull.Value);
                            continue;
                        }
                    }

                    if (prop.ValueKind == JsonValueKind.Null)
                        cmd.Parameters.AddWithValue($"p{i}", DBNull.Value);
                    else if (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
                        cmd.Parameters.AddWithValue($"p{i}", prop.GetBoolean());
                    else if (prop.ValueKind == JsonValueKind.Number)
                        cmd.Parameters.AddWithValue($"p{i}", prop.GetDecimal());
                    else if (prop.ValueKind == JsonValueKind.String && DateTime.TryParse(prop.GetString(), null, DateTimeStyles.AdjustToUniversal, out var dt))
                        cmd.Parameters.AddWithValue($"p{i}", dt);
                    else
                        cmd.Parameters.AddWithValue($"p{i}", prop.GetString());
                }
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return Ok(new { success = true, count = items.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

public class SyncSalePayload
{
    public SyncSaleDto Sale { get; set; } = new();
    public List<SyncSaleItemDto> Items { get; set; } = new();
}

public class SyncSaleDto
{
    public int PosId { get; set; }
    public string InvoiceNo { get; set; } = "";
    public DateTime SaleDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Change { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public int? CustomerId { get; set; }
    public int? UserId { get; set; }
    public bool IsVoided { get; set; }
    public string ReferenceNo { get; set; } = "";
    public string OrderType { get; set; } = "Walk-in";
    public decimal CashPaid { get; set; }
    public decimal EwPaid { get; set; }
    public string CashierName { get; set; } = "";
}

public class SyncSaleItemDto
{
    public int PosId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public bool IsVoided { get; set; }
    public string UnitName { get; set; } = "";
    public int QtyPerUnit { get; set; } = 1;
    public decimal UnitCost { get; set; }
}
