using System.Data.SQLite;
using System.Text;
using System.Text.Json;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public static class SyncService
{
    private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static string? _storeId;
    private static string? _storeName;
    private static bool _storeNameSynced;

    public static string StoreId
    {
        get
        {
            if (_storeId == null)
            {
                try
                {
                    using var conn = DatabaseHelper.GetConnection();
                    conn.Open();
                    using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'StoreId'", conn);
                    var val = cmd.ExecuteScalar()?.ToString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        _storeId = val;
                    }
                    else
                    {
                        _storeId = $"STORE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
                        using var ins = new SQLiteCommand("INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('StoreId', @v)", conn);
                        ins.Parameters.AddWithValue("v", _storeId);
                        ins.ExecuteNonQuery();
                    }
                }
                catch { _storeId = "STORE-DEFAULT"; }
            }
            return _storeId;
        }
    }

    public static string StoreName
    {
        get
        {
            if (_storeName == null)
            {
                try
                {
                    using var conn = DatabaseHelper.GetConnection();
                    conn.Open();
                    using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'StoreName'", conn);
                    _storeName = cmd.ExecuteScalar()?.ToString() ?? "";
                }
                catch { _storeName = ""; }
            }
            return _storeName;
        }
        set
        {
            _storeName = value;
            _storeNameSynced = false;
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SQLiteCommand("INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('StoreName', @v)", conn);
                cmd.Parameters.AddWithValue("v", value);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }
    }

    public static string ApiUrl
    {
        get
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'CloudApiUrl'", conn);
                var val = cmd.ExecuteScalar()?.ToString();
                if (!string.IsNullOrEmpty(val)) return val;
            }
            catch { }
            return "https://api-production-99fb.up.railway.app/api";
        }
    }

    public static async Task SyncProduct(Product product)
    {
        var data = new[]
        {
            new {
                PosId = product.Id,
                Name = product.Name,
                Barcode = (string?)product.Barcode,
                Category = product.Category,
                Price = product.Price,
                Cost = product.Cost,
                StockQty = product.StockQty,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ModifiedBy = product.ModifiedBy
            }
        };
        await PostAsync("/products", data);
    }

    public static async Task SyncCustomer(Customer customer)
    {
        var data = new[]
        {
            new {
                PosId = customer.Id,
                Name = customer.Name,
                Phone = (string?)customer.Phone,
                Email = (string?)customer.Email,
                LoyaltyPoints = customer.LoyaltyPoints,
                IsActive = customer.IsActive,
                CreditBalance = customer.CreditBalance,
                CreditLimit = customer.CreditLimit,
                Address = customer.Address,
                CreatedAt = customer.CreatedAt.ToString("o"),
                ModifiedBy = customer.ModifiedBy
            }
        };
        await PostAsync("/customers", data);
    }

    public static async Task SyncUser(User user)
    {
        var data = new[]
        {
            new {
                PosId = user.Id,
                Username = user.Username,
                Role = user.Role,
                FullName = user.FullName,
                IsActive = user.IsActive
            }
        };
        await PostAsync("/users", data);
    }

    public static async Task SyncSale(Sale sale, List<SaleItem> items)
    {
        var payload = new
        {
            sale = new
            {
                PosId = sale.Id,
                InvoiceNo = sale.InvoiceNo,
                SaleDate = sale.SaleDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                SubTotal = sale.SubTotal,
                Discount = sale.Discount,
                Tax = sale.Tax,
                GrandTotal = sale.GrandTotal,
                AmountPaid = sale.AmountPaid,
                Change = sale.Change,
                PaymentMethod = sale.PaymentMethod,
                CustomerId = sale.CustomerId,
                UserId = sale.UserId,
                IsVoided = sale.IsVoided,
                ReferenceNo = sale.ReferenceNo,
                OrderType = sale.OrderType,
                CashPaid = sale.CashPaid,
                EwPaid = sale.EwPaid,
                CashierName = sale.CashierName ?? ""
            },
            items = items.Select(i => new
            {
                PosId = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Barcode = i.Barcode,
                Price = i.Price,
                Quantity = i.Quantity,
                TotalPrice = i.TotalPrice,
                IsVoided = i.IsVoided,
                UnitName = i.UnitName,
                QtyPerUnit = i.QtyPerUnit
            }).ToList()
        };
        await PostAsync("/sales", payload, sale.Id);
    }

    public static async Task SyncVoidLog(VoidLog log)
    {
        var data = new[]
        {
            new {
                PosId = log.Id,
                SaleId = log.SaleId,
                SaleItemId = log.SaleItemId,
                Action = log.Action,
                Reason = log.Reason,
                InvoiceNo = log.InvoiceNo,
                ProductName = log.ProductName,
                Quantity = log.Quantity,
                Amount = (decimal)log.Amount,
                UserId = log.UserId,
                UserName = log.UserName,
                CreatedAt = ToUtcString(log.CreatedAt)
            }
        };
        await PostAsync("/voidlogs", data);
    }

    public static async Task SyncStockTrail(StockTrail trail)
    {
        var data = new[]
        {
            new {
                PosId = trail.Id,
                ProductId = trail.ProductId,
                ProductName = trail.ProductName,
                Barcode = trail.Barcode,
                QuantityAdded = trail.QuantityAdded,
                StockBefore = trail.StockBefore,
                StockAfter = trail.StockAfter,
                Reference = trail.Reference,
                UserId = trail.UserId,
                UserName = trail.UserName,
                CreatedAt = ToUtcString(trail.CreatedAt)
            }
        };
        await PostAsync("/stocktrails", data);
    }

    public static async Task SyncCreditTransaction(CreditTransaction ct)
    {
        var data = new[]
        {
            new {
                PosId = ct.Id,
                CustomerId = ct.CustomerId,
                SaleId = ct.SaleId,
                Type = ct.Type,
                Description = ct.Description,
                Debit = ct.Debit,
                Credit = ct.Credit,
                Balance = ct.Balance,
                PaymentMethod = ct.PaymentMethod,
                ReferenceNo = ct.ReferenceNo,
                UserId = ct.UserId,
                UserName = ct.UserName,
                CreatedAt = ToUtcString(ct.CreatedAt)
            }
        };
        await PostAsync("/credittransactions", data);
    }

    public static async Task SyncDailyClose(DailyClose dc)
    {
        var data = new[]
        {
            new {
                PosId = dc.Id,
                CloseDate = dc.CloseDate,
                TotalSales = dc.TotalSales,
                TotalCash = dc.TotalCash,
                TotalEwallet = dc.TotalEWallet,
                TotalCredit = dc.TotalCredit,
                TotalVoided = dc.TotalVoided,
                CashOnHand = dc.CashOnHand,
                Difference = dc.Difference,
                OpeningCash = dc.OpeningCash,
                TotalExpenses = dc.TotalExpenses,
                Notes = dc.Notes,
                UserId = dc.UserId,
                UserName = dc.UserName,
                CreatedAt = ToUtcString(dc.CreatedAt)
            }
        };
        await PostAsync("/dailycloses", data);
    }

    public static async Task SyncExpense(Expense expense)
    {
        var data = new[]
        {
            new {
                PosId = expense.Id,
                Amount = expense.Amount,
                Category = expense.Category,
                Description = expense.Description,
                ReferenceNo = (string?)expense.ReferenceNo,
                CashierUsername = expense.CashierUsername,
                Timestamp = DateTime.TryParse(expense.Timestamp, out var et) ? et.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss") : expense.Timestamp,
                ReceiptImage = expense.ReceiptImage
            }
        };
        await PostAsync("/expenses", data);
    }

    private static async Task PostAsync(string endpoint, object data, int? saleId = null)
    {
        try
        {
            var url = ApiUrl.TrimEnd('/') + endpoint + "?store_id=" + StoreId + "&store_name=" + Uri.EscapeDataString(StoreName);
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "sync_log.txt"),
                $"{DateTime.Now:HH:mm:ss} POST {url}{Environment.NewLine}");
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                LogSync(endpoint, "OK", "");
                if (saleId.HasValue) MarkSynced(saleId.Value);
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                LogSync(endpoint, "FAIL", err);
                EnqueueFailed(endpoint, json);
            }
        }
        catch (Exception ex)
        {
            LogSync(endpoint, "ERROR", ex.Message);
            try
            {
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                EnqueueFailed(endpoint, json);
            }
            catch { }
        }
    }

    private static void MarkSynced(int saleId)
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("UPDATE Sales SET Synced = 1 WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", saleId);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private static string ToUtcString(string? localTime)
    {
        if (string.IsNullOrEmpty(localTime)) return "";
        if (DateTime.TryParse(localTime, out var dt))
            return dt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
        return localTime;
    }

    private static void LogSync(string endpoint, string status, string error)
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand(
                "INSERT INTO SyncLog (Endpoint, Status, Error, CreatedAt) VALUES (@e, @s, @err, datetime('now','localtime'))",
                conn);
            cmd.Parameters.AddWithValue("e", endpoint);
            cmd.Parameters.AddWithValue("s", status);
            cmd.Parameters.AddWithValue("err", error);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private static void EnqueueFailed(string endpoint, string json)
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand(
                "INSERT INTO SyncQueue (Endpoint, Payload, CreatedAt) VALUES (@e, @p, datetime('now','localtime'))",
                conn);
            cmd.Parameters.AddWithValue("e", endpoint);
            cmd.Parameters.AddWithValue("p", json);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public static async Task RetryFailedAsync()
    {
        List<(int Id, string Endpoint, string Payload)>? failed = null;
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Id, Endpoint, Payload FROM SyncQueue ORDER BY Id LIMIT 50", conn);
            using var reader = cmd.ExecuteReader();
            failed = new List<(int, string, string)>();
            while (reader.Read())
                failed.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }
        catch { return; }

        if (failed == null || failed.Count == 0) return;

        foreach (var (id, endpoint, payload) in failed)
        {
            try
            {
                var url = ApiUrl.TrimEnd('/') + endpoint + "?store_id=" + StoreId + "&store_name=" + Uri.EscapeDataString(StoreName);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    RemoveFromQueue(id);
                }
            }
            catch { }
        }
    }

    private static void RemoveFromQueue(int id)
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("DELETE FROM SyncQueue WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public static void EnsureSyncQueueTable()
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand(
                "CREATE TABLE IF NOT EXISTS SyncQueue (Id INTEGER PRIMARY KEY AUTOINCREMENT, Endpoint TEXT NOT NULL, Payload TEXT NOT NULL, CreatedAt TEXT NOT NULL)",
                conn);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public static async Task<int> DownloadMasterCatalog(IProgress<string>? progress = null)
    {
        var added = 0; var updated = 0;
        try
        {
            var url = ApiUrl.TrimEnd('/') + "/dashboard/products/master/download";
            progress?.Report("Downloading master catalog...");
            var json = await _client.GetStringAsync(url);
            var products = JsonSerializer.Deserialize<List<JsonElement>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (products == null || products.Count == 0) return 0;

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            foreach (var p in products)
            {
                var name = p.GetProperty("name").GetString() ?? "";
                var barcode = p.TryGetProperty("barcode", out var bc) ? bc.GetString() : null;
                var category = p.TryGetProperty("category", out var cat) ? cat.GetString() : "";
                var price = p.GetProperty("price").GetDecimal();
                var cost = p.GetProperty("cost").GetDecimal();

                // Check if product exists locally (by barcode, then name)
                var existingId = 0;
                if (!string.IsNullOrEmpty(barcode))
                {
                    using var chk = new SQLiteCommand("SELECT Id FROM Products WHERE Barcode = @b AND IsActive = 1 LIMIT 1", conn);
                    chk.Parameters.AddWithValue("@b", barcode);
                    var val = chk.ExecuteScalar();
                    if (val != null) existingId = Convert.ToInt32(val);
                }
                if (existingId == 0)
                {
                    using var chk = new SQLiteCommand("SELECT Id FROM Products WHERE Name = @n AND IsActive = 1 LIMIT 1", conn);
                    chk.Parameters.AddWithValue("@n", name);
                    var val = chk.ExecuteScalar();
                    if (val != null) existingId = Convert.ToInt32(val);
                }

                if (existingId > 0)
                {
                    // Update existing product
                    using var upd = new SQLiteCommand("UPDATE Products SET Name=@n, Category=@c, Price=@p, Cost=@co, ModifiedBy='cloud' WHERE Id=@id", conn);
                    upd.Parameters.AddWithValue("@n", name);
                    upd.Parameters.AddWithValue("@c", category ?? "");
                    upd.Parameters.AddWithValue("@p", price);
                    upd.Parameters.AddWithValue("@co", cost);
                    upd.Parameters.AddWithValue("@id", existingId);
                    upd.ExecuteNonQuery();

                    // Delete old units and re-insert
                    using var del = new SQLiteCommand("DELETE FROM ProductUnits WHERE ProductId = @pid", conn);
                    del.Parameters.AddWithValue("@pid", existingId);
                    del.ExecuteNonQuery();

                    if (p.TryGetProperty("units", out var uEl) && uEl.ValueKind == JsonValueKind.Array)
                        InsertUnits(conn, existingId, uEl);

                    updated++;
                }
                else
                {
                    // Insert new product
                    using var ins = new SQLiteCommand(@"
                        INSERT INTO Products (Name, Barcode, Category, Price, Cost, StockQty, IsActive, CreatedAt, ModifiedBy, SourceId)
                        VALUES (@n, @b, @c, @p, @co, 0, 1, datetime('now','localtime'), 'cloud', 'master')", conn);
                    ins.Parameters.AddWithValue("@n", name);
                    ins.Parameters.AddWithValue("@b", (object?)barcode ?? DBNull.Value);
                    ins.Parameters.AddWithValue("@c", category ?? "");
                    ins.Parameters.AddWithValue("@p", price);
                    ins.Parameters.AddWithValue("@co", cost);
                    ins.ExecuteNonQuery();

                    using var getId = new SQLiteCommand("SELECT last_insert_rowid()", conn);
                    var localId = Convert.ToInt32(getId.ExecuteScalar());

                    if (p.TryGetProperty("units", out var uEl) && uEl.ValueKind == JsonValueKind.Array)
                        InsertUnits(conn, localId, uEl);

                    added++;
                }

                if ((added + updated) % 50 == 0) progress?.Report($"Processed {added + updated} products...");
            }

            progress?.Report($"Complete! {added} added, {updated} updated.");
        }
        catch (Exception ex) { progress?.Report($"Error: {ex.Message}"); }
        return added + updated;
    }

    private static void InsertUnits(SQLiteConnection conn, int productId, JsonElement unitsEl)
    {
        foreach (var u in unitsEl.EnumerateArray())
        {
            var unitName = u.GetProperty("unitName").GetString() ?? "Piece";
            var uPrice = u.GetProperty("price").GetDecimal();
            var uCost = u.GetProperty("cost").GetDecimal();
            var qtyPerUnit = u.GetProperty("qtyPerUnit").GetInt32();
            var isDefault = u.GetProperty("isDefault").GetBoolean();

            using var ucmd = new SQLiteCommand(@"
                INSERT INTO ProductUnits (ProductId, UnitName, Price, Cost, QtyPerUnit, IsDefault)
                VALUES (@pid, @un, @pr, @co, @qpu, @def)", conn);
            ucmd.Parameters.AddWithValue("@pid", productId);
            ucmd.Parameters.AddWithValue("@un", unitName);
            ucmd.Parameters.AddWithValue("@pr", uPrice);
            ucmd.Parameters.AddWithValue("@co", uCost);
            ucmd.Parameters.AddWithValue("@qpu", qtyPerUnit);
            ucmd.Parameters.AddWithValue("@def", isDefault ? 1 : 0);
            ucmd.ExecuteNonQuery();
        }
    }
}
