using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using JumongPosV1._01.Data;
using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class SettingsForm : Form
{
    private readonly User _currentUser;
    private readonly Dictionary<string, string> _originalSettings = new();
    private Panel _pnlScroll = null!;

    public SettingsForm(User user)
    {
        _currentUser = user;
        InitializeComponent();
        LoadSettings();
        DebugHelper.AddFormLabel(this);
    }

    private void LoadSettings()
    {
        cmbPrinter.Items.Clear();
        cmbPrinter.Items.AddRange(PrinterService.GetPrinters().ToArray<object>());

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();

        var val = GetSetting(conn, "PrinterName");
        if (!string.IsNullOrEmpty(val)) cmbPrinter.SelectedItem = val;

        txtCompanyName.Text = GetSetting(conn, "CompanyName") ?? "";
        txtAddress.Text = GetSetting(conn, "CompanyAddress") ?? "";
        txtMobile.Text = GetSetting(conn, "CompanyMobile") ?? "";
        txtFooter.Text = GetSetting(conn, "ReceiptFooter") ?? "Thank You! Come Again!";

        cmbPaperSize.Items.Clear();
        cmbPaperSize.Items.Add("58mm");
        cmbPaperSize.Items.Add("74mm");
        cmbPaperSize.Items.Add("78mm");
        cmbPaperSize.Items.Add("80mm");
        var paperSz = GetSetting(conn, "PaperWidth") ?? "307";
        var paperLabel = paperSz switch { "228" => "58mm", "291" => "74mm", "307" => "78mm", "315" => "80mm", _ => "78mm" };
        cmbPaperSize.Text = paperLabel;

        numMarginLeft.Value = int.TryParse(GetSetting(conn, "PrinterMarginLeft"), out var ml) ? ml : 5;
        numMarginRight.Value = int.TryParse(GetSetting(conn, "PrinterMarginRight"), out var mr) ? mr : 2;

        var screens = Screen.AllScreens;
        cmbPosScreen.Items.Clear();
        cmbCustomerScreen.Items.Clear();
        for (var i = 0; i < screens.Length; i++)
        {
            var item = $"Screen {i + 1}{(i == 0 ? " (Primary)" : "")}";
            cmbPosScreen.Items.Add(item);
            cmbCustomerScreen.Items.Add(item);
        }

        var posIdx = int.TryParse(GetSetting(conn, "PosScreenIndex"), out var p) ? p : 0;
        var custIdx = int.TryParse(GetSetting(conn, "CustomerScreenIndex"), out var c) ? c : (screens.Length > 1 ? 1 : 0);

        if (posIdx >= 0 && posIdx < cmbPosScreen.Items.Count) cmbPosScreen.SelectedIndex = posIdx; else cmbPosScreen.SelectedIndex = 0;
        if (custIdx >= 0 && custIdx < cmbCustomerScreen.Items.Count) cmbCustomerScreen.SelectedIndex = custIdx; else if (screens.Length > 1) cmbCustomerScreen.SelectedIndex = 1; else cmbCustomerScreen.SelectedIndex = 0;

        _originalSettings.Clear();
        _originalSettings["PrinterName"] = cmbPrinter.Text;
        _originalSettings["CompanyName"] = txtCompanyName.Text;
        _originalSettings["CompanyAddress"] = txtAddress.Text;
        _originalSettings["CompanyMobile"] = txtMobile.Text;
        _originalSettings["ReceiptFooter"] = txtFooter.Text;
        _originalSettings["PaperWidth"] = paperValForLabel(cmbPaperSize.Text);
        _originalSettings["PrinterMarginLeft"] = numMarginLeft.Value.ToString();
        _originalSettings["PrinterMarginRight"] = numMarginRight.Value.ToString();
        _originalSettings["PosScreenIndex"] = cmbPosScreen.SelectedIndex.ToString();
        _originalSettings["CustomerScreenIndex"] = cmbCustomerScreen.SelectedIndex.ToString();

        var schedHour = GetSetting(conn, "EmailScheduleHour") ?? "20";
        numEmailScheduleHour.Value = int.TryParse(schedHour, out var sh) && sh >= 0 && sh <= 23 ? sh : 20;
        _originalSettings["EmailScheduleHour"] = numEmailScheduleHour.Value.ToString();
        var enableOnline = GetSetting(conn, "EnableOnlineOrders") ?? "true";
        chkEnableOnlineOrders.Checked = enableOnline == "true";
        _originalSettings["EnableOnlineOrders"] = enableOnline;
        var enableCustDisplay = GetSetting(conn, "EnableCustomerDisplay") ?? "true";
        chkCustomerDisplay.Checked = enableCustDisplay == "true";
        _originalSettings["EnableCustomerDisplay"] = enableCustDisplay;
        var cloudUrl = GetSetting(conn, "CloudApiUrl") ?? "https://api-production-99fb.up.railway.app/api";
        txtCloudApiUrl.Text = cloudUrl;
        _originalSettings["CloudApiUrl"] = cloudUrl;
    }

    private static string paperValForLabel(string label) =>
        label switch { "58mm" => "228", "74mm" => "291", "78mm" => "307", "80mm" => "315", _ => "307" };

    private static string? GetSetting(SQLiteConnection conn, string key)
    {
        var sql = "SELECT Value FROM Settings WHERE Key = @key";
        using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@key", key);
        return cmd.ExecuteScalar()?.ToString();
    }

    private static void UpsertSetting(SQLiteConnection conn, string key, string value)
    {
        using var cmd = new SQLiteCommand("INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@key, @val)", conn);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@val", value);
        cmd.ExecuteNonQuery();
    }

    private void btnSave_Click(object? sender, EventArgs e)
    {
        var userName = string.IsNullOrEmpty(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName;
        var paperVal = cmbPaperSize.Text switch { "58mm" => "228", "74mm" => "291", "78mm" => "307", "80mm" => "315", _ => "307" };

        var newValues = new Dictionary<string, string>
        {
            ["PrinterName"] = cmbPrinter.Text,
            ["CompanyName"] = txtCompanyName.Text,
            ["CompanyAddress"] = txtAddress.Text,
            ["CompanyMobile"] = txtMobile.Text,
            ["ReceiptFooter"] = txtFooter.Text,
            ["PaperWidth"] = paperVal,
            ["PrinterMarginLeft"] = numMarginLeft.Value.ToString(),
            ["PrinterMarginRight"] = numMarginRight.Value.ToString(),
            ["PosScreenIndex"] = cmbPosScreen.SelectedIndex.ToString(),
            ["CustomerScreenIndex"] = cmbCustomerScreen.SelectedIndex.ToString(),
            ["EmailScheduleHour"] = numEmailScheduleHour.Value.ToString(),
            ["EnableOnlineOrders"] = chkEnableOnlineOrders.Checked ? "true" : "false",
            ["EnableCustomerDisplay"] = chkCustomerDisplay.Checked ? "true" : "false",
            ["CloudApiUrl"] = txtCloudApiUrl.Text
        };

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();
        try
        {
            UpsertSetting(conn, "PrinterName", cmbPrinter.Text);
            UpsertSetting(conn, "CompanyName", txtCompanyName.Text);
            UpsertSetting(conn, "CompanyAddress", txtAddress.Text);
            UpsertSetting(conn, "CompanyMobile", txtMobile.Text);
            UpsertSetting(conn, "ReceiptFooter", txtFooter.Text);
            UpsertSetting(conn, "PaperWidth", paperVal);
            UpsertSetting(conn, "PrinterMarginLeft", numMarginLeft.Value.ToString());
            UpsertSetting(conn, "PrinterMarginRight", numMarginRight.Value.ToString());
            UpsertSetting(conn, "PosScreenIndex", cmbPosScreen.SelectedIndex.ToString());
            UpsertSetting(conn, "CustomerScreenIndex", cmbCustomerScreen.SelectedIndex.ToString());
            UpsertSetting(conn, "EmailScheduleHour", numEmailScheduleHour.Value.ToString());
            UpsertSetting(conn, "EnableOnlineOrders", chkEnableOnlineOrders.Checked ? "true" : "false");
            UpsertSetting(conn, "EnableCustomerDisplay", chkCustomerDisplay.Checked ? "true" : "false");
            UpsertSetting(conn, "CloudApiUrl", txtCloudApiUrl.Text);

            foreach (var kv in newValues)
            {
                var oldVal = _originalSettings.GetValueOrDefault(kv.Key, "");
                if (oldVal != kv.Value)
                    AuditLogService.LogTransaction(conn, "SettingChanged", kv.Key, oldVal, kv.Value, userName);
            }

            trans.Commit();
        }
        catch (Exception ex)
        {
            trans.Rollback();
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _originalSettings.Clear();
        foreach (var kv in newValues) _originalSettings[kv.Key] = kv.Value;

        MessageBox.Show("Settings saved! Restart POS for screen changes to take effect.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void btnExportProducts_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog { Filter = "JSON files (*.json)|*.json", FileName = $"JumongPos_Products_{DateTime.Now:yyyyMMdd}.json" };
        if (sfd.ShowDialog() != DialogResult.OK) return;
        DataExporter.ExportProducts(sfd.FileName);
    }

    private void btnImportAndSync_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("This will UPDATE existing products (Price/Cost/Category) and ADD new products.\nStock will NOT be changed.\n\nContinue?", "Import & Sync", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        using var ofd = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" };
        if (ofd.ShowDialog() != DialogResult.OK) return;
        DataExporter.ImportAndSyncProducts(ofd.FileName);
    }

    private void btnBackupDb_Click(object? sender, EventArgs e)
    {
        try
        {
            var dbPath = DatabaseHelper.DbPath;
            if (!File.Exists(dbPath)) { MessageBox.Show("Database file not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            using var sfd = new SaveFileDialog { Filter = "SQLite DB (*.db)|*.db", FileName = $"JumongPos_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db" };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            File.Copy(dbPath, sfd.FileName, overwrite: true);
            var userName = string.IsNullOrEmpty(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName;
            AuditLogService.Log("DatabaseBackup", "Database", "", sfd.FileName, userName);
            MessageBox.Show($"Database backed up to:\n{sfd.FileName}", "Backup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show($"Backup failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void btnRestoreDb_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("WARNING: Restoring will replace ALL current data with the backup.\n\nThe application will restart after restore.\n\nAre you sure?", "Confirm Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        using var ofd = new OpenFileDialog { Filter = "SQLite DB (*.db)|*.db" };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        try
        {
            var dbPath = DatabaseHelper.DbPath;
            var safetyBackup = Path.Combine(Path.GetDirectoryName(dbPath)!, $"JumongPos_PreRestore_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            File.Copy(dbPath, safetyBackup, overwrite: true);

            File.Copy(ofd.FileName, dbPath, overwrite: true);

            var userName = string.IsNullOrEmpty(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName;
            AuditLogService.Log("DatabaseRestore", "Database", safetyBackup, ofd.FileName, userName);
            MessageBox.Show("Database restored. Application will now restart.", "Restore Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Restart();
            Environment.Exit(0);
        }
        catch (Exception ex) { MessageBox.Show($"Restore failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void btnAuditLog_Click(object? sender, EventArgs e)
    {
        using var form = new Form { Text = "Audit Log — Settings Changes", WindowState = FormWindowState.Maximized, StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.Sizable, MaximizeBox = true };
        var dgv = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
        dgv.DataSource = AuditLogService.GetHistory();
        var btnClose = new Button { Text = "CLOSE", Location = new Point(20, 10), Size = new Size(100, 30), Cursor = Cursors.Hand };
        btnClose.Click += (_, __) => form.Close();
        var pnlBtn = new Panel { Dock = DockStyle.Top, Height = 50 };
        pnlBtn.Controls.Add(btnClose);
        form.Controls.AddRange(new Control[] { dgv, pnlBtn });
        form.ShowDialog();
    }

    private void InitializeComponent()
    {
        var canvasBg = Color.FromArgb(10, 10, 26);
        var panelBg = Color.FromArgb(20, 20, 40);
        var inputBg = Color.FromArgb(30, 30, 55);
        var inputFg = Color.FromArgb(230, 230, 245);
        var neonTitle = Color.FromArgb(0, 245, 255);
        var dimText = Color.FromArgb(140, 140, 170);
        var borderColor = Color.FromArgb(40, 40, 70);
        var accentBlue = Color.FromArgb(72, 126, 176);
        var accentRed = Color.FromArgb(231, 76, 60);
        var accentGreen = Color.FromArgb(46, 204, 113);

        BackColor = canvasBg;
        Text = "Settings";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };
        var lblPageTitle = new Label { Text = "\u2699\uFE0F SYSTEM SETTINGS", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(250, 28) };
        btnSave = new Button { Text = "\uD83D\uDCBE SAVE SETTINGS", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(0, 8), Size = new Size(160, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentBlue, ForeColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnSave.Click += btnSave_Click;
        pnlToolbar.Controls.AddRange(new Control[] { lblPageTitle, btnSave });

        _pnlScroll = new Panel { Dock = DockStyle.Fill, BackColor = canvasBg, AutoScroll = true };
        var left = 20;
        var pnlW = 700;
        var gap = 15;
        var y = gap;

        // Helper to create a section
        Panel MakeSection(string title, int height, Control[] controls)
        {
            var p = new Panel { Location = new Point(left, y), Size = new Size(pnlW, height), BackColor = panelBg };
            p.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1); };
            var lbl = new Label { Text = title, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(15, 10), Size = new Size(400, 22) };
            p.Controls.Add(lbl);
            p.Controls.AddRange(controls);
            _pnlScroll.Controls.Add(p);
            y += height + gap;
            return p;
        }

        Button MakeBtn(string text, int x, int y, Color color, EventHandler click)
        {
            var b = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = color,
                ForeColor = Color.White,
                Location = new Point(x, y),
                Size = new Size(200, 36),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            b.Click += click;
            return b;
        }

        Label MakeDesc(string text, int x, int y)
        {
            return new Label { Text = text, Font = new Font("Segoe UI", 8F), ForeColor = dimText, Location = new Point(x, y), Size = new Size(400, 30), Padding = new Padding(0, 4, 0, 0) };
        }

        // ═══════════════════════════════════════════
        // 1. RECEIPT SETUP
        // ═══════════════════════════════════════════
        var ry = 40;
        cmbPrinter = new ComboBox { Location = new Point(180, ry), Size = new Size(260, 25), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = inputBg, ForeColor = inputFg, FlatStyle = FlatStyle.Flat };
        var lblPrinter = new Label { Text = "Receipt Printer:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, ry), Size = new Size(140, 25) };
        ry += 32;
        txtCompanyName = new TextBox { Location = new Point(180, ry), Size = new Size(260, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 9F) };
        var lblCo = new Label { Text = "Company Name:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, ry), Size = new Size(140, 25) };
        ry += 32;
        txtAddress = new TextBox { Location = new Point(180, ry), Size = new Size(260, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 9F) };
        var lblAddr = new Label { Text = "Address:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, ry), Size = new Size(140, 25) };
        ry += 32;
        txtMobile = new TextBox { Location = new Point(180, ry), Size = new Size(260, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 9F) };
        var lblMob = new Label { Text = "Mobile No:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, ry), Size = new Size(140, 25) };
        ry += 32;
        txtFooter = new TextBox { Location = new Point(180, ry), Size = new Size(260, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 9F), Text = "Thank You! Come Again!" };
        var lblFt = new Label { Text = "Footer Message:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, ry), Size = new Size(140, 25) };
        ry += 32;
        cmbPaperSize = new ComboBox { Location = new Point(180, ry), Size = new Size(100, 25), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = inputBg, ForeColor = inputFg, FlatStyle = FlatStyle.Flat };
        var lblPaper = new Label { Text = "Paper Size:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, ry), Size = new Size(140, 25) };
        numMarginLeft = new NumericUpDown { Location = new Point(380, ry), Size = new Size(60, 25), Minimum = 0, Maximum = 30, Value = 5, BackColor = inputBg, ForeColor = inputFg };
        var lblMl = new Label { Text = "Left Margin:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(290, ry), Size = new Size(80, 25) };
        ry += 32;
        numMarginRight = new NumericUpDown { Location = new Point(180, ry), Size = new Size(60, 25), Minimum = 0, Maximum = 30, Value = 2, BackColor = inputBg, ForeColor = inputFg };
        var lblMr = new Label { Text = "Right Margin:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, ry), Size = new Size(140, 25) };
        MakeSection("RECEIPT SETUP", 270, new Control[] {
            lblPrinter, cmbPrinter, lblCo, txtCompanyName, lblAddr, txtAddress,
            lblMob, txtMobile, lblFt, txtFooter, lblPaper, cmbPaperSize,
            lblMl, numMarginLeft, lblMr, numMarginRight
        });

        // ═══════════════════════════════════════════
        // 2. DISPLAY SETUP
        // ═══════════════════════════════════════════
        if (_currentUser.Role == "Admin")
        {
            var dy = 40;
            cmbPosScreen = new ComboBox { Location = new Point(180, dy), Size = new Size(260, 25), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = inputBg, ForeColor = inputFg, FlatStyle = FlatStyle.Flat };
            var lblPos = new Label { Text = "POS Screen:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, dy), Size = new Size(140, 25) };
            dy += 32;
            cmbCustomerScreen = new ComboBox { Location = new Point(180, dy), Size = new Size(260, 25), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = inputBg, ForeColor = inputFg, FlatStyle = FlatStyle.Flat };
            var lblCust = new Label { Text = "Customer Display:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, dy), Size = new Size(140, 25) };
            dy += 32;
            numEmailScheduleHour = new NumericUpDown { Location = new Point(180, dy), Size = new Size(60, 25), Minimum = 0, Maximum = 23, BackColor = inputBg, ForeColor = inputFg };
            var lblEmailSched = new Label { Text = "Email Report Hour:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, dy), Size = new Size(140, 25) };
            var lblEmailSchedHint = new Label { Text = "(24hr, e.g. 20 = 8PM - daily auto-email)", Font = new Font("Segoe UI", 8F), ForeColor = dimText, Location = new Point(250, dy + 3), Size = new Size(250, 20) };
            dy += 32;
            chkEnableOnlineOrders = new CheckBox { Text = "Enable Online Ordering", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, dy), Size = new Size(200, 25), FlatStyle = FlatStyle.Flat };
            chkCustomerDisplay = new CheckBox { Text = "Enable Customer Display", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(220, dy), Size = new Size(200, 25), FlatStyle = FlatStyle.Flat };
            dy += 32;
            var lblRestart = new Label { Text = "Screen changes take effect after restart.", Font = new Font("Segoe UI", 8F), ForeColor = dimText, Location = new Point(15, dy), Size = new Size(380, 20) };
            var lblLowStock = new Label { Text = "Low Stock\nThreshold:", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, dy + 24), Size = new Size(120, 32), TextAlign = ContentAlignment.MiddleLeft };
            var numLowStock = new NumericUpDown { Location = new Point(140, dy + 26), Size = new Size(80, 25), Minimum = 1, Maximum = 999, Value = ProductService.GetLowStockThreshold(), BackColor = inputBg, ForeColor = inputFg, BorderStyle = BorderStyle.FixedSingle };
            numLowStock.ValueChanged += (_, _) =>
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SQLiteCommand("INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('LowStockThreshold', @v)", conn);
                cmd.Parameters.AddWithValue("@v", ((int)numLowStock.Value).ToString());
                cmd.ExecuteNonQuery();
            };
            var lblLowStockHint = new Label { Text = "Products at or below this qty show orange.", Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(128, 128, 128), Location = new Point(225, dy + 26), Size = new Size(200, 22) };
            MakeSection("DISPLAY SETUP", 205, new Control[] {
                lblPos, cmbPosScreen, lblCust, cmbCustomerScreen,
                lblEmailSched, numEmailScheduleHour, lblEmailSchedHint,
                chkEnableOnlineOrders, chkCustomerDisplay, lblRestart,
                lblLowStock, numLowStock, lblLowStockHint
            });
        }

        // ═══════════════════════════════════════════
        // 3. CLOUD SYNC
        // ═══════════════════════════════════════════
        if (_currentUser.Role == "Admin")
        {
            var cy = 40;
            txtCloudApiUrl = new TextBox { Location = new Point(180, cy), Size = new Size(350, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 9F), Text = "https://api-production-99fb.up.railway.app/api" };
            var lblCloudApi = new Label { Text = "API URL:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, cy), Size = new Size(140, 25) };
            cy += 32;
            lblStoreId = new Label { Text = SyncService.StoreId, Font = new Font("Consolas", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(0, 245, 255), Location = new Point(180, cy), Size = new Size(350, 25), TextAlign = ContentAlignment.MiddleLeft };
            var lblStoreIdLabel = new Label { Text = "Store ID:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, cy), Size = new Size(140, 25) };
            cy += 32;
            var txtStoreName = new TextBox { Location = new Point(180, cy), Size = new Size(350, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 9F), Text = SyncService.StoreName, PlaceholderText = "e.g. Main Branch" };
            txtStoreName.TextChanged += (_, _) => SyncService.StoreName = txtStoreName.Text;
            var lblStoreNameLabel = new Label { Text = "Store Name:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, cy), Size = new Size(140, 25) };
            cy += 42;
            var btnX = 15;
            var btnW = 210;
            var btnH = 36;
            var descX = btnX + btnW + 10;

            btnSyncAll = MakeBtn("\u2601 SYNC ALL TO CLOUD", btnX, cy, Color.FromArgb(0, 195, 255), btnSyncAll_Click);
            var desc1 = MakeDesc("Upload all data (products, customers, sales, expenses, etc.) to cloud server", descX, cy);
            cy += 42;
            btnSyncToday = MakeBtn("\u2601 SYNC TODAY ONLY", btnX, cy, accentGreen, btnSyncToday_Click);
            var desc2 = MakeDesc("Upload only today's unsynced sales, expenses, voids, and stock trails", descX, cy);
            cy += 42;
            btnSyncFromCloud = MakeBtn("\u2B06 SYNC FROM CLOUD", btnX, cy, Color.FromArgb(241, 196, 15), btnSyncFromCloud_Click);
            btnSyncFromCloud.ForeColor = Color.FromArgb(10, 10, 26);
            var desc3 = MakeDesc("Download master catalog prices & costs from cloud (stock quantity unchanged)", descX, cy);
            cy += 42;
            btnSyncLog = MakeBtn("\uD83D\uDCCB VIEW SYNC LOG", btnX, cy, accentBlue, btnSyncLog_Click);
            var desc4 = MakeDesc("View history of all sync operations and any errors", descX, cy);
            cy += 42;
            btnUpdate = MakeBtn("\u2B07 UPDATE APP", btnX, cy, Color.FromArgb(155, 89, 182), btnUpdate_Click);
            var desc5 = MakeDesc($"Check GitHub for new version (current: v{AppVersion.Current})", descX, cy);

            MakeSection("CLOUD SYNC", 350, new Control[] {
                lblCloudApi, txtCloudApiUrl, lblStoreIdLabel, lblStoreId,
                lblStoreNameLabel, txtStoreName,
                btnSyncAll, desc1, btnSyncToday, desc2, btnSyncFromCloud, desc3,
                btnSyncLog, desc4, btnUpdate, desc5
            });
        }

        // ═══════════════════════════════════════════
        // 4. DATA MANAGEMENT
        // ═══════════════════════════════════════════
        if (_currentUser.Role == "Admin")
        {
            var dy = 40;
            var btnX = 15;
            var btnW = 210;
            var btnH = 36;
            var descX = btnX + btnW + 10;

            btnExportProducts = MakeBtn("\uD83D\uDCE4 EXPORT PRODUCTS", btnX, dy, accentBlue, btnExportProducts_Click);
            var d1 = MakeDesc("Save products list to a JSON file for backup or transfer", descX, dy);
            dy += 42;
            btnImportAndSync = MakeBtn("\uD83D\uDCE5 IMPORT & SYNC PROD", btnX, dy, accentRed, btnImportAndSync_Click);
            var d2 = MakeDesc("Update products from JSON file (updates price/cost/category, adds new)", descX, dy);
            dy += 42;
            btnBackupDb = MakeBtn("\uD83D\uDCBE BACKUP DATABASE", btnX, dy, accentGreen, btnBackupDb_Click);
            var d3 = MakeDesc("Create a full backup of the local database", descX, dy);
            dy += 42;
            btnRestoreDb = MakeBtn("\uD83D\uDD04 RESTORE DATABASE", btnX, dy, Color.FromArgb(243, 156, 18), btnRestoreDb_Click);
            var d4 = MakeDesc("Restore database from a backup file (app will restart)", descX, dy);
            dy += 42;
            btnAuditLog = MakeBtn("\uD83D\uDCDD AUDIT LOG", btnX, dy, Color.FromArgb(155, 89, 182), btnAuditLog_Click);
            var d5 = MakeDesc("View history of all setting changes for security tracking", descX, dy);

            MakeSection("DATA MANAGEMENT", 255, new Control[] {
                btnExportProducts, d1, btnImportAndSync, d2,
                btnBackupDb, d3, btnRestoreDb, d4, btnAuditLog, d5
            });
        }

        Controls.AddRange(new Control[] { pnlToolbar, _pnlScroll });
    }

    private static void ShowSyncProgress(string title, Func<IProgress<string>, int> work)
    {
        var form = new Form
        {
            Text = title,
            Size = new Size(380, 110),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ControlBox = false,
            ShowInTaskbar = false,
            TopMost = true,
            BackColor = Color.FromArgb(20, 20, 40),
            ForeColor = Color.FromArgb(230, 230, 245)
        };
        var bar = new ProgressBar { Location = new Point(15, 15), Size = new Size(350, 28), Style = ProgressBarStyle.Continuous, ForeColor = Color.FromArgb(46, 204, 113) };
        var lbl = new Label { Text = "Preparing...", Location = new Point(15, 50), Size = new Size(350, 22), ForeColor = Color.FromArgb(0, 245, 255) };
        form.Controls.AddRange(new Control[] { bar, lbl });
        form.Load += async (_, _) =>
        {
            var progress = new Progress<string>(m =>
            {
                try { form.Invoke(() => { lbl.Text = m; }); } catch { }
            });
            try
            {
                var count = await Task.Run(() => work(progress));
                try { form.Invoke(() => { lbl.Text = "Complete! " + count + " items. Closing..."; }); } catch { }
            }
            catch (Exception ex) { try { form.Invoke(() => { lbl.Text = "Error: " + ex.Message; }); } catch { } }
            await Task.Delay(1500);
            try { form.Close(); } catch { }
        };
        form.Show();
    }

    private void btnSyncAll_Click(object? sender, EventArgs e)
    {
        ShowSyncProgress("Syncing All To Cloud...", p =>
        {
            var products = ProductService.GetAll();
            var customers = CustomerService.GetAll();
            var users = UserService.GetAll();
            var expenses = ExpenseService.GetExpensesBetween(null, DateTime.MaxValue);
            var voids = SaleService.GetVoidLogs();
            var dailyCloses = DailyCloseService.GetHistory();
            var stockTrails = StockService.GetTrail(limit: 10000);
            var creditTxns = CreditService.GetAll();
            var sales = SaleService.GetSales();
            var total = products.Count + customers.Count + users.Count + expenses.Count + voids.Count + dailyCloses.Count + stockTrails.Count + creditTxns.Count + sales.Count;
            var done = 0;
            foreach (var x in products) { p.Report($"Products: {++done}/{total}"); SyncService.SyncProduct(x).Wait(); Thread.Sleep(20); }
            foreach (var x in customers) { p.Report($"Customers: {++done}/{total}"); SyncService.SyncCustomer(x).Wait(); Thread.Sleep(20); }
            foreach (var x in users) { p.Report($"Users: {++done}/{total}"); SyncService.SyncUser(x).Wait(); Thread.Sleep(20); }
            foreach (var x in expenses) { p.Report($"Expenses: {++done}/{total}"); SyncService.SyncExpense(x).Wait(); Thread.Sleep(20); }
            foreach (var x in voids) { p.Report($"Voids: {++done}/{total}"); SyncService.SyncVoidLog(x).Wait(); Thread.Sleep(20); }
            foreach (var x in dailyCloses) { p.Report($"Shifts: {++done}/{total}"); SyncService.SyncDailyClose(x).Wait(); Thread.Sleep(20); }
            foreach (var x in stockTrails) { p.Report($"Stock: {++done}/{total}"); SyncService.SyncStockTrail(x).Wait(); Thread.Sleep(20); }
            foreach (var x in creditTxns) { p.Report($"Credit: {++done}/{total}"); SyncService.SyncCreditTransaction(x).Wait(); Thread.Sleep(20); }
            foreach (var s in sales) { p.Report($"Sales: {++done}/{total} (+ items)"); var items = SaleService.GetSaleItems(s.Id); SyncService.SyncSale(s, items).Wait(); Thread.Sleep(50); done += items.Count; }
            return total;
        });
    }

    private void btnSyncToday_Click(object? sender, EventArgs e)
    {
        ShowSyncProgress("Syncing Today...", p =>
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var todayStart = today + " 00:00:00";
            var todayEnd = today + " 23:59:59";
            var sales = SaleService.GetSales(from: DateTime.Parse(todayStart), to: DateTime.Parse(todayEnd));
            var unsyncedSales = new List<Sale>();
            foreach (var s in sales) { s.Items = SaleService.GetSaleItems(s.Id); if (!s.Synced) unsyncedSales.Add(s); }
            var expenses = ExpenseService.GetExpensesBetween(todayStart, DateTime.Parse(todayEnd));
            var voids = SaleService.GetVoidLogs().Where(v => v.CreatedAt?.StartsWith(today) == true).ToList();
            var stockTrails = StockService.GetTrail(limit: 10000).Where(t => t.CreatedAt?.StartsWith(today) == true).ToList();
            var creditTxns = CreditService.GetAll().Where(ct => ct.CreatedAt?.StartsWith(today) == true).ToList();
            var total = unsyncedSales.Count + expenses.Count + voids.Count + stockTrails.Count + creditTxns.Count;
            var done = 0;
            if (total == 0) { p.Report("Nothing to sync today."); Thread.Sleep(1500); return 0; }
            foreach (var x in unsyncedSales) { p.Report($"Sales: {++done}/{total}"); SyncService.SyncSale(x, x.Items).Wait(); }
            foreach (var x in expenses) { p.Report($"Expenses: {++done}/{total}"); SyncService.SyncExpense(x).Wait(); }
            foreach (var x in voids) { p.Report($"Voids: {++done}/{total}"); SyncService.SyncVoidLog(x).Wait(); }
            foreach (var x in stockTrails) { p.Report($"Stock: {++done}/{total}"); SyncService.SyncStockTrail(x).Wait(); }
            foreach (var x in creditTxns) { p.Report($"Credit: {++done}/{total}"); SyncService.SyncCreditTransaction(x).Wait(); }
            return total;
        });
    }

    private void btnSyncLog_Click(object? sender, EventArgs e)
    {
        var canvasBg = Color.FromArgb(10, 10, 26);
        var panelBg = Color.FromArgb(20, 20, 40);
        var neonTitle = Color.FromArgb(0, 245, 255);
        var borderColor = Color.FromArgb(40, 40, 70);
        var accentGreen = Color.FromArgb(46, 204, 113);
        var accentRed = Color.FromArgb(231, 76, 60);

        using var form = new Form { Text = "Cloud Sync Log", WindowState = FormWindowState.Maximized, StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.Sizable, MaximizeBox = true, BackColor = canvasBg };

        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
        pnlToolbar.Paint += (s, ev) => { using var pen = new Pen(borderColor, 1); ev.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };
        var lblTitle = new Label { Text = "\uD83D\uDCCB CLOUD SYNC LOG", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(300, 28) };

        var btnClear = new Button { Text = "CLEAR LOG", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, BackColor = accentRed, ForeColor = Color.White, Location = new Point(350, 8), Size = new Size(120, 34), Cursor = Cursors.Hand };
        btnClear.Click += (_, _) =>
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var c = new SQLiteCommand("DELETE FROM SyncLog", conn);
            c.ExecuteNonQuery();
            form.Close();
        };
        pnlToolbar.Controls.AddRange(new Control[] { lblTitle, btnClear });

        var dgv = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = panelBg, BorderStyle = BorderStyle.None, GridColor = Color.FromArgb(40, 40, 70), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, Font = new Font("Consolas", 9F), ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(25, 25, 50), ForeColor = neonTitle, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, ColumnHeadersHeight = 30, EnableHeadersVisualStyles = false, DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(22, 22, 45), ForeColor = Color.FromArgb(230, 230, 245), SelectionBackColor = Color.FromArgb(40, 40, 80), SelectionForeColor = Color.White }, RowTemplate = { Height = 26 }, AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(15, 15, 32) } };

        dgv.AutoGenerateColumns = false;
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedAt", HeaderText = "TIME", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Endpoint", HeaderText = "ENDPOINT", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "STATUS", Width = 70, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = accentGreen } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Error", HeaderText = "ERROR", Width = 400, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(231, 76, 60) }, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

        using var conn2 = DatabaseHelper.GetConnection();
        conn2.Open();
        using var cmd2 = new SQLiteCommand("SELECT CreatedAt, Endpoint, Status, Error FROM SyncLog ORDER BY Id DESC LIMIT 500", conn2);
        var table = new DataTable();
        table.Load(cmd2.ExecuteReader());

        foreach (DataRow row in table.Rows)
        {
            var idx = dgv.Rows.Add(row["CreatedAt"], row["Endpoint"], row["Status"], row["Error"]);
            if (row["Status"].ToString() == "FAIL" || row["Status"].ToString() == "ERROR")
                dgv.Rows[idx].DefaultCellStyle.ForeColor = accentRed;
        }

        form.Controls.AddRange(new Control[] { dgv, pnlToolbar });
        form.Controls.SetChildIndex(pnlToolbar, 0);
        form.ShowDialog();
    }

    private async void btnSyncFromCloud_Click(object? sender, EventArgs e)
    {
        btnSyncFromCloud.Enabled = false;
        btnSyncFromCloud.Text = "SYNCING...";
        var progress = new Progress<string>(m => { if (m.StartsWith("Complete") || m.StartsWith("Error")) btnSyncFromCloud.Text = "\u2B06 SYNC FROM CLOUD"; });
        var count = await SyncService.DownloadMasterCatalog(progress);
        if (count > 0)
            MessageBox.Show($"Synced {count} products from cloud.\nPrice, Cost, and Units updated.\nStock was NOT changed.", "Sync Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show("No updates from cloud.", "Sync Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        btnSyncFromCloud.Enabled = true;
    }

    private async void btnUpdate_Click(object? sender, EventArgs e)
    {
        btnUpdate.Enabled = false;
        btnUpdate.Text = "CHECKING...";
        var (available, version, changes, downloadUrl) = await UpdateService.CheckUpdate();
        if (!available)
        {
            MessageBox.Show("You're on the latest version.", "Up to Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
            btnUpdate.Enabled = true;
            btnUpdate.Text = "\u2B07 UPDATE APP";
            return;
        }

        var result = MessageBox.Show($"New version {version} available!\n\nChanges: {changes}\n\nDownload and install update?", "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes) { btnUpdate.Enabled = true; btnUpdate.Text = "\u2B07 UPDATE APP"; return; }

        btnUpdate.Text = "DOWNLOADING...";
        var progress = new Progress<int>(p => btnUpdate.Text = $"DOWNLOADING {p}%");
        var ok = await UpdateService.DownloadAndUpdate(downloadUrl ?? "", progress);
        if (!ok)
        {
            MessageBox.Show("Download failed. Check your connection and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnUpdate.Enabled = true;
            btnUpdate.Text = "\u2B07 UPDATE APP";
        }
    }

    private Button btnSave = new();
    private ComboBox cmbPrinter = null!;
    private TextBox txtCompanyName = new();
    private TextBox txtAddress = new();
    private TextBox txtMobile = new();
    private TextBox txtFooter = new();
    private ComboBox cmbPaperSize = new();
    private NumericUpDown numMarginLeft = new();
    private NumericUpDown numMarginRight = new();
    private ComboBox cmbPosScreen = null!;
    private ComboBox cmbCustomerScreen = null!;
    private NumericUpDown numEmailScheduleHour = new();
    private CheckBox chkEnableOnlineOrders = new();
    private CheckBox chkCustomerDisplay = new();
    private TextBox txtCloudApiUrl = new();
    private Label lblStoreId = new();
    private Button btnSyncAll = null!;
    private Button btnSyncToday = null!;
    private Button btnSyncLog = null!;
    private Button btnSyncFromCloud = null!;
    private Button btnUpdate = null!;
    private Button btnExportProducts = null!;
    private Button btnImportAndSync = null!;
    private Button btnBackupDb = null!;
    private Button btnRestoreDb = null!;
    private Button btnAuditLog = null!;
}
