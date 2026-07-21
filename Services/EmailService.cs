using System.Net;
using System.Net.Mail;
using System.Text.Json;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public class EmailService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUser;
    private readonly string _smtpPass;
    private readonly string _recipient;

    public EmailService()
    {
        _smtpHost = "smtp.gmail.com";
        _smtpPort = 587;
        _smtpUser = "admin@jumongdev.com";
        _smtpPass = "muni fyee ooph iqnq";
        _recipient = "admin@jumongdev.com";
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var host = GetSetting(conn, "SmtpHost");
            var port = GetSetting(conn, "SmtpPort");
            var user = GetSetting(conn, "SmtpUser");
            var pass = GetSetting(conn, "SmtpPass");
            var to   = GetSetting(conn, "SmtpTo");
            if (!string.IsNullOrWhiteSpace(host)) _smtpHost = host;
            if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var p)) _smtpPort = p;
            if (!string.IsNullOrWhiteSpace(user)) _smtpUser = user;
            if (!string.IsNullOrWhiteSpace(pass)) _smtpPass = pass;
            if (!string.IsNullOrWhiteSpace(to)) _recipient = to;
        }
        catch { }
    }

    private static string? GetSetting(System.Data.SQLite.SQLiteConnection conn, string key)
    {
        using var cmd = new System.Data.SQLite.SQLiteCommand("SELECT Value FROM Settings WHERE Key = @key", conn);
        cmd.Parameters.AddWithValue("@key", key);
        return cmd.ExecuteScalar()?.ToString();
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_smtpHost) && !string.IsNullOrWhiteSpace(_smtpUser);

    public string? SendEndShiftReport(decimal totalSales, decimal totalCash, decimal totalEWallet,
        decimal totalCredit, decimal totalVoided, decimal cashOnHand, decimal difference, string cashierName,
        decimal totalExpenses, List<Expense> expenses,
        List<(string InvoiceNo, string SaleDate, decimal Amount, string ReferenceNo)> gcashTxns,
        List<(string Name, decimal Amount)> creditCustomers,
        List<(string CustomerName, string PaymentMethod, decimal Amount, string Timestamp)> creditPayments,
        int denom1000, int denom500, int denom200, int denom100, int denom50, int denom20, decimal denomCoins,
        decimal totalInventoryCost = 0, decimal totalCostSold = 0, decimal totalStockReceivedCost = 0, decimal previousInventory = 0)
    {
        if (!IsConfigured) return "Email not configured. Set SMTP settings first.";

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var company = GetSetting(conn, "CompanyName") ?? "JUMONG POS";
        var address = GetSetting(conn, "CompanyAddress") ?? "";
        var mobile = GetSetting(conn, "CompanyMobile") ?? "";
        var creditPayCash = creditPayments.Where(cp => cp.PaymentMethod == "Cash").Sum(cp => cp.Amount);
        var expectedCash = totalCash - totalExpenses + creditPayCash;

        var html = $@"
<html>
<head>
<style>
body {{ font-family: 'Segoe UI', Tahoma, sans-serif; margin: 0; padding: 16px; background: #F0F0F5; color: #222; }}
.container {{ max-width: 800px; margin: 0 auto; background: #FFFFFF; border-radius: 8px; overflow: hidden; border: 1px solid #DDD; }}
.header {{ background: #1A1A3E; padding: 20px; text-align: center; }}
.header h1 {{ margin: 0; color: #00F5FF; font-size: 22px; }}
.header p {{ margin: 4px 0 0; color: #B0B0D0; font-size: 13px; }}
.section {{ padding: 16px 20px; border-bottom: 1px solid #E0E0E0; }}
.section h2 {{ margin: 0 0 12px; color: #1A1A3E; font-size: 14px; }}
.summary-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }}
.summary-item {{ background: #F5F5FA; padding: 10px 14px; border-radius: 4px; }}
.summary-item .label {{ color: #666; font-size: 12px; }}
.summary-item .value {{ color: #111; font-size: 18px; font-weight: bold; }}
.summary-item .value.over {{ color: #27AE60; }}
.summary-item .value.short {{ color: #E74C3C; }}
table {{ width: 100%; border-collapse: collapse; margin-top: 8px; }}
th {{ background: #1A1A3E; color: #FFF; font-size: 12px; text-align: left; padding: 8px 10px; }}
td {{ padding: 8px 10px; font-size: 13px; color: #222; border-bottom: 1px solid #E0E0E0; }}
tr:nth-child(even) td {{ background: #F8F8FC; }}
.total-row td {{ font-weight: bold; color: #1A1A3E; border-top: 2px solid #CCC; }}
.footer {{ padding: 16px 20px; text-align: center; color: #888; font-size: 12px; background: #F5F5FA; }}
</style>
</head>
<body>
<div class=""container"">
<div class=""header"">
<h1>{company.ToUpper()}</h1>
{(string.IsNullOrEmpty(address) ? "" : $"<p>{address}</p>")}
{(string.IsNullOrEmpty(mobile) ? "" : $"<p>Mobile: {mobile}</p>")}
<p>End Shift Report — {TimeHelper.Now:MMMM dd, yyyy  hh:mm tt}</p>
<p>Cashier: <strong>{cashierName}</strong></p>
</div>

<div class=""section"">
<h2>Financial Variance</h2>
<div class=""summary-grid"">
<div class=""summary-item""><div class=""label"">Total Sales</div><div class=""value"">Php {totalSales:N2}</div></div>
<div class=""summary-item""><div class=""label"">Cash Sales</div><div class=""value"">Php {totalCash:N2}</div></div>
<div class=""summary-item""><div class=""label"">Less Expenses</div><div class=""value"">Php {expenses.Sum(e => e.Amount):N2}</div></div>
<div class=""summary-item""><div class=""label"">Expected Cash</div><div class=""value"">Php {expectedCash:N2}</div></div>
<div class=""summary-item""><div class=""label"">Counted Cash</div><div class=""value"">Php {cashOnHand:N2}</div></div>
<div class=""summary-item""><div class=""label"">Difference</div><div class=""value {(difference >= 0 ? "over" : "short")}"">Php {Math.Abs(difference):N2} ({(difference >= 0 ? "OVER" : "SHORT")})</div></div>
</div>
</div>

<div class=""section"">
<h2>Cash Denomination Breakdown</h2>
<table>
<tr><th>Denomination</th><th>Count</th><th style=""text-align:right"">Amount</th></tr>
{(denom1000 > 0 ? $"<tr><td>1000</td><td>{denom1000}</td><td style='text-align:right'>Php {denom1000 * 1000:N2}</td></tr>" : "")}
{(denom500 > 0 ? $"<tr><td>500</td><td>{denom500}</td><td style='text-align:right'>Php {denom500 * 500:N2}</td></tr>" : "")}
{(denom200 > 0 ? $"<tr><td>200</td><td>{denom200}</td><td style='text-align:right'>Php {denom200 * 200:N2}</td></tr>" : "")}
{(denom100 > 0 ? $"<tr><td>100</td><td>{denom100}</td><td style='text-align:right'>Php {denom100 * 100:N2}</td></tr>" : "")}
{(denom50 > 0 ? $"<tr><td>50</td><td>{denom50}</td><td style='text-align:right'>Php {denom50 * 50:N2}</td></tr>" : "")}
{(denom20 > 0 ? $"<tr><td>20</td><td>{denom20}</td><td style='text-align:right'>Php {denom20 * 20:N2}</td></tr>" : "")}
{(denomCoins > 0 ? $"<tr><td>Coins</td><td></td><td style='text-align:right'>Php {denomCoins:N2}</td></tr>" : "")}
<tr class=""total-row""><td colspan=""2""><strong>TOTAL</strong></td><td style=""text-align:right""><strong>Php {cashOnHand:N2}</strong></td></tr>
</table>
</div>

<div class=""section"">
<h2>E-Wallet / GCash</h2>
<div class=""summary-grid"">
<div class=""summary-item""><div class=""label"">E-Wallet Sales</div><div class=""value"">Php {totalEWallet:N2}</div></div>
<div class=""summary-item""><div class=""label"">Voided/Refunded</div><div class=""value"">Php {totalVoided:N2}</div></div>
</div>
{(gcashTxns.Count > 0 ? $@"
<table>
<tr><th>Date</th><th>Invoice</th><th>Reference</th><th>Amount</th></tr>
{string.Join("\n", gcashTxns.Select(t => $"<tr><td>{t.SaleDate}</td><td>{t.InvoiceNo}</td><td>{t.ReferenceNo}</td><td>Php {t.Amount:N2}</td></tr>"))}
<tr class=""total-row""><td colspan=""3"">Total E-Wallet</td><td>Php {gcashTxns.Sum(t => t.Amount):N2}</td></tr>
</table>" : "<p style='color:#8C8CAA'>No e-wallet transactions this shift.</p>")}
</div>

<div class=""section"">
<h2>Shift Expenses</h2>
{(expenses.Count > 0 ? $@"
<table>
<tr><th>Time</th><th>Category</th><th>Description</th><th>Ref No.</th><th>Amount</th></tr>
{string.Join("\n", expenses.Select(e => $"<tr><td>{e.Timestamp[10..]}</td><td>{e.Category}</td><td>{e.Description}</td><td>{(string.IsNullOrEmpty(e.ReferenceNo) ? "—" : e.ReferenceNo)}</td><td>Php {e.Amount:N2}</td></tr>"))}
<tr class=""total-row""><td colspan=""4"">Total Expenses</td><td>Php {expenses.Sum(e => e.Amount):N2}</td></tr>
</table>" : "<p style='color:#8C8CAA'>No expenses logged this shift.</p>")}
</div>

<div class=""section"">
<h2>Extended Store Credit</h2>
{(creditCustomers.Count > 0 ? $@"
<table>
<tr><th>Customer</th><th>Credit Amount</th></tr>
{string.Join("\n", creditCustomers.Select(c => $"<tr><td>{c.Name}</td><td>Php {c.Amount:N2}</td></tr>"))}
<tr class=""total-row""><td>Total Credit Sales</td><td>Php {creditCustomers.Sum(c => c.Amount):N2}</td></tr>
</table>" : "<p style='color:#8C8CAA'>No credit sales this shift.</p>")}
</div>

<div class=""section"">
<h2>Debt Collections (Paid Credit)</h2>
        {(creditPayments.Count > 0 ? $@"
<table>
<tr><th>Customer</th><th>Payment Type</th><th>Time</th><th>Amount Collected</th></tr>
{string.Join("\n", creditPayments.Select(p => $"<tr><td>{p.CustomerName}</td><td>{p.PaymentMethod}</td><td>{p.Timestamp[10..]}</td><td>Php {p.Amount:N2}</td></tr>"))}
<tr class=""total-row""><td colspan=""3"">Total Collected</td><td>Php {creditPayments.Sum(p => p.Amount):N2}</td></tr>
</table>" : "<p style='color:#8C8CAA'>No debt collections this shift.</p>")}
</div>

{(previousInventory > 0 || totalCostSold > 0 || totalStockReceivedCost > 0 || totalInventoryCost > 0 ? $@"
<div class=""section"">
<h2>Inventory Reconciliation</h2>
<table>
<tr><th>Description</th><th style=""text-align:right"">Amount</th></tr>
<tr><td>Previous Inventory</td><td style=""text-align:right"">Php {previousInventory:N2}</td></tr>
{(totalStockReceivedCost > 0 ? $"<tr><td>+ Stock Received Today</td><td style='text-align:right'>Php {totalStockReceivedCost:N2}</td></tr>" : "")}
<tr><td>- Cost of Goods Sold Today</td><td style=""text-align:right"">(Php {totalCostSold:N2})</td></tr>
<tr class=""total-row""><td>Expected Inventory</td><td style=""text-align:right"">Php {(previousInventory + totalStockReceivedCost - totalCostSold):N2}</td></tr>
<tr class=""total-row""><td>Actual Inventory</td><td style=""text-align:right"">Php {totalInventoryCost:N2}</td></tr>
<tr class=""total-row""><td>Variance</td><td style=""text-align:right; color:{(previousInventory + totalStockReceivedCost - totalCostSold == totalInventoryCost ? "#27AE60" : "#E74C3C")}"">{(previousInventory + totalStockReceivedCost - totalCostSold == totalInventoryCost ? "✔" : "⚠")} Php {Math.Abs(totalInventoryCost - (previousInventory + totalStockReceivedCost - totalCostSold)):N2} {(previousInventory + totalStockReceivedCost - totalCostSold == totalInventoryCost ? "Balanced" : totalInventoryCost > previousInventory + totalStockReceivedCost - totalCostSold ? "OVER" : "SHORT")}</td></tr>
</table>
</div>" : "")}

<div class=""footer"">
<p>— End of Report —</p>
<p>Generated by {company.ToUpper()} POS System</p>
</div>
</div>
</body>
</html>";

        var subject = $"End Shift Report — {cashierName} — {TimeHelper.Now:yyyy-MM-dd}";
        var result = SendEmailHtml(subject, html);
        if (result != null)
        {
            QueueEmail(subject, html, true);
            return result;
        }
        return null;
    }

    public string? SendInventoryReport()
    {
        if (!IsConfigured) return "Email not configured. Set SMTP settings first.";

        var products = ProductService.GetAll();
        var body = $@"INVENTORY COUNT
{TimeHelper.Now:MMMM dd, yyyy  hh:mm tt}

{"Product",-30} {"Stock",8}
──────────────────────────────────────
";
        foreach (var p in products)
            body += $"{p.Name,-30} {p.StockQty,8}\n";

        body += $"\nTotal products: {products.Count}";
        return SendEmail("Inventory Report", body);
    }

    public string? SendReceipt(Sale sale, Customer? customer, List<SaleItem> items)
    {
        if (customer == null || string.IsNullOrWhiteSpace(customer.Email))
            return "Customer has no email address.";

        var company = "JUMONG POS";
        using (var conn = DatabaseHelper.GetConnection())
        {
            conn.Open();
            var setting = GetSetting(conn, "CompanyName");
            if (!string.IsNullOrEmpty(setting)) company = setting;
        }

        var itemsHtml = string.Join("\n", items.Select((i, idx) => $@"
<tr><td>{idx + 1}</td><td>{i.ProductName}</td><td>{i.Quantity}</td><td>₱{i.Price:N2}</td><td style='text-align:right'>₱{i.TotalPrice:N2}</td></tr>"));

        var html = $@"
<html>
<head>
<style>
body {{ font-family: 'Segoe UI', Tahoma, sans-serif; margin: 0; padding: 16px; background: #F0F0F5; color: #222; }}
.container {{ max-width: 500px; margin: 0 auto; background: #FFFFFF; border-radius: 8px; overflow: hidden; border: 1px solid #DDD; }}
.header {{ background: #1A1A3E; padding: 20px; text-align: center; }}
.header h1 {{ margin: 0; color: #00F5FF; font-size: 20px; }}
.header p {{ margin: 4px 0 0; color: #B0B0D0; font-size: 12px; }}
.body {{ padding: 16px 20px; }}
table {{ width: 100%; border-collapse: collapse; margin-top: 8px; }}
th {{ background: #1A1A3E; color: #FFF; font-size: 11px; text-align: left; padding: 6px 8px; }}
td {{ padding: 6px 8px; font-size: 12px; color: #222; border-bottom: 1px solid #E0E0E0; }}
tr:nth-child(even) td {{ background: #F8F8FC; }}
.total-row td {{ font-weight: bold; color: #1A1A3E; border-top: 2px solid #CCC; }}
.footer {{ padding: 16px 20px; text-align: center; color: #888; font-size: 11px; background: #F5F5FA; }}
</style>
</head>
<body>
<div class='container'>
<div class='header'>
<h1>{company.ToUpper()}</h1>
<p>OFFICIAL RECEIPT</p>
</div>
<div class='body'>
<p><strong>Invoice:</strong> {sale.InvoiceNo}</p>
<p><strong>Date:</strong> {sale.SaleDate:MMMM dd, yyyy  hh:mm tt}</p>
<p><strong>Customer:</strong> {customer.Name}</p>
{(string.IsNullOrWhiteSpace(customer.Address) ? "" : $"<p><strong>Address:</strong> {customer.Address}</p>")}
<hr style='border:none;border-top:1px solid #E0E0E0'>
<table>
<tr><th>#</th><th>Item</th><th>Qty</th><th>Price</th><th style='text-align:right'>Total</th></tr>
{itemsHtml}
<tr class='total-row'><td colspan='4'>TOTAL</td><td style='text-align:right'>₱{sale.GrandTotal:N2}</td></tr>
</table>
<hr style='border:none;border-top:1px solid #E0E0E0'>
<p><strong>Paid:</strong> ₱{sale.AmountPaid:N2}</p>
<p><strong>Change:</strong> ₱{sale.Change:N2}</p>
<p><strong>Payment:</strong> {sale.PaymentMethod}</p>
</div>
<div class='footer'>
<p>Thank you for your purchase!</p>
<p>— {company.ToUpper()} —</p>
</div>
</div>
</body>
</html>";

        return SendEmailTo(customer.Email, $"Receipt — {sale.InvoiceNo}", html);
    }

    private string? SendEmailTo(string to, string subject, string htmlBody)
    {
        try
        {
            using var msg = new MailMessage(_smtpUser, to, subject, "");
            msg.IsBodyHtml = true;
            msg.Body = htmlBody;
            using var client = new SmtpClient(_smtpHost, _smtpPort);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(_smtpUser, _smtpPass);
            client.Send(msg);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private string? SendEmail(string subject, string body)
    {
        try
        {
            using var msg = new MailMessage(_smtpUser, _recipient, subject, body);
            using var client = new SmtpClient(_smtpHost, _smtpPort);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(_smtpUser, _smtpPass);
            client.Send(msg);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private string? SendEmailHtml(string subject, string htmlBody)
    {
        try
        {
            using var msg = new MailMessage(_smtpUser, _recipient, subject, "");
            msg.IsBodyHtml = true;
            msg.Body = htmlBody;
            using var client = new SmtpClient(_smtpHost, _smtpPort);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(_smtpUser, _smtpPass);
            client.Send(msg);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public void SendErrorReport(string subject, string body)
    {
        SendOrQueuePlain(subject, body);
    }

    private void SendOrQueuePlain(string subject, string body)
    {
        var result = SendEmail(subject, body);
        if (result != null)
            QueueEmail(subject, body, false);
    }

    private void SendOrQueueHtml(string subject, string htmlBody)
    {
        var result = SendEmailHtml(subject, htmlBody);
        if (result != null)
            QueueEmail(subject, htmlBody, true);
    }

    private static string QueuePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pending_emails.json");

    private static void QueueEmail(string subject, string body, bool isHtml)
    {
        try
        {
            var list = new List<PendingEmail>();
            if (File.Exists(QueuePath))
            {
                var existing = File.ReadAllText(QueuePath);
                list = JsonSerializer.Deserialize<List<PendingEmail>>(existing) ?? new();
            }
            list.Add(new PendingEmail { Subject = subject, Body = body, IsHtml = isHtml, QueuedAt = TimeHelper.Now });
            File.WriteAllText(QueuePath, JsonSerializer.Serialize(list));
        }
        catch { }
    }

    public static void FlushQueue()
    {
        try
        {
            if (!File.Exists(QueuePath)) return;
            var json = File.ReadAllText(QueuePath);
            var list = JsonSerializer.Deserialize<List<PendingEmail>>(json);
            if (list == null || list.Count == 0) return;

            var svc = new EmailService();
            var sent = new List<int>();
            for (int i = 0; i < list.Count; i++)
            {
                var email = list[i];
                string? result;
                if (email.IsHtml)
                    result = svc.SendEmailHtml(email.Subject, email.Body);
                else
                    result = svc.SendEmail(email.Subject, email.Body);

                if (result == null) sent.Add(i);
            }

            list = list.Where((_, i) => !sent.Contains(i)).ToList();
            if (list.Count == 0)
                File.Delete(QueuePath);
            else
                File.WriteAllText(QueuePath, JsonSerializer.Serialize(list));
        }
        catch { }
    }
}

