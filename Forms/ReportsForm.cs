using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class ReportsForm : Form
{
    private readonly User? _currentUser;
    private readonly bool _isAdmin;

    public ReportsForm(User? user = null)
    {
        _currentUser = user;
        _isAdmin = _currentUser?.Role == "Admin";
        InitializeComponent();
        if (_isAdmin) LoadReport();
        DebugHelper.AddFormLabel(this);
    }

    private void LoadReport()
    {
        var inv = txtInvoiceSearch.Text.Trim();
        if (string.IsNullOrEmpty(inv) && !_isAdmin)
            return;
        var dt = dtpDate.Value;
        var pm = cmbPaymentFilter.SelectedItem?.ToString();
        if (pm == "All") pm = null;
        var sales = SaleService.GetSales(dt, dt, inv, paymentMethod: pm);

        dgvSales.AutoGenerateColumns = false;
        dgvSales.Columns.Clear();
        dgvSales.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "InvoiceNo", HeaderText = "INVOICE", Width = 220, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(0, 245, 255) } });
        dgvSales.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SaleDate", HeaderText = "DATE", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm", ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvSales.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EffectiveTotal", HeaderText = "TOTAL", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.FromArgb(0, 245, 255), Font = new Font("Segoe UI", 9F, FontStyle.Bold) } });
        dgvSales.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PaymentMethod", HeaderText = "METHOD", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvSales.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "OrderType", HeaderText = "TYPE", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvSales.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "STATUS", Width = 90 });
        dgvSales.DataSource = sales;
        dgvSales.RowHeadersVisible = false;
        dgvSales.BackgroundColor = Color.FromArgb(20, 20, 40);
        dgvSales.BorderStyle = BorderStyle.None;
        dgvSales.GridColor = Color.FromArgb(40, 40, 70);
        dgvSales.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 25, 50);
        dgvSales.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(0, 245, 255);
        dgvSales.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgvSales.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvSales.ColumnHeadersHeight = 30;
        dgvSales.EnableHeadersVisualStyles = false;
        dgvSales.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 40, 80);
        dgvSales.DefaultCellStyle.SelectionForeColor = Color.White;
        dgvSales.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        dgvSales.RowTemplate.Height = 28;
        dgvSales.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(15, 15, 32);
        dgvSales.DefaultCellStyle.BackColor = Color.FromArgb(22, 22, 45);
        dgvSales.DefaultCellStyle.ForeColor = Color.FromArgb(230, 230, 245);

        dgvSales.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || dgvSales.Rows[e.RowIndex].DataBoundItem is not Sale row) return;
            if (e.ColumnIndex == dgvSales.Columns["Status"]?.Index)
            {
                e.Value = row.IsVoided ? "VOIDED" : "COMPLETED";
                if (e.CellStyle != null)
                {
                    e.CellStyle.ForeColor = row.IsVoided ? Color.FromArgb(231, 76, 60) : Color.FromArgb(46, 204, 113);
                    e.CellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
                }
            }
            else if (row.IsVoided)
            {
                if (e.CellStyle != null) e.CellStyle.ForeColor = Color.Gray;
            }
        };

        if (_isAdmin)
        {
            var totalSales = sales.Where(x => !x.IsVoided).Sum(x => x.EffectiveTotal);
            lblMetricCount.Text = $"TRANSACTIONS: {sales.Count}";
            lblMetricTotal.Text = $"TOTAL SALES: \u20b1{totalSales:N2}";
        }
    }

    private void btnRefresh_Click(object? sender, EventArgs e) => LoadReport();

    private void btnReprint_Click(object? sender, EventArgs e)
    {
        if (dgvSales.CurrentRow?.DataBoundItem is not Sale sale) { MessageBox.Show("Select a receipt from the list.", "No Receipt", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var fullSale = SaleService.GetById(sale.Id);
        if (fullSale == null) return;

        var activeItems = fullSale.Items.Where(x => !x.IsVoided).ToList();
        if (activeItems.Count == 0)
        {
            MessageBox.Show("All items in this receipt are voided. Nothing to reprint.", "No Items", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Customer? customer = fullSale.CustomerId.HasValue ? CustomerService.GetById(fullSale.CustomerId.Value) : null;

        if (activeItems.Count < fullSale.Items.Count)
        {
            var adjusted = new Sale { InvoiceNo = fullSale.InvoiceNo, SaleDate = fullSale.SaleDate, SubTotal = activeItems.Sum(x => x.TotalPrice), Discount = 0, Tax = 0, GrandTotal = activeItems.Sum(x => x.TotalPrice), AmountPaid = activeItems.Sum(x => x.TotalPrice), Change = 0, PaymentMethod = fullSale.PaymentMethod, OrderType = fullSale.OrderType, Items = activeItems };
            PrinterService.PrintReceipt(adjusted, "Reprint (Void Adjusted)", customer);
        }
        else
        {
            PrinterService.PrintReceipt(fullSale, "Reprint", customer);
        }
    }

    private void btnVoidReceipt_Click(object? sender, EventArgs e)
    {
        if (dgvSales.CurrentRow?.DataBoundItem is not Sale sale) { MessageBox.Show("Select a receipt from the list.", "No Receipt", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (sale.IsVoided)
        {
            MessageBox.Show("This receipt is already voided.", "Already Voided", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var reason = Microsoft.VisualBasic.Interaction.InputBox("Enter reason for voiding this receipt:", "Void Receipt", "");
        if (string.IsNullOrWhiteSpace(reason)) return;
        if (MessageBox.Show($"Void entire receipt '{sale.InvoiceNo}' (\u20b1{sale.GrandTotal:N2})?\nReason: {reason}", "Confirm Void", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var voidUserId = _currentUser?.Id ?? 0;
        var voidUserName = !string.IsNullOrEmpty(_currentUser?.FullName) ? _currentUser.FullName : (_currentUser?.Username ?? "System");
        try { SaleService.VoidSale(sale.Id, reason, voidUserId, voidUserName); MessageBox.Show("Receipt voided successfully. Stock has been restored.", "Voided", MessageBoxButtons.OK, MessageBoxIcon.Information); LoadReport(); }
        catch (Exception ex) { MessageBox.Show($"Error voiding receipt: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void btnVoidItem_Click(object? sender, EventArgs e)
    {
        if (dgvSales.CurrentRow?.DataBoundItem is not Sale sale) { MessageBox.Show("Select a receipt from the list.", "No Receipt", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (sale.IsVoided) { MessageBox.Show("This receipt is already voided.", "Cannot Void Item", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var fullSale = SaleService.GetById(sale.Id);
        if (fullSale == null || fullSale.Items.Count == 0) return;
        var items = fullSale.Items.Where(x => !x.IsVoided).ToList();
        if (items.Count == 0) { MessageBox.Show("All items in this receipt are already voided.", "No Items", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var itemNames = items.Select((x, i) => $"{i + 1}. {x.ProductName} x{x.Quantity} = \u20b1{x.TotalPrice:N2}").ToArray();
        var choice = ShowItemPicker("Select item to void:", itemNames);
        if (choice < 0 || choice >= items.Count) return;
        var selected = items[choice];
        var reason = Microsoft.VisualBasic.Interaction.InputBox($"Enter reason for voiding '{selected.ProductName}':", "Void Item", "");
        if (string.IsNullOrWhiteSpace(reason)) return;
        if (MessageBox.Show($"Void '{selected.ProductName}' x{selected.Quantity} (\u20b1{selected.TotalPrice:N2})?\nReason: {reason}", "Confirm Void Item", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var voidUserId = _currentUser?.Id ?? 0;
        var voidUserName = !string.IsNullOrEmpty(_currentUser?.FullName) ? _currentUser.FullName : (_currentUser?.Username ?? "System");
        try { SaleService.VoidItem(selected.Id, reason, voidUserId, voidUserName); MessageBox.Show("Item voided successfully. Stock has been restored.", "Voided", MessageBoxButtons.OK, MessageBoxIcon.Information); LoadReport(); }
        catch (Exception ex) { MessageBox.Show($"Error voiding item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void btnVoidLog_Click(object? sender, EventArgs e) { using var form = new VoidLogForm(); form.ShowDialog(); }

    private int ShowItemPicker(string prompt, string[] items)
    {
        var panelBg = Color.FromArgb(20, 20, 40);
        var inputBg = Color.FromArgb(30, 30, 55);
        var inputFg = Color.FromArgb(230, 230, 245);
        var neonTitle = Color.FromArgb(0, 245, 255);
        var borderColor = Color.FromArgb(40, 40, 70);

        using var form = new Form { Text = prompt, Size = new Size(420, 380), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.Sizable, BackColor = Color.FromArgb(10, 10, 26) };
        var lbl = new Label { Text = prompt, Location = new Point(15, 15), Size = new Size(380, 20), ForeColor = neonTitle, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        var lb = new ListBox { Location = new Point(15, 42), Size = new Size(380, 240), BackColor = panelBg, ForeColor = inputFg, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9F) };
        lb.Items.AddRange(items);
        var result = -1;
        var btnOk = new Button { Text = "OK", Location = new Point(110, 295), Size = new Size(90, 35), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(72, 126, 176), ForeColor = Color.White, Cursor = Cursors.Hand, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", Location = new Point(210, 295), Size = new Size(90, 35), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(149, 165, 166), ForeColor = Color.White, Cursor = Cursors.Hand, DialogResult = DialogResult.Cancel };
        lb.DoubleClick += (s, e) => { result = lb.SelectedIndex; form.Close(); };
        lb.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { result = lb.SelectedIndex; form.Close(); } };
        form.Controls.AddRange(new Control[] { lbl, lb, btnOk, btnCancel });
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;
        if (form.ShowDialog() == DialogResult.OK) result = lb.SelectedIndex;
        return result;
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
        var accentOrange = Color.FromArgb(243, 156, 18);
        var accentBlue = Color.FromArgb(72, 126, 176);
        var accentPurple = Color.FromArgb(155, 89, 182);

        BackColor = canvasBg;
        Text = "Sales Reports";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        // ── TOP TOOLBAR ──
        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };

        var lblPageTitle = new Label { Text = "\uD83D\uDCCA SALES REPORTS", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(250, 28) };

        var toolbarCtls = new List<Control> { lblPageTitle };

        var lblD = new Label { Text = "Date:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(290, 16), Size = new Size(40, 20) };
        dtpDate = new DateTimePicker { Location = new Point(333, 12), Size = new Size(120, 25), BackColor = inputBg, ForeColor = inputFg, Value = TimeHelper.Today };
        if (_isAdmin) dtpDate.ValueChanged += (_, _) => LoadReport();
        toolbarCtls.AddRange(new Control[] { lblD, dtpDate });

        var lblInv = new Label { Text = "Receipt:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(470, 16), Size = new Size(55, 20) };
        txtInvoiceSearch = new TextBox { Location = new Point(528, 12), Size = new Size(140, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 9F) };
        txtInvoiceSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; LoadReport(); } };
        toolbarCtls.AddRange(new Control[] { lblInv, txtInvoiceSearch });

        var lblPm = new Label { Text = "Method:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(685, 16), Size = new Size(55, 20) };
        cmbPaymentFilter = new ComboBox { Location = new Point(743, 12), Size = new Size(110, 25), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = inputBg, ForeColor = inputFg, FlatStyle = FlatStyle.Flat };
        cmbPaymentFilter.Items.AddRange(new string[] { "All", "Cash", "E-Wallet", "Credit", "Split" });
        cmbPaymentFilter.SelectedIndex = 0;
        cmbPaymentFilter.SelectedIndexChanged += (_, _) => LoadReport();
        toolbarCtls.AddRange(new Control[] { lblPm, cmbPaymentFilter });

        pnlToolbar.Controls.AddRange(toolbarCtls.ToArray());

        // ── METRICS BAR ──
        if (_isAdmin)
        {
            var pnlMetrics = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = canvasBg };
            lblMetricCount = new Label { Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, 8), Size = new Size(200, 20), TextAlign = ContentAlignment.MiddleLeft, AutoSize = false };
            lblMetricTotal = new Label { Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(250, 8), Size = new Size(300, 20), TextAlign = ContentAlignment.MiddleLeft, AutoSize = false };
            pnlMetrics.Controls.AddRange(new Control[] { lblMetricCount, lblMetricTotal });
            Controls.Add(pnlMetrics);
        }

        // ── MAIN PANEL ──
        var pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = canvasBg };

        var pnlGrid = new Panel { Location = new Point(10, 10), Size = new Size(1000, 400), BackColor = panelBg };
        pnlGrid.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlGrid.Width - 1, pnlGrid.Height - 1); };

        dgvSales = new DataGridView
        {
            Location = new Point(8, 32),
            Size = new Size(984, 360),
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            Font = new Font("Segoe UI", 9F),
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
        };
        pnlGrid.Controls.Add(dgvSales);
        pnlMain.Controls.Add(pnlGrid);

        // ── ACTION BUTTONS ──
        var pnlActions = new Panel { Location = new Point(10, 420), Size = new Size(1000, 50), BackColor = canvasBg };
        var actionBtns = new List<Control>();

        btnRefresh = new Button { Text = "\uD83D\uDD04 REFRESH", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(0, 5), Size = new Size(120, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnRefresh.Click += btnRefresh_Click;
        actionBtns.Add(btnRefresh);

        btnReprint = new Button { Text = "\uD83D\uDDA8\uFE0F REPRINT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(125, 5), Size = new Size(120, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentPurple, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnReprint.Click += btnReprint_Click;
        actionBtns.Add(btnReprint);

        btnVoidReceipt = new Button { Text = "\u2716 VOID RECEIPT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(250, 5), Size = new Size(130, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentRed, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnVoidReceipt.Click += btnVoidReceipt_Click;
        actionBtns.Add(btnVoidReceipt);

        btnVoidItem = new Button { Text = "\u2716 VOID ITEM", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(385, 5), Size = new Size(120, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentOrange, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnVoidItem.Click += btnVoidItem_Click;
        actionBtns.Add(btnVoidItem);

        btnVoidLog = new Button { Text = "\uD83D\uDCCB VOID LOG", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(510, 5), Size = new Size(120, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(149, 165, 166), ForeColor = Color.White, Cursor = Cursors.Hand };
        btnVoidLog.Click += btnVoidLog_Click;
        actionBtns.Add(btnVoidLog);

        pnlActions.Controls.AddRange(actionBtns.ToArray());
        pnlMain.Controls.Add(pnlActions);

        Controls.Add(pnlToolbar);
        Controls.Add(pnlMain);

        Shown += (_, _) => ResizeLayout(pnlGrid, dgvSales, pnlActions);
        Resize += (_, _) => ResizeLayout(pnlGrid, dgvSales, pnlActions);
    }

    private void ResizeLayout(Panel pnlGrid, DataGridView dgv, Panel pnlActions)
    {
        var margin = 10;
        var toolbarH = 50;
        var metricsH = _isAdmin ? 35 : 0;
        var availH = ClientSize.Height - toolbarH - metricsH - margin * 4 - 50;
        var availW = ClientSize.Width - margin * 3;

        pnlGrid.Location = new Point(margin, margin);
        pnlGrid.Size = new Size(availW, availH);
        pnlActions.Location = new Point(margin, availH + margin * 2);
        pnlActions.Size = new Size(availW, 50);

        dgv.Location = new Point(8, 32);
        dgv.Size = new Size(availW - 16, availH - 40);
    }

    private DataGridView dgvSales = null!;
    private DateTimePicker dtpDate = null!;
    private ComboBox cmbPaymentFilter = null!;
    private Button btnRefresh = null!, btnReprint = null!, btnVoidReceipt = null!, btnVoidItem = null!, btnVoidLog = null!;
    private Label lblMetricCount = null!, lblMetricTotal = null!;
    private TextBox txtInvoiceSearch = null!;
}
