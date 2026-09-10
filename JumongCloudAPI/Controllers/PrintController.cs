using System.Drawing;
using System.Drawing.Printing;
using Microsoft.AspNetCore.Mvc;

namespace JumongCloudAPI.Controllers;

// Server-side printing: ang mga printer ay naka-install sa SERVER PC mismo (hal. HP Smart Tank 580,
// XP-80C thermal). Ang API na ito ay tumatakbo doon — kaya ang kahit anong app (dashboard, phone,
// POS ng ibang store) ay makakapag-print sa pamamagitan ng HTTP call na ito.
// Security: lahat ng request ay nangangailangan ng key (query ?key= o header X-Print-Key).
[Route("api/print")]
public class PrintController : ControllerBase
{
    private const string Key = "JumongPrint2026";

    private bool HasKey()
    {
        var q = Request.Query["key"].ToString();
        var h = Request.Headers["X-Print-Key"].ToString();
        return q == Key || h == Key;
    }

    // Listahan ng mga printer na naka-install sa server
    [HttpGet("printers")]
    public IActionResult Printers()
    {
        if (!HasKey()) return StatusCode(401, new { error = "Invalid print key" });
        try
        {
            var names = PrinterSettings.InstalledPrinters.Cast<string>()
                .Select(n => new { name = n, isDefault = string.Equals(n, new PrinterSettings().PrinterName, StringComparison.OrdinalIgnoreCase) })
                .ToList();
            return Ok(new { printers = names, count = names.Count });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // Print TEXT (check / order slip / resibo copy). paperWidthMm: 80 (thermal) o 210 (A4/HP)
    [HttpPost("text")]
    public IActionResult PrintText([FromBody] PrintTextRequest req)
    {
        if (!HasKey()) return StatusCode(401, new { error = "Invalid print key" });
        if (req == null || string.IsNullOrWhiteSpace(req.Text)) return BadRequest(new { error = "text required" });
        try
        {
            var printer = string.IsNullOrWhiteSpace(req.Printer) ? new PrinterSettings().PrinterName : req.Printer;
            var lines = (req.Text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            var fontSize = req.FontSize > 0 ? req.FontSize : 10f;
            var widthMm = req.PaperWidthMm > 0 ? req.PaperWidthMm : (req.Thermal ? 80 : 210);
            var copies = Math.Clamp(req.Copies > 0 ? req.Copies : 1, 1, 5);

            var doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = printer;
            doc.PrinterSettings.Copies = (short)copies;
            if (!doc.PrinterSettings.IsValid) return BadRequest(new { error = $"Printer not found: {printer}" });

            using var font = new Font(req.FontName ?? "Courier New", fontSize);

            var y = 0f;
            doc.DefaultPageSettings.PaperSize = widthMm == 210
                ? new PaperSize("A4", 827, 1169)
                : new PaperSize("Thermal", (int)(widthMm * 4f), 5000);
            doc.DefaultPageSettings.Margins = new Margins(widthMm == 210 ? 40 : 15, widthMm == 210 ? 40 : 15, 20, 20);

            doc.PrintPage += (s, e) =>
            {
                e.Graphics.PageUnit = GraphicsUnit.Display; // 1/100 inch
                var drawW = e.MarginBounds.Width;
                var lineH = font.GetHeight(e.Graphics) * 1.05f;
                foreach (var ln in lines)
                {
                    if (ln == "\f") { e.HasMorePages = false; return; }
                    if (ln == "\x0c") { e.HasMorePages = true; y = 0; return; }
                    e.Graphics.DrawString(ln == "" ? " " : ln, font, Brushes.Black,
                        new RectangleF(e.MarginBounds.Left, e.MarginBounds.Top + y, drawW, lineH),
                        StringFormat.GenericDefault);
                    y += lineH;
                    if (y + lineH > e.MarginBounds.Height)
                    {
                        e.HasMorePages = true;
                        y = 0;
                        return;
                    }
                }
                e.HasMorePages = false;
            };

            doc.Print();
            return Ok(new { ok = true, printer, pages = lines.Count > 0 ? 1 : 0, note = "Sent to spooler" });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // Print IMAGE (poster/check na may graphics). imageUrl = public URL (o base64 data URI).
    [HttpPost("image")]
    public async Task<IActionResult> PrintImage([FromBody] PrintImageRequest req)
    {
        if (!HasKey()) return StatusCode(401, new { error = "Invalid print key" });
        if (req == null || string.IsNullOrWhiteSpace(req.ImageUrl)) return BadRequest(new { error = "imageUrl required" });
        try
        {
            var printer = string.IsNullOrWhiteSpace(req.Printer) ? new PrinterSettings().PrinterName : req.Printer;
            byte[] bytes;
            if (req.ImageUrl.StartsWith("data:"))
            {
                var b64 = req.ImageUrl.Substring(req.ImageUrl.IndexOf(',') + 1);
                bytes = Convert.FromBase64String(b64);
            }
            else
            {
                using var hc = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                bytes = await hc.GetByteArrayAsync(req.ImageUrl);
            }
            using var ms = new MemoryStream(bytes);
            using var img = Image.FromStream(ms);

            var doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = printer;
            if (!doc.PrinterSettings.IsValid) return BadRequest(new { error = $"Printer not found: {printer}" });
            doc.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
            doc.DefaultPageSettings.Margins = new Margins(25, 25, 25, 25);
            doc.PrintPage += (s, e) =>
            {
                var bw = e.MarginBounds.Width;
                var bh = e.MarginBounds.Height;
                var scale = Math.Min(bw / img.Width, bh / img.Height);
                if (scale > 1) scale = 1;
                var w = (int)(img.Width * scale);
                var hgt = (int)(img.Height * scale);
                var x = e.MarginBounds.Left + (bw - w) / 2;
                var yy = e.MarginBounds.Top + (bh - hgt) / 2;
                e.Graphics.DrawImage(img, x, yy, w, hgt);
                e.HasMorePages = false;
            };
            doc.Print();
            return Ok(new { ok = true, printer, note = "Image sent to spooler" });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    public class PrintTextRequest
    {
        public string? Printer { get; set; }
        public string? Text { get; set; }
        public float FontSize { get; set; } = 10f;
        public string? FontName { get; set; }
        public int PaperWidthMm { get; set; } // 80 = thermal, 210 = A4
        public bool Thermal { get; set; }
        public int Copies { get; set; } = 1;
    }

    public class PrintImageRequest
    {
        public string? Printer { get; set; }
        public string? ImageUrl { get; set; }
    }
}
