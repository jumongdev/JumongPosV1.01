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
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", HeaderText = "ID", Width = 45, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(140, 140, 170) } });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "PRODUCT NAME", Width = 200, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(230, 230, 245) } });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Barcode", HeaderText = "BARCODE", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Category", HeaderText = "CATEGORY", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Price", HeaderText = "PRICE", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.FromArgb(0, 245, 255) } });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Cost", HeaderText = "COST", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.FromArgb(231, 76, 60) } });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StockQty", HeaderText = "STOCK", Width = 65, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(230, 230, 245), Font = new Font("Segoe UI", 9F, FontStyle.Bold) } });
        dgvProducts.DataSource = data;
        dgvProducts.RowHeadersVisible = false;
        dgvProducts.BackgroundColor = Color.FromArgb(20, 20, 40);
        dgvProducts.BorderStyle = BorderStyle.None;
        dgvProducts.GridColor = Color.FromArgb(40, 40, 70);
        dgvProducts.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 25, 50);
        dgvProducts.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(0, 245, 255);
        dgvProducts.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgvProducts.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvProducts.ColumnHeadersHeight = 30;
        dgvProducts.EnableHeadersVisualStyles = false;
        dgvProducts.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 40, 80);
        dgvProducts.DefaultCellStyle.SelectionForeColor = Color.White;
        dgvProducts.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        dgvProducts.RowTemplate.Height = 28;
        dgvProducts.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(15, 15, 32);
        dgvProducts.DefaultCellStyle.BackColor = Color.FromArgb(22, 22, 45);
        dgvProducts.DefaultCellStyle.ForeColor = Color.FromArgb(230, 230, 245);
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
                    e.CellStyle!.ForeColor = Color.FromArgb(231, 76, 60);
                    e.CellStyle.SelectionForeColor = Color.FromArgb(231, 76, 60);
                }
                else if (val.StockQty <= 10)
                {
                    e.CellStyle!.ForeColor = Color.FromArgb(243, 156, 18);
                    e.CellStyle.SelectionForeColor = Color.FromArgb(243, 156, 18);
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

        lblMetricTotal.ForeColor = _stockFilter == null ? Color.FromArgb(0, 245, 255) : Color.FromArgb(140, 140, 170);
        lblMetricLow.ForeColor = _stockFilter == "low" ? Color.FromArgb(243, 156, 18) : Color.FromArgb(140, 140, 170);
        lblMetricOut.ForeColor = _stockFilter == "out" ? Color.FromArgb(231, 76, 60) : Color.FromArgb(140, 140, 170);
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

    private void btnCheckCost_Click(object? sender, EventArgs e)
    {
        var form = new Form
        {
            Text = "Cost Check",
            Size = new Size(1000, 600),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(10, 10, 26)
        };

        var canvasBg = Color.FromArgb(10, 10, 26);
        var panelBg = Color.FromArgb(20, 20, 40);
        var neonTitle = Color.FromArgb(0, 245, 255);
        var accentGreen = Color.FromArgb(46, 204, 113);
        var accentRed = Color.FromArgb(231, 76, 60);
        var accentOrange = Color.FromArgb(243, 156, 18);
        var borderColor = Color.FromArgb(40, 40, 70);

        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
        var lblTitle = new Label { Text = "COST ISSUES", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(15, 14), Size = new Size(150, 25) };

        var btnAll = new Button { Text = "ALL", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(180, 12), Size = new Size(80, 28), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(72, 126, 176), ForeColor = Color.White, Cursor = Cursors.Hand, Tag = "all" };
        var btnNoCost = new Button { Text = "COST = 0", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(270, 12), Size = new Size(90, 28), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentRed, ForeColor = Color.White, Cursor = Cursors.Hand, Tag = "zero" };
        var btnEqual = new Button { Text = "COST = PRICE", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(370, 12), Size = new Size(100, 28), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentOrange, ForeColor = Color.White, Cursor = Cursors.Hand, Tag = "equal" };
        var btnOver = new Button { Text = "COST > PRICE", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(480, 12), Size = new Size(100, 28), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(155, 89, 182), ForeColor = Color.White, Cursor = Cursors.Hand, Tag = "over" };

        var lblCount = new Label { Text = "", Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(140, 140, 170), Location = new Point(600, 16), Size = new Size(200, 20), TextAlign = ContentAlignment.MiddleLeft };

        pnlToolbar.Controls.AddRange(new Control[] { lblTitle, btnAll, btnNoCost, btnEqual, btnOver, lblCount });

        var dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Font = new Font("Segoe UI", 9F),
            BackgroundColor = canvasBg,
            BorderStyle = BorderStyle.None,
            GridColor = borderColor,
            RowHeadersVisible = false,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersDefaultCellStyle = { BackColor = Color.FromArgb(25, 25, 50), ForeColor = neonTitle, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleCenter },
            ColumnHeadersHeight = 32,
            EnableHeadersVisualStyles = false,
            DefaultCellStyle = { BackColor = Color.FromArgb(22, 22, 45), ForeColor = Color.FromArgb(230, 230, 245), SelectionBackColor = Color.FromArgb(40, 40, 80), SelectionForeColor = Color.White, Padding = new Padding(4, 2, 4, 2) },
            AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(15, 15, 32) },
            RowTemplate = { Height = 28 }
        };

        void LoadData(string filter)
        {
            using var conn = Data.DatabaseHelper.GetConnection();
            conn.Open();
            var where = filter switch
            {
                "zero" => "AND Cost = 0",
                "equal" => "AND Cost = Price",
                "over" => "AND Cost > Price",
                _ => "AND (Cost = 0 OR Cost = Price OR Cost > Price)"
            };
            using var cmd = new System.Data.SQLite.SQLiteCommand($@"
                SELECT Id, Name, Barcode, Category, Price, Cost,
                       CASE 
                           WHEN Cost = 0 THEN 'NO COST'
                           WHEN Cost > Price THEN 'LOSS'
                           WHEN Cost = Price THEN 'NO PROFIT'
                           ELSE 'OK'
                       END as Issue
                FROM Products 
                WHERE IsActive = 1 {where}
                ORDER BY 
                    CASE WHEN Cost = 0 THEN 1 WHEN Cost > Price THEN 2 ELSE 3 END,
                    Name
            ", conn);
            using var reader = cmd.ExecuteReader();
            var dt = new System.Data.DataTable();
            dt.Load(reader);
            dgv.DataSource = dt;
            lblCount.Text = $"{dt.Rows.Count} products";

            dgv.Columns.Clear();
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", HeaderText = "ID", Width = 50 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "PRODUCT NAME", Width = 250 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Barcode", HeaderText = "BARCODE", Width = 130 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Category", HeaderText = "CATEGORY", Width = 100 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Price", HeaderText = "PRICE", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = neonTitle } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Cost", HeaderText = "COST", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = accentRed } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Issue", HeaderText = "ISSUE", Width = 100 });
        }

        dgv.CellFormatting += (s, ev) =>
        {
            if (ev.RowIndex < 0 || ev.ColumnIndex < 0) return;
            var issueCol = dgv.Columns["Issue"]?.Index ?? -1;
            if (ev.ColumnIndex == issueCol && ev.Value != null)
            {
                var issue = ev.Value.ToString();
                ev.CellStyle!.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                if (issue == "NO COST") { ev.CellStyle.ForeColor = accentRed; ev.CellStyle.SelectionForeColor = accentRed; }
                else if (issue == "LOSS") { ev.CellStyle.ForeColor = Color.FromArgb(155, 89, 182); ev.CellStyle.SelectionForeColor = Color.FromArgb(155, 89, 182); }
                else if (issue == "NO PROFIT") { ev.CellStyle.ForeColor = accentOrange; ev.CellStyle.SelectionForeColor = accentOrange; }
            }
        };

        void SetActive(Button active)
        {
            foreach (var btn in new[] { btnAll, btnNoCost, btnEqual, btnOver })
                btn.FlatAppearance.BorderSize = btn == active ? 2 : 0;
        }

        btnAll.Click += (s, ev) => { SetActive(btnAll); LoadData("all"); };
        btnNoCost.Click += (s, ev) => { SetActive(btnNoCost); LoadData("zero"); };
        btnEqual.Click += (s, ev) => { SetActive(btnEqual); LoadData("equal"); };
        btnOver.Click += (s, ev) => { SetActive(btnOver); LoadData("over"); };

        form.Controls.Add(dgv);
        form.Controls.Add(pnlToolbar);
        form.Shown += (s, ev) => { SetActive(btnAll); LoadData("all"); };
        form.ShowDialog();
    }

    private void SetReadOnly(bool readOnly)
    {
        _isEditing = !readOnly;
        txtName.ReadOnly = readOnly;
        txtBarcode.ReadOnly = readOnly;
        cmbCategory.Enabled = !readOnly;
        txtPrice.ReadOnly = readOnly;
        txtCost.ReadOnly = readOnly;
        txtStock.ReadOnly = true;

        var inputBg = readOnly ? Color.FromArgb(20, 20, 35) : Color.FromArgb(30, 30, 55);
        txtName.BackColor = inputBg;
        txtBarcode.BackColor = inputBg;
        cmbCategory.BackColor = inputBg;
        txtPrice.BackColor = inputBg;
        txtCost.BackColor = inputBg;
        txtStock.BackColor = Color.FromArgb(20, 20, 35);

        btnEdit.Visible = readOnly && _selected != null;
        btnUnits.Visible = readOnly && _selected != null;
        btnStockMovement.Visible = readOnly && _selected != null;
        btnCancel.Visible = !readOnly;
        btnSave.Visible = !readOnly;
        btnDelete.Visible = !readOnly && _selected != null;
        btnNew.Visible = readOnly;
        btnPrintChecklist.Visible = readOnly;
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
            SetReadOnly(true);
        }
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
        sb.AppendLine($"Printed: {DateTime.Now:yyyy-MM-dd hh:mm tt}".PadLeft((lineChars + 37) / 2).PadRight(lineChars));
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
        var canvasBg = Color.FromArgb(10, 10, 26);
        var panelBg = Color.FromArgb(20, 20, 40);
        var inputBg = Color.FromArgb(30, 30, 55);
        var inputFg = Color.FromArgb(230, 230, 245);
        var neonTitle = Color.FromArgb(0, 245, 255);
        var metaText = Color.FromArgb(230, 230, 245);
        var dimText = Color.FromArgb(140, 140, 170);
        var borderColor = Color.FromArgb(40, 40, 70);
        var accentBlue = Color.FromArgb(72, 126, 176);
        var accentGreen = Color.FromArgb(46, 204, 113);
        var accentRed = Color.FromArgb(231, 76, 60);
        var accentOrange = Color.FromArgb(243, 156, 18);
        var accentPurple = Color.FromArgb(155, 89, 182);

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

        txtSearch = new TextBox { Font = new Font("Segoe UI", 10F), Location = new Point(340, 12), Size = new Size(220, 28), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg };
        txtSearch.TextChanged += TriggerSearch;

        var lblSearchIcon = new Label { Text = "\uD83D\uDD0D", Font = new Font("Segoe UI", 10F), Location = new Point(315, 14), Size = new Size(25, 22), TextAlign = ContentAlignment.MiddleRight };

        var lblCatFilter = new Label { Text = "Category:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(580, 16), Size = new Size(65, 20), TextAlign = ContentAlignment.MiddleRight };
        cmbFilterCategory = new ComboBox { Location = new Point(650, 12), Size = new Size(150, 28), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = inputBg, ForeColor = inputFg };
        cmbFilterCategory.SelectedIndexChanged += TriggerSearch;

        var btnCheckCost = new Button { Text = "CHECK COST", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(820, 12), Size = new Size(110, 28), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentOrange, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnCheckCost.Click += btnCheckCost_Click;

        var btnDownload = new Button { Text = "DOWNLOAD MASTER", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(940, 12), Size = new Size(150, 28), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(72, 126, 176), ForeColor = Color.White, Cursor = Cursors.Hand };
        btnDownload.Click += async (_, _) =>
        {
            btnDownload.Enabled = false;
            btnDownload.Text = "DOWNLOADING...";
            var progress = new Progress<string>(msg => btnDownload.Text = msg.Length < 25 ? msg : "DOWNLOADING...");
            var count = await SyncService.DownloadMasterCatalog(progress);
            MessageBox.Show($"Downloaded {count} products from master catalog.", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            btnDownload.Enabled = true;
            btnDownload.Text = "DOWNLOAD MASTER";
            LoadProducts();
            UpdateStats();
        };

        pnlToolbar.Controls.AddRange(new Control[] { lblPageTitle, lblSearchIcon, txtSearch, lblCatFilter, cmbFilterCategory, btnCheckCost, btnDownload });

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
        AddField("STOCK", ref txtStock, ref y, pnlRight, Color.FromArgb(20, 20, 35), Color.FromArgb(140, 140, 170), dimText, HorizontalAlignment.Right);

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

        pnlRight.Controls.AddRange(new Control[] { lblFormTitle, btnStockMovement, btnNew, btnEdit, btnUnits, btnSave, btnCancel, btnDelete, btnPrintChecklist });

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
        var leftW = (int)(availW * 0.64);
        var rightW = availW - leftW;

        pnlLeft.Location = new Point(margin, margin);
        pnlLeft.Size = new Size(leftW, availH);
        pnlRight.Location = new Point(leftW + margin * 2, margin);
        pnlRight.Size = new Size(rightW, availH);

        dgv.Location = new Point(8, 32);
        dgv.Size = new Size(leftW - 16, availH - 40);
    }

    private TextBox txtSearch = null!;
    private ComboBox cmbFilterCategory = null!;
    private Label lblMetricTotal = null!, lblMetricLow = null!, lblMetricOut = null!, lblMetricRetail = null!, lblMetricCost = null!;
    private DataGridView dgvProducts = null!;
    private TextBox txtName = null!, txtBarcode = null!, txtPrice = null!, txtCost = null!, txtStock = null!;
    private ComboBox cmbCategory = null!;
    private Button btnNew = null!, btnEdit = null!, btnUnits = null!, btnStockMovement = null!, btnCancel = null!, btnSave = null!, btnDelete = null!;
    private Button btnPrintChecklist = null!;
    private Label lblFormTitle = null!;
}
