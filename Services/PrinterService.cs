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

    public static void PrintReceipt(Sale sale, string cashierName = "Admin", Customer? customer = null, bool includeShopQr = true, int? ptsPrevious = null, int? ptsUsed = null)
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

        var showQr = includeShopQr &&
            (SyncService.StoreId == "STORE-20260602-7159" || SyncService.StoreId == "STORE-20260602-AA36");
        var lines = BuildReceiptLines(sale, cashierName, customer, lineChars, showQr, ptsPrevious, ptsUsed);
        ExtendPaperIfNeeded(doc, lines.Count + (lines.Any(x => x.IsQr) ? 30 : 0));

        doc.PrintPage += (sender, e) =>
        {
            var pageW = e.PageBounds.Width;
            var leftMargin = pageW * marginL / paperW;
            var rightMargin = pageW * marginR / paperW;

            var printW = pageW - leftMargin - rightMargin;
            var sf = StringFormat.GenericTypographic;

            using var font9B = new Font("Courier New", 9, FontStyle.Bold);

            var y = 5f;

            foreach (var entry in lines)
            {
                if (entry.IsQr)
                {
                    y += DrawShopQr(e.Graphics!, leftMargin, y, printW);
                    continue;
                }

                Font f;
                if (entry.Align == TextAlign.Center)
                    f = font9B;
                else
                    f = entry.Bold ? font9B : font9B;

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
        public bool IsQr { get; set; }
    }

    private static List<LineEntry> BuildReceiptLines(Sale sale, string cashierName, Customer? customer = null, int lineChars = 32, bool includeShopQr = true, int? ptsPrevious = null, int? ptsUsed = null)
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

        var totalQty = sale.Items.Where(x => !x.IsVoided).Sum(x => x.Quantity);
        var voidedQty = sale.Items.Where(x => x.IsVoided).Sum(x => x.Quantity);
        lines.Add(new LineEntry { Text = $"Total Items: {totalQty}", RightText = $"{sale.Items.Count} line(s)", Bold = true, Spacing = 14 });
        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Spacing = 12 });

        foreach (var item in sale.Items)
        {
            var voided = item.IsVoided;
            // Unit name sa resibo: isang barcode ay pwedeng maraming pack forms (by 5/by 10/box) —
            // ipakita kung aling unit ang nabenta (skip ang pc/per piece para hindi maingay).
            var displayName = item.ProductName;
            if (!string.IsNullOrEmpty(item.UnitName)
                && !item.UnitName.Equals("pc", StringComparison.OrdinalIgnoreCase)
                && !item.UnitName.Equals("per piece", StringComparison.OrdinalIgnoreCase))
                displayName += " (" + item.UnitName + ")";
            var nameLines = WrapText(displayName, lineChars - (voided ? 9 : 0));
            for (int i = 0; i < nameLines.Count; i++)
            {
                var txt = i == 0 ? nameLines[i] : "  " + nameLines[i];
                if (voided && i == 0) txt += " [VOIDED]";
                lines.Add(new LineEntry { Text = txt, Bold = true, Spacing = 14 });
            }
            lines.Add(new LineEntry
            {
                Text = $"  {item.Quantity}x {item.Price:N2}",
                RightText = voided ? $"({item.TotalPrice.ToString("N2")})" : item.TotalPrice.ToString("N2"),
                Spacing = 16
            });
        }

        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Spacing = 12 });
        lines.Add(new LineEntry { Text = "Sub Total", RightText = sale.Items.Where(x => !x.IsVoided).Sum(x => x.TotalPrice).ToString("N2"), Spacing = 14 });

        if (sale.Discount > 0)
            lines.Add(new LineEntry { Text = "Discount", RightText = sale.Discount.ToString("N2"), Spacing = 14 });

        if (sale.Tax > 0)
            lines.Add(new LineEntry { Text = "Tax", RightText = sale.Tax.ToString("N2"), Spacing = 14 });

        lines.Add(new LineEntry { Text = "TOTAL", RightText = sale.Items.Where(x => !x.IsVoided).Sum(x => x.TotalPrice).ToString("N2"), Bold = true, Spacing = 18 });

        var voidedTotal = sale.Items.Where(x => x.IsVoided).Sum(x => x.TotalPrice);
        if (voidedTotal > 0)
            lines.Add(new LineEntry { Text = $"VOIDED ({voidedQty} pc)", RightText = $"({voidedTotal.ToString("N2")})", Bold = true, Spacing = 14 });

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

        // Loyalty points: STAR members (QR code) only - show previous / earned / new balance
        var ptsEarned = sale.Items.Where(x => !x.IsVoided).Sum(x => x.PointsEarned);
        if (customer != null && !string.IsNullOrEmpty(customer.QrCode) && (ptsEarned > 0 || (ptsUsed ?? 0) > 0))
        {
            lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Spacing = 12 });
            if (ptsPrevious.HasValue)
            {
                lines.Add(new LineEntry { Text = "POINTS", Bold = true, Spacing = 14 });
                lines.Add(new LineEntry { Text = "Previous", RightText = ptsPrevious.Value.ToString(), Spacing = 14 });
                if (ptsEarned > 0)
                    lines.Add(new LineEntry { Text = "Earned", RightText = "+" + ptsEarned.ToString(), Spacing = 14 });
                if ((ptsUsed ?? 0) > 0)
                    lines.Add(new LineEntry { Text = "Redeemed", RightText = "-" + ptsUsed.Value.ToString(), Spacing = 14 });
                lines.Add(new LineEntry { Text = "New Balance", RightText = customer.LoyaltyPoints.ToString(), Bold = true, Spacing = 14 });
            }
            else
            {
                lines.Add(new LineEntry { Text = "POINTS EARNED", RightText = "+" + ptsEarned.ToString(), Bold = true, Spacing = 14 });
                lines.Add(new LineEntry { Text = "Total Points", RightText = customer.LoyaltyPoints.ToString(), Spacing = 14 });
            }
            lines.Add(new LineEntry { Text = "Points redeemable sa susunod na bili", Spacing = 14 });
        }

        lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Spacing = 12 });
        lines.Add(new LineEntry { Text = footer, Align = TextAlign.Center, Bold = true, Spacing = 20 });

        if (includeShopQr)
        {
            lines.Add(new LineEntry { Text = "", Spacing = 10 });
            lines.Add(new LineEntry { Text = "Order online: shop.jumongdev.com", Align = TextAlign.Center, Bold = true, Spacing = 12 });
            lines.Add(new LineEntry { IsQr = true, Spacing = 28 });
            lines.Add(new LineEntry { Text = "Scan para sa aming online shop!", Align = TextAlign.Center, Bold = true, Spacing = 12 });
            lines.Add(new LineEntry { Text = "", Spacing = 8 });
            lines.Add(new LineEntry { Text = "PAANO MAG-REGISTER:", Align = TextAlign.Center, Bold = true, Spacing = 12 });
            lines.Add(new LineEntry { Text = "1. I-scan ang QR o i-type ang shop.jumongdev.com", Spacing = 12 });
            lines.Add(new LineEntry { Text = "2. I-click ang SIGN IN WITH GOOGLE", Spacing = 12 });
            lines.Add(new LineEntry { Text = "3. Piliin ang iyong Gmail account", Spacing = 12 });
            lines.Add(new LineEntry { Text = "4. Punuin ang pangalan at mobile number", Spacing = 12 });
            lines.Add(new LineEntry { Text = "5. Mag-order na - Cash on Delivery ang bayad!", Spacing = 14 });
            lines.Add(new LineEntry { Text = "", Spacing = 8 });
        }

        lines.Add(new LineEntry { Text = "", Spacing = 8 });

        return lines;
    }

    // shop.jumongdev.com QR matrix (25x25, EC-M) - verified scannable via jsQR decode test.
    // Drawn as graphics rectangles (not text) so thermal prints are crisp and phone-scannable.
    private const int ShopQrSize = 25;
    private static readonly string[] ShopQrMatrix = {
        "1111111000010100101111111","1000001000010111101000001","1011101010000000101011101","1011101010011110001011101","1011101010100000101011101","1000001011010111101000001","1111111010101010101111111","0000000010101011100000000","1011111001001110001111100","0110000111011000100100010","1101111000100011100111011","0110000111111001101000001","1010001100011111111110111","1010010111101000100101010","1001101110100011010111011","1010000011100010010110001","1001011101111110111110100","0000000010010011100011000","1111111001000110101010111","1000001011110000100011000","1011101010011101111110100","1011101011110101111011111","1011101010100110110001101","1000001001011001101111001","1111111010101111001111111"
    };

    private static float DrawShopQr(Graphics g, float x0, float y0, float printW)
    {
        const int quiet = 2;
        var module = (printW * 0.85f) / (ShopQrSize + quiet * 2);
        for (var r = 0; r < ShopQrSize; r++)
        {
            var row = ShopQrMatrix[r];
            for (var c = 0; c < ShopQrSize; c++)
            {
                if (row[c] != '1') continue;
                g.FillRectangle(Brushes.Black,
                    x0 + (c + quiet) * module, y0 + (r + quiet) * module, module, module);
            }
        }
        return (ShopQrSize + quiet * 2) * module;
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

            using var font9B = new Font("Courier New", 9, FontStyle.Bold);

            var y = 5f;

            foreach (var entry in lines)
            {
                Font f;
                if (entry.Align == TextAlign.Center)
                    f = font9B;
                else
                    f = entry.Bold ? font9B : font9B;

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
        decimal totalInventoryCost = 0, decimal totalCostSold = 0, decimal saleTrailsCost = 0, decimal totalStockReceivedCost = 0, decimal previousInventory = 0,
        decimal voidReturns = 0, decimal adjustDown = 0,
        decimal adjDownTransfers = 0, decimal adjDownEcom = 0, decimal adjDownMobile = 0,
        int mobileSales = 0, decimal mobileTotal = 0, int ecomOrders = 0, decimal ecomTotal = 0, int receivedPcs = 0, int transferOutPcs = 0,
        decimal ecomCollectedCash = 0, decimal ecomCollectedGcash = 0, decimal ecomRemitted = 0,
        (int Total, int Voided, int Deleted, decimal Lost, List<string> VoidedInvs, List<string> MissingInvs)? receiptAudit = null)
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

        var lines = BuildAuditEndShiftReportLines(cashOnHand, difference, cashierName, timestamp, notes, totalSales, totalCash, totalEWallet, totalCredit, totalVoided, expenses, gcashTxns, creditCustomers, creditPayments, lineChars, denom1000, denom500, denom200, denom100, denom50, denom20, denomCoins, totalInventoryCost, totalCostSold, saleTrailsCost, totalStockReceivedCost, previousInventory, voidReturns, adjustDown, adjDownTransfers, adjDownEcom, adjDownMobile, mobileSales, mobileTotal, ecomOrders, ecomTotal, receivedPcs, transferOutPcs, ecomCollectedCash, ecomCollectedGcash, ecomRemitted, receiptAudit);
        ExtendPaperIfNeeded(doc, lines.Count, 16);

        doc.PrintPage += (sender, e) =>
        {
            var pageW = e.PageBounds.Width;
            var leftMargin = pageW * marginL / paperW;
            var rightMargin = pageW * marginR / paperW;
            var printW = pageW - leftMargin - rightMargin;
            var sf = StringFormat.GenericTypographic;

            using var font9B = new Font("Courier New", 9, FontStyle.Bold);

            var y = 5f;

            foreach (var entry in lines)
            {
                Font f;
                if (entry.Align == TextAlign.Center)
                    f = font9B;
                else
                    f = entry.Bold ? font9B : font9B;

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
        decimal totalInventoryCost = 0, decimal totalCostSold = 0, decimal saleTrailsCost = 0, decimal totalStockReceivedCost = 0, decimal previousInventory = 0,
        decimal voidReturns = 0, decimal adjustDown = 0,
        decimal adjDownTransfers = 0, decimal adjDownEcom = 0, decimal adjDownMobile = 0,
        int mobileSales = 0, decimal mobileTotal = 0, int ecomOrders = 0, decimal ecomTotal = 0, int receivedPcs = 0, int transferOutPcs = 0,
        decimal ecomCollectedCash = 0, decimal ecomCollectedGcash = 0, decimal ecomRemitted = 0,
        (int Total, int Voided, int Deleted, decimal Lost, List<string> VoidedInvs, List<string> MissingInvs)? receiptAudit = null)
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
        if (denom1000 > 0) lines.Add(new LineEntry { Text = $"  1000  x  {denom1000}", RightText = $"= {(denom1000 * 1000m):N2}", Spacing = 12 });
        if (denom500 > 0) lines.Add(new LineEntry { Text = $"  500   x  {denom500}", RightText = $"= {(denom500 * 500m):N2}", Spacing = 12 });
        if (denom200 > 0) lines.Add(new LineEntry { Text = $"  200   x  {denom200}", RightText = $"= {(denom200 * 200m):N2}", Spacing = 12 });
        if (denom100 > 0) lines.Add(new LineEntry { Text = $"  100   x  {denom100}", RightText = $"= {(denom100 * 100m):N2}", Spacing = 12 });
        if (denom50 > 0) lines.Add(new LineEntry { Text = $"  50    x  {denom50}", RightText = $"= {(denom50 * 50m):N2}", Spacing = 12 });
        if (denom20 > 0) lines.Add(new LineEntry { Text = $"  20    x  {denom20}", RightText = $"= {(denom20 * 20m):N2}", Spacing = 12 });
        if (denomCoins > 0) lines.Add(new LineEntry { Text = "  Coins", RightText = $"= {denomCoins:N2}", Spacing = 12 });
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
            var cashPayments = creditPayments.Where(p => p.PaymentMethod == "Cash").ToList();
            var walletPayments = creditPayments.Where(p => p.PaymentMethod != "Cash").ToList();
            lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });
            lines.Add(new LineEntry { Text = "DEBT COLLECTIONS (PAID CREDIT)", Bold = true, Spacing = 14 });
            if (cashPayments.Count > 0)
            {
                foreach (var (cust, payType, amt, ts) in cashPayments)
                {
                    lines.Add(new LineEntry { Text = $"{cust} [CASH]", Spacing = 12 });
                    lines.Add(new LineEntry { Text = $"  {ts[..16]}", RightText = amt.ToString("N2"), Spacing = 14 });
                }
                lines.Add(new LineEntry { Text = "TOTAL COLLECTED (CASH)", RightText = cashPayments.Sum(t => t.Amount).ToString("N2"), Bold = true, Spacing = 18 });
            }
            if (walletPayments.Count > 0)
            {
                foreach (var (cust, payType, amt, ts) in walletPayments)
                {
                    lines.Add(new LineEntry { Text = $"{cust} [WALLET]", Spacing = 12 });
                    lines.Add(new LineEntry { Text = $"  {ts[..16]}", RightText = amt.ToString("N2"), Spacing = 14 });
                }
                lines.Add(new LineEntry { Text = "TOTAL COLLECTED (WALLET)", RightText = walletPayments.Sum(t => t.Amount).ToString("N2"), Bold = true, Spacing = 18 });
            }
            lines.Add(new LineEntry { Text = "TOTAL COLLECTED", RightText = creditPayments.Sum(t => t.Amount).ToString("N2"), Bold = true, Spacing = 18 });
        }

        // Inventory Reconciliation
        if (previousInventory > 0 || totalCostSold > 0 || saleTrailsCost != 0 || totalStockReceivedCost > 0 || totalInventoryCost > 0)
        {
            var expected = previousInventory + totalStockReceivedCost + voidReturns + saleTrailsCost + adjustDown;
            var variance = totalInventoryCost - expected;
            lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });
            lines.Add(new LineEntry { Text = "INVENTORY RECONCILIATION", Bold = true, Spacing = 14 });
            lines.Add(new LineEntry { Text = "Previous Inventory", RightText = previousInventory.ToString("N2"), Spacing = 14 });
            if (totalStockReceivedCost > 0)
                lines.Add(new LineEntry { Text = "+ Stock Received", RightText = totalStockReceivedCost.ToString("N2"), Spacing = 14 });
            if (voidReturns > 0)
                lines.Add(new LineEntry { Text = "+ Void Returns", RightText = voidReturns.ToString("N2"), Spacing = 14 });
            lines.Add(new LineEntry { Text = "- Sales (inv trails, current cost)", RightText = $"({Math.Abs(saleTrailsCost).ToString("N2")})", Spacing = 14 });
            if (adjDownTransfers != 0)
                lines.Add(new LineEntry { Text = "- Transfers out (HQ->POS)", RightText = $"({Math.Abs(adjDownTransfers).ToString("N2")})", Spacing = 14 });
            if (adjDownEcom != 0)
                lines.Add(new LineEntry { Text = "- E-commerce (holds)", RightText = $"({Math.Abs(adjDownEcom).ToString("N2")})", Spacing = 14 });
            if (adjDownMobile != 0)
                lines.Add(new LineEntry { Text = "- Mobile wholesale", RightText = $"({Math.Abs(adjDownMobile).ToString("N2")})", Spacing = 14 });
            var adjManual = Math.Abs(adjustDown) - Math.Abs(adjDownTransfers) - Math.Abs(adjDownEcom) - Math.Abs(adjDownMobile);
            if (adjManual > 0)
                lines.Add(new LineEntry { Text = "- Adjustments / Loss (manual)", RightText = $"({adjManual.ToString("N2")})", Spacing = 14 });
            if (adjustDown != 0)
                lines.Add(new LineEntry { Text = "Total Adjustments", RightText = $"({Math.Abs(adjustDown).ToString("N2")})", Bold = true, Spacing = 14 });
            lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Align = TextAlign.Center, Spacing = 12 });
            lines.Add(new LineEntry { Text = "Expected Inventory", RightText = expected.ToString("N2"), Bold = true, Spacing = 14 });
            lines.Add(new LineEntry { Text = "Actual Inventory", RightText = totalInventoryCost.ToString("N2"), Bold = true, Spacing = 14 });
            lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Align = TextAlign.Center, Spacing = 12 });
            var rounded = Math.Round(variance, 2);
            var label = rounded == 0 ? "✔ BALANCED" : rounded > 0 ? $"⚠ OVER by {rounded:N2}" : $"❌ SHORT by {Math.Abs(rounded):N2}";
            lines.Add(new LineEntry { Text = label, Align = TextAlign.Center, Bold = true, Spacing = 18 });
        }

        // Channel breakdown (server-side movements: mobile wholesale / ecommerce / receives / transfers)
        if (mobileSales > 0 || ecomOrders > 0 || receivedPcs > 0 || transferOutPcs > 0)
        {
            lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });
            lines.Add(new LineEntry { Text = "CHANNEL BREAKDOWN (SERVER)", Bold = true, Spacing = 14 });
            if (mobileSales > 0)
                lines.Add(new LineEntry { Text = $"Wholesale (mobile): {mobileSales} sale(s)", RightText = mobileTotal.ToString("N2"), Spacing = 14 });
            if (ecomOrders > 0)
                lines.Add(new LineEntry { Text = $"E-commerce: {ecomOrders} order(s)", RightText = ecomTotal.ToString("N2"), Spacing = 14 });
            if (ecomCollectedCash > 0 || ecomCollectedGcash > 0)
            {
                lines.Add(new LineEntry { Text = "E-commerce collected (COD)", RightText = (ecomCollectedCash + ecomCollectedGcash).ToString("N2"), Spacing = 14 });
                if (ecomCollectedCash > 0)
                    lines.Add(new LineEntry { Text = "  Cash", RightText = ecomCollectedCash.ToString("N2"), Spacing = 14 });
                if (ecomCollectedGcash > 0)
                    lines.Add(new LineEntry { Text = "  Gcash", RightText = ecomCollectedGcash.ToString("N2"), Spacing = 14 });
            }
            if (ecomRemitted > 0)
                lines.Add(new LineEntry { Text = "E-commerce remitted (dashboard)", RightText = ecomRemitted.ToString("N2"), Spacing = 14 });
            if (receivedPcs > 0)
                lines.Add(new LineEntry { Text = $"Received (server): +{receivedPcs} pcs", Spacing = 14 });
            if (transferOutPcs > 0)
                lines.Add(new LineEntry { Text = $"Transfers out (HQ->POS): -{transferOutPcs} pcs", Spacing = 14 });
        }

        lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });
        if (!string.IsNullOrWhiteSpace(notes))
        {
            lines.Add(new LineEntry { Text = $"Notes: {notes}", Spacing = 14 });
            lines.Add(new LineEntry { Text = new string('-', lineChars + 2), Align = TextAlign.Center, Spacing = 12 });
        }

        // RECEIPT AUDIT — anti-theft check
        if (receiptAudit.HasValue)
        {
            var ra = receiptAudit.Value;
            lines.Add(new LineEntry { Text = "RECEIPT AUDIT", Align = TextAlign.Center, Bold = true, Spacing = 16 });
            lines.Add(new LineEntry { Text = "Total Receipts", RightText = ra.Total.ToString(), Spacing = 14 });
            lines.Add(new LineEntry { Text = "Voided", RightText = ra.Voided.ToString(), Spacing = 14 });
            lines.Add(new LineEntry { Text = "Deleted/Missing", RightText = ra.Deleted.ToString(), Spacing = 14 });
            lines.Add(new LineEntry { Text = "Lost Value", RightText = "Php " + ra.Lost.ToString("N2"), Spacing = 14 });

            if (ra.Deleted > 0)
            {
                lines.Add(new LineEntry { Text = "⚠ DELETED RECEIPTS DETECTED!", Align = TextAlign.Center, Bold = true, Spacing = 14 });
                foreach (var mi in ra.MissingInvs.Take(10))
                    lines.Add(new LineEntry { Text = mi, Align = TextAlign.Center, Spacing = 12 });
                if (ra.MissingInvs.Count > 10)
                    lines.Add(new LineEntry { Text = $"+{ra.MissingInvs.Count - 10} more...", Align = TextAlign.Center, Spacing = 12 });
            }
            else
            {
                lines.Add(new LineEntry { Text = "✓ ALL RECEIPTS COUNTED", Align = TextAlign.Center, Bold = true, Spacing = 14 });
            }
            lines.Add(new LineEntry { Text = new string('=', lineChars), Align = TextAlign.Center, Spacing = 14 });
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

            using var font9B = new Font("Courier New", 9, FontStyle.Bold);

            var y = 5f;

            foreach (var entry in lines)
            {
                Font f;
                if (entry.Align == TextAlign.Center)
                    f = font9B;
                else
                    f = entry.Bold ? font9B : font9B;

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
        var marginL = int.TryParse(GetSetting("PrinterMarginLeft"), out var ml) ? ml : 2;
        var marginR = int.TryParse(GetSetting("PrinterMarginRight"), out var mr) ? mr : 2;

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
            string statsPart = string.Format("{0,4}{1,4}{2,4}", stockBefore, qty, newStock);
            var nameLines = WrapText(name, maxNameWidth - 2);
            if (nameLines.Count == 0) nameLines.Add("");
            for (int i = 0; i < nameLines.Count; i++)
            {
                var prefix = i == 0 ? "" : "  ";
                var pad = i == 0 ? maxNameWidth : maxNameWidth - 2;
                var text = prefix + nameLines[i].PadRight(pad);
                if (i == nameLines.Count - 1) text += statsPart;
                lines.Add(new LineEntry { Text = text, Spacing = 14 });
            }
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

            using var font9B = new Font("Courier New", 9, FontStyle.Bold);

            var y = 5f;

            foreach (var entry in lines)
            {
                Font f;
                if (entry.Align == TextAlign.Center)
                    f = font9B;
                else
                    f = font9B;

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
        var marginL = int.TryParse(GetSetting("PrinterMarginLeft"), out var ml) ? ml : 2;
        var marginR = int.TryParse(GetSetting("PrinterMarginRight"), out var mr) ? mr : 2;

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
            string statsPart = string.Format("{0,4}{1,4}{2,4}", entry.StockBefore, (int)entry.QuantityAdded, entry.StockAfter);
            var nameLines = WrapText(name, maxNameWidth - 2);
            if (nameLines.Count == 0) nameLines.Add("");
            for (int i = 0; i < nameLines.Count; i++)
            {
                var prefix = i == 0 ? "" : "  ";
                var pad = i == 0 ? maxNameWidth : maxNameWidth - 2;
                var text = prefix + nameLines[i].PadRight(pad);
                if (i == nameLines.Count - 1) text += statsPart;
                lines.Add(new LineEntry { Text = text, Spacing = 14 });
            }
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

            using var font9B = new Font("Courier New", 9, FontStyle.Bold);

            var y = 5f;

            foreach (var entry in lines)
            {
                Font f;
                if (entry.Align == TextAlign.Center)
                    f = font9B;
                else
                    f = font9B;

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

    public static void PrintWhReceipt(int saleId, string customerName, List<(string ProductName, string UnitName, int Qty, decimal Price, decimal Subtotal)> items, decimal grandTotal, string cashierName, string invoiceNo = "", string title = "")
    {
        var printer = GetSetting("PrinterName");
        if (string.IsNullOrEmpty(printer)) { MessageBox.Show("No printer configured. Go to Settings to set a printer.", "Printer Not Set", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        var paperWidth = int.TryParse(GetSetting("PaperWidth"), out var pw) ? pw : 280;
        var marginLeft = int.TryParse(GetSetting("PrinterMarginLeft"), out var ml) ? ml : 0;
        var marginRight = int.TryParse(GetSetting("PrinterMarginRight"), out var mr) ? mr : 0;
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
        if (!string.IsNullOrEmpty(title)) AddLine(title, true);
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
            var nameLines = WrapText(name, chars);
            for (int i = 0; i < nameLines.Count; i++)
                AddLine(i == 0 ? nameLines[i] : "  " + nameLines[i]);
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

        var font = new Font("Courier New", 9F, FontStyle.Bold);
        var fontBold = new Font("Courier New", 9F, FontStyle.Bold);
        var lineHeight = font.Height + 3;

        ExtendPaperIfNeeded(doc, lines.Count, lineHeight);

        doc.PrintPage += (_, e) =>
        {
            var pageW = e.PageBounds.Width;
            var lm = pageW * marginLeft / paperWidth;
            var rm = pageW * marginRight / paperWidth;

            var y = 0;
            foreach (var line in lines)
            {
                var f = line[2] == "1" ? fontBold : font;
                e.Graphics.DrawString(line[0], f, Brushes.Black, lm, y);
                if (!string.IsNullOrEmpty(line[1]))
                {
                    var rw = e.Graphics.MeasureString(line[1], f).Width;
                    e.Graphics.DrawString(line[1], f, Brushes.Black, pageW - rm - rw, y);
                }
                y += lineHeight;
            }
        };

        try { doc.Print(); }
        catch (Exception ex) { MessageBox.Show("Print error: " + ex.Message, "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    public static void PrintWarehouseInventory(List<(string Name, int Stock, decimal Cost, decimal Value)> items, string category, string title)
    {
        var printer = GetSetting("PrinterName");
        if (string.IsNullOrEmpty(printer)) { MessageBox.Show("No printer configured.", "Printer Not Set", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        var paperWidth = int.TryParse(GetSetting("PaperWidth"), out var pw) ? pw : 315;
        var marginLeft = int.TryParse(GetSetting("PrinterMarginLeft"), out var ml) ? ml : 0;
        var marginRight = int.TryParse(GetSetting("PrinterMarginRight"), out var mr) ? mr : 0;
        var chars = Math.Max(24, (paperWidth - marginLeft - marginRight) / 10);

        var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printer;
        doc.DefaultPageSettings.PaperSize = new PaperSize("Custom", paperWidth, 3000);
        doc.DefaultPageSettings.Margins = new Margins(marginLeft, marginRight, 0, 0);

        var totalItems = items.Count;
        var totalValue = items.Sum(x => x.Value);

        doc.PrintPage += (sender, e) =>
        {
            var sf = StringFormat.GenericTypographic;
            using var font9B = new Font("Courier New", 9, FontStyle.Bold);
            var brush = Brushes.Black;
            var pageW = e.PageBounds.Width;
            var lm = pageW * marginLeft / paperWidth;
            var rm = pageW * marginRight / paperWidth;
            var pw2 = pageW - lm - rm;
            var y = 5f;

            void Draw(string text)
            {
                var sz = e.Graphics.MeasureString(text, font9B);
                e.Graphics.DrawString(text, font9B, brush, lm, y, sf);
                y += sz.Height + 2;
            }

            string Pad(string a, string b, string c, string d)
            {
                var line = a.PadRight(chars - 22) + b.PadLeft(6) + c.PadLeft(8) + d.PadLeft(8);
                return line.Length > chars - 2 ? line[..(chars - 2)] : line;
            }

            Draw("=== WAREHOUSE INVENTORY ===");
            Draw("Category: " + title);
            Draw(new string('-', chars));
            Draw(Pad("Product", "Qty", "Cost", "Value"));
            Draw(new string('-', chars));

            foreach (var item in items)
            {
                var name = item.Name.Length > chars - 24 ? item.Name[..(chars - 24)] : item.Name;
                Draw(Pad(name, item.Stock.ToString("N0"), item.Cost.ToString("N2"), item.Value.ToString("N2")));
            }

            Draw(new string('-', chars));
            Draw($"Total Items: {totalItems}");
            Draw($"Total Value: \u20b1{totalValue:N2}");
        };

        try { doc.Print(); }
        catch (Exception ex) { MessageBox.Show("Print failed: " + ex.Message, "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    public static void PrintRawText(string text)
    {
        var printer = GetSetting("PrinterName");
        if (string.IsNullOrEmpty(printer)) { MessageBox.Show("No printer configured.", "Printer Not Set", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        var paperWidth = int.TryParse(GetSetting("PaperWidth"), out var pw) ? pw : 315;
        var marginLeft = int.TryParse(GetSetting("PrinterMarginLeft"), out var ml) ? ml : 0;
        var marginRight = int.TryParse(GetSetting("PrinterMarginRight"), out var mr) ? mr : 0;

        var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printer;
        doc.DefaultPageSettings.PaperSize = new PaperSize("Custom", paperWidth, 3000);
        doc.DefaultPageSettings.Margins = new Margins(marginLeft, marginRight, 0, 0);

        doc.PrintPage += (sender, e) =>
        {
            var sf = StringFormat.GenericTypographic;
            using var font9B = new Font("Courier New", 9, FontStyle.Bold);
            var brush = Brushes.Black;
            var y = 5f;
            foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            {
                e.Graphics.DrawString(line, font9B, brush, 0, y, sf);
                y += e.Graphics.MeasureString(line, font9B).Height + 2;
            }
        };

        try { doc.Print(); }
        catch (Exception ex) { MessageBox.Show("Print failed: " + ex.Message, "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}


