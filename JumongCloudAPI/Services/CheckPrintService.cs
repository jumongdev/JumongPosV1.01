using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Text;

namespace JumongCloudAPI.Services;

// EastWest / RCBC check printing: positioned text (mm) sa custom-size check.
// Date mode: 'boxes' = 3 box-groups (MM / DD / YYYY — bawat digit iginigiit sa box via pitch);
//            'line'  = isang linya (hal. MM-dd-yyyy) sa dateMmX/dateMmY.
// Ang ₱ at "PESOS" ay PRE-PRINTED sa check form, kaya hindi na ipi-print.
// Lahat ng X/Y ay adjustable PER-BANK sa dashboard (check_template) + TEST PRINT calibration.
public static class CheckPrintService
{
    public class Template
    {
        public string Bank { get; set; } = "EastWest Bank";
        public string Printer { get; set; } = "HP Smart Tank 580-590 series";
        public double PaperWmm { get; set; } = 158.75;
        public double PaperHmm { get; set; } = 69.85;
        public string FontName { get; set; } = "Arial";
        public string DateMode { get; set; } = "boxes";
        public string DateFormat { get; set; } = "MM-dd-yyyy";
        public double DateMmX { get; set; } = 40;
        public double DateMmY { get; set; } = 8;
        public double DateDdX { get; set; } = 52;
        public double DateDdY { get; set; } = 8;
        public double DateYyyyX { get; set; } = 62;
        public double DateYyyyY { get; set; } = 8;
        public double DatePitch { get; set; } = 3.5;
        public double DateFontSize { get; set; } = 10;
        public double PayeeX { get; set; } = 15;
        public double PayeeY { get; set; } = 25;
        public double PayeeMaxW { get; set; } = 95;
        public double PayeeFontSize { get; set; } = 11;
        public double AmountX { get; set; } = 100;
        public double AmountY { get; set; } = 25;
        public double AmountFontSize { get; set; } = 11;
        public double WordsX { get; set; } = 15;
        public double WordsY { get; set; } = 40;
        public double WordsMaxW { get; set; } = 140;
        public double WordsFontSize { get; set; } = 9;
        public bool WordsIncludePesos { get; set; } = false;
        public bool WordsIncludeOnly { get; set; } = true;
        public bool WordsAsterisks { get; set; } = false;
        public bool RecordEnabled { get; set; } = true;
        public double RecordX { get; set; } = 15;
        public double RecordY { get; set; } = 80;
        public double RecordFontSize { get; set; } = 9;
        public double RecordLineMm { get; set; } = 5;
    }

    // Kumpletong detalye ng check record (ipi-print sa ibaba ng check sa visible bond paper)
    public class CheckData
    {
        public string Bank { get; set; } = "";
        public string CheckNo { get; set; } = "";
        public string Payee { get; set; } = "";
        public decimal Amount { get; set; }
        public string AmountWords { get; set; } = "";
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Agent { get; set; } = "";
        public string Contact { get; set; } = "";
    }

    private static float Mm(double mm) => (float)(mm * 100.0 / 25.4); // mm -> hundredths of inch (Display unit)

    public static string AmountInWords(decimal amount, bool includePesos = false, bool includeOnly = true, bool asterisks = false)
    {
        if (amount < 0) amount = -amount;
        var pesos = (long)Math.Floor(amount);
        var cents = (int)Math.Round((amount - pesos) * 100m, MidpointRounding.AwayFromZero);
        if (cents >= 100) { pesos += 1; cents -= 100; }

        var sb = new StringBuilder();
        sb.Append(pesos == 0 ? "ZERO" : NumberToWords(pesos));
        if (includePesos) sb.Append(" PESOS");
        sb.Append(" AND ").Append(cents.ToString("D2")).Append("/100");
        if (includeOnly) sb.Append(" ONLY");
        var s = sb.ToString();
        if (asterisks) s = "*** " + s + " ***";
        return s;
    }

