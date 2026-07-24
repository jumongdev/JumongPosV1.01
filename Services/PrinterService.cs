using System.Drawing.Printing;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

public class PrinterService
{
    public static List<string> GetPrinters()
    {
        return PrinterSettings.InstalledPrinters.Cast<string>().ToList();
    }

    private static void ExtendPaperIfNeeded(PrintDocument doc, int totalLines, int lineHeight = 14)
    {
        var needed = (totalLines * lineHeight + 10) * 100 / 96;
        if (needed > doc.DefaultPageSettings.PaperSize.Height)
        {
            var ps = doc.DefaultPageSettings.PaperSize;
            doc.DefaultPageSettings.PaperSize = new PaperSize(ps.PaperName, ps.Width, needed);
        }
    }

    public static void PrintReceipt(Sale sale, string cashierName = "Admin", Customer? customer = null)
    {
        var printerName = GetSetting("PrinterName");
        if (string.IsNullOrEmpty(printerName))
        {
            MessageBox.Show("No printer configured. Go to Settings to set a printer.", "Printer Not Set",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printerName;

        var paperW = int.TryParse(GetSetting("PaperWidth"), out var pw) ? pw : 315;
        var marginL = int.TryParse(GetSetting("PrinterMarginLeft"), out var ml) ? ml : 0;
        var marginR = int.TryParse(GetSetting("PrinterMarginRight"), out var mr) ? mr : 0;

        var lineChars = (int)((paperW - marginL - marginR) * 12 / 100);
        if (lineChars < 20) lineChars = 20;
        if (lineChars > 48) lineChars = 48;

        var lines = BuildReceiptLines(sale, cashierName, customer, lineChars);
        ExtendPaperIfNeeded(doc, lines.Count);

        doc.PrintPage += (sender, e) =>
        {
            var pageW = e.PageBounds.Width;
            var leftMargin = pageW * marginL / paperW;
            var rightMargin = pageW * marginR / paperW;

            var printW = pageW - leftMargin - rightMargin;
            var sf = StringFormat.GenericTypographic;

            using var font9 = new Font("Courier New", 9);
            using var font9B = new Font("Courier New", 9, FontStyle.Bold);
            using var font11 = new Font("Courier New", 11);

            var y = 5f;

            foreach (var entry in lines)
            {
                Font f;
                if (entry.Align == TextAlign.Center)
                    f = font11;
                else
                    f = entry.Bold ? font9B : font9;

                if (entry.RightText != null)
                {
                    e.Graphics!.DrawString(entry.Text, f, Brushes.Black, leftMargin, y, sf);
                    var rw = e.Graphics.MeasureString(entry.RightText, f, int.MaxValue, sf).Width;
                    e.Graphics.DrawString(entry.RightText, f, Brushes.Black, leftMargin + printW - rw, y, sf);
                }
                else if (entry.Align == TextAlign.Center)
                {
                    var tw = e.Graphics!.MeasureString(entry.Text, f, int.MaxValue, sf).Width;
                    e.Graphics.DrawString(entry.Text, f, Brushes.Black, leftMargin + (printW - tw) / 2, y, sf);
                }
                else
                {
                    e.Graphics!.DrawString(entry.Text, f, Brushes.Black, leftMargin, y, sf);
                }

                y += entry.Spacing;
            }
            e.HasMorePages = false;
        };

        try
        {
            doc.Print();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Print error: {ex.Message}", "Print Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private enum TextAlign { Left, Center }

    private class LineEntry
    {
        public string Text { get; set; } = "";
        public string? RightText { get; set; }
        public bool Bold { get; set; }
        public int Spacing { get; set; } = 14;
        public TextAlign Align { get; set; } = TextAlign.Left;
    }

    private static List<LineEntry> BuildReceiptLines(Sale sale, string cashierName, Customer? customer = null, int lineChars = 32)
    {
        var lines = new List<LineEntry>();

        var companyName = GetSetting("CompanyName");
        var address = GetSetting("CompanyAddress");
        var mobile = GetSetting("CompanyMobile");
        var footer = GetSetting("ReceiptFooter");
        if (string.IsNullOrEmpty(footer)) footer = "Thank You! Come Again!";

        var header = string.IsNullOrEmpty(companyName) ? "JUMONG POS" : companyName.ToUpper();
        lines.Add(new LineEntry { Text = header, Align = TextAlign.Center, Bold = true, Spacing = 22 });
        if (!string.IsNullOrEmpty(address))
            lines.Add(new LineEntry { Text = address, Align = TextAlign.Center, Spacing = 14 });
        if (!string.IsNullOrEmpty(mobile))
            lines.Add(new LineEntry { Text = mobile, Align = TextAlign.Center, Spacing = 14 });
        lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });
        lines.Add(new LineEntry { Text = $"Inv: {sale.InvoiceNo}", Spacing = 14 });
        lines.Add(new LineEntry { Text = $"{sale.SaleDate:yyyy-MM-dd HH:mm}", Spacing = 14 });
        lines.Add(new LineEntry { Text = $"Cashier: {cashierName}", Spacing = 14 });

        if (customer != null)
        {
            lines.Add(new LineEntry { Text = $"Customer: {customer.Name}", Spacing = 14 });
            if (sale.OrderType == "Online")
            {
                if (!string.IsNullOrEmpty(customer.Phone))
                    lines.Add(new LineEntry { Text = $"Mobile: {customer.Phone}", Spacing = 14 });
                if (!string.IsNullOrEmpty(customer.Address))
                    lines.Add(new LineEntry { Text = $"Addr: {customer.Address}", Spacing = 14 });
            }
        }
        else
        {
            lines.Add(new LineEntry { Text = "Walk-in", Spacing = 14 });
        }

        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Spacing = 12 });

        var totalQty = sale.Items.Sum(x => x.Quantity);
        lines.Add(new LineEntry { Text = $"Total Items: {totalQty}", RightText = $"{sale.Items.Count} line(s)", Bold = true, Spacing = 14 });
        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Spacing = 12 });

        foreach (var item in sale.Items)
        {
            var name = item.ProductName;
            if (name.Length > lineChars)
                name = name[..(lineChars - 2)] + "..";
            lines.Add(new LineEntry { Text = name, Bold = true, Spacing = 14 });
            lines.Add(new LineEntry
            {
                Text = $"  {item.Quantity}x {item.Price:N2}",
                RightText = item.TotalPrice.ToString("N2"),
                Spacing = 16
            });
        }

        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Spacing = 12 });
        lines.Add(new LineEntry { Text = "Sub Total", RightText = sale.SubTotal.ToString("N2"), Spacing = 14 });

        if (sale.Discount > 0)
            lines.Add(new LineEntry { Text = "Discount", RightText = sale.Discount.ToString("N2"), Spacing = 14 });

        if (sale.Tax > 0)
            lines.Add(new LineEntry { Text = "Tax", RightText = sale.Tax.ToString("N2"), Spacing = 14 });

        lines.Add(new LineEntry { Text = "TOTAL", RightText = sale.GrandTotal.ToString("N2"), Bold = true, Spacing = 18 });

        if (sale.PaymentMethod == "Split")
        {
            lines.Add(new LineEntry { Text = "Cash Paid", RightText = sale.CashPaid.ToString("N2"), Spacing = 14 });
            lines.Add(new LineEntry { Text = "E-Wallet Paid", RightText = sale.EwPaid.ToString("N2"), Spacing = 14 });
            if (!string.IsNullOrEmpty(sale.ReferenceNo))
                lines.Add(new LineEntry { Text = "E-Wallet Ref", RightText = sale.ReferenceNo, Spacing = 14 });
            lines.Add(new LineEntry { Text = "Total Paid", RightText = sale.AmountPaid.ToString("N2"), Bold = true, Spacing = 14 });
        }
        else
        {
            lines.Add(new LineEntry { Text = "Paid", RightText = sale.AmountPaid.ToString("N2"), Spacing = 14 });
            if (!string.IsNullOrEmpty(sale.ReferenceNo))
                lines.Add(new LineEntry { Text = "Ref", RightText = sale.ReferenceNo, Spacing = 14 });
        }

        lines.Add(new LineEntry { Text = "Change", RightText = sale.Change.ToString("N2"), Spacing = 14 });
        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Spacing = 12 });
        lines.Add(new LineEntry { Text = footer, Align = TextAlign.Center, Bold = true, Spacing = 20 });
        lines.Add(new LineEntry { Text = "", Spacing = 8 });

        return lines;
    }

    public static void PrintDetailedEndShiftReport(decimal totalSales, decimal totalCash, decimal totalEWallet,
        decimal totalCredit, decimal totalVoided, decimal cashOnHand, decimal difference,
        string cashierName, List<(string InvoiceNo, string SaleDate, decimal Amount, string ReferenceNo)> gcashTxns,
        List<(string Name, decimal Amount)> creditCustomers)
    {
        var printerName = GetSetting("PrinterName");
        if (string.IsNullOrEmpty(printerName))
        {
            MessageBox.Show("No printer configured. Go to Settings to set a printer.", "Printer Not Set",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printerName;

        var paperW = int.TryParse(GetSetting("PaperWidth"), out var pw) ? pw : 315;
        var marginL = int.TryParse(GetSetting("PrinterMarginLeft"), out var ml) ? ml : 0;
        var marginR = int.TryParse(GetSetting("PrinterMarginRight"), out var mr) ? mr : 0;

        var lines = BuildEndShiftReportLines(totalSales, totalCash, totalEWallet, totalCredit, totalVoided,
            cashOnHand, difference, cashierName, gcashTxns, creditCustomers, paperW);
        ExtendPaperIfNeeded(doc, lines.Count);

        doc.PrintPage += (sender, e) =>
        {
            var pageW = e.PageBounds.Width;
            var leftMargin = pageW * marginL / paperW;
            var rightMargin = pageW * marginR / paperW;
            var printW = pageW - leftMargin - rightMargin;
            var sf = StringFormat.GenericTypographic;

            using var font9 = new Font("Courier New", 9);
            using var font9B = new Font("Courier New", 9, FontStyle.Bold);
            using var font11 = new Font("Courier New", 11);

            var y = 5f;

            foreach (var entry in lines)
            {
                Font f;
                if (entry.Align == TextAlign.Center)
                    f = font11;
                else
                    f = entry.Bold ? font9B : font9;

                if (entry.RightText != null)
                {
                    e.Graphics!.DrawString(entry.Text, f, Brushes.Black, leftMargin, y, sf);
                    var rw = e.Graphics.MeasureString(entry.RightText, f, int.MaxValue, sf).Width;
                    e.Graphics.DrawString(entry.RightText, f, Brushes.Black, leftMargin + printW - rw, y, sf);
                }
                else if (entry.Align == TextAlign.Center)
                {
                    var tw = e.Graphics!.MeasureString(entry.Text, f, int.MaxValue, sf).Width;
                    e.Graphics.DrawString(entry.Text, f, Brushes.Black, leftMargin + (printW - tw) / 2, y, sf);
                }
                else
                {
                    e.Graphics!.DrawString(entry.Text, f, Brushes.Black, leftMargin, y, sf);
                }

                y += entry.Spacing;
            }
            e.HasMorePages = false;
        };

        try { doc.Print(); }
        catch (Exception ex)
        {
            MessageBox.Show($"Print error: {ex.Message}", "Print Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static List<LineEntry> BuildEndShiftReportLines(decimal totalSales, decimal totalCash, decimal totalEWallet,
        decimal totalCredit, decimal totalVoided, decimal cashOnHand, decimal difference,
        string cashierName, List<(string InvoiceNo, string SaleDate, decimal Amount, string ReferenceNo)> gcashTxns,
        List<(string Name, decimal Amount)> creditCustomers, int paperW)
    {
        var lines = new List<LineEntry>();
        var company = GetSetting("CompanyName");
        var header = string.IsNullOrEmpty(company) ? "JUMONG POS" : company.ToUpper();

        lines.Add(new LineEntry { Text = header, Align = TextAlign.Center, Bold = true, Spacing = 22 });
        lines.Add(new LineEntry { Text = "END SHIFT REPORT", Align = TextAlign.Center, Bold = true, Spacing = 18 });
        lines.Add(new LineEntry { Text = TimeHelper.Now.ToString("MMMM dd, yyyy  hh:mm tt"), Align = TextAlign.Center, Spacing = 14 });
        lines.Add(new LineEntry { Text = $"Cashier: {cashierName}", Spacing = 14 });
        lines.Add(new LineEntry { Text = new string('=', paperW / 3), Align = TextAlign.Center, Spacing = 14 });

        lines.Add(new LineEntry { Text = "SUMMARY", Bold = true, Spacing = 14 });
        lines.Add(new LineEntry { Text = "Total Sales", RightText = totalSales.ToString("N2"), Spacing = 14 });
        lines.Add(new LineEntry { Text = "Cash", RightText = totalCash.ToString("N2"), Spacing = 14 });
        lines.Add(new LineEntry { Text = "E-Wallet", RightText = totalEWallet.ToString("N2"), Spacing = 14 });
        lines.Add(new LineEntry { Text = "Credit", RightText = totalCredit.ToString("N2"), Spacing = 14 });
        lines.Add(new LineEntry { Text = "Voided", RightText = totalVoided.ToString("N2"), Spacing = 14 });
        lines.Add(new LineEntry { Text = new string('-', paperW / 3), Align = TextAlign.Center, Spacing = 12 });
        lines.Add(new LineEntry { Text = "Cash on Hand", RightText = cashOnHand.ToString("N2"), Spacing = 14 });
        lines.Add(new LineEntry { Text = "Difference", RightText = difference.ToString("N2"), Bold = true, Spacing = 18 });

        if (gcashTxns.Count > 0)
        {
            lines.Add(new LineEntry { Text = new string('=', paperW / 3), Align = TextAlign.Center, Spacing = 14 });
            lines.Add(new LineEntry { Text = "GCASH TRANSACTIONS", Bold = true, Spacing = 14 });
            foreach (var (inv, date, amt, refNo) in gcashTxns)
            {
                lines.Add(new LineEntry { Text = $"{date}  {inv}", Spacing = 12 });
                lines.Add(new LineEntry { Text = $"  Ref: {refNo}", RightText = amt.ToString("N2"), Spacing = 14 });
            }
        }

        if (creditCustomers.Count > 0)
        {
            lines.Add(new LineEntry { Text = new string('=', paperW / 3), Align = TextAlign.Center, Spacing = 14 });
            lines.Add(new LineEntry { Text = "CREDIT CUSTOMERS", Bold = true, Spacing = 14 });
            foreach (var (name, amt) in creditCustomers)
                lines.Add(new LineEntry { Text = name, RightText = amt.ToString("N2"), Spacing = 14 });
        }

        lines.Add(new LineEntry { Text = new string('=', paperW / 3), Align = TextAlign.Center, Spacing = 14 });
        var footer = GetSetting("ReceiptFooter");
        if (string.IsNullOrEmpty(footer)) footer = "Thank You! Come Again!";
        lines.Add(new LineEntry { Text = footer, Align = TextAlign.Center, Bold = true, Spacing = 20 });
        lines.Add(new LineEntry { Text = "", Spacing = 8 });

        return lines;
    }

    public static void PrintAuditEndShiftReport(decimal cashOnHand, decimal difference, string cashierName, DateTime timestamp, string notes,
        decimal totalSales, decimal totalCash, decimal totalEWallet, decimal totalCredit, decimal totalVoided,
        List<Expense> expenses, List<(string InvoiceNo, string SaleDate, decimal Amount, string ReferenceNo)> gcashTxns,
        List<(string Name, decimal Amount)> creditCustomers, List<(string CustomerName, string PaymentMethod, decimal Amount, string Timestamp)> creditPayments,
        int denom1000, int denom500, int denom200, int denom100, int denom50, int denom20, decimal denomCoins,
        decimal totalInventoryCost = 0, decimal totalCostSold = 0, decimal totalStockReceivedCost = 0, decimal previousInventory = 0)
    {
        var printerName = GetSetting("PrinterName");
        if (string.IsNullOrEmpty(printerName))
        {
            MessageBox.Show("No printer configured. Go to Settings to set a printer.", "Printer Not Set",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printerName;

        var paperW = int.TryParse(GetSetting("PaperWidth"), out var pw) ? pw : 315;
        var marginL = int.TryParse(GetSetting("PrinterMarginLeft"), out var ml) ? ml : 0;
        var marginR = int.TryParse(GetSetting("PrinterMarginRight"), out var mr) ? mr : 0;

        var lineChars = (int)((paperW - marginL - marginR) * 12 / 100);
        if (lineChars < 20) lineChars = 20;
        if (lineChars > 48) lineChars = 48;

        var lines = BuildAuditEndShiftReportLines(cashOnHand, difference, cashierName, timestamp, notes, totalSales, totalCash, totalEWallet, totalCredit, totalVoided, expenses, gcashTxns, creditCustomers, creditPayments, lineChars, denom1000, denom500, denom200, denom100, denom50, denom20, denomCoins, totalInventoryCost, totalCostSold, totalStockReceivedCost, previousInventory);
        ExtendPaperIfNeeded(doc, lines.Count, 16);

        doc.PrintPage += (sender, e) =>
        {
            var pageW = e.PageBounds.Width;
            var leftMargin = pageW * marginL / paperW;
            var rightMargin = pageW * marginR / paperW;
            var printW = pageW - leftMargin - rightMargin;
            var sf = StringFormat.GenericTypographic;

            using var font9 = new Font("Courier New", 9);
            using var font9B = new Font("Courier New", 9, FontStyle.Bold);
            using var font11 = new Font("Courier New", 11);

            var y = 5f;

            foreach (var entry in lines)
            {
                Font f;
                if (entry.Align == TextAlign.Center)
                    f = font11;
                else
                    f = entry.Bold ? font9B : font9;

                if (entry.RightText != null)
                {
                    e.Graphics!.DrawString(entry.Text, f, Brushes.Black, leftMargin, y, sf);
                    var rw = e.Graphics.MeasureString(entry.RightText, f, int.MaxValue, sf).Width;
                    e.Graphics.DrawString(entry.RightText, f, Brushes.Black, leftMargin + printW - rw, y, sf);
                }
                else if (entry.Align == TextAlign.Center)
                {
                    var tw = e.Graphics!.MeasureString(entry.Text, f, int.MaxValue, sf).Width;
                    e.Graphics.DrawString(entry.Text, f, Brushes.Black, leftMargin + (printW - tw) / 2, y, sf);
                }
                else
                {
                    e.Graphics!.DrawString(entry.Text, f, Brushes.Black, leftMargin, y, sf);
                }

                y += entry.Spacing;
            }
            e.HasMorePages = false;
        };

        try { doc.Print(); }
        catch (Exception ex)
        {
            MessageBox.Show($"Print error: {ex.Message}", "Print Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static List<LineEntry> BuildAuditEndShiftReportLines(decimal cashOnHand, decimal difference, string cashierName, DateTime timestamp, string notes,
        decimal totalSales, decimal totalCash, decimal totalEWallet, decimal totalCredit, decimal totalVoided,
        List<Expense> expenses, List<(string InvoiceNo, string SaleDate, decimal Amount, string ReferenceNo)> gcashTxns,
        List<(string Name, decimal Amount)> creditCustomers, List<(string CustomerName, string PaymentMethod, decimal Amount, string Timestamp)> creditPayments, int lineChars,
        int denom1000, int denom500, int denom200, int denom100, int denom50, int denom20, decimal denomCoins,
        decimal totalInventoryCost = 0, decimal totalCostSold = 0, decimal totalStockReceivedCost = 0, decimal previousInventory = 0)
    {
        var lines = new List<LineEntry>();
        var company = GetSetting("CompanyName");
        var header = string.IsNullOrEmpty(company) ? "JUMONG POS" : company.ToUpper();

        lines.Add(new LineEntry { Text = header, Align = TextAlign.Center, Bold = true, Spacing = 22 });
        lines.Add(new LineEntry { Text = "END SHIFT AUDIT REPORT", Align = TextAlign.Center, Bold = true, Spacing = 18 });
        lines.Add(new LineEntry { Text = timestamp.ToString("MMMM dd, yyyy  hh:mm tt"), Align = TextAlign.Center, Spacing = 14 });
        lines.Add(new LineEntry { Text = $"Cashier: {cashierName}", Spacing = 14 });
        lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });

        lines.Add(new LineEntry { Text = "SALES SUMMARY", Bold = true, Spacing = 14 });
        lines.Add(new LineEntry { Text = "Total Sales", RightText = totalSales.ToString("N2"), Spacing = 14 });
        lines.Add(new LineEntry { Text = "Cash Sales", RightText = totalCash.ToString("N2"), Spacing = 14 });
        lines.Add(new LineEntry { Text = "E-Wallet Sales", RightText = totalEWallet.ToString("N2"), Spacing = 14 });
        lines.Add(new LineEntry { Text = "Credit Sales", RightText = totalCredit.ToString("N2"), Spacing = 14 });
        lines.Add(new LineEntry { Text = "Less Expenses", RightText = expenses.Sum(e => e.Amount).ToString("N2"), Spacing = 14 });
        lines.Add(new LineEntry { Text = "Voided/Refunded", RightText = totalVoided.ToString("N2"), Spacing = 18 });

        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Align = TextAlign.Center, Spacing = 12 });
        lines.Add(new LineEntry { Text = "CASH ON HAND", Bold = true, Spacing = 14 });
        if (denom1000 > 0) lines.Add(new LineEntry { Text = "  1000  x", RightText = denom1000.ToString(), Spacing = 12 });
        if (denom500 > 0) lines.Add(new LineEntry { Text = "  500   x", RightText = denom500.ToString(), Spacing = 12 });
        if (denom200 > 0) lines.Add(new LineEntry { Text = "  200   x", RightText = denom200.ToString(), Spacing = 12 });
        if (denom100 > 0) lines.Add(new LineEntry { Text = "  100   x", RightText = denom100.ToString(), Spacing = 12 });
        if (denom50 > 0) lines.Add(new LineEntry { Text = "  50    x", RightText = denom50.ToString(), Spacing = 12 });
        if (denom20 > 0) lines.Add(new LineEntry { Text = "  20    x", RightText = denom20.ToString(), Spacing = 12 });
        if (denomCoins > 0) lines.Add(new LineEntry { Text = "  Coins", RightText = denomCoins.ToString("N2"), Spacing = 12 });
        lines.Add(new LineEntry { Text = "Counted Cash Drop", RightText = cashOnHand.ToString("N2"), Bold = true, Spacing = 18 });

        lines.Add(new LineEntry { Text = "VARIANCE", Bold = true, Spacing = 14 });
        lines.Add(new LineEntry { Text = "Difference", RightText = difference.ToString("N2"), Bold = true, Spacing = 14 });
        lines.Add(new LineEntry { Text = $"({(difference >= 0 ? "OVER" : "SHORT")})", Align = TextAlign.Center, Bold = true, Spacing = 18 });

        if (expenses.Count > 0)
        {
            lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });
            lines.Add(new LineEntry { Text = "SHIFT EXPENSES", Bold = true, Spacing = 14 });
            foreach (var exp in expenses)
            {
                lines.Add(new LineEntry { Text = $"{exp.Timestamp[..16]}  {exp.Category}", Bold = true, Spacing = 12 });
                lines.Add(new LineEntry { Text = $"  {exp.Description}", Spacing = 12 });
                if (!string.IsNullOrEmpty(exp.ReferenceNo))
                    lines.Add(new LineEntry { Text = $"  Ref: {exp.ReferenceNo}", Spacing = 12 });
                lines.Add(new LineEntry { Text = "  Amount", RightText = exp.Amount.ToString("N2"), Spacing = 14 });
                lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Align = TextAlign.Center, Spacing = 8 });
            }
            var totalExp = expenses.Sum(e => e.Amount);
            lines.Add(new LineEntry { Text = "TOTAL EXPENSES", RightText = totalExp.ToString("N2"), Bold = true, Spacing = 18 });
        }

        if (gcashTxns.Count > 0)
        {
            lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });
            lines.Add(new LineEntry { Text = "E-WALLET / GCASH REGISTRY", Bold = true, Spacing = 14 });
            foreach (var (inv, date, amt, refNo) in gcashTxns)
            {
                lines.Add(new LineEntry { Text = $"{date}  {inv}", Spacing = 12 });
                lines.Add(new LineEntry { Text = $"  Ref: {refNo}", RightText = amt.ToString("N2"), Spacing = 14 });
            }
            var totalGcash = gcashTxns.Sum(t => t.Amount);
            lines.Add(new LineEntry { Text = "TOTAL E-WALLET", RightText = totalGcash.ToString("N2"), Bold = true, Spacing = 18 });
        }

        if (creditCustomers.Count > 0)
        {
            lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });
            lines.Add(new LineEntry { Text = "EXTENDED STORE CREDIT", Bold = true, Spacing = 14 });
            foreach (var (name, amt) in creditCustomers)
                lines.Add(new LineEntry { Text = name, RightText = amt.ToString("N2"), Spacing = 14 });
            var creditTotal = creditCustomers.Sum(t => t.Amount);
            lines.Add(new LineEntry { Text = "TOTAL CREDIT SALES", RightText = creditTotal.ToString("N2"), Bold = true, Spacing = 18 });
        }

        if (creditPayments.Count > 0)
        {
            lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });
            lines.Add(new LineEntry { Text = "DEBT COLLECTIONS (PAID CREDIT)", Bold = true, Spacing = 14 });
            foreach (var (cust, payType, amt, ts) in creditPayments)
            {
                var payLabel = payType == "Cash" ? "CASH" : "WALLET";
                lines.Add(new LineEntry { Text = $"{cust} [{payLabel}]", Spacing = 12 });
                lines.Add(new LineEntry { Text = $"  {ts[..16]}", RightText = amt.ToString("N2"), Spacing = 14 });
            }
            var totalCollected = creditPayments.Sum(t => t.Amount);
            lines.Add(new LineEntry { Text = "TOTAL COLLECTED", RightText = totalCollected.ToString("N2"), Bold = true, Spacing = 18 });
        }

        // Inventory Reconciliation
        if (previousInventory > 0 || totalCostSold > 0 || totalStockReceivedCost > 0 || totalInventoryCost > 0)
        {
            var expected = previousInventory + totalStockReceivedCost - totalCostSold;
            var variance = totalInventoryCost - expected;
            lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });
            lines.Add(new LineEntry { Text = "INVENTORY RECONCILIATION", Bold = true, Spacing = 14 });
            lines.Add(new LineEntry { Text = "Previous Inventory", RightText = previousInventory.ToString("N2"), Spacing = 14 });
            if (totalStockReceivedCost > 0)
                lines.Add(new LineEntry { Text = "+ Stock Received", RightText = totalStockReceivedCost.ToString("N2"), Spacing = 14 });
            lines.Add(new LineEntry { Text = "- Cost of Goods Sold", RightText = $"({totalCostSold.ToString("N2")})", Spacing = 14 });
            lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Align = TextAlign.Center, Spacing = 12 });
            lines.Add(new LineEntry { Text = "Expected Inventory", RightText = expected.ToString("N2"), Bold = true, Spacing = 14 });
            lines.Add(new LineEntry { Text = "Actual Inventory", RightText = totalInventoryCost.ToString("N2"), Bold = true, Spacing = 14 });
            lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Align = TextAlign.Center, Spacing = 12 });
            var label = variance == 0 ? "✔ BALANCED" : variance > 0 ? $"⚠ OVER by {variance:N2}" : $"❌ SHORT by {Math.Abs(variance):N2}";
            lines.Add(new LineEntry { Text = label, Align = TextAlign.Center, Bold = true, Spacing = 18 });
        }

        lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });
        if (!string.IsNullOrWhiteSpace(notes))
        {
            lines.Add(new LineEntry { Text = $"Notes: {notes}", Spacing = 14 });
            lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Align = TextAlign.Center, Spacing = 12 });
        }
        lines.Add(new LineEntry { Text = "", Spacing = 30 });
        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Align = TextAlign.Center, Spacing = 12 });
        lines.Add(new LineEntry { Text = "Cashier Signature Over Printed Name", Align = TextAlign.Center, Bold = true, Spacing = 30 });
        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Align = TextAlign.Center, Spacing = 14 });
        lines.Add(new LineEntry { Text = "", Spacing = 8 });

        return lines;
    }

    public static void PrintBlindEndShiftSlip(decimal cashOnHand, string cashierName, DateTime timestamp, string notes)
    {
        var printerName = GetSetting("PrinterName");
        if (string.IsNullOrEmpty(printerName))
        {
            MessageBox.Show("No printer configured. Go to Settings to set a printer.", "Printer Not Set",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printerName;

        var paperW = int.TryParse(GetSetting("PaperWidth"), out var pw) ? pw : 315;
        var marginL = int.TryParse(GetSetting("PrinterMarginLeft"), out var ml) ? ml : 0;
        var marginR = int.TryParse(GetSetting("PrinterMarginRight"), out var mr) ? mr : 0;

        var lineChars = (int)((paperW - marginL - marginR) * 12 / 100);
        if (lineChars < 20) lineChars = 20;
        if (lineChars > 48) lineChars = 48;

        var lines = BuildBlindEndShiftSlipLines(cashOnHand, cashierName, timestamp, notes, lineChars);
        ExtendPaperIfNeeded(doc, lines.Count);

        doc.PrintPage += (sender, e) =>
        {
            var pageW = e.PageBounds.Width;
            var leftMargin = pageW * marginL / paperW;
            var rightMargin = pageW * marginR / paperW;
            var printW = pageW - leftMargin - rightMargin;
            var sf = StringFormat.GenericTypographic;

            using var font9 = new Font("Courier New", 9);
            using var font9B = new Font("Courier New", 9, FontStyle.Bold);
            using var font11 = new Font("Courier New", 11);

            var y = 5f;

            foreach (var entry in lines)
            {
                Font f;
                if (entry.Align == TextAlign.Center)
                    f = font11;
                else
                    f = entry.Bold ? font9B : font9;

                if (entry.RightText != null)
                {
                    e.Graphics!.DrawString(entry.Text, f, Brushes.Black, leftMargin, y, sf);
                    var rw = e.Graphics.MeasureString(entry.RightText, f, int.MaxValue, sf).Width;
                    e.Graphics.DrawString(entry.RightText, f, Brushes.Black, leftMargin + printW - rw, y, sf);
                }
                else if (entry.Align == TextAlign.Center)
                {
                    var tw = e.Graphics!.MeasureString(entry.Text, f, int.MaxValue, sf).Width;
                    e.Graphics.DrawString(entry.Text, f, Brushes.Black, leftMargin + (printW - tw) / 2, y, sf);
                }
                else
                {
                    e.Graphics!.DrawString(entry.Text, f, Brushes.Black, leftMargin, y, sf);
                }

                y += entry.Spacing;
            }
            e.HasMorePages = false;
        };

        try { doc.Print(); }
        catch (Exception ex)
        {
            MessageBox.Show($"Print error: {ex.Message}", "Print Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static void PrintStockReceiving(List<(int ProductId, string ProductName, string Barcode, int StockBefore, int Qty)> items, string cashierName, string reference)
    {
        var printerName = GetSetting("PrinterName");
        if (string.IsNullOrEmpty(printerName))
        {
            MessageBox.Show("No printer configured. Go to Settings to set a printer.", "Printer Not Set",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printerName;

        var paperW = int.TryParse(GetSetting("PaperWidth"), out var pw) ? pw : 315;
        var marginL = 2;
        var marginR = 2;

        var lineChars = (int)((paperW - marginL - marginR) * 13 / 100);
        if (lineChars < 24) lineChars = 24;
        if (lineChars > 48) lineChars = 48;

        var lines = new List<LineEntry>();
        var company = GetSetting("CompanyName");
        var header = string.IsNullOrEmpty(company) ? "JUMONG POS" : company.ToUpper();
        var address = GetSetting("CompanyAddress");
        var mobile = GetSetting("CompanyMobile");

        lines.Add(new LineEntry { Text = header, Align = TextAlign.Center, Bold = true, Spacing = 22 });
        if (!string.IsNullOrEmpty(address))
            lines.Add(new LineEntry { Text = address, Align = TextAlign.Center, Spacing = 14 });
        if (!string.IsNullOrEmpty(mobile))
            lines.Add(new LineEntry { Text = mobile, Align = TextAlign.Center, Spacing = 14 });
        lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });
        lines.Add(new LineEntry { Text = "STOCK RECEIVING", Align = TextAlign.Center, Bold = true, Spacing = 18 });
        lines.Add(new LineEntry { Text = TimeHelper.Now.ToString("yyyy-MM-dd HH:mm"), Align = TextAlign.Center, Spacing = 14 });
        lines.Add(new LineEntry { Text = $"Received by: {cashierName}", Spacing = 14 });
        if (!string.IsNullOrEmpty(reference))
        lines.Add(new LineEntry { Text = $"Ref: {reference}", Spacing = 14 });
        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Spacing = 12 });

        int statsWidth = 12; 
        int safetyMargin = -2;
        int maxNameWidth = lineChars - statsWidth - safetyMargin;

        if (maxNameWidth < 10) maxNameWidth = 10;

        string headerNamePart = "Item".PadRight(maxNameWidth);
        string headerStatsPart = string.Format("{0,4}{1,4}{2,4}", "Cur", "Rcv", "New");
        lines.Add(new LineEntry { Text = headerNamePart + headerStatsPart, Bold = true, Spacing = 14 });

        foreach (var (_, name, _, stockBefore, qty) in items)
        {
            var newStock = stockBefore + qty;
            var displayName = name.Length > maxNameWidth ? name[..(maxNameWidth - 2)] + ".." : name.PadRight(maxNameWidth);
            string statsPart = string.Format("{0,4}{1,4}{2,4}", stockBefore, qty, newStock);
            lines.Add(new LineEntry { Text = displayName + statsPart, Spacing = 14 });
        }

        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Spacing = 12 });
        lines.Add(new LineEntry { Text = $"Total Items: {items.Count}", RightText = items.Sum(i => i.Qty).ToString(), Bold = true, Spacing = 16 });
        lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 20 });
        lines.Add(new LineEntry { Text = "STOCK RECEIVED", Align = TextAlign.Center, Bold = true, Spacing = 18 });
        lines.Add(new LineEntry { Text = "", Spacing = 8 });
        ExtendPaperIfNeeded(doc, lines.Count);

        doc.PrintPage += (sender, e) =>
        {
            var pageW = e.PageBounds.Width;
            var leftMargin = pageW * marginL / paperW;
            var rightMargin = pageW * marginR / paperW;
            var printW = pageW - leftMargin - rightMargin;
            var sf = StringFormat.GenericTypographic;

            using var font8 = new Font("Courier New", 8);
            using var font8B = new Font("Courier New", 8, FontStyle.Bold);
            using var font9B = new Font("Courier New", 9, FontStyle.Bold);

            var y = 5f;

            foreach (var entry in lines)
            {
                Font f;
                if (entry.Align == TextAlign.Center)
                    f = font9B;
                else
                    f = entry.Bold ? font8B : font8;

                if (entry.RightText != null)
                {
                    e.Graphics!.DrawString(entry.Text, f, Brushes.Black, leftMargin, y, sf);
                    var rw = e.Graphics.MeasureString(entry.RightText, f, int.MaxValue, sf).Width;
                    e.Graphics.DrawString(entry.RightText, f, Brushes.Black, leftMargin + printW - rw, y, sf);
                }
                else if (entry.Align == TextAlign.Center)
                {
                    var tw = e.Graphics!.MeasureString(entry.Text, f, int.MaxValue, sf).Width;
                    e.Graphics.DrawString(entry.Text, f, Brushes.Black, leftMargin + (printW - tw) / 2, y, sf);
                }
                else
                {
                    e.Graphics!.DrawString(entry.Text, f, Brushes.Black, leftMargin, y, sf);
                }

                y += entry.Spacing;
            }
            e.HasMorePages = false;
        };

        try { doc.Print(); }
        catch (Exception ex)
        {
            MessageBox.Show($"Print error: {ex.Message}", "Print Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static void PrintStockReceivingHistory(List<StockTrail> trailEntries, string? filter = null, string? dateLabel = null)
    {
        var printerName = GetSetting("PrinterName");
        if (string.IsNullOrEmpty(printerName))
        {
            MessageBox.Show("No printer configured. Go to Settings to set a printer.", "Printer Not Set",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printerName;

        var paperW = int.TryParse(GetSetting("PaperWidth"), out var pw) ? pw : 315;
        var marginL = 2;
        var marginR = 2;

        var lineChars = (int)((paperW - marginL - marginR) * 13 / 100);
        if (lineChars < 24) lineChars = 24;
        if (lineChars > 48) lineChars = 48;

        var receivingOnly = trailEntries.Where(t => t.QuantityAdded > 0).ToList();

        var lines = new List<LineEntry>();
        var company = GetSetting("CompanyName");
        var header = string.IsNullOrEmpty(company) ? "JUMONG POS" : company.ToUpper();

        lines.Add(new LineEntry { Text = header, Align = TextAlign.Center, Bold = true, Spacing = 22 });
        lines.Add(new LineEntry { Text = "STOCK RECEIVING LOG", Align = TextAlign.Center, Bold = true, Spacing = 18 });
        lines.Add(new LineEntry { Text = dateLabel ?? TimeHelper.Now.ToString("yyyy-MM-dd HH:mm"), Align = TextAlign.Center, Spacing = 14 });
        if (!string.IsNullOrEmpty(filter))
            lines.Add(new LineEntry { Text = $"Filter: {filter}", Spacing = 14 });
        lines.Add(new LineEntry { Text = $"{receivingOnly.Count} entries", Spacing = 14 });
        lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });

        int statsWidth = 12;
        int safetyMargin = -2;
        int maxNameWidth = lineChars - statsWidth - safetyMargin;

        if (maxNameWidth < 10) maxNameWidth = 10;

        string headerNamePart = "Item".PadRight(maxNameWidth);
        string headerStatsPart = string.Format("{0,4}{1,4}{2,4}", "Cur", "Rcv", "New");
        lines.Add(new LineEntry { Text = headerNamePart + headerStatsPart, Bold = true, Spacing = 14 });

        foreach (var entry in receivingOnly)
        {
            var name = entry.ProductName;
            var displayName = name.Length > maxNameWidth ? name[..(maxNameWidth - 2)] + ".." : name.PadRight(maxNameWidth);
            string statsPart = string.Format("{0,4}{1,4}{2,4}", entry.StockBefore, (int)entry.QuantityAdded, entry.StockAfter);
            lines.Add(new LineEntry { Text = displayName + statsPart, Spacing = 14 });
        }

        lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 20 });
        lines.Add(new LineEntry { Text = $"Total: {receivingOnly.Sum(t => t.QuantityAdded)} items", Bold = true, Spacing = 14 });
        lines.Add(new LineEntry { Text = "", Spacing = 8 });
        ExtendPaperIfNeeded(doc, lines.Count);

        doc.PrintPage += (sender, e) =>
        {
            var pageW = e.PageBounds.Width;
            var leftMargin = pageW * marginL / paperW;
            var rightMargin = pageW * marginR / paperW;
            var printW = pageW - leftMargin - rightMargin;
            var sf = StringFormat.GenericTypographic;

            using var font8 = new Font("Courier New", 8);
            using var font8B = new Font("Courier New", 8, FontStyle.Bold);
            using var font9B = new Font("Courier New", 9, FontStyle.Bold);

            var y = 5f;

            foreach (var entry in lines)
            {
                Font f;
                if (entry.Align == TextAlign.Center)
                    f = font9B;
                else
                    f = entry.Bold ? font8B : font8;

                if (entry.RightText != null)
                {
                    e.Graphics!.DrawString(entry.Text, f, Brushes.Black, leftMargin, y, sf);
                    var rw = e.Graphics.MeasureString(entry.RightText, f, int.MaxValue, sf).Width;
                    e.Graphics.DrawString(entry.RightText, f, Brushes.Black, leftMargin + printW - rw, y, sf);
                }
                else if (entry.Align == TextAlign.Center)
                {
                    var tw = e.Graphics!.MeasureString(entry.Text, f, int.MaxValue, sf).Width;
                    e.Graphics.DrawString(entry.Text, f, Brushes.Black, leftMargin + (printW - tw) / 2, y, sf);
                }
                else
                {
                    e.Graphics!.DrawString(entry.Text, f, Brushes.Black, leftMargin, y, sf);
                }

                y += entry.Spacing;
            }
            e.HasMorePages = false;
        };

        try { doc.Print(); }
        catch (Exception ex)
        {
            MessageBox.Show($"Print error: {ex.Message}", "Print Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static List<LineEntry> BuildBlindEndShiftSlipLines(decimal cashOnHand, string cashierName, DateTime timestamp, string notes, int lineChars)
    {
        var lines = new List<LineEntry>();
        var company = GetSetting("CompanyName");
        var header = string.IsNullOrEmpty(company) ? "JUMONG POS" : company.ToUpper();

        lines.Add(new LineEntry { Text = header, Align = TextAlign.Center, Bold = true, Spacing = 22 });
        lines.Add(new LineEntry { Text = "SHIFT CASH DROP SLIP", Align = TextAlign.Center, Bold = true, Spacing = 18 });
        lines.Add(new LineEntry { Text = timestamp.ToString("MMMM dd, yyyy  hh:mm tt"), Align = TextAlign.Center, Spacing = 14 });
        lines.Add(new LineEntry { Text = $"Cashier: {cashierName}", Spacing = 14 });
        lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });

        lines.Add(new LineEntry { Text = "COUNTED CASH ON HAND", Bold = true, Spacing = 14 });
        lines.Add(new LineEntry { Text = "Total Cash Drop", RightText = cashOnHand.ToString("N2"), Bold = true, Spacing = 18 });

        if (!string.IsNullOrWhiteSpace(notes))
        {
            lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Align = TextAlign.Center, Spacing = 12 });
            lines.Add(new LineEntry { Text = $"Notes: {notes}", Spacing = 14 });
        }

        lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 20 });
        lines.Add(new LineEntry { Text = "", Spacing = 30 });
        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Align = TextAlign.Center, Spacing = 12 });
        lines.Add(new LineEntry { Text = "Cashier Signature Over Printed Name", Align = TextAlign.Center, Bold = true, Spacing = 30 });
        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Align = TextAlign.Center, Spacing = 14 });
        lines.Add(new LineEntry { Text = "", Spacing = 8 });

        return lines;
    }

    private static List<string> WrapText(string text, int width)
    {
        var lines = new List<string>();
        while (text.Length > width)
        {
            var cut = width;
            var space = text.LastIndexOf(' ', width);
            if (space > width / 2) cut = space;
            lines.Add(text[..cut].TrimEnd());
            text = text[cut..].TrimStart();
        }
        if (text.Length > 0) lines.Add(text);
        return lines;
    }

    private static string GetSetting(string key)
    {
        using var conn = Data.DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT Value FROM Settings WHERE Key = @key";
        using var cmd = new System.Data.SQLite.SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@key", key);
        var val = cmd.ExecuteScalar();
        return val?.ToString() ?? "";
    }

    public static void PrintWhReceipt(int saleId, string customerName, List<(string ProductName, string UnitName, int Qty, decimal Price, decimal Subtotal)> items, decimal grandTotal, string cashierName, string invoiceNo = "")
    {
        var printer = GetSetting("PrinterName");
        if (string.IsNullOrEmpty(printer)) { MessageBox.Show("No printer configured. Go to Settings to set a printer.", "Printer Not Set", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        var paperWidth = int.TryParse(GetSetting("PaperWidth"), out var pw) ? pw : 280;
        var marginLeft = int.TryParse(GetSetting("MarginLeft"), out var ml) ? ml : 0;
        var marginRight = int.TryParse(GetSetting("MarginRight"), out var mr) ? mr : 0;
        var chars = (paperWidth - marginLeft - marginRight) / 9;

        var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printer;
        doc.DefaultPageSettings.PaperSize = new PaperSize("Custom", paperWidth, 3000);
        doc.DefaultPageSettings.Margins = new Margins(marginLeft, marginRight, 0, 0);

        if (chars < 24) chars = 24;

        var lines = new List<string[]>();
        void AddLine(string text, bool bold = false, string right = "")
        {
            lines.Add(new[] { text, right, bold ? "1" : "0" });
        }

        var companyName = GetSetting("CompanyName");
        var address = GetSetting("CompanyAddress");
        var mobile = GetSetting("CompanyMobile");
        var footer = GetSetting("ReceiptFooter");
        if (string.IsNullOrEmpty(footer)) footer = "Thank You! Come Again!";

        var header = string.IsNullOrEmpty(companyName) ? "WAREHOUSE SALE" : companyName.ToUpper();
        AddLine("");
        AddLine(header, true);
        if (!string.IsNullOrEmpty(address)) AddLine(address);
        if (!string.IsNullOrEmpty(mobile)) AddLine("Mobile: " + mobile);
        AddLine("─── WALK-IN SALE ───", true);
        AddLine("Sale #" + saleId);
        if (!string.IsNullOrEmpty(invoiceNo)) AddLine("Invoice: " + invoiceNo);
        AddLine("Customer: " + customerName);
        AddLine("Cashier: " + cashierName);
        AddLine(DateTime.Now.ToString("MMM dd, yyyy  hh:mm tt"));
        AddLine(new string('─', Math.Min(chars, 40)));
        AddLine("ITEMS", true);
        AddLine("");

        foreach (var item in items)
        {
            var name = item.ProductName + " (" + item.UnitName + ")";
            if (name.Length > chars - 8) name = name[..(chars - 11)] + "...";
            AddLine(name);
            var qtyLine = $"  {item.Qty} x ₱{item.Price:N2}";
            var sub = $"₱{item.Subtotal:N2}";
            var pad = Math.Max(0, chars - qtyLine.Length - sub.Length);
            AddLine(qtyLine + new string(' ', pad) + sub);
        }

        AddLine(new string('─', Math.Min(chars, 40)));
        AddLine("TOTAL: ₱" + grandTotal.ToString("N2"), true);

        if (!string.IsNullOrEmpty(footer))
        {
            AddLine("");
            AddLine(footer, true);
        }
        AddLine("");

        var font = new Font("Courier New", 9F);
        var fontBold = new Font("Courier New", 9F, FontStyle.Bold);
        var lineHeight = font.Height + 3;

        ExtendPaperIfNeeded(doc, lines.Count, lineHeight);

        doc.PrintPage += (_, e) =>
        {
            var y = 0;
            foreach (var line in lines)
            {
                var f = line[2] == "1" ? fontBold : font;
                e.Graphics.DrawString(line[0], f, Brushes.Black, 0, y);
                if (!string.IsNullOrEmpty(line[1]))
                {
                    var rw = e.Graphics.MeasureString(line[1], f).Width;
                    e.Graphics.DrawString(line[1], f, Brushes.Black, e.PageBounds.Width - marginRight - rw, y);
                }
                y += lineHeight;
            }
        };

        try { doc.Print(); }
        catch (Exception ex) { MessageBox.Show("Print error: " + ex.Message, "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}


