using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class StockMovementForm : Form
{
    private readonly Product _product;
    private readonly User? _currentUser;
    private readonly bool _isAdmin;

    public StockMovementForm(Product product, User? currentUser = null)
    {
        _product = product;
        _currentUser = currentUser;
        _isAdmin = currentUser?.Role == "Admin";
        InitializeComponent();
        DebugHelper.AddFormLabel(this);
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
        var accentRed = Color.FromArgb(231, 76, 60);

        BackColor = canvasBg;
        Text = $"Stock Movement - {_product.Name} ({_product.Barcode})";
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };

        var stockColor = _product.StockQty == 0 ? accentRed : accentGreen;
        var lblPageTitle = new Label { Text = $"\uD83D\uDCE6 STOCK MOVEMENT: {_product.Name.ToUpper()}", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(500, 28) };
        lblCurrentStock = new Label { Text = $"CURRENT STOCK: {_product.StockQty}", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = stockColor, Location = new Point(540, 12), Size = new Size(200, 28) };

        var btnClose = new Button { Text = "\u2716 CLOSE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(0, 8), Size = new Size(110, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(149, 165, 166), ForeColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnClose.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        pnlToolbar.Controls.AddRange(new Control[] { lblPageTitle, lblCurrentStock, btnClose });

        pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = canvasBg };
        var margin = 10;

        // ── ADJUSTMENT PANEL ──
        pnlAdjust = new Panel { Location = new Point(margin, margin), Size = new Size(100, 65), BackColor = panelBg };
        pnlAdjust.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlAdjust.Width - 1, pnlAdjust.Height - 1); };
        var lblAdjTitle = new Label { Text = "STOCK ADJUSTMENT (Admin)", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(12, 8), Size = new Size(200, 20) };
        var lblQty = new Label { Text = "Qty (+/-):", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(12, 33), Size = new Size(70, 22) };
        numAdjustQty = new NumericUpDown { Location = new Point(82, 32), Size = new Size(70, 25), Minimum = -99999, Maximum = 99999, Value = 0, BackColor = inputBg, ForeColor = inputFg };
        var lblReason = new Label { Text = "Reason:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(160, 33), Size = new Size(55, 22) };
        txtAdjustReason = new TextBox { Location = new Point(215, 32), Size = new Size(250, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 9F), Text = "Manual adjustment" };
        var btnAdjust = new Button { Text = "APPLY ADJUSTMENT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentGreen, ForeColor = Color.White, Location = new Point(475, 30), Size = new Size(150, 28), Cursor = Cursors.Hand };
        btnAdjust.Click += btnAdjust_Click;
        pnlAdjust.Controls.AddRange(new Control[] { lblAdjTitle, lblQty, numAdjustQty, lblReason, txtAdjustReason, btnAdjust });
        pnlAdjust.Visible = _isAdmin;

        var trailTop = _isAdmin ? margin + 75 : margin;
        var trailSizeH = _isAdmin ? 100 : 100;
        pnlGrid = new Panel { Location = new Point(margin, trailTop), Size = new Size(100, trailSizeH), BackColor = panelBg };
        pnlGrid.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlGrid.Width - 1, pnlGrid.Height - 1); };

        var lblGridTitle = new Label { Text = "STOCK TRAIL HISTORY", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(12, 8), Size = new Size(200, 20) };
        _dgv = new DataGridView
        {
            Location = new Point(8, 32),
            Size = new Size(100, 100),
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            BackgroundColor = panelBg,
            BorderStyle = BorderStyle.None,
            GridColor = Color.FromArgb(40, 40, 70),
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Font = new Font("Segoe UI", 9F),
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(25, 25, 50), ForeColor = neonTitle, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleCenter },
            ColumnHeadersHeight = 30,
            EnableHeadersVisualStyles = false,
            DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(22, 22, 45), ForeColor = inputFg, SelectionBackColor = Color.FromArgb(40, 40, 80), SelectionForeColor = Color.White, Padding = new Padding(4, 2, 4, 2) },
            RowTemplate = { Height = 28 },
            AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(15, 15, 32) }
        };

        _dgv.AutoGenerateColumns = false;
        _dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "InvoiceNo", HeaderText = "INVOICE", Width = 90 });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "QuantityAdded", HeaderText = "QTY +/-", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10F, FontStyle.Bold) } });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StockBefore", HeaderText = "BEFORE", Width = 70, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StockAfter", HeaderText = "AFTER", Width = 70, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TYPE", Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Reference", HeaderText = "REFERENCE", Width = 160 });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedAt", HeaderText = "DATE/TIME", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" } });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UserName", HeaderText = "CASHIER", Width = 100 });

        RefreshGrid(_dgv);

        _dgv.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (_dgv.Rows[e.RowIndex].DataBoundItem is StockTrail st)
            {
                if (e.ColumnIndex == _dgv.Columns["InvoiceNo"]?.Index)
                {
                    var displayInv = !string.IsNullOrEmpty(st.InvoiceNo) ? st.InvoiceNo
                        : (st.Reference.Contains("void") ? "" : (st.Reference.Length >= 8 ? st.Reference : ""));
                    e.Value = displayInv;
                }
                if (e.ColumnIndex == _dgv.Columns["QuantityAdded"]?.Index)
                {
                    if (st.QuantityAdded > 0) e.CellStyle!.ForeColor = accentGreen;
                    else if (st.QuantityAdded < 0) e.CellStyle!.ForeColor = accentRed;
                }
                if (e.ColumnIndex == _dgv.Columns["TYPE"]?.Index)
                {
                    if (st.QuantityAdded > 0)
                        e.Value = st.Reference.StartsWith("Adjustment") ? "Adjustment" : "Stock Receiving";
                    else
                        e.Value = !string.IsNullOrEmpty(st.InvoiceNo) ? "Sale" :
                                  st.Reference.Contains("void", StringComparison.OrdinalIgnoreCase) ? "Void/Return" :
                                  !string.IsNullOrWhiteSpace(st.CustomerName) ? st.CustomerName : "Walk-in";
                }
            }
        };

        _dgv.CellMouseClick += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (_dgv.Rows[e.RowIndex].DataBoundItem is not StockTrail st) return;
            var inv = !string.IsNullOrEmpty(st.InvoiceNo) ? st.InvoiceNo 
                : st.Reference.Contains("void") ? null 
                : (st.Reference.Length >= 8 ? st.Reference : null);
            if (string.IsNullOrEmpty(inv)) return;

            var sale = SaleService.GetByInvoiceNo(inv);
            if (sale == null) return;

            using var receipt = new Form { Text = $"Receipt - {inv}", Size = new Size(600, 500), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.Sizable, BackColor = Color.FromArgb(10, 10, 26) };
            
            var padding = 10;
            var clientW = receipt.ClientSize.Width - (padding * 2);
            var clientH = receipt.ClientSize.Height;

            var pnlTool = new Panel { Location = new Point(padding, 0), Size = new Size(clientW, 45), BackColor = Color.FromArgb(20, 20, 40) };
            var lblTitle = new Label { Text = $"INVOICE: {inv}", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(15, 12), Size = new Size(350, 25) };
            var btnReprint = new Button { Text = " REPRINT", Font = new Font("Segoe UI", 10F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(72, 126, 176), ForeColor = Color.White, Cursor = Cursors.Hand, Size = new Size(120, 32), Location = new Point(pnlTool.Width - 135, 6) };
            btnReprint.Click += (_, _) =>
            {
                var activeItems = sale.Items.Where(x => !x.IsVoided).ToList();
                if (activeItems.Count < sale.Items.Count)
                {
                    var adjusted = new Sale
                    {
                        InvoiceNo = sale.InvoiceNo, SaleDate = sale.SaleDate,
                        SubTotal = activeItems.Sum(x => x.TotalPrice), Discount = 0, Tax = 0,
                        GrandTotal = activeItems.Sum(x => x.TotalPrice),
                        AmountPaid = activeItems.Sum(x => x.TotalPrice), Change = 0,
                        PaymentMethod = sale.PaymentMethod, OrderType = sale.OrderType,
                        Items = activeItems
                    };
                    PrinterService.PrintReceipt(adjusted, "Reprint (Void Adjusted)");
                }
                else
                {
                    PrinterService.PrintReceipt(sale, sale.UserId?.ToString() ?? "System");
                }
                receipt.DialogResult = DialogResult.OK;
                receipt.Close();
            };
            pnlTool.Controls.AddRange(new Control[] { lblTitle, btnReprint });

            var info = new Label { Text = $"Cashier: {sale.UserId}  |  Customer: {sale.CustomerName ?? "Walk-in"}  |  Payment: {sale.PaymentMethod}  |  Total: \u20b1{sale.GrandTotal:N2}", Location = new Point(padding, clientH - 40), Size = new Size(clientW, 30), Font = new Font("Segoe UI", 9F), ForeColor = dimText, BackColor = Color.FromArgb(20, 20, 40), TextAlign = ContentAlignment.MiddleLeft };

            var gridY = pnlTool.Bottom + padding;
            var gridH = info.Top - gridY - padding;

            var dgvReceipt = new DataGridView { Location = new Point(padding, gridY), Size = new Size(clientW, gridH), ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = Color.FromArgb(20, 20, 40), BorderStyle = BorderStyle.None, GridColor = Color.FromArgb(40, 40, 70), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, Font = new Font("Segoe UI", 9F), ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(25, 25, 50), ForeColor = neonTitle, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, ColumnHeadersHeight = 30, EnableHeadersVisualStyles = false, DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(22, 22, 45), ForeColor = inputFg, SelectionBackColor = Color.FromArgb(40, 40, 80), SelectionForeColor = Color.White }, RowTemplate = { Height = 28 }, AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(15, 15, 32) } };
            dgvReceipt.AutoGenerateColumns = false;
            dgvReceipt.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ITEM", DataPropertyName = "ProductName", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvReceipt.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "QTY", DataPropertyName = "Quantity", Width = 50 });
            dgvReceipt.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PRICE", DataPropertyName = "Price", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
            dgvReceipt.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TOTAL", DataPropertyName = "TotalPrice", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
            dgvReceipt.DataSource = sale.Items;
            dgvReceipt.CellFormatting += (_, ef) =>
            {
                if (ef.RowIndex < 0) return;
                if (dgvReceipt.Rows[ef.RowIndex].DataBoundItem is SaleItem si && si.IsVoided)
                {
                    ef.CellStyle!.ForeColor = Color.FromArgb(231, 76, 60);
                    ef.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Strikeout);
                }
            };

            receipt.Resize += (_, _) =>
            {
                var curW = receipt.ClientSize.Width - (padding * 2);
                var curH = receipt.ClientSize.Height;
                pnlTool.Size = new Size(curW, pnlTool.Height);
                btnReprint.Left = pnlTool.Width - 135;
                info.Location = new Point(padding, curH - 40);
                info.Size = new Size(curW, info.Height);
                dgvReceipt.Size = new Size(curW, info.Top - dgvReceipt.Top - padding);
            };

            receipt.Controls.AddRange(new Control[] { pnlTool, dgvReceipt, info });
            receipt.ShowDialog();
        };

        pnlGrid.Controls.AddRange(new Control[] { lblGridTitle, _dgv });
        pnlMain.Controls.AddRange(new Control[] { pnlAdjust, pnlGrid });
        Controls.Clear();
        Controls.AddRange(new Control[] { pnlMain, pnlToolbar });

        Shown += (_, _) => LayoutPanels();
        Resize += (_, _) => LayoutPanels();
    }

    private void LayoutPanels()
    {
        var margin = 10;
        var w = Math.Max(pnlMain.ClientSize.Width - margin * 2, 100);
        var adjustH = _isAdmin ? 65 : 0;
        var gridY = _isAdmin ? margin + adjustH + margin : margin;
        var gridH = Math.Max(pnlMain.ClientSize.Height - gridY - margin, 50);

        pnlAdjust.Location = new Point(margin, margin);
        pnlAdjust.Size = new Size(w, adjustH);

        pnlGrid.Location = new Point(margin, gridY);
        pnlGrid.Size = new Size(w, gridH);

        _dgv.Location = new Point(8, 32);
        _dgv.Size = new Size(Math.Max(w - 16, 50), Math.Max(gridH - 40, 50));
    }

    private void btnAdjust_Click(object? sender, EventArgs e)
    {
        var qty = (int)numAdjustQty.Value;
        if (qty == 0) { MessageBox.Show("Enter a non-zero quantity.", "Adjustment", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        var reason = string.IsNullOrWhiteSpace(txtAdjustReason.Text) ? "Manual adjustment" : txtAdjustReason.Text.Trim();
        var userName = _currentUser != null && !string.IsNullOrEmpty(_currentUser.FullName)
            ? _currentUser.FullName : _currentUser?.Username ?? "Admin";

        var stockBefore = _product.StockQty;
        var stockAfter = stockBefore + qty;
        if (stockAfter < 0) { MessageBox.Show("Resulting stock would be negative.", "Adjustment", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            using var upd = new SQLiteCommand("UPDATE Products SET StockQty = @new WHERE Id = @id", conn);
            upd.Parameters.AddWithValue("@new", stockAfter);
            upd.Parameters.AddWithValue("@id", _product.Id);
            upd.ExecuteNonQuery();

            var now = TimeHelper.Now.ToString("yyyy-MM-dd HH:mm:ss");

            using var ins = new SQLiteCommand(
                "INSERT INTO StockTrail (ProductId, ProductName, Barcode, QuantityAdded, StockBefore, StockAfter, Reference, UserId, UserName, CreatedAt) " +
                "VALUES (@pid, @pn, @bc, @qa, @sb, @sa, @ref, @uid, @un, @ca)", conn);
            ins.Parameters.AddWithValue("@pid", _product.Id);
            ins.Parameters.AddWithValue("@pn", _product.Name);
            ins.Parameters.AddWithValue("@bc", _product.Barcode);
            ins.Parameters.AddWithValue("@qa", qty);
            ins.Parameters.AddWithValue("@sb", stockBefore);
            ins.Parameters.AddWithValue("@sa", stockAfter);
            ins.Parameters.AddWithValue("@ref", $"Adjustment: {reason}");
            ins.Parameters.AddWithValue("@uid", _currentUser?.Id ?? 0);
            ins.Parameters.AddWithValue("@un", userName);
            ins.Parameters.AddWithValue("@ca", now);
            ins.ExecuteNonQuery();

            using var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", conn);
            var trailId = Convert.ToInt32(idCmd.ExecuteScalar());

            tx.Commit();

            _ = SyncService.SyncStockTrail(new StockTrail { Id = trailId, ProductId = _product.Id, ProductName = _product.Name, Barcode = _product.Barcode, QuantityAdded = qty, StockBefore = stockBefore, StockAfter = stockAfter, Reference = $"Adjustment: {reason}", UserId = _currentUser?.Id ?? 0, UserName = userName, CreatedAt = now });
            _ = SyncService.SyncProduct(ProductService.GetById(_product.Id)!);

            _product.StockQty = stockAfter;
            lblCurrentStock.Text = $"CURRENT STOCK: {stockAfter}";
            lblCurrentStock.ForeColor = stockAfter == 0 ? Color.FromArgb(231, 76, 60) : Color.FromArgb(46, 204, 113);

            RefreshGrid(_dgv);
            numAdjustQty.Value = 0;

            MessageBox.Show($"Stock adjusted: {stockBefore} → {stockAfter} ({qty:+0;-#}).", "Adjustment Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            MessageBox.Show($"Adjustment failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshGrid(DataGridView dgv)
    {
        dgv.SuspendLayout();
        dgv.DataSource = StockService.GetTrail(_product.Id, 500);
        dgv.ResumeLayout();
    }

    private Label lblCurrentStock = null!;
    private NumericUpDown numAdjustQty = null!;
    private TextBox txtAdjustReason = null!;
    private Panel pnlAdjust = null!;
    private Panel pnlGrid = null!;
    private Panel pnlMain = null!;
    private DataGridView _dgv = null!;
}
