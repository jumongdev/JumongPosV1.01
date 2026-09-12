using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Drawing.Printing;
using JumongCloudAPI.Services;

namespace JumongCloudAPI.Controllers;

// EastWest check recording + printing (server-side print sa HP Smart Tank).
// Format rules (bank requirement): date = MM-DD-YYYY sa pre-printed boxes; amount = #,##0.00 (walang ₱ sa box);
// isang payee lang (mula sa suppliers); amount in words = uppercase, "AND xx/100 ONLY".
[ApiController]
[Route("api/checks")]
public class ChecksController : ControllerBase
{
    // ── list / CRUD ──────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult List([FromQuery] string? search = null, [FromQuery] string? status = null,
        [FromQuery] string? from = null, [FromQuery] string? to = null)
    {
        try
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            var where = new List<string>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                where.Add("(c.check_no ILIKE @s OR c.payee ILIKE @s OR c.memo ILIKE @s)");
                cmd.Parameters.AddWithValue("s", "%" + search.Trim() + "%");
            }
            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                where.Add("c.status = @st");
                cmd.Parameters.AddWithValue("st", status);
            }
            if (DateTime.TryParse(from, out var df)) { where.Add("c.check_date >= @df"); cmd.Parameters.AddWithValue("df", df.Date); }
            if (DateTime.TryParse(to, out var dt)) { where.Add("c.check_date <= @dt"); cmd.Parameters.AddWithValue("dt", dt.Date); }

            cmd.CommandText = $@"
                SELECT c.id, c.bank, c.check_no, c.check_date, c.supplier_id, c.payee, c.amount, c.amount_words,
                       c.memo, c.status, c.printed_at, c.print_count, c.created_by, c.created_at
                FROM checks c
                {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
                ORDER BY c.check_date DESC, c.id DESC LIMIT 500";
            using var r = cmd.ExecuteReader();
            var list = new List<object>();
            while (r.Read())
                list.Add(new
                {
                    id = r.GetInt32(0),
                    bank = r.GetString(1),
                    checkNo = r.GetString(2),
                    checkDate = r.GetDateTime(3).ToString("yyyy-MM-dd"),
                    supplierId = r.IsDBNull(4) ? 0 : r.GetInt32(4),
                    payee = r.GetString(5),
                    amount = r.GetDecimal(6),
                    amountWords = r.GetString(7),
                    memo = r.GetString(8),
                    status = r.GetString(9),
                    printedAt = r.IsDBNull(10) ? (DateTime?)null : r.GetDateTime(10),
                    printCount = r.GetInt32(11),
                    createdBy = r.GetString(12),
                    createdAt = r.GetDateTime(13)
                });
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPost]
    public IActionResult Create([FromBody] CheckDto dto)
    {
        try
        {
            var (ok, err, vals) = Validate(conn: null, dto, 0);
            if (!ok) return BadRequest(new { error = err });

            using var conn = Data.PgDatabaseHelper.GetConnection();
            using (var dup = conn.CreateCommand())
            {
                dup.CommandText = "SELECT id FROM checks WHERE bank = @b AND check_no = @n";
                dup.Parameters.AddWithValue("b", vals!.Bank);
                dup.Parameters.AddWithValue("n", vals.CheckNo);
                if (dup.ExecuteScalar() != null) return Conflict(new { error = $"Check No. {vals.CheckNo} ay gamit na sa {vals.Bank}." });
            }

            var words = CheckPrintService.AmountInWords(vals.Amount, vals.IncludePesos, vals.IncludeOnly, vals.Asterisks);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO checks (bank, check_no, check_date, supplier_id, payee, amount, amount_words, memo, created_by)
                VALUES (@b, @n, @d, @sid, @payee, @amt, @words, @memo, @by) RETURNING id";
            cmd.Parameters.AddWithValue("b", vals.Bank);
            cmd.Parameters.AddWithValue("n", vals.CheckNo);
            cmd.Parameters.AddWithValue("d", vals.Date);
            cmd.Parameters.AddWithValue("sid", vals.SupplierId);
            cmd.Parameters.AddWithValue("payee", vals.Payee);
            cmd.Parameters.AddWithValue("amt", vals.Amount);
            cmd.Parameters.AddWithValue("words", words);
            cmd.Parameters.AddWithValue("memo", dto.Memo ?? "");
            cmd.Parameters.AddWithValue("by", dto.CreatedBy ?? "");
            var id = Convert.ToInt32(cmd.ExecuteScalar());
            return Ok(new { ok = true, id, amountWords = words });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] CheckDto dto)
    {
        try
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using (var chk = conn.CreateCommand())
            {
                chk.CommandText = "SELECT status FROM checks WHERE id = @id";
                chk.Parameters.AddWithValue("id", id);
                var st = chk.ExecuteScalar() as string;
                if (st == null) return NotFound(new { error = "Check not found" });
                if (st != "issued") return BadRequest(new { error = "Hindi na ma-edit ang check na ito (cleared/void na)." });
            }

            var (ok, err, vals) = Validate(conn, dto, id);
            if (!ok) return BadRequest(new { error = err });

            var words = CheckPrintService.AmountInWords(vals!.Amount, vals.IncludePesos, vals.IncludeOnly, vals.Asterisks);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE checks SET bank=@b, check_no=@n, check_date=@d, supplier_id=@sid, payee=@payee,
                amount=@amt, amount_words=@words, memo=@memo, updated_at=NOW() WHERE id=@id";
            cmd.Parameters.AddWithValue("b", vals.Bank);
            cmd.Parameters.AddWithValue("n", vals.CheckNo);
            cmd.Parameters.AddWithValue("d", vals.Date);
            cmd.Parameters.AddWithValue("sid", vals.SupplierId);
            cmd.Parameters.AddWithValue("payee", vals.Payee);
            cmd.Parameters.AddWithValue("amt", vals.Amount);
            cmd.Parameters.AddWithValue("words", words);
            cmd.Parameters.AddWithValue("memo", dto.Memo ?? "");
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
            return Ok(new { ok = true, amountWords = words });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPost("{id:int}/void")]
    public IActionResult Void(int id) => SetStatus(id, "void");

    [HttpPost("{id:int}/clear")]
    [HttpPost("{id:int}/cleared")] // alias — luma/na-cache na UI ay gumagamit ng /cleared
    public IActionResult Clear(int id) => SetStatus(id, "cleared");

    private IActionResult SetStatus(int id, string status)
    {
        try
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE checks SET status=@st, updated_at=NOW() WHERE id=@id";
            cmd.Parameters.AddWithValue("st", status);
            cmd.Parameters.AddWithValue("id", id);
            return cmd.ExecuteNonQuery() > 0 ? Ok(new { ok = true }) : NotFound(new { error = "Check not found" });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // ── printing ─────────────────────────────────────────────────────────────
    [HttpPost("{id:int}/print")]
    public IActionResult PrintCheck(int id)
    {
        try
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            var data = new CheckPrintService.CheckData();
            string status;
            using (var q = conn.CreateCommand())
            {
                q.CommandText = @"SELECT c.payee, c.status, c.check_no, c.check_date, c.amount, c.bank,
                        c.created_at AT TIME ZONE 'Asia/Manila' AS created_local,
                        COALESCE(s.agent,''), COALESCE(s.contact_no,'')
                    FROM checks c LEFT JOIN suppliers s ON s.id = c.supplier_id WHERE c.id = @id";
                q.Parameters.AddWithValue("id", id);
                using var r = q.ExecuteReader();
                if (!r.Read()) return NotFound(new { error = "Check not found" });
                data.Payee = r.GetString(0); status = r.GetString(1); data.CheckNo = r.GetString(2);
                data.DueDate = r.GetDateTime(3); data.Amount = r.GetDecimal(4); data.Bank = r.GetString(5);
                data.CreatedAt = r.GetDateTime(6); data.Agent = r.GetString(7); data.Contact = r.GetString(8);
            }
            if (status == "void") return BadRequest(new { error = "Void ang check na ito — hindi na ma-print." });

            var t = LoadTemplate(conn, data.Bank);
            data.AmountWords = CheckPrintService.AmountInWords(data.Amount, t.WordsIncludePesos, t.WordsIncludeOnly, t.WordsAsterisks);
            var (ok, err) = CheckPrintService.PrintCheck(t, data);
            if (!ok) return StatusCode(500, new { error = err });

            using var upd = conn.CreateCommand();
            upd.CommandText = "UPDATE checks SET printed_at=NOW(), print_count=print_count+1, updated_at=NOW() WHERE id=@id";
            upd.Parameters.AddWithValue("id", id);
            upd.ExecuteNonQuery();
            return Ok(new { ok = true, printed = data.CheckNo, bank = data.Bank, amountWords = data.AmountWords });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // Calibration: sample values lang (walang DB record) — i-align sa aktwal na check ng bank
    [HttpPost("test-print")]
    public IActionResult TestPrint([FromQuery] string? bank = null)
    {
        try
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            var t = LoadTemplate(conn, bank ?? "EastWest Bank");
            var data = new CheckPrintService.CheckData
            {
                Bank = t.Bank, CheckNo = "000124", Payee = "SAMPLE SUPPLIER INC", Amount = 50000.00m,
                DueDate = DateTime.Today, CreatedAt = DateTime.Now, Agent = "JUAN DELA CRUZ", Contact = "09171234567"
            };
            data.AmountWords = CheckPrintService.AmountInWords(data.Amount, t.WordsIncludePesos, t.WordsIncludeOnly, t.WordsAsterisks);
            var (ok, err) = CheckPrintService.PrintCheck(t, data);
            return ok ? Ok(new { ok = true, bank = t.Bank }) : StatusCode(500, new { error = err });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpGet("banks")]
    public IActionResult Banks()
    {
        try
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT bank FROM check_template ORDER BY (bank = 'EastWest Bank') DESC, bank";
            using var r = cmd.ExecuteReader();
            var list = new List<string>();
            while (r.Read()) list.Add(r.GetString(0));
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpGet("printers")]
    public IActionResult Printers()
    {
        try
        {
            var def = new PrinterSettings().PrinterName;
            var list = PrinterSettings.InstalledPrinters.Cast<string>()
                .Select(n => new { name = n, isDefault = string.Equals(n, def, StringComparison.OrdinalIgnoreCase) }).ToList();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpGet("words")]
    public IActionResult Words([FromQuery] decimal amount = 0, [FromQuery] string? bank = null)
    {
        try
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            var t = LoadTemplate(conn, bank ?? "EastWest Bank");
            return Ok(new { words = CheckPrintService.AmountInWords(amount, t.WordsIncludePesos, t.WordsIncludeOnly, t.WordsAsterisks) });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // ── template (X/Y calibration — per bank) ────────────────────────────────
    [HttpGet("template")]
    public IActionResult GetTemplate([FromQuery] string? bank = null)
    {
        try
        {
            using var conn = Data.PgDatabaseHelper.GetConnection();
            return Ok(LoadTemplate(conn, bank ?? "EastWest Bank"));
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPut("template")]
    public IActionResult SaveTemplate([FromBody] CheckPrintService.Template t)
    {
        try
        {
            if (t == null) return BadRequest(new { error = "template required" });
            if (string.IsNullOrWhiteSpace(t.Bank)) return BadRequest(new { error = "bank required" });
            if (t.PaperWmm < 50 || t.PaperWmm > 300 || t.PaperHmm < 30 || t.PaperHmm > 300)
                return BadRequest(new { error = "Paper size (mm) out of range" });
            using var conn = Data.PgDatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE check_template SET printer=@printer, paper_w_mm=@pw, paper_h_mm=@ph, font_name=@fn,
                date_mode=@dm, date_format=@dfmt,
                date_mm_x=@d1, date_mm_y=@d2, date_dd_x=@d3, date_dd_y=@d4, date_yyyy_x=@d5, date_yyyy_y=@d6,
                date_pitch_mm=@d7, date_font_size=@d8,
                payee_x=@p1, payee_y=@p2, payee_max_w_mm=@p3, payee_font_size=@p4,
                amount_x=@a1, amount_y=@a2, amount_font_size=@a3,
                words_x=@w1, words_y=@w2, words_max_w_mm=@w3, words_font_size=@w4,
                words_include_pesos=@w5, words_include_only=@w6, words_asterisks=@w7,
                record_enabled=@r1, record_x=@r2, record_y=@r3, record_font_size=@r4, record_line_mm=@r5,
                updated_at=NOW() WHERE bank=@bank";
            cmd.Parameters.AddWithValue("bank", t.Bank.Trim());
            cmd.Parameters.AddWithValue("printer", t.Printer ?? "");
            cmd.Parameters.AddWithValue("pw", (decimal)t.PaperWmm);
            cmd.Parameters.AddWithValue("ph", (decimal)t.PaperHmm);
            cmd.Parameters.AddWithValue("fn", t.FontName ?? "Arial");
            cmd.Parameters.AddWithValue("dm", (t.DateMode ?? "boxes").ToLower() == "line" ? "line" : "boxes");
            cmd.Parameters.AddWithValue("dfmt", string.IsNullOrWhiteSpace(t.DateFormat) ? "MM-dd-yyyy" : t.DateFormat.Trim());
            cmd.Parameters.AddWithValue("d1", (decimal)t.DateMmX); cmd.Parameters.AddWithValue("d2", (decimal)t.DateMmY);
            cmd.Parameters.AddWithValue("d3", (decimal)t.DateDdX); cmd.Parameters.AddWithValue("d4", (decimal)t.DateDdY);
            cmd.Parameters.AddWithValue("d5", (decimal)t.DateYyyyX); cmd.Parameters.AddWithValue("d6", (decimal)t.DateYyyyY);
            cmd.Parameters.AddWithValue("d7", (decimal)t.DatePitch); cmd.Parameters.AddWithValue("d8", (decimal)t.DateFontSize);
            cmd.Parameters.AddWithValue("p1", (decimal)t.PayeeX); cmd.Parameters.AddWithValue("p2", (decimal)t.PayeeY);
            cmd.Parameters.AddWithValue("p3", (decimal)t.PayeeMaxW); cmd.Parameters.AddWithValue("p4", (decimal)t.PayeeFontSize);
            cmd.Parameters.AddWithValue("a1", (decimal)t.AmountX); cmd.Parameters.AddWithValue("a2", (decimal)t.AmountY);
            cmd.Parameters.AddWithValue("a3", (decimal)t.AmountFontSize);
            cmd.Parameters.AddWithValue("w1", (decimal)t.WordsX); cmd.Parameters.AddWithValue("w2", (decimal)t.WordsY);
            cmd.Parameters.AddWithValue("w3", (decimal)t.WordsMaxW); cmd.Parameters.AddWithValue("w4", (decimal)t.WordsFontSize);
            cmd.Parameters.AddWithValue("w5", t.WordsIncludePesos); cmd.Parameters.AddWithValue("w6", t.WordsIncludeOnly);
            cmd.Parameters.AddWithValue("w7", t.WordsAsterisks);
            cmd.Parameters.AddWithValue("r1", t.RecordEnabled);
            cmd.Parameters.AddWithValue("r2", (decimal)t.RecordX);
            cmd.Parameters.AddWithValue("r3", (decimal)t.RecordY);
            cmd.Parameters.AddWithValue("r4", (decimal)t.RecordFontSize);
            cmd.Parameters.AddWithValue("r5", (decimal)t.RecordLineMm);
            if (cmd.ExecuteNonQuery() == 0)
            {
                // Bagong bank — gumawa ng template row (defaults sa ibang columns)
                using var ins = conn.CreateCommand();
                ins.CommandText = "INSERT INTO check_template (id, bank) VALUES ((SELECT COALESCE(MAX(id),1)+1 FROM check_template), @bank)";
                ins.Parameters.AddWithValue("bank", t.Bank.Trim());
                ins.ExecuteNonQuery();
                cmd.ExecuteNonQuery();
            }
            return Ok(new { ok = true });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    private static CheckPrintService.Template LoadTemplate(NpgsqlConnection conn, string bank)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT bank, printer, paper_w_mm, paper_h_mm, font_name, date_mode, date_format,
                date_mm_x, date_mm_y, date_dd_x, date_dd_y, date_yyyy_x, date_yyyy_y, date_pitch_mm, date_font_size,
                payee_x, payee_y, payee_max_w_mm, payee_font_size,
                amount_x, amount_y, amount_font_size,
                words_x, words_y, words_max_w_mm, words_font_size,
                words_include_pesos, words_include_only, words_asterisks,
                record_enabled, record_x, record_y, record_font_size, record_line_mm
            FROM check_template
            ORDER BY (bank = @bank) DESC, id
            LIMIT 1";
        cmd.Parameters.AddWithValue("bank", bank);
        using var r = cmd.ExecuteReader();
        var t = new CheckPrintService.Template { Bank = bank };
        if (r.Read())
        {
            t.Bank = r.GetString(0);
            t.Printer = r.GetString(1);
            t.PaperWmm = (double)r.GetDecimal(2);
            t.PaperHmm = (double)r.GetDecimal(3);
            t.FontName = r.GetString(4);
            t.DateMode = r.GetString(5);
            t.DateFormat = r.GetString(6);
            t.DateMmX = (double)r.GetDecimal(7); t.DateMmY = (double)r.GetDecimal(8);
            t.DateDdX = (double)r.GetDecimal(9); t.DateDdY = (double)r.GetDecimal(10);
            t.DateYyyyX = (double)r.GetDecimal(11); t.DateYyyyY = (double)r.GetDecimal(12);
            t.DatePitch = (double)r.GetDecimal(13); t.DateFontSize = (double)r.GetDecimal(14);
            t.PayeeX = (double)r.GetDecimal(15); t.PayeeY = (double)r.GetDecimal(16);
            t.PayeeMaxW = (double)r.GetDecimal(17); t.PayeeFontSize = (double)r.GetDecimal(18);
            t.AmountX = (double)r.GetDecimal(19); t.AmountY = (double)r.GetDecimal(20); t.AmountFontSize = (double)r.GetDecimal(21);
            t.WordsX = (double)r.GetDecimal(22); t.WordsY = (double)r.GetDecimal(23);
            t.WordsMaxW = (double)r.GetDecimal(24); t.WordsFontSize = (double)r.GetDecimal(25);
            t.WordsIncludePesos = r.GetBoolean(26); t.WordsIncludeOnly = r.GetBoolean(27); t.WordsAsterisks = r.GetBoolean(28);
            t.RecordEnabled = r.GetBoolean(29);
            t.RecordX = (double)r.GetDecimal(30); t.RecordY = (double)r.GetDecimal(31);
            t.RecordFontSize = (double)r.GetDecimal(32); t.RecordLineMm = (double)r.GetDecimal(33);
        }
        return t;
    }

    // Shared validation — payee ay laging galing sa suppliers registry (single payee lang).
    private (bool ok, string err, CheckVals? vals) Validate(NpgsqlConnection? conn, CheckDto dto, int excludeId)
    {
        var bank = string.IsNullOrWhiteSpace(dto.Bank) ? "EastWest Bank" : dto.Bank.Trim();
        var checkNo = (dto.CheckNo ?? "").Trim();
        if (checkNo.Length == 0) return (false, "Check number is required", null);
        if (!DateTime.TryParse(dto.CheckDate, out var date)) return (false, "Valid date is required", null);
        if (dto.SupplierId <= 0) return (false, "Payee (supplier) is required", null);
        if (dto.Amount <= 0) return (false, "Amount must be greater than 0", null);

        var ownConn = false;
        if (conn == null) { conn = Data.PgDatabaseHelper.GetConnection(); ownConn = true; }
        try
        {
            string payee;
            using (var s = conn.CreateCommand())
            {
                s.CommandText = "SELECT company_name FROM suppliers WHERE id = @id AND is_active = TRUE";
                s.Parameters.AddWithValue("id", dto.SupplierId);
                var name = s.ExecuteScalar() as string;
                if (string.IsNullOrEmpty(name)) return (false, "Supplier not found (o archived na).", null);
                payee = name;
            }
            using (var dup = conn.CreateCommand())
            {
                dup.CommandText = "SELECT id FROM checks WHERE bank = @b AND check_no = @n AND id != @id";
                dup.Parameters.AddWithValue("b", bank);
                dup.Parameters.AddWithValue("n", checkNo);
                dup.Parameters.AddWithValue("id", excludeId);
                if (dup.ExecuteScalar() != null) return (false, $"Check No. {checkNo} ay gamit na sa {bank}.", null);
            }
            using (var bc = conn.CreateCommand())
            {
                bc.CommandText = "SELECT COUNT(*) FROM check_template WHERE bank = @b";
                bc.Parameters.AddWithValue("b", bank);
                if (Convert.ToInt32(bc.ExecuteScalar()) == 0) return (false, $"Hindi kilalang bank: {bank}", null);
            }

            var t = LoadTemplate(conn, bank);
            return (true, "", new CheckVals
            {
                Bank = bank, CheckNo = checkNo, Date = date.Date, SupplierId = dto.SupplierId, Payee = payee,
                Amount = decimal.Round(dto.Amount, 2, MidpointRounding.AwayFromZero),
                IncludePesos = t.WordsIncludePesos, IncludeOnly = t.WordsIncludeOnly, Asterisks = t.WordsAsterisks
            });
        }
        finally { if (ownConn) conn!.Dispose(); }
    }

    private class CheckVals
    {
        public string Bank = ""; public string CheckNo = ""; public DateTime Date; public int SupplierId;
        public string Payee = ""; public decimal Amount; public bool IncludePesos, IncludeOnly, Asterisks;
    }

    public class CheckDto
    {
        public string? Bank { get; set; }
        public string? CheckNo { get; set; }
        public string? CheckDate { get; set; }
        public int SupplierId { get; set; }
        public decimal Amount { get; set; }
        public string? Memo { get; set; }
        public string? CreatedBy { get; set; }
    }
}