    public static (bool ok, string error) PrintCheck(Template t, CheckData d)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(t.Printer)) return (false, "Walang printer na naka-set sa template.");
            var doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = t.Printer;
            if (!doc.PrinterSettings.IsValid) return (false, $"Printer not found: {t.Printer}");

            doc.DefaultPageSettings.PaperSize = new PaperSize("Check", (int)Math.Round(Mm(t.PaperWmm)), (int)Math.Round(Mm(t.PaperHmm)));
            doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            doc.OriginAtMargins = false;

            var words = string.IsNullOrWhiteSpace(d.AmountWords)
                ? AmountInWords(d.Amount, t.WordsIncludePesos, t.WordsIncludeOnly, t.WordsAsterisks)
                : d.AmountWords;
            var amountStr = d.Amount.ToString("#,##0.00");
            var dateLine = d.DueDate.ToString(t.DateFormat ?? "MM-dd-yyyy", CultureInfo.InvariantCulture);
            var lineMode = string.Equals(t.DateMode, "line", StringComparison.OrdinalIgnoreCase);

            doc.PrintPage += (s, e) =>
            {
                var g = e.Graphics;
                g.PageUnit = GraphicsUnit.Display;

                using (var dateFont = new Font(t.FontName, (float)t.DateFontSize))
                {
                    if (lineMode)
                        g.DrawString(dateLine, dateFont, Brushes.Black, Mm(t.DateMmX), Mm(t.DateMmY));
                    else
                    {
                        DrawDigits(g, d.DueDate.ToString("MM"), t.DateMmX, t.DateMmY, t.DatePitch, dateFont);
                        DrawDigits(g, d.DueDate.ToString("dd"), t.DateDdX, t.DateDdY, t.DatePitch, dateFont);
                        DrawDigits(g, d.DueDate.ToString("yyyy"), t.DateYyyyX, t.DateYyyyY, t.DatePitch, dateFont);
                    }
                }
                DrawFit(g, d.Payee, t.PayeeX, t.PayeeY, t.PayeeMaxW, t.FontName, t.PayeeFontSize, false);
                DrawFit(g, amountStr, t.AmountX, t.AmountY, t.PayeeMaxW + 50, t.FontName, t.AmountFontSize, false);
                DrawFit(g, words, t.WordsX, t.WordsY, t.WordsMaxW, t.FontName, t.WordsFontSize, false);

                if (t.RecordEnabled) DrawRecord(g, t, d, words);

                e.HasMorePages = false;
            };

            doc.Print();
            return (true, "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // Record block sa IBABA ng check (sa visible bond paper ng carrier sheet):
    // date created · bank · check no · payee · amount · words · due date · agent/contact + signature line.
    private static void DrawRecord(Graphics g, Template t, CheckData d, string words)
    {
        var fs = (float)t.RecordFontSize;
        using var f = new Font(t.FontName, fs);
        using var fb = new Font(t.FontName, fs, FontStyle.Bold);
        var maxW = t.PaperWmm - t.RecordX - 10;
        var ym = t.RecordY;
        var lh = t.RecordLineMm;

        void Line(string text, bool bold)
        {
            if (!string.IsNullOrEmpty(text))
            {
                if (bold) g.DrawString(text, fb, Brushes.Black, Mm(t.RecordX), Mm(ym));
                else DrawFit(g, text, t.RecordX, ym, maxW, t.FontName, fs, false);
            }
            ym += lh;
        }

        Line("CHECK RECORD", true);
        Line("Date Created : " + d.CreatedAt.ToString("MM-dd-yyyy HH:mm"), false);
        Line("Bank         : " + d.Bank, false);
        Line("Check No.    : " + d.CheckNo, false);
        Line("Payee        : " + d.Payee, false);
        Line("Amount       : PHP " + d.Amount.ToString("#,##0.00"), false);
        Line("Amount Words : " + words, false);
        Line("Due Date     : " + d.DueDate.ToString("MM-dd-yyyy"), false);
        ym += lh; // blank line
        Line("Agent        : " + d.Agent + (string.IsNullOrWhiteSpace(d.Contact) ? "" : " / " + d.Contact), false);

        ym += 2;
        g.DrawLine(Pens.Black, Mm(t.RecordX), Mm(ym), Mm(t.RecordX + 70), Mm(ym));
        ym += 1.5;
        g.DrawString("Name & Signature", f, Brushes.Black, Mm(t.RecordX), Mm(ym));
    }

    // Bawat digit: centered sa cell na may lapad = pitch (para pumasok sa pre-printed boxes)
    private static void DrawDigits(Graphics g, string s, double xMm, double yMm, double pitchMm, Font f)
    {
        if (string.IsNullOrEmpty(s)) return;
        var h = f.GetHeight(g) * 1.2f;
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        for (var i = 0; i < s.Length; i++)
        {
            var rect = new RectangleF(Mm(xMm + i * pitchMm), Mm(yMm), Mm(pitchMm), h);
            g.DrawString(s[i].ToString(), f, Brushes.Black, rect, fmt);
        }
    }

    // I-shrink ang font hanggang kasya sa max width (para hindi lumampas sa linya/box)
    private static void DrawFit(Graphics g, string text, double xMm, double yMm, double maxWMm, string fontName, double size, bool bold)
    {
        if (string.IsNullOrEmpty(text)) return;
        var maxW = Mm(maxWMm);
        var fs = (float)size;
        Font f;
        while (true)
        {
            f = new Font(fontName, fs, bold ? FontStyle.Bold : FontStyle.Regular);
            if (g.MeasureString(text, f).Width <= maxW || fs <= 6f) break;
            f.Dispose();
            fs -= 0.5f;
        }
        g.DrawString(text, f, Brushes.Black, Mm(xMm), Mm(yMm));
        f.Dispose();
    }

    private static readonly string[] Ones =
    {
        "", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "TEN",
        "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN"
    };

    private static readonly string[] Tens =
    { "", "", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY" };

    private static string NumberToWords(long n)
    {
        if (n == 0) return "ZERO";
        if (n < 20) return Ones[n];
        if (n < 100) return Tens[n / 10] + (n % 10 > 0 ? "-" + Ones[n % 10] : "");
        if (n < 1_000) return Ones[n / 100] + " HUNDRED" + (n % 100 > 0 ? " " + NumberToWords(n % 100) : "");
        if (n < 1_000_000) return NumberToWords(n / 1_000) + " THOUSAND" + (n % 1_000 > 0 ? " " + NumberToWords(n % 1_000) : "");
        if (n < 1_000_000_000) return NumberToWords(n / 1_000_000) + " MILLION" + (n % 1_000_000 > 0 ? " " + NumberToWords(n % 1_000_000) : "");
        return NumberToWords(n / 1_000_000_000) + " BILLION" + (n % 1_000_000_000 > 0 ? " " + NumberToWords(n % 1_000_000_000) : "");
    }
}
