using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class StockReceivingForm : Form
{
    private readonly User _currentUser;
    private readonly List<(int ProductId, string ProductName, string Barcode, int StockBefore, int Qty)> _pending = new();
    private Product? _currentProduct;

    public StockReceivingForm(User user)
    {
        _currentUser = user;
        InitializeComponent();
        DebugHelper.AddFormLabel(this);
    }

    private void SearchProduct(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) { ClearCurrent(); return; }

        var results = StockService.Search(keyword);
        if (results.Count == 1) _currentProduct = results[0];
        else if (results.Count > 1)
        {
            using var picker = new Form { Text = "Select Product", Size = new Size(800, 500), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.Sizable, BackColor = Color.FromArgb(10, 10, 26) };
            var pnlPicker = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 10, 26) };
            var dgv = new DataGridView { Location = new Point(10, 10), Size = new Size(760, 400), ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = Color.FromArgb(20, 20, 40), BorderStyle = BorderStyle.None, GridColor = Color.FromArgb(40, 40, 70), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, Font = new Font("Segoe UI", 9F), ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(25, 25, 50), ForeColor = Color.FromArgb(0, 245, 255), Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, ColumnHeadersHeight = 30, EnableHeadersVisualStyles = false, DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(22, 22, 45), ForeColor = Color.FromArgb(230, 230, 245), SelectionBackColor = Color.FromArgb(40, 40, 80), SelectionForeColor = Color.White }, RowTemplate = { Height = 28 }, AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(15, 15, 32) } };
            dgv.DataSource = results.Select(p => new { p.Id, p.Name, p.Barcode, p.Category, Price = p.Price.ToString("N2"), p.StockQty }).ToList();
            if (dgv.Columns["Name"] != null) { dgv.Columns["Name"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; }
            var btnOk = new Button { Text = "SELECT", Font = new Font("Segoe UI", 10F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(72, 126, 176), ForeColor = Color.White, Location = new Point(350, 420), Size = new Size(100, 30), Cursor = Cursors.Hand };
            btnOk.Click += (_, _) => { if (dgv.SelectedRows.Count > 0) { _currentProduct = results[dgv.SelectedRows[0].Index]; picker.Close(); } };
            dgv.CellDoubleClick += (_, _) => btnOk.PerformClick();
            dgv.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; btnOk.PerformClick(); } };
            pnlPicker.Controls.AddRange(new Control[] { dgv, btnOk });
            picker.Controls.Add(pnlPicker);
            picker.Controls.SetChildIndex(pnlPicker, 0);
            picker.ShowDialog();
        }
        else _currentProduct = null;

        if (_currentProduct != null) ShowCurrentProduct(); else ClearCurrent();
    }

    private void ShowCurrentProduct()
    {
        if (_currentProduct == null) return;
        lblProductName.Text = _currentProduct.Name;
        lblCurrentStock.Text = _currentProduct.StockQty.ToString();
        numQty.Value = 1;
        numQty.Enabled = true;
        btnAddToList.Enabled = true;
        UpdatePreview();
    }

    private void ClearCurrent()
    {
        _currentProduct = null;
        lblProductName.Text = "---";
        lblCurrentStock.Text = "0";
        numQty.Value = 1;
        numQty.Enabled = false;
        btnAddToList.Enabled = false;
        lblNewStock.Text = "0";
    }

    private void UpdatePreview()
    {
        if (_currentProduct == null) return;
        lblNewStock.Text = (_currentProduct.StockQty + (int)numQty.Value).ToString();
    }

    private async Task CheckPendingTransfers()
    {
        btnCheckTransfers.Enabled = false;
        btnCheckTransfers.Text = "Loading...";
        try
        {
            var transfers = await SyncService.GetPendingTransfersAsync();
            btnCheckTransfers.Text = "\uD83D\uDCE5 CHECK PENDING TRANSFERS";
            btnCheckTransfers.Enabled = true;

            if (transfers == null || transfers.Count == 0)
            {
                MessageBox.Show("No pending transfers from warehouse.", "Pending Transfers", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var picker = new Form { Text = "Pending Warehouse Transfers", Size = new Size(750, 500), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.Sizable, BackColor = Color.FromArgb(10, 10, 26) };
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 10, 26) };
            var lbl = new Label { Text = "Select a transfer to receive:", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(0, 245, 255), Location = new Point(12, 10), Size = new Size(700, 25) };

            var dgv = new DataGridView { Location = new Point(12, 42), Size = new Size(710, 370), ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = Color.FromArgb(20, 20, 40), BorderStyle = BorderStyle.None, GridColor = Color.FromArgb(40, 40, 70), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, Font = new Font("Segoe UI", 9F), ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(25, 25, 50), ForeColor = Color.FromArgb(0, 245, 255), Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, ColumnHeadersHeight = 30, EnableHeadersVisualStyles = false, DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(22, 22, 45), ForeColor = Color.FromArgb(230, 230, 245), SelectionBackColor = Color.FromArgb(40, 40, 80), SelectionForeColor = Color.White }, RowTemplate = { Height = 28 }, AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(15, 15, 32) } };
            dgv.DataSource = transfers.Select(t => new { t.OrderId, t.ClientName, Total = t.TotalAmount.ToString("N2"), Notes = t.Notes ?? "", Date = t.CreatedAt.ToString("yyyy-MM-dd HH:mm") }).ToList();
            if (dgv.Columns["OrderId"] != null) { dgv.Columns["OrderId"].Width = 60; }
            if (dgv.Columns["ClientName"] != null) { dgv.Columns["ClientName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; }

            var btnReceive = new Button { Text = "RECEIVE SELECTED", Font = new Font("Segoe UI", 10F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(46, 204, 113), ForeColor = Color.White, Location = new Point(280, 422), Size = new Size(180, 35), Cursor = Cursors.Hand };
            btnReceive.Click += async (s, ev) =>
            {
                if (dgv.SelectedRows.Count == 0) return;
                var orderId = (int)dgv.SelectedRows[0].Cells[0].Value;
                btnReceive.Enabled = false;
                btnReceive.Text = "Processing...";

                var items = await SyncService.MarkTransferReceivedAsync(orderId);
                if (items == null || items.Count == 0)
                {
                    MessageBox.Show("Failed to receive transfer or no items found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnReceive.Enabled = true;
                    btnReceive.Text = "RECEIVE SELECTED";
                    return;
                }

                var matched = 0;
                foreach (var item in items)
                {
                    var product = StockService.Search(item.ProductName).FirstOrDefault() ??
                                  (!string.IsNullOrEmpty(item.Barcode) ? StockService.GetByBarcode(item.Barcode) : null);
                    if (product == null) continue;

                    var existingIdx = _pending.FindIndex(p => p.ProductId == product.Id);
                    if (existingIdx >= 0) { var old = _pending[existingIdx]; _pending[existingIdx] = (old.ProductId, old.ProductName, old.Barcode, old.StockBefore, old.Qty + item.BaseQty); }
                    else _pending.Add((product.Id, product.Name, product.Barcode, product.StockQty, item.BaseQty));
                    matched++;
                }

                picker.Close();
                RefreshPendingGrid();
                txtReference.Text = "WH-Transfer #" + orderId;
                MessageBox.Show($"{matched} of {items.Count} item(s) received from transfer #{orderId}.", "Transfer Received", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            pnl.Controls.AddRange(new Control[] { lbl, dgv, btnReceive });
            picker.Controls.Add(pnl);
            picker.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error checking transfers: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnCheckTransfers.Text = "\uD83D\uDCE5 CHECK PENDING TRANSFERS";
            btnCheckTransfers.Enabled = true;
        }
    }

    private void AddCurrentToList()
    {
        if (_currentProduct == null || (int)numQty.Value <= 0) return;
        var qty = (int)numQty.Value;
        var existing = _pending.FindIndex(p => p.ProductId == _currentProduct.Id);
        if (existing >= 0) { var old = _pending[existing]; _pending[existing] = (old.ProductId, old.ProductName, old.Barcode, old.StockBefore, old.Qty + qty); }
        else _pending.Add((_currentProduct.Id, _currentProduct.Name, _currentProduct.Barcode, _currentProduct.StockQty, qty));
        RefreshPendingGrid();
        txtBarcode.Clear();
        txtBarcode.Focus();
        ClearCurrent();
    }

    private void RemoveFromList(int index) { if (index >= 0 && index < _pending.Count) _pending.RemoveAt(index); RefreshPendingGrid(); }

    private void RefreshPendingGrid()
    {
        dgvPending.Rows.Clear();
        foreach (var (productId, productName, barcode, stockBefore, qty) in _pending)
        {
            var idx = dgvPending.Rows.Add();
            dgvPending.Rows[idx].Cells[0].Value = productName;
            dgvPending.Rows[idx].Cells[1].Value = barcode;
            dgvPending.Rows[idx].Cells[2].Value = stockBefore;
            dgvPending.Rows[idx].Cells[3].Value = qty;
            dgvPending.Rows[idx].Cells[4].Value = stockBefore + qty;
            dgvPending.Rows[idx].Cells[5].Value = "✕";
            dgvPending.Rows[idx].Tag = productId;
        }
        lblCount.Text = $"Items: {_pending.Count}";
    }

    private void ConfirmReceiving()
    {
        if (_pending.Count == 0) { MessageBox.Show("No items to confirm.", "Receiving", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var reference = txtReference.Text.Trim();
        var userName = string.IsNullOrEmpty(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName;
        var result = StockService.ConfirmReceiving(_pending, _currentUser.Id, userName, reference);
        if (result != null) { MessageBox.Show($"Error: {result}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

        var itemsCopy = new List<(int, string, string, int, int)>(_pending);
        _pending.Clear();
        RefreshPendingGrid();
        txtReference.Clear();
        txtBarcode.Focus();

        MessageBox.Show($"{itemsCopy.Count} item(s) received successfully.", "Stock Receiving", MessageBoxButtons.OK, MessageBoxIcon.Information);

        var print = MessageBox.Show("Print receiving receipt?", "Print", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (print == DialogResult.Yes)
        {
            PrinterService.PrintStockReceiving(itemsCopy, userName, reference);
        }
    }

    private void ShowTrail()
    {
        using var form = new Form { Text = "Stock Receiving History", WindowState = FormWindowState.Maximized, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.Sizable, BackColor = Color.FromArgb(10, 10, 26) };
        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(20, 20, 40) };
        var searchBox = new TextBox { Location = new Point(15, 18), Size = new Size(150, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(30, 30, 55), ForeColor = Color.FromArgb(230, 230, 245) };
        var dtpDate = new DateTimePicker { Location = new Point(175, 18), Size = new Size(130, 25), Format = DateTimePickerFormat.Short, Value = TimeHelper.Today };
        var btnFilter = new Button { Text = "FILTER", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(72, 126, 176), ForeColor = Color.White, Location = new Point(315, 16), Size = new Size(70, 28), Cursor = Cursors.Hand };
        var btnPrint = new Button { Text = "\uD83D\uDDAB PRINT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(46, 204, 113), ForeColor = Color.White, Location = new Point(393, 16), Size = new Size(80, 28), Cursor = Cursors.Hand };
        var lblTitle = new Label { Text = "\uD83D\uDCE6 RECEIVING HISTORY", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = Color.FromArgb(0, 245, 255), Location = new Point(490, 15), Size = new Size(350, 30), AutoSize = false };
        pnlToolbar.Controls.AddRange(new Control[] { searchBox, dtpDate, btnFilter, btnPrint, lblTitle });

        var dgv = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = Color.FromArgb(20, 20, 40), BorderStyle = BorderStyle.None, GridColor = Color.FromArgb(40, 40, 70), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Font = new Font("Segoe UI", 9F), ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(25, 25, 50), ForeColor = Color.FromArgb(0, 245, 255), Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, ColumnHeadersHeight = 35, EnableHeadersVisualStyles = false, DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(22, 22, 45), ForeColor = Color.FromArgb(230, 230, 245), SelectionBackColor = Color.FromArgb(40, 40, 80), SelectionForeColor = Color.White }, RowTemplate = { Height = 30 }, AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(15, 15, 32) } };

        dgv.AutoGenerateColumns = false;
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "DATE/TIME", DataPropertyName = "CreatedAt", Width = 150 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PRODUCT NAME", DataPropertyName = "ProductName", Width = 180, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "BARCODE", DataPropertyName = "Barcode", Width = 110 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "BEFORE", DataPropertyName = "StockBefore", Width = 70, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "QTY", DataPropertyName = "QuantityAdded", Width = 70, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9F, FontStyle.Bold) } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "AFTER", DataPropertyName = "StockAfter", Width = 70, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "REFERENCE", DataPropertyName = "Reference", Width = 150 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "CASHIER", DataPropertyName = "UserName", Width = 100 });

        List<StockTrail>? _trailData = null;

        void LoadTrail()
        {
            var from = dtpDate.Value.Date;
            var to = from.AddDays(1);
            var keyword = searchBox.Text.Trim();
            List<StockTrail> data;
            if (!string.IsNullOrEmpty(keyword))
                data = StockService.Search(keyword).SelectMany(p => StockService.GetTrail(p.Id, 500)).Where(t => t.QuantityAdded > 0 && DateTime.TryParse(t.CreatedAt, out var d) && d >= from && d < to).ToList();
            else
                data = StockService.GetTrailByDateRange(from, to);
            _trailData = data;
            dgv.DataSource = data;
        }

        btnFilter.Click += (_, _) => LoadTrail();
        btnPrint.Click += (_, _) =>
        {
            if (_trailData != null && _trailData.Count > 0)
            {
                var dateLabel = dtpDate.Value.ToString("yyyy-MM-dd dddd");
                PrinterService.PrintStockReceivingHistory(_trailData, null, dateLabel);
            }
            else
            {
                MessageBox.Show("No data to print.", "Print", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        form.Controls.Add(dgv);
        form.Controls.Add(pnlToolbar);
        LoadTrail();
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
        var accentGreen = Color.FromArgb(46, 204, 113);
        var accentBlue = Color.FromArgb(72, 126, 176);

        BackColor = canvasBg;
        Text = "Stock Receiving";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };
        var lblPageTitle = new Label { Text = "\uD83D\uDCE6 STOCK RECEIVING", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(250, 28) };
        btnCheckTransfers = new Button { Text = "\uD83D\uDCE5 CHECK PENDING TRANSFERS", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentGreen, ForeColor = Color.White, Location = new Point(280, 10), Size = new Size(200, 28), Cursor = Cursors.Hand };
        btnCheckTransfers.Click += async (_, _) => await CheckPendingTransfers();
        pnlToolbar.Controls.Add(lblPageTitle);
        pnlToolbar.Controls.Add(btnCheckTransfers);

        var pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = canvasBg };
        var margin = 10;

        // ── SCAN / SEARCH ──
        var pnlScan = new Panel { Location = new Point(margin, margin), Size = new Size(100, 50), BackColor = panelBg };
        pnlScan.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlScan.Width - 1, pnlScan.Height - 1); };
        var lblScan = new Label { Text = "Scan Barcode / Search:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(12, 14), Size = new Size(150, 20) };
        txtBarcode = new TextBox { Location = new Point(170, 10), Size = new Size(250, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 9F) };
        txtBarcode.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { SearchProduct(txtBarcode.Text.Trim()); e.SuppressKeyPress = true; } };
        var btnSearch = new Button { Text = "SEARCH", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentBlue, ForeColor = Color.White, Location = new Point(430, 8), Size = new Size(80, 28), Cursor = Cursors.Hand };
        btnSearch.Click += (_, _) => SearchProduct(txtBarcode.Text.Trim());
        pnlScan.Controls.AddRange(new Control[] { lblScan, txtBarcode, btnSearch });

        // ── PRODUCT INFO CARD ──
        var pnlProduct = new Panel { Location = new Point(margin, 70), Size = new Size(100, 100), BackColor = panelBg };
        pnlProduct.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlProduct.Width - 1, pnlProduct.Height - 1); };
        var lblProdLabel = new Label { Text = "Product:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, 10), Size = new Size(60, 18) };
        lblProductName = new Label { Text = "---", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(80, 8), Size = new Size(400, 25) };
        var lblCurLabel = new Label { Text = "Current Stock:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, 45), Size = new Size(100, 20) };
        lblCurrentStock = new Label { Text = "0", Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = inputFg, Location = new Point(120, 42), Size = new Size(70, 25), TextAlign = ContentAlignment.MiddleLeft };
        var lblQtyLabel = new Label { Text = "Qty to Add:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(200, 45), Size = new Size(80, 20) };
        numQty = new NumericUpDown { Minimum = 1, Maximum = 99999, Value = 1, Enabled = false, Location = new Point(280, 43), Size = new Size(60, 25), BackColor = inputBg, ForeColor = inputFg };
        numQty.ValueChanged += (_, _) => UpdatePreview();
        var lblNewLabel = new Label { Text = "New Stock:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(360, 45), Size = new Size(80, 20) };
        lblNewStock = new Label { Text = "0", Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = accentGreen, Location = new Point(440, 42), Size = new Size(70, 25), TextAlign = ContentAlignment.MiddleLeft };
        btnAddToList = new Button { Text = "ADD TO LIST", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentGreen, ForeColor = Color.White, Enabled = false, Location = new Point(530, 40), Size = new Size(120, 30), Cursor = Cursors.Hand };
        btnAddToList.Click += (_, _) => AddCurrentToList();
        pnlProduct.Controls.AddRange(new Control[] { lblProdLabel, lblProductName, lblCurLabel, lblCurrentStock, lblQtyLabel, numQty, lblNewLabel, lblNewStock, btnAddToList });

        // ── PENDING LIST ──
        var pnlPending = new Panel { Location = new Point(margin, 180), Size = new Size(100, 300), BackColor = panelBg };
        pnlPending.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlPending.Width - 1, pnlPending.Height - 1); };
        var lblPendingTitle = new Label { Text = "PENDING ITEMS", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(12, 8), Size = new Size(150, 20) };
        dgvPending = new DataGridView { Location = new Point(8, 40), Size = new Size(100, 200), ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = panelBg, BorderStyle = BorderStyle.None, GridColor = Color.FromArgb(40, 40, 70), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, Font = new Font("Segoe UI", 9F), CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(25, 25, 50), ForeColor = neonTitle, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, ColumnHeadersHeight = 30, EnableHeadersVisualStyles = false, DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(22, 22, 45), ForeColor = inputFg, SelectionBackColor = Color.FromArgb(40, 40, 80), SelectionForeColor = Color.White }, RowTemplate = { Height = 28 }, AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(15, 15, 32) } };
        dgvPending.Columns.Add("Product", "PRODUCT");
        dgvPending.Columns.Add("Barcode", "BARCODE");
        dgvPending.Columns.Add("Current", "CURRENT");
        dgvPending.Columns.Add("Qty", "QTY ADDED");
        dgvPending.Columns.Add("NewStock", "NEW STOCK");
        dgvPending.Columns.Add("Remove", "✕");
        dgvPending.Columns[0].MinimumWidth = 200;
        dgvPending.Columns[5].Width = 35;
        dgvPending.Columns[5].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        dgvPending.CellClick += (_, e) => { if (e.ColumnIndex == 5 && e.RowIndex >= 0) RemoveFromList(e.RowIndex); };
        pnlPending.Controls.AddRange(new Control[] { lblPendingTitle, dgvPending });

        // ── BOTTOM ACTIONS ──
        var pnlBottom = new Panel { Location = new Point(margin, 490), Size = new Size(100, 50), BackColor = canvasBg };
        lblCount = new Label { Text = "Items: 0", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(5, 15), Size = new Size(150, 25) };
        var lblRefLabel = new Label { Text = "Ref#:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(160, 15), Size = new Size(40, 25) };
        txtReference = new TextBox { Location = new Point(200, 12), Size = new Size(180, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 9F) };
        btnConfirm = new Button { Text = "\u2705 CONFIRM RECEIVING", Font = new Font("Segoe UI", 10F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentBlue, ForeColor = Color.White, Location = new Point(400, 5), Size = new Size(180, 35), Cursor = Cursors.Hand };
        btnConfirm.Click += (_, _) => ConfirmReceiving();
        btnTrail = new Button { Text = "\uD83D\uDCCB HISTORY", Font = new Font("Segoe UI", 10F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(149, 165, 166), ForeColor = Color.White, Location = new Point(590, 5), Size = new Size(120, 35), Cursor = Cursors.Hand };
        btnTrail.Click += (_, _) => ShowTrail();
        pnlBottom.Controls.AddRange(new Control[] { lblCount, lblRefLabel, txtReference, btnConfirm, btnTrail });

        pnlMain.Controls.AddRange(new Control[] { pnlScan, pnlProduct, pnlPending, pnlBottom });
        Controls.Clear();
        Controls.AddRange(new Control[] { pnlMain, pnlToolbar });

        Shown += (_, _) => ResizeLayout(pnlScan, pnlProduct, pnlPending, pnlBottom);
        Resize += (_, _) => ResizeLayout(pnlScan, pnlProduct, pnlPending, pnlBottom);
    }

    private void ResizeLayout(Panel pnlScan, Panel pnlProduct, Panel pnlPending, Panel pnlBottom)
    {
        var margin = 10;
        var w = ClientSize.Width - margin * 2;
        
        var scanH = 60;
        var prodH = 110;
        var bottomH = 55;
        
        var mainAvailHeight = ClientSize.Height - 50;
        var availH = mainAvailHeight - scanH - prodH - bottomH - (margin * 5);

        if (availH < 100) availH = 100;

        pnlScan.Location = new Point(margin, margin);
        pnlScan.Size = new Size(w, scanH);

        pnlProduct.Location = new Point(margin, pnlScan.Bottom + margin);
        pnlProduct.Size = new Size(w, prodH);

        pnlPending.Location = new Point(margin, pnlProduct.Bottom + margin);
        pnlPending.Size = new Size(w, availH);

        pnlBottom.Location = new Point(margin, pnlPending.Bottom + margin);
        pnlBottom.Size = new Size(w, bottomH);

        dgvPending.Location = new Point(8, 40);
        dgvPending.Size = new Size(w - 16, availH - 48);
    }

    private TextBox txtBarcode = null!;
    private Label lblProductName = null!, lblCurrentStock = null!, lblNewStock = null!;
    private NumericUpDown numQty = null!;
    private Button btnAddToList = null!;
    private DataGridView dgvPending = null!;
    private Label lblCount = null!;
    private TextBox txtReference = null!;
    private Button btnConfirm = null!, btnTrail = null!, btnCheckTransfers = null!;
}
