using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class ProductsForm : Form
{
    private Product? _selected;
    private readonly string? _prefillBarcode;
    private readonly User? _currentUser;
    private bool _isEditing;
    private bool _suppressSearch;
    private string? _stockFilter;

    public ProductsForm(User? currentUser = null, string? prefillBarcode = null)
    {
        _currentUser = currentUser;
        _prefillBarcode = prefillBarcode;
        InitializeComponent();
        LoadCategories();
        LoadProducts();
        UpdateStats();
        SetReadOnly(true);
        if (_prefillBarcode != null)
            txtBarcode.Text = _prefillBarcode;
        DebugHelper.AddFormLabel(this);
    }

    private void LoadCategories()
    {
        cmbFilterCategory.Items.Clear();
        cmbFilterCategory.Items.Add("All Categories");
        cmbFilterCategory.Items.AddRange(ProductService.GetCategories().ToArray<object>());
        cmbFilterCategory.SelectedIndex = 0;
        cmbCategory.Items.Clear();
        cmbCategory.Items.AddRange(ProductService.GetCategories().ToArray<object>());
    }

    private void LoadProducts()
    {
        _suppressSearch = true;
        var keyword = txtSearch.Text.Trim();
        var cat = cmbFilterCategory.SelectedIndex > 0 ? cmbFilterCategory.Text : null;

        var data = ProductService.Search(keyword, cat, _stockFilter);
        dgvProducts.AutoGenerateColumns = false;
        dgvProducts.Columns.Clear();
        var tDgv = ThemeManager.Current;
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", HeaderText = "ID", Width = 45, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = tDgv.TextMuted } });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "PRODUCT NAME", Width = 250, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = tDgv.TextPrimary } });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Barcode", HeaderText = "BARCODE", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = tDgv.TextSecondary } });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Category", HeaderText = "CATEGORY", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = tDgv.TextSecondary } });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Price", HeaderText = "PRICE", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = tDgv.AccentCyan } });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Cost", HeaderText = "COST", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = tDgv.AccentRed } });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StockQty", HeaderText = "STOCK", Width = 65, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = tDgv.TextPrimary, Font = new Font("Segoe UI", 9F, FontStyle.Bold) } });
        dgvProducts.DataSource = data;
        dgvProducts.RowHeadersVisible = false;
        dgvProducts.BackgroundColor = tDgv.PanelBg;
        dgvProducts.BorderStyle = BorderStyle.None;
        dgvProducts.GridColor = tDgv.DgvGrid;
        dgvProducts.ColumnHeadersDefaultCellStyle.BackColor = tDgv.DgvHeaderBg;
        dgvProducts.ColumnHeadersDefaultCellStyle.ForeColor = tDgv.DgvHeaderFg;
        dgvProducts.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgvProducts.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvProducts.ColumnHeadersHeight = 30;
        dgvProducts.EnableHeadersVisualStyles = false;
        dgvProducts.DefaultCellStyle.SelectionBackColor = tDgv.DgvSelection;
        dgvProducts.DefaultCellStyle.SelectionForeColor = Color.White;
        dgvProducts.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        dgvProducts.RowTemplate.Height = 28;
        dgvProducts.AlternatingRowsDefaultCellStyle.BackColor = tDgv.DgvRowAlt;
        dgvProducts.DefaultCellStyle.BackColor = tDgv.DgvRowNormal;
        dgvProducts.DefaultCellStyle.ForeColor = tDgv.TextPrimary;
        _suppressSearch = false;
    }

    private void dgvProducts_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.ColumnIndex == dgvProducts.Columns["StockQty"]?.Index && e.RowIndex >= 0)
        {
            if (dgvProducts.Rows[e.RowIndex].DataBoundItem is Product val)
            {
                if (val.StockQty == 0)
                {
                    e.CellStyle!.ForeColor = ThemeManager.Current.AccentRed;
                    e.CellStyle.SelectionForeColor = ThemeManager.Current.AccentRed;
                }
                else if (val.StockQty <= ProductService.GetLowStockThreshold())
                {
                    e.CellStyle!.ForeColor = ThemeManager.Current.AccentOrange;
                    e.CellStyle.SelectionForeColor = ThemeManager.Current.AccentOrange;
                }
            }
        }
    }

    private void UpdateStats()
    {
        var (total, low, outOf) = ProductService.GetStockStats();
        var (retailValue, costValue) = ProductService.GetStockValues();

        lblMetricTotal.Text = $"TOTAL: {total}";
        lblMetricLow.Text = $"LOW STOCK: {low}";
        lblMetricOut.Text = $"OUT OF STOCK: {outOf}";
        lblMetricRetail.Text = $"RETAIL: \u20b1{retailValue:N2}";
        lblMetricCost.Text = $"COST: \u20b1{costValue:N2}";

        var tStats = ThemeManager.Current;
        lblMetricTotal.ForeColor = _stockFilter == null ? tStats.AccentCyan : tStats.TextMuted;
        lblMetricLow.ForeColor = _stockFilter == "low" ? tStats.AccentOrange : tStats.TextMuted;
        lblMetricOut.ForeColor = _stockFilter == "out" ? tStats.AccentRed : tStats.TextMuted;
    }

    private void ToggleStockFilter(string? filter)
    {
        _stockFilter = _stockFilter == filter ? null : filter;
        LoadProducts();
        UpdateStats();
    }

    private void TriggerSearch(object? sender, EventArgs e)
    {
        if (!_suppressSearch) LoadProducts();
    }

    private void SetReadOnly(bool readOnly)
    {
        _isEditing = !readOnly;
        if (readOnly)
        {
            UpdateDisplayLabels();
            txtName.Visible = false;   _lblNameDisplay.Visible = true;
            txtBarcode.Visible = false; _lblBarcodeDisplay.Visible = true;
            txtPrice.Visible = false;   _lblPriceDisplay.Visible = true;
            txtCost.Visible = false;    _lblCostDisplay.Visible = true;
            txtStock.Visible = false;   _lblStockDisplay.Visible = true;
        }
        else
        {
            _lblNameDisplay.Visible = false;   txtName.Visible = true;
            _lblBarcodeDisplay.Visible = false; txtBarcode.Visible = true;
            _lblPriceDisplay.Visible = false;   txtPrice.Visible = true;
            _lblCostDisplay.Visible = false;    txtCost.Visible = true;
            _lblStockDisplay.Visible = false;   txtStock.Visible = true;
        }
        cmbCategory.Enabled = !readOnly;
        btnStockMovement.Visible = true;
    }

    private void dgvProducts_SelectionChanged(object? sender, EventArgs e)
    {
        if (_isEditing) return;
        if (dgvProducts.CurrentRow?.DataBoundItem is Product p)
        {
            _selected = p;
            txtName.Text = p.Name;
            txtBarcode.Text = p.Barcode;
            cmbCategory.Text = p.Category;
            txtPrice.Text = p.Price.ToString("N2");
            txtCost.Text = p.Cost.ToString("N2");
            txtStock.Text = p.StockQty.ToString();
            lblFormTitle.Text = $"EDIT: {p.Name}";
            lblFormTitle.ForeColor = Color.FromArgb(46, 204, 113);
            _picProduct.Image = Base64ToImage(p.ImageData);
            SetReadOnly(true);
        }
    }

    private static Image? Base64ToImage(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        try
        {
            var clean = data;
            var idx = data.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) clean = data[(idx + 7)..];
            clean = clean.Trim();
            var bytes = Convert.FromBase64String(clean);
            var ms = new MemoryStream(bytes);
            return Image.FromStream(ms);
        }
        catch (Exception ex) { ErrorLogger.Log("ProductsForm.Base64ToImage", ex); return null; }
    }

    private void btnNew_Click(object? sender, EventArgs e)
    {
        _selected = null;
        txtName.Clear();
        txtBarcode.Clear();
        cmbCategory.Text = "";
        txtPrice.Clear();
        txtCost.Clear();
        txtStock.Text = "0";
        lblFormTitle.Text = "NEW PRODUCT";
        lblFormTitle.ForeColor = Color.FromArgb(0, 245, 255);
        SetReadOnly(false);
        txtName.Focus();
    }

    private void btnEdit_Click(object? sender, EventArgs e)
    {
        if (_selected == null) return;
        SetReadOnly(false);
        txtName.Focus();
    }

    private void btnUnits_Click(object? sender, EventArgs e)
    {
        if (_selected == null) return;
        using var form = new ProductUnitsForm(_selected.Id, _selected.Name);
        form.ShowDialog();
    }

    private void btnCancel_Click(object? sender, EventArgs e)
    {
        if (_selected != null)
        {
            txtName.Text = _selected.Name;
            txtBarcode.Text = _selected.Barcode;
            cmbCategory.Text = _selected.Category;
            txtPrice.Text = _selected.Price.ToString("N2");
            txtCost.Text = _selected.Cost.ToString("N2");
            txtStock.Text = _selected.StockQty.ToString();
        }
        SetReadOnly(true);
    }

    private void btnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("Product name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var p = _selected ?? new Product();
        p.Name = txtName.Text.Trim();
        p.Barcode = txtBarcode.Text.Trim();
        p.Category = cmbCategory.Text.Trim();
        p.Price = decimal.TryParse(txtPrice.Text, out var price) ? price : 0;
        p.Cost = decimal.TryParse(txtCost.Text, out var cost) ? cost : 0;
        p.StockQty = int.TryParse(txtStock.Text, out var stock) ? stock : 0;
        if (p.Id == 0) p.IsActive = true;

        var modifiedBy = _currentUser != null && !string.IsNullOrEmpty(_currentUser.FullName)
            ? _currentUser.FullName : _currentUser?.Username ?? "";
        try
        {
            ProductService.Save(p, modifiedBy);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Duplicate Barcode", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_selected != null)
        {
            var units = ProductUnitService.GetByProduct(p.Id);
            foreach (var unit in units)
            {
                unit.Price = p.Price;
                unit.Cost = p.Cost;
                ProductUnitService.Save(unit);
            }
        }

        LoadCategories();
        LoadProducts();
        UpdateStats();
        ClearForm();
    }

    private void btnDelete_Click(object? sender, EventArgs e)
    {
        if (_selected == null) return;
        if (MessageBox.Show($"Delete '{_selected.Name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            ProductService.Delete(_selected.Id);
            LoadProducts();
            UpdateStats();
            ClearForm();
        }
    }

    private void btnPrintChecklist_Click(object? sender, EventArgs e)
    {
        var cat = cmbFilterCategory.SelectedIndex > 0 ? cmbFilterCategory.Text : null;
        var allProducts = ProductService.Search("", cat, null);
        if (allProducts == null || allProducts.Count == 0)
        {
            MessageBox.Show("No products found in the database to generate a checklist.", "Empty Inventory", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var printerName = GetPrintSetting("PrinterName");
        if (string.IsNullOrEmpty(printerName))
        {
            MessageBox.Show("No printer configured. Go to Settings to set a printer.", "Printer Not Set",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var paperW = int.TryParse(GetPrintSetting("PaperWidth"), out var pw) ? pw : 315;
        var marginL = int.TryParse(GetPrintSetting("PrinterMarginLeft"), out var ml) ? ml : 0;
        var marginR = int.TryParse(GetPrintSetting("PrinterMarginRight"), out var mr) ? mr : 0;

        var lineChars = (int)((paperW - marginL - marginR) * 12 / 100);
        if (lineChars < 20) lineChars = 20;
        if (lineChars > 58) lineChars = 58;

        var groupedProducts = allProducts
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Category) ? "UNASSIGNED" : p.Category.ToUpper())
            .OrderBy(g => g.Key)
            .ToList();

        var nameW = lineChars - 11;
        if (nameW < 10) nameW = 10;
        var sep = new string('=', lineChars);
        var dash = new string('-', lineChars);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(sep);
        sb.AppendLine("INVENTORY STOCK CHECKLIST".PadLeft((lineChars + 28) / 2).PadRight(lineChars));
        var cashierName = _currentUser != null && !string.IsNullOrEmpty(_currentUser.FullName) ? _currentUser.FullName : _currentUser?.Username ?? "";
        var storeName = SyncService.StoreName;
        var storeId = SyncService.StoreId;
        var storeLine = $"Store: {storeName} ({storeId})";
        var cashierLine = $"Cashier: {cashierName}";
        var printedLine = $"Printed: {TimeHelper.Now:yyyy-MM-dd hh:mm tt}";
        sb.AppendLine(storeLine.PadLeft((lineChars + storeLine.Length) / 2).PadRight(lineChars));
        sb.AppendLine(cashierLine.PadLeft((lineChars + cashierLine.Length) / 2).PadRight(lineChars));
        sb.AppendLine(printedLine.PadLeft((lineChars + printedLine.Length) / 2).PadRight(lineChars));
        sb.AppendLine(sep);
        sb.AppendLine();

        foreach (var group in groupedProducts)
        {
            sb.AppendLine($"CATEGORY: {group.Key}");
            sb.AppendLine(dash);
            sb.AppendLine(string.Format("{0,-" + nameW + "} {1,6}|CNT", "PRODUCT NAME", "SYS"));
            sb.AppendLine(dash);

            foreach (var prod in group.OrderBy(p => p.Name))
            {
                var name = prod.Name;
                var stockQty = prod.StockQty.ToString().PadLeft(5);
                if (name.Length > nameW)
                {
                    var firstLine = name.Substring(0, nameW);
                    var rest = name.Substring(nameW);
                    if (rest.Length > nameW + 2)
                        rest = rest.Substring(0, nameW + 2) + "\u2026";
                    sb.AppendLine(string.Format("{0,-" + nameW + "} {1,6}|___", firstLine, stockQty));
                    sb.AppendLine(string.Format("  {0,-" + (nameW - 2) + "} {1,6}|", rest, "").PadRight(lineChars));
                }
                else
                {
                    sb.AppendLine(string.Format("{0,-" + nameW + "} {1,6}|___", name, stockQty));
                }
            }
            sb.AppendLine();
        }
        
        sb.AppendLine(sep);
        sb.AppendLine("Checked By: ______________________");
        sb.AppendLine("Signature:  ______________________");

        var reportText = sb.ToString();

        var doc = new System.Drawing.Printing.PrintDocument();
        doc.PrinterSettings.PrinterName = printerName;

        doc.PrintPage += (s, args) =>
        {
            var pageW = args.PageBounds.Width;
            var leftMargin = pageW * marginL / paperW;
            var rightMargin = pageW * marginR / paperW;

            using var font = new Font("Courier New", 9F);
            var sf = StringFormat.GenericTypographic;
            var lines = reportText.Split('\n');
            if (args.Graphics == null) return;
            
            float yPos = 5f;
            var lineH = font.GetHeight();
            foreach (var line in lines)
            {
                var clean = line.Replace("\r", "");
                args.Graphics.DrawString(clean, font, Brushes.Black, leftMargin, yPos, sf);
                yPos += lineH;
                if (yPos > args.MarginBounds.Bottom)
                    break;
            }
        };

        using var dlg = new PrintDialog { Document = doc };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try { doc.Print(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Print error: {ex.Message}", "Print Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private static string GetPrintSetting(string key)
    {
        using var conn = JumongPosV1._01.Data.DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new System.Data.SQLite.SQLiteCommand("SELECT Value FROM Settings WHERE Key = @key", conn);
        cmd.Parameters.AddWithValue("@key", key);
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    private void ClearForm()
    {
        _selected = null;
        txtName.Clear();
        txtBarcode.Clear();
        cmbCategory.Text = "";
        txtPrice.Clear();
        txtCost.Clear();
        txtStock.Clear();
        lblFormTitle.Text = "NEW PRODUCT";
        lblFormTitle.ForeColor = Color.FromArgb(0, 245, 255);
        SetReadOnly(true);
    }

    private void InitializeComponent()
    {
        var t = ThemeManager.Current;
        var canvasBg = t.CanvasBg;
        var panelBg = t.PanelBg;
        var inputBg = t.InputBg;
        var inputFg = t.InputFg;
        var neonTitle = t.AccentCyan;
        var metaText = t.TextPrimary;
        var dimText = t.TextMuted;
        var borderColor = t.BorderColor;
        var accentBlue = t.AccentBlue;
        var accentGreen = t.AccentGreen;
        var accentRed = t.AccentRed;
        var accentOrange = t.AccentOrange;
        var accentPurple = t.AccentPurple;

        BackColor = canvasBg;
        Text = "Manage Products";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        // ── TOP TOOLBAR ──
        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };

        var lblPageTitle = new Label { Text = "\uD83D\uDCE6 PRODUCT MANAGEMENT", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(300, 28) };

        var searchY = 12;
        var searchH = 28;
        txtSearch = new TextBox { Font = new Font("Segoe UI", 10F), Location = new Point(340, searchY), Size = new Size(220, searchH), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg };
        txtSearch.TextChanged += TriggerSearch;

        var lblSearchIcon = new Label { Text = "\uD83D\uDD0D", Font = new Font("Segoe UI", 10F), Location = new Point(340 - 25 - 5, searchY), Size = new Size(25, searchH), TextAlign = ContentAlignment.MiddleRight };

        var lblCatFilter = new Label { Text = "Category:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(580, 16), Size = new Size(65, 20), TextAlign = ContentAlignment.MiddleRight };
        cmbFilterCategory = new ComboBox { Location = new Point(650, 12), Size = new Size(150, 28), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = inputBg, ForeColor = inputFg };
        cmbFilterCategory.SelectedIndexChanged += TriggerSearch;

        var btnUpdateMaster = new Button { Text = "UPDATE MASTER", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(940, 12), Size = new Size(150, 28), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(0, 150, 136), ForeColor = Color.White, Cursor = Cursors.Hand };
        btnUpdateMaster.Click += (_, _) =>
        {
            var form = new Form
            {
                Text = "Updating Master Catalog...",
                Size = new Size(400, 120),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ControlBox = false,
                ShowInTaskbar = false,
                TopMost = true,
                BackColor = Color.FromArgb(20, 20, 40),
                ForeColor = Color.FromArgb(230, 230, 245)
            };
            var bar = new ProgressBar { Location = new Point(15, 15), Size = new Size(360, 28), Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, ForeColor = Color.FromArgb(0, 150, 136) };
            var lbl = new Label { Text = "Starting...", Location = new Point(15, 55), Size = new Size(360, 22), ForeColor = Color.FromArgb(0, 245, 255) };
            form.Controls.AddRange(new Control[] { bar, lbl });
            form.Load += async (_, __) =>
            {
                var p = new Progress<string>(m => { try { form.Invoke(() => lbl.Text = m); } catch { } });
                try
                {
                    var count = await SyncService.DownloadUpdatedMasterCatalog(p);
                    try { form.Invoke(() => { lbl.Text = count > 0 ? $"Updated {count} products." : "No changes found."; }); } catch { }
                    await Task.Delay(1000);
                    _suppressSearch = true;
                    LoadProducts();
                    UpdateStats();
                    _suppressSearch = false;
                }
                catch (Exception ex)
                {
                    try { form.Invoke(() => lbl.Text = "Error: " + ex.Message); } catch { }
                    await Task.Delay(2000);
                }
                try { form.Close(); } catch { }
            };
            form.ShowDialog();
        };

        var btnInvCheck = new Button { Text = "\uD83D\uDCCA INV CHECK", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(1100, 12), Size = new Size(110, 28), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(100, 80, 180), ForeColor = Color.White, Cursor = Cursors.Hand };
        btnInvCheck.Click += ShowInventoryCheck;

        pnlToolbar.Controls.AddRange(new Control[] { lblPageTitle, lblSearchIcon, txtSearch, lblCatFilter, cmbFilterCategory, btnUpdateMaster, btnInvCheck });

        // ── METRICS BAR ──
        var pnlMetrics = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = canvasBg };
        lblMetricTotal = CreateMetricLabel(20, dimText);
        lblMetricTotal.Size = new Size(160, 20);
        lblMetricLow = CreateMetricLabel(180, accentOrange);
        lblMetricLow.Size = new Size(180, 20);
        lblMetricOut = CreateMetricLabel(360, accentRed);
        lblMetricOut.Size = new Size(180, 20);
        lblMetricRetail = CreateMetricLabel(540, neonTitle);
        lblMetricRetail.Size = new Size(200, 20);
        lblMetricCost = CreateMetricLabel(740, accentGreen);
        lblMetricCost.Size = new Size(200, 20);
        pnlMetrics.Controls.AddRange(new Control[] { lblMetricTotal, lblMetricLow, lblMetricOut, lblMetricRetail, lblMetricCost });
        lblMetricTotal.Click += (_, _) => { _stockFilter = null; LoadProducts(); UpdateStats(); };
        lblMetricLow.Click += (_, _) => ToggleStockFilter("low");
        lblMetricOut.Click += (_, _) => ToggleStockFilter("out");

        // ── MAIN SPLIT ──
        var pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = canvasBg };

        // LEFT - Grid
        var pnlLeft = new Panel { Location = new Point(10, 10), Size = new Size(600, 400), BackColor = panelBg };
        pnlLeft.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlLeft.Width - 1, pnlLeft.Height - 1); };

        var lblGridTitle = new Label { Text = "PRODUCT INVENTORY", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(12, 8), Size = new Size(200, 20) };
        dgvProducts = new DataGridView
        {
            Location = new Point(8, 32),
            Size = new Size(584, 360),
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            Font = new Font("Segoe UI", 9F),
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
        };
        dgvProducts.SelectionChanged += dgvProducts_SelectionChanged;
        dgvProducts.CellFormatting += dgvProducts_CellFormatting;
        dgvProducts.DoubleClick += (_, _) => { if (dgvProducts.CurrentRow?.DataBoundItem is Product p) using (var form = new StockMovementForm(p, _currentUser)) form.ShowDialog(); };
        pnlLeft.Controls.AddRange(new Control[] { lblGridTitle, dgvProducts });

        // RIGHT - Entry Card
        var pnlRight = new Panel { Location = new Point(625, 10), Size = new Size(340, 400), BackColor = panelBg };
        pnlRight.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlRight.Width - 1, pnlRight.Height - 1); };

        lblFormTitle = new Label { Text = "NEW PRODUCT", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(15, 10), Size = new Size(310, 25), TextAlign = ContentAlignment.MiddleLeft };

        var y = 42;
        AddField("NAME", ref txtName, ref y, pnlRight, inputBg, inputFg, dimText);
        AddField("BARCODE", ref txtBarcode, ref y, pnlRight, inputBg, inputFg, dimText);
        AddComboField("CATEGORY", ref cmbCategory, ref y, pnlRight, inputBg, inputFg, dimText);
        AddField("PRICE", ref txtPrice, ref y, pnlRight, inputBg, inputFg, dimText, HorizontalAlignment.Right);
        AddField("COST", ref txtCost, ref y, pnlRight, inputBg, inputFg, dimText, HorizontalAlignment.Right);
        AddField("STOCK", ref txtStock, ref y, pnlRight, ThemeManager.Current.PanelBg, ThemeManager.Current.TextMuted, dimText, HorizontalAlignment.Right);

        _lblNameDisplay = MakeDisplayLabel(txtName, t.PanelBg, t.TextPrimary);
        _lblBarcodeDisplay = MakeDisplayLabel(txtBarcode, t.PanelBg, t.TextPrimary);
        _lblPriceDisplay = MakeDisplayLabel(txtPrice, t.PanelBg, t.TextPrimary, HorizontalAlignment.Right);
        _lblCostDisplay = MakeDisplayLabel(txtCost, t.PanelBg, t.TextPrimary, HorizontalAlignment.Right);
        _lblStockDisplay = MakeDisplayLabel(txtStock, t.PanelBg, t.TextMuted, HorizontalAlignment.Right);

        _picProduct = new PictureBox
        {
            Location = new Point(110, y + 4),
            Size = new Size(120, 120),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(20, 20, 35),
            BorderStyle = BorderStyle.FixedSingle
        };
        pnlRight.Controls.Add(_picProduct);
        y += 128;

        btnStockMovement = new Button { Text = "\uD83D\uDCC8 STOCK MOV'T", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(15, y), Size = new Size(200, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnStockMovement.Click += (_, _) => { if (_selected == null) return; using var form = new StockMovementForm(_selected, _currentUser); form.ShowDialog(); };
        pnlRight.Controls.Add(btnStockMovement);
        y += 40;

        btnNew = new Button { Text = "+ NEW", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(15, y), Size = new Size(95, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnNew.Click += btnNew_Click;

        btnEdit = new Button { Text = "\u270E EDIT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(115, y), Size = new Size(95, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(52, 152, 219), ForeColor = Color.White, Cursor = Cursors.Hand };
        btnEdit.Click += btnEdit_Click;

        btnUnits = new Button { Text = "\uD83D\uDCE6 UNITS", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(215, y), Size = new Size(100, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentPurple, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnUnits.Click += btnUnits_Click;
        y += 40;

        btnSave = new Button { Text = "\u2714 SAVE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(15, y), Size = new Size(100, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentGreen, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnSave.Click += btnSave_Click;

        btnCancel = new Button { Text = "\u2716 CANCEL", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(120, y), Size = new Size(100, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(149, 165, 166), ForeColor = Color.White, Cursor = Cursors.Hand };
        btnCancel.Click += btnCancel_Click;

        btnDelete = new Button { Text = "\u2716 DELETE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(225, y), Size = new Size(95, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentRed, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnDelete.Click += btnDelete_Click;
        y += 45;

        btnPrintChecklist = new Button { Text = "\uD83D\uDCCB CHECKLIST", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(15, y), Size = new Size(310, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentGreen, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnPrintChecklist.Click += btnPrintChecklist_Click;

        pnlRight.Controls.AddRange(new Control[] { lblFormTitle, _lblNameDisplay, _lblBarcodeDisplay, _lblPriceDisplay, _lblCostDisplay, _lblStockDisplay, btnStockMovement, btnNew, btnEdit, btnUnits, btnSave, btnCancel, btnDelete, btnPrintChecklist });

        // Product creation/editing only via cloud master catalog
        btnNew.Visible = false;
        btnEdit.Visible = false;
        btnUnits.Visible = false;
        btnSave.Visible = false;
        btnCancel.Visible = false;
        btnDelete.Visible = false;
        btnStockMovement.Text = "\uD83D\uDCC8 VIEW STOCK MOV'T";

        pnlMain.Controls.AddRange(new Control[] { pnlLeft, pnlRight });
        Controls.Clear();
        Controls.AddRange(new Control[] { pnlMain, pnlMetrics, pnlToolbar });

        Shown += (_, _) => ResizeLayout(pnlLeft, pnlRight, dgvProducts);
        Resize += (_, _) => ResizeLayout(pnlLeft, pnlRight, dgvProducts);
    }

    private Label CreateMetricLabel(int x, Color color) => new() { Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = color, Location = new Point(x, 8), Size = new Size(150, 20), TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand, AutoSize = false };

    private void AddField(string label, ref TextBox box, ref int y, Panel parent, Color inputBg, Color inputFg, Color labelColor, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var lbl = new Label { Text = label, Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), ForeColor = labelColor, Location = new Point(15, y), Size = new Size(80, 16), TextAlign = ContentAlignment.MiddleLeft };
        box = new TextBox { Location = new Point(15, y + 16), Size = new Size(310, 26), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 10F), TextAlign = align };
        parent.Controls.AddRange(new Control[] { lbl, box });
        y += 48;
    }

    private void AddComboField(string label, ref ComboBox box, ref int y, Panel parent, Color inputBg, Color inputFg, Color labelColor)
    {
        var lbl = new Label { Text = label, Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), ForeColor = labelColor, Location = new Point(15, y), Size = new Size(80, 16), TextAlign = ContentAlignment.MiddleLeft };
        box = new ComboBox { Location = new Point(15, y + 16), Size = new Size(310, 26), DropDownStyle = ComboBoxStyle.DropDown, FlatStyle = FlatStyle.Flat, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 10F) };
        parent.Controls.AddRange(new Control[] { lbl, box });
        y += 48;
    }

    private void ResizeLayout(Panel pnlLeft, Panel pnlRight, DataGridView dgv)
    {
        var margin = 10;
        var availH = ClientSize.Height - 50 - 35 - margin * 4;
        var availW = ClientSize.Width - margin * 3;
        var leftW = (int)(availW * 0.78);
        var rightW = availW - leftW;

        pnlLeft.Location = new Point(margin, margin);
        pnlLeft.Size = new Size(leftW, availH);
        pnlRight.Location = new Point(leftW + margin * 2, margin);
        pnlRight.Size = new Size(rightW, availH);

        dgv.Location = new Point(8, 32);
        dgv.Size = new Size(leftW - 16, availH - 40);
    }

    public void ApplyTheme()
    {
        var t = ThemeManager.Current;
        BackColor = t.CanvasBg;
        ForeColor = t.TextPrimary;
    }

    private void UpdateDisplayLabels()
    {
        _lblNameDisplay.Text = txtName.Text;
        _lblBarcodeDisplay.Text = txtBarcode.Text;
        _lblPriceDisplay.Text = txtPrice.Text;
        _lblCostDisplay.Text = txtCost.Text;
        _lblStockDisplay.Text = txtStock.Text;
    }

    private Label MakeDisplayLabel(TextBox source, Color bg, Color fg, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        return new Label
        {
            Location = source.Location,
            Size = source.Size,
            BackColor = bg,
            ForeColor = fg,
            Font = source.Font,
            TextAlign = align switch
            {
                HorizontalAlignment.Right => ContentAlignment.MiddleRight,
                _ => ContentAlignment.MiddleLeft
            },
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false,
            Text = source.Text
        };
    }

    private void ShowInventoryCheck(object? sender, EventArgs e)
    {
        var popup = new Form
        {
            Text = "Inventory Reconciliation Check",
            Size = new Size(820, 520),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = ThemeManager.Current.SurfaceBg
        };
        var txt = new RichTextBox { Location = new Point(12, 12), Size = new Size(780, 430), ReadOnly = true, Font = new Font("Consolas", 10F), BackColor = ThemeManager.Current.CardBg, ForeColor = ThemeManager.Current.TextPrimary, BorderStyle = BorderStyle.None, WordWrap = false };
        var btnClose = new Button { Text = "CLOSE", Location = new Point(680, 450), Size = new Size(110, 32), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = ThemeManager.Current.CardBg, ForeColor = ThemeManager.Current.TextSecondary, Cursor = Cursors.Hand };
        btnClose.Click += (_, _) => popup.Close();
        popup.Controls.AddRange(new Control[] { txt, btnClose });

        popup.Shown += (_, _) =>
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== INVENTORY RECONCILIATION CHECK ===");
                sb.AppendLine();

                // Check if DailyClose has inventory columns
                using (var chk = new SQLiteCommand("SELECT COUNT(*) FROM pragma_table_info('DailyClose') WHERE name='TotalInventoryCost'", conn))
                {
                    if (Convert.ToInt32(chk.ExecuteScalar()) == 0)
                    {
                        sb.AppendLine("DailyClose table missing inventory columns.");
                        sb.AppendLine("Update POS client to v1.0.86+.");
                        txt.Text = sb.ToString();
                        return;
                    }
                }

                // Latest end shift
                string? closeDate = null;
                decimal totalInvCost = 0, totalCOGS = 0, totalRecvCost = 0;
                using (var dcCmd = new SQLiteCommand("SELECT CloseDate, TotalInventoryCost, TotalCostSold, TotalStockReceivedCost FROM DailyClose ORDER BY Id DESC LIMIT 1", conn))
                using (var r = dcCmd.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        sb.AppendLine("No end shift found. Run End Shift first.");
                        txt.Text = sb.ToString();
                        return;
                    }
                    closeDate = r.GetString(0);
                    totalInvCost = r.GetDecimal(1);
                    totalCOGS = r.GetDecimal(2);
                    totalRecvCost = r.GetDecimal(3);
                }

                // Previous shift
                decimal prevInvCost = 0;
                using (var pCmd = new SQLiteCommand("SELECT TotalInventoryCost FROM DailyClose WHERE CloseDate < @cd ORDER BY Id DESC LIMIT 1", conn))
                {
                    pCmd.Parameters.AddWithValue("@cd", closeDate);
                    var o = pCmd.ExecuteScalar();
                    if (o != DBNull.Value && o != null) prevInvCost = Convert.ToDecimal(o);
                }

                var expected = prevInvCost + totalRecvCost - totalCOGS;
                var variance = totalInvCost - expected;

                sb.AppendLine($"Close Date:    {closeDate}");
                sb.AppendLine($"Prev. Inv:     {prevInvCost,14:N2}");
                sb.AppendLine($"+ Received:    {totalRecvCost,14:N2}");
                sb.AppendLine($"- COGS:        {totalCOGS,14:N2}");
                sb.AppendLine($"= Expected:    {expected,14:N2}");
                sb.AppendLine($"Actual Inv:    {totalInvCost,14:N2}");
                sb.AppendLine($"Variance:      {variance,14:N2} {(variance == 0 ? "[OK]" : (variance > 0 ? "[OVER]" : "[SHORT]"))}");
                sb.AppendLine();

                if (variance == 0)
                {
                    sb.AppendLine("Balanced. No issue.");
                    txt.Text = sb.ToString();
                    return;
                }

                // Cost mismatches in sales
                sb.AppendLine("=== ITEMS WITH COST MISMATCH (sale vs current) ===");
                using (var sCmd = new SQLiteCommand(@"SELECT si.ProductName, si.Quantity, si.UnitCost, COALESCE(p.Cost,0) AS ProdCost,
                    ROUND((COALESCE(p.Cost,0) - si.UnitCost/si.Quantity) * si.Quantity, 2) AS Impact
                    FROM SaleItems si JOIN Sales s ON si.SaleId=s.Id
                    LEFT JOIN Products p ON si.ProductId=p.Id
                    WHERE si.IsVoided=0 AND s.IsVoided=0 AND s.SaleDate>=@since
                    AND ABS(COALESCE(p.Cost,0) - si.UnitCost/si.Quantity) > 0.005
                    ORDER BY ABS(Impact) DESC LIMIT 20", conn))
                {
                    sCmd.Parameters.AddWithValue("@since", closeDate);
                    using var r = sCmd.ExecuteReader();
                    var found = false;
                    var saleImpact = 0m;
                    while (r.Read())
                    {
                        found = true;
                        var name = r.GetString(0);
                        if (name.Length > 30) name = name[..30];
                        var qty = r.GetInt32(1);
                        var impact = r.GetDecimal(4);
                        saleImpact += impact;
                        sb.AppendLine($"  {name,-30} qty={qty,4}  impact={impact,10:N2}");
                    }
                    if (!found) sb.AppendLine("  (none — all sale costs match)");
                    sb.AppendLine($"  Subtotal sale mismatch: {saleImpact,12:N2}");
                    sb.AppendLine();
                }

                // Receiving recompute
                sb.AppendLine("=== RECEIVING TODAY ===");
                using (var rCmd = new SQLiteCommand(@"SELECT st.ProductName, SUM(st.QuantityAdded), COALESCE(p.Cost,0)
                    FROM StockTrail st LEFT JOIN Products p ON st.ProductId=p.Id
                    WHERE st.QuantityAdded>0 AND st.CreatedAt>=@since
                    GROUP BY st.ProductId ORDER BY st.ProductName", conn))
                {
                    rCmd.Parameters.AddWithValue("@since", closeDate);
                    using var r = rCmd.ExecuteReader();
                    var calc = 0m;
                    while (r.Read())
                    {
                        var name = r.GetString(0); if (name.Length > 30) name = name[..30];
                        var qty = r.GetDecimal(1);
                        var cost = r.GetDecimal(2);
                        var ext = qty * cost;
                        calc += ext;
                        sb.AppendLine($"  {name,-30} qty={qty,5:N0}  cost={cost,8:N2}  ext={ext,10:N2}");
                    }
                    sb.AppendLine($"  Calculated: {calc,12:N2}  Stored: {totalRecvCost,12:N2}  Diff: {calc - totalRecvCost,10:N2}");
                    sb.AppendLine();
                    sb.AppendLine("  --- Per-item cost change detection ---");
                    // Try to find old cost from prior sales of received products
                    using (var ccCmd = new SQLiteCommand(@"
                        SELECT p.Name, st.qty AS RcvdQty, p.Cost AS CurrentCost,
                               (SELECT si.UnitCost/si.Quantity FROM SaleItems si JOIN Sales s ON si.SaleId=s.Id 
                                WHERE si.ProductId=p.Id AND si.IsVoided=0 AND s.IsVoided=0 AND si.Quantity>0
                                ORDER BY s.SaleDate DESC LIMIT 1) AS LastSaleUnitCost
                        FROM (SELECT ProductId, SUM(QuantityAdded) AS qty FROM StockTrail 
                              WHERE QuantityAdded>0 AND CreatedAt>=@since2 GROUP BY ProductId) st
                        JOIN Products p ON st.ProductId=p.Id
                        ORDER BY ABS(COALESCE(p.Cost,0) - (SELECT si.UnitCost/si.Quantity FROM SaleItems si JOIN Sales s ON si.SaleId=s.Id WHERE si.ProductId=p.Id AND si.IsVoided=0 AND s.IsVoided=0 AND si.Quantity>0 ORDER BY s.SaleDate DESC LIMIT 1)) DESC", conn))
                    {
                        ccCmd.Parameters.AddWithValue("@since2", closeDate);
                        using var r = ccCmd.ExecuteReader();
                        sb.AppendLine($"    {"Product",-28} {"Rcvd",5} {"NowCost",9}  {"LastSaleCost",12}  {"Delta",10}");
                        var hadChanges = false;
                        while (r.Read())
                        {
                            var name = r.GetString(0); if (name.Length > 26) name = name[..26];
                            var rcvdQty = r.GetDecimal(1);
                            var curCost = r.GetDecimal(2);
                            var lastCost = r.IsDBNull(3) ? 0m : r.GetDecimal(3);
                            var delta = lastCost > 0 ? curCost - lastCost : 0m;
                            var marker = "";
                            if (lastCost == 0)
                                marker = " [no prior sale]";
                            else if (Math.Abs(delta) > 0.01m)
                                marker = delta > 0 ? " [COST UP]" : " [COST DOWN]";
                            if (marker != "") hadChanges = true;
                            sb.AppendLine($"    {name,-28} {rcvdQty,5:N0} {curCost,9:N2}  {lastCost,12:N2}  {delta,10:N2}{marker}");
                        }
                        if (!hadChanges) sb.AppendLine("    (no cost changes detected)");
                    }
                    sb.AppendLine();
                }

                // Zero cost products
                using (var zCmd = new SQLiteCommand("SELECT Name, StockQty FROM Products WHERE IsActive=1 AND StockQty>0 AND (Cost=0 OR Cost IS NULL)", conn))
                using (var r = zCmd.ExecuteReader())
                {
                    var found = false;
                    while (r.Read())
                    {
                        if (!found) { sb.AppendLine("=== ZERO-COST PRODUCTS WITH STOCK ==="); found = true; }
                        sb.AppendLine($"  {r.GetString(0)}  (stock: {r.GetInt32(1)})");
                    }
                    if (!found) sb.AppendLine("(no zero-cost products)");
                }

                txt.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                txt.Text = "Error: " + ex.Message + "\n" + ex.StackTrace;
            }
        };

        popup.ShowDialog(this);
    }

    private TextBox txtSearch = null!;
    private ComboBox cmbFilterCategory = null!;
    private Label lblMetricTotal = null!, lblMetricLow = null!, lblMetricOut = null!, lblMetricRetail = null!, lblMetricCost = null!;
    private DataGridView dgvProducts = null!;
    private TextBox txtName = null!, txtBarcode = null!, txtPrice = null!, txtCost = null!, txtStock = null!;
    private Label _lblNameDisplay = null!, _lblBarcodeDisplay = null!, _lblPriceDisplay = null!, _lblCostDisplay = null!, _lblStockDisplay = null!;
    private ComboBox cmbCategory = null!;
    private Button btnNew = null!, btnEdit = null!, btnUnits = null!, btnStockMovement = null!, btnCancel = null!, btnSave = null!, btnDelete = null!;
    private Button btnPrintChecklist = null!;
    private Label lblFormTitle = null!;
    private PictureBox _picProduct = null!;
}
