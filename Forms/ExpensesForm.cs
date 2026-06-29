using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public class ExpensesForm : Form
{
    private readonly User _currentUser;

    public ExpensesForm(User user)
    {
        _currentUser = user;
        InitializeComponent();
        LoadExpenses();
        DebugHelper.AddFormLabel(this);
    }

    private void LoadExpenses()
    {
        var expenses = ExpenseService.GetExpensesForCurrentShift();
        dgvExpenses.AutoGenerateColumns = false;
        dgvExpenses.Columns.Clear();
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Timestamp", HeaderText = "DATE/TIME", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm", ForeColor = ThemeManager.Current.TextSecondary } });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Category", HeaderText = "CATEGORY", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ThemeManager.Current.AccentCyan } });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Description", HeaderText = "DESCRIPTION", Width = 200, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ThemeManager.Current.TextPrimary } });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReferenceNo", HeaderText = "REF NO", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ThemeManager.Current.TextSecondary } });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "AMOUNT", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = ThemeManager.Current.AccentRed, Font = new Font("Segoe UI", 9F, FontStyle.Bold) } });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CashierUsername", HeaderText = "CASHIER", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ThemeManager.Current.TextSecondary } });
        dgvExpenses.DataSource = expenses;
        dgvExpenses.RowHeadersVisible = false;
        dgvExpenses.BackgroundColor = ThemeManager.Current.PanelBg;
        dgvExpenses.BorderStyle = BorderStyle.None;
        dgvExpenses.GridColor = ThemeManager.Current.BorderColor;
        dgvExpenses.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.Current.DgvHeaderBg;
        dgvExpenses.ColumnHeadersDefaultCellStyle.ForeColor = ThemeManager.Current.AccentCyan;
        dgvExpenses.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgvExpenses.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvExpenses.ColumnHeadersHeight = 30;
        dgvExpenses.EnableHeadersVisualStyles = false;
        dgvExpenses.DefaultCellStyle.SelectionBackColor = ThemeManager.Current.DgvSelection;
        dgvExpenses.DefaultCellStyle.SelectionForeColor = Color.White;
        dgvExpenses.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        dgvExpenses.RowTemplate.Height = 28;
        dgvExpenses.AlternatingRowsDefaultCellStyle.BackColor = ThemeManager.Current.DgvRowAlt;
        dgvExpenses.DefaultCellStyle.BackColor = ThemeManager.Current.DgvRowNormal;
        dgvExpenses.DefaultCellStyle.ForeColor = ThemeManager.Current.TextPrimary;

        var total = expenses.Sum(x => x.Amount);
        lblTotalExpenses.Text = $"TOTAL EXPENSES: \u20b1{total:N2}";
        lblCount.Text = $"Records: {expenses.Count}";
    }

    private void btnAdd_Click(object? sender, EventArgs e)
    {
        using var form = new ExpenseEntryForm(_currentUser);
        if (form.ShowDialog() == DialogResult.OK) LoadExpenses();
    }

    private void btnRefresh_Click(object? sender, EventArgs e) => LoadExpenses();

    private void InitializeComponent()
    {
        var t = ThemeManager.Current;

        BackColor = t.CanvasBg;
        Text = "Expense Management";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = t.PanelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(t.BorderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };
        var lblPageTitle = new Label { Text = "\uD83D\uDCB8 EXPENSE MANAGEMENT", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = t.AccentCyan, Location = new Point(20, 12), Size = new Size(280, 28) };
        lblTotalExpenses = new Label { Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = t.AccentRed, Location = new Point(400, 12), Size = new Size(300, 25), TextAlign = ContentAlignment.MiddleRight, AutoSize = false };
        btnAdd = new Button { Text = "+ ADD EXPENSE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(pnlToolbar.Width - 250, 8), Size = new Size(120, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = t.AccentBlue, ForeColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnAdd.Click += btnAdd_Click;
        btnRefresh = new Button { Text = "\uD83D\uDD04 REFRESH", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(pnlToolbar.Width - 120, 8), Size = new Size(100, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = ThemeManager.Current.AccentGrey, ForeColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnRefresh.Click += btnRefresh_Click;
        pnlToolbar.Controls.AddRange(new Control[] { lblPageTitle, lblTotalExpenses, btnAdd, btnRefresh });

        var pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = t.CanvasBg };
        var margin = 10;
        var pnlGrid = new Panel { Location = new Point(margin, margin), Size = new Size(760, 440), BackColor = t.PanelBg };
        pnlGrid.Paint += (s, e) => { using var pen = new Pen(t.BorderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlGrid.Width - 1, pnlGrid.Height - 1); };
        var lblGridTitle = new Label { Text = "CURRENT SHIFT EXPENSES", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = t.TextMuted, Location = new Point(12, 8), Size = new Size(200, 20) };
        dgvExpenses = new DataGridView { Location = new Point(8, 32), Size = new Size(744, 400), SelectionMode = DataGridViewSelectionMode.FullRowSelect, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, MultiSelect = false, Font = new Font("Segoe UI", 9F), CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal };
        pnlGrid.Controls.AddRange(new Control[] { lblGridTitle, dgvExpenses });

        var pnlFooter = new Panel { Location = new Point(margin, 460), Size = new Size(760, 30), BackColor = t.CanvasBg };
        lblCount = new Label { Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = t.TextMuted, Location = new Point(5, 5), Size = new Size(200, 20), TextAlign = ContentAlignment.MiddleLeft, AutoSize = false };
        pnlFooter.Controls.Add(lblCount);
        pnlMain.Controls.AddRange(new Control[] { pnlGrid, pnlFooter });

        Controls.Clear();
        Controls.AddRange(new Control[] { pnlMain, pnlToolbar });

        Shown += (_, _) => ResizeLayout(pnlGrid, pnlFooter, pnlMain);
        Resize += (_, _) => ResizeLayout(pnlGrid, pnlFooter, pnlMain);
    }

    private void ResizeLayout(Panel pnlGrid, Panel pnlFooter, Panel pnlMain)
    {
        var margin = 10;
        var w = ClientSize.Width - margin * 3;
        var h = ClientSize.Height - 50 - margin * 4 - 30;
        pnlGrid.Location = new Point(margin, margin);
        pnlGrid.Size = new Size(w, h);
        pnlFooter.Location = new Point(margin, h + margin * 2);
        pnlFooter.Size = new Size(w, 30);
        dgvExpenses.Location = new Point(8, 32);
        dgvExpenses.Size = new Size(w - 16, h - 40);
    }

    public void ApplyTheme()
    {
        var t = ThemeManager.Current;
        BackColor = t.CanvasBg;
        ForeColor = t.TextPrimary;
    }

    private DataGridView dgvExpenses = null!;
    private Button btnAdd = null!, btnRefresh = null!;
    private Label lblTotalExpenses = null!, lblCount = null!;
}

public class ExpenseEntryForm : Form
{
    private readonly User _currentUser;
    private string _receiptPath = "";

    public ExpenseEntryForm(User user)
    {
        _currentUser = user;
        InitializeComponent();
    }

    private void btnAttachReceipt_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
            Title = "Select Receipt Photo"
        };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        try
        {
            var receiptsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExpenseReceipts");
            Directory.CreateDirectory(receiptsDir);
            var ext = Path.GetExtension(ofd.FileName);
            var destFile = Path.Combine(receiptsDir, $"expense_{TimeHelper.Now:yyyyMMddHHmmss}{ext}");
            File.Copy(ofd.FileName, destFile, overwrite: true);
            _receiptPath = destFile;
            pbReceipt.Image = Image.FromFile(destFile);
            pbReceipt.Visible = true;
            lblReceiptPath.Text = "Receipt attached: " + Path.GetFileName(destFile);
            lblReceiptPath.Visible = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to attach receipt: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void btnSave_Click(object? sender, EventArgs e)
    {
        if (!decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0) { MessageBox.Show("Amount must be greater than zero.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); txtAmount.Focus(); return; }
        if (cmbCategory.SelectedIndex < 0) { MessageBox.Show("Please select a category.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); cmbCategory.Focus(); return; }
        if (string.IsNullOrWhiteSpace(txtDescription.Text)) { MessageBox.Show("Description cannot be blank.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); txtDescription.Focus(); return; }

        ExpenseService.SaveExpense(amount, cmbCategory.Text, txtDescription.Text.Trim(), txtReferenceNo.Text.Trim(), _currentUser.Username, _receiptPath);
        MessageBox.Show($"Expense of \u20b1{amount:N2} recorded successfully.", "Expense Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void InitializeComponent()
    {
        var t = ThemeManager.Current;

        BackColor = t.CanvasBg;
        Text = "Add Expense";
        Size = new Size(520, 480);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;

        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = t.PanelBg };
        pnlToolbar.Paint += (s, ev) => { using var pen = new Pen(t.BorderColor, 1); ev.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };
        var lblTitle = new Label { Text = "\uD83D\uDCB8 NEW EXPENSE ENTRY", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = t.AccentCyan, Location = new Point(20, 12), Size = new Size(360, 25) };
        pnlToolbar.Controls.Add(lblTitle);

        var pnlContent = new Panel { Dock = DockStyle.Fill, BackColor = t.CanvasBg };
        var margin = 15;
        var y = margin;
        var left = margin;
        var mid = 130;
        var fw = 360;

        var lblAmount = new Label { Text = "Amount:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = t.AccentCyan, Location = new Point(left, y), Size = new Size(100, 25) };
        txtAmount = new TextBox { Location = new Point(mid, y), Size = new Size(fw, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = t.InputBg, ForeColor = t.InputFg, Font = new Font("Segoe UI", 10F), TextAlign = HorizontalAlignment.Right };
        y += 40;

        var lblCategory = new Label { Text = "Category:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = t.AccentCyan, Location = new Point(left, y), Size = new Size(100, 25) };
        cmbCategory = new ComboBox { Location = new Point(mid, y), Size = new Size(fw, 25), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = t.InputBg, ForeColor = t.InputFg, Font = new Font("Segoe UI", 9F) };
        cmbCategory.Items.AddRange(new object[] { "Store Supplies", "Payouts", "Staff Meals", "Deliveries", "Utilities", "Maintenance", "Other" });
        y += 40;

        var lblDesc = new Label { Text = "Description:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = t.AccentCyan, Location = new Point(left, y), Size = new Size(100, 25) };
        txtDescription = new TextBox { Location = new Point(mid, y), Size = new Size(fw, 40), BorderStyle = BorderStyle.FixedSingle, BackColor = t.InputBg, ForeColor = t.InputFg, Font = new Font("Segoe UI", 9F), Multiline = true };
        y += 45;

        var lblRef = new Label { Text = "Reference No:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = t.AccentCyan, Location = new Point(left, y), Size = new Size(100, 25) };
        txtReferenceNo = new TextBox { Location = new Point(mid, y), Size = new Size(fw, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = t.InputBg, ForeColor = t.InputFg, Font = new Font("Segoe UI", 9F) };
        y += 35;

        btnAttachReceipt = new Button { Text = "\uD83D\uDCF7 ATTACH RECEIPT PHOTO", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(mid, y), Size = new Size(fw, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = t.AccentBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnAttachReceipt.Click += btnAttachReceipt_Click;
        y += 35;

        pbReceipt = new PictureBox { Location = new Point(mid + fw + 5, margin), Size = new Size(1, 1), SizeMode = PictureBoxSizeMode.Zoom, Visible = false, BorderStyle = BorderStyle.FixedSingle };
        lblReceiptPath = new Label { Font = new Font("Segoe UI", 8F), ForeColor = ThemeManager.Current.AccentGreen, Location = new Point(mid, y - 2), Size = new Size(fw, 18), Visible = false };
        pnlContent.Controls.Add(lblReceiptPath);

        btnSave = new Button { Text = "\u2714 SAVE EXPENSE", Font = new Font("Segoe UI", 10F, FontStyle.Bold), Location = new Point(80, y + 5), Size = new Size(340, 38), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = t.AccentGreen, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnSave.Click += btnSave_Click;
        var btnCancel = new Button { Text = "\u2716 CANCEL", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(80, y + 47), Size = new Size(340, 32), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = ThemeManager.Current.AccentGrey, ForeColor = Color.White, Cursor = Cursors.Hand, DialogResult = DialogResult.Cancel };

        pnlContent.Controls.AddRange(new Control[] { lblAmount, txtAmount, lblCategory, cmbCategory, lblDesc, txtDescription, lblRef, txtReferenceNo, btnAttachReceipt, pbReceipt, btnSave, btnCancel });
        Controls.AddRange(new Control[] { pnlContent, pnlToolbar });

        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    public void ApplyTheme()
    {
        var t = ThemeManager.Current;
        BackColor = t.CanvasBg;
        ForeColor = t.TextPrimary;
    }

    private TextBox txtAmount = null!;
    private ComboBox cmbCategory = null!;
    private TextBox txtDescription = null!;
    private TextBox txtReferenceNo = null!;
    private Button btnSave = null!;
    private Button btnAttachReceipt = null!;
    private PictureBox pbReceipt = null!;
    private Label lblReceiptPath = null!;
}
