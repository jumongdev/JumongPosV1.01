using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class CustomersForm : Form
{
    private Customer? _selected;
    private readonly User? _currentUser;

    public CustomersForm(User? currentUser = null)
    {
        _currentUser = currentUser;
        InitializeComponent();
        LoadCustomers();
        DebugHelper.AddFormLabel(this);
    }

    private void LoadCustomers(string? keyword = null)
    {
        dgvCustomers.AutoGenerateColumns = false;
        dgvCustomers.Columns.Clear();
        dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", HeaderText = "ID", Width = 45, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = ThemeManager.Current.TextMuted } });
        dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "NAME", Width = 160, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ThemeManager.Current.TextPrimary } });
        dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Phone", HeaderText = "PHONE", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ThemeManager.Current.TextSecondary } });
        dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Email", HeaderText = "EMAIL", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ThemeManager.Current.TextSecondary } });
        dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Address", HeaderText = "ADDRESS", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ThemeManager.Current.TextSecondary } });
        dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LoyaltyPoints", HeaderText = "POINTS", Width = 65, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = ThemeManager.Current.AccentOrange, Font = new Font("Segoe UI", 9F, FontStyle.Bold) } });
        dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreditBalance", HeaderText = "CREDIT BAL", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = ThemeManager.Current.AccentRed, Font = new Font("Segoe UI", 9F, FontStyle.Bold) } });
        dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreditLimit", HeaderText = "CREDIT LIMIT", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = ThemeManager.Current.TextMuted } });
        dgvCustomers.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsActive", HeaderText = "ACTIVE", Width = 55 });

        var data = keyword switch
        {
            null or "" => CustomerService.GetAll(),
            _ => CustomerService.Search(keyword)
        };
        dgvCustomers.DataSource = data;
        UpdateMetrics(data);
    }

    private void UpdateMetrics(IEnumerable<Customer> data)
    {
        var list = data.ToList();
        var total = list.Count;
        var totalCredit = list.Sum(c => c.CreditBalance);
        var totalPoints = list.Sum(c => c.LoyaltyPoints);
        var withDebt = list.Count(c => c.CreditBalance > 0);

        lblMetricTotal.Text = $"TOTAL: {total}";
        lblMetricCredit.Text = $"CREDIT OUT: \u20b1{totalCredit:N2}";
        lblMetricDebtors.Text = $"DEBTORS: {withDebt}";
        lblMetricPoints.Text = $"POINTS: {totalPoints:N0}";
    }

    private void btnSave_Click(object? sender, EventArgs e)
    {
        var name = txtName.Text.Trim();
        var phone = txtPhone.Text.Trim();
        var email = txtEmail.Text.Trim();
        var address = txtAddress.Text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            ShowValidationAlert("Customer name is required.");
            txtName.Focus();
            return;
        }
        if (name.Length < 2)
        {
            ShowValidationAlert("Customer name must be at least 2 characters.");
            txtName.Focus();
            return;
        }
        if (!string.IsNullOrEmpty(phone) && !IsValidPhone(phone))
        {
            ShowValidationAlert("Phone number must contain only digits, spaces, hyphens, or + sign.");
            txtPhone.Focus();
            return;
        }

        var c = _selected ?? new Customer();
        c.Name = name;
        c.Phone = phone;
        c.Email = email;
        c.Address = address;
        c.CreditLimit = decimal.TryParse(txtCreditLimit.Text, out var cl) ? cl : 0;
        c.IsActive = chkActive.Checked;

        var modifiedBy = _currentUser != null && !string.IsNullOrEmpty(_currentUser.FullName)
            ? _currentUser.FullName : _currentUser?.Username ?? "";
        var error = CustomerService.Save(c, modifiedBy);
        if (error != null)
        {
            ShowValidationAlert(error);
            return;
        }

        LoadCustomers(txtSearch.Text.Trim());
        ClearForm();
        ShowSuccessAlert(_selected != null ? "Customer updated successfully." : "Customer created successfully.");
    }

    private bool IsValidPhone(string phone)
    {
        foreach (var ch in phone)
        {
            if (!char.IsDigit(ch) && ch != ' ' && ch != '-' && ch != '+' && ch != '(' && ch != ')')
                return false;
        }
        return true;
    }

    private void ShowValidationAlert(string message)
    {
        MessageBox.Show(message, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ShowSuccessAlert(string message)
    {
        MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ClearForm()
    {
        _selected = null;
        txtName.Clear();
        txtPhone.Clear();
        txtEmail.Clear();
        txtAddress.Clear();
        txtCreditLimit.Clear();
        chkActive.Checked = true;
        lblFormTitle.Text = "NEW CUSTOMER";
        lblFormTitle.ForeColor = ThemeManager.Current.AccentCyan;
    }

    private void InitializeComponent()
    {
        var t = ThemeManager.Current;

        BackColor = t.CanvasBg;
        Text = "Manage Customers";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        // ── TOP TOOLBAR ──
        var pnlToolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = t.PanelBg
        };
        pnlToolbar.Paint += (s, e) =>
        {
            using var pen = new Pen(t.BorderColor, 1);
            e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1);
        };

        var lblPageTitle = new Label
        {
            Text = "\u2699 CUSTOMER MANAGEMENT",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = t.AccentCyan,
            Location = new Point(20, 12),
            Size = new Size(300, 28),
            AutoSize = false
        };

        txtSearch = new TextBox
        {
            Font = new Font("Segoe UI", 10F),
            Location = new Point(340, 12),
            Size = new Size(250, 28),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = t.InputBg,
            ForeColor = t.InputFg
        };
        txtSearch.TextChanged += (_, _) => LoadCustomers(txtSearch.Text.Trim());

        var lblSearchIcon = new Label
        {
            Text = "\uD83D\uDD0D",
            Font = new Font("Segoe UI", 10F),
            Location = new Point(315, 14),
            Size = new Size(25, 22),
            TextAlign = ContentAlignment.MiddleRight
        };

        var btnClean = new Button
        {
            Text = "\u267B CLEAN DUPES",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(720, 12),
            Size = new Size(120, 28),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = t.AccentOrange,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Visible = _currentUser?.Role == "Admin"
        };
        btnClean.Click += (_, _) =>
        {
            if (MessageBox.Show("Delete duplicate customers (same name + phone) with 0 points?", "Clean Duplicates", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var deleted = CustomerService.RemoveDuplicatesNoPoints();
            MessageBox.Show($"{deleted} duplicate(s) removed.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadCustomers(txtSearch.Text.Trim());
        };

        pnlToolbar.Controls.AddRange(new Control[] { lblPageTitle, lblSearchIcon, txtSearch, btnClean });

        // ── METRICS BAR ──
        var pnlMetrics = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = t.CanvasBg
        };

        lblMetricTotal = CreateMetricLabel(20, t.TextMuted);
        lblMetricTotal.Size = new Size(180, 20);
        lblMetricCredit = CreateMetricLabel(200, t.AccentRed);
        lblMetricCredit.Size = new Size(200, 20);
        lblMetricDebtors = CreateMetricLabel(400, t.AccentOrange);
        lblMetricDebtors.Size = new Size(200, 20);
        lblMetricPoints = CreateMetricLabel(600, t.AccentGreen);
        lblMetricPoints.Size = new Size(200, 20);

        pnlMetrics.Controls.AddRange(new Control[] { lblMetricTotal, lblMetricCredit, lblMetricDebtors, lblMetricPoints });

        // ── MAIN SPLIT ──
        var pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = t.CanvasBg };

        // LEFT PANEL - Data Grid
        var pnlLeft = new Panel
        {
            Location = new Point(10, 10),
            Size = new Size(600, 400),
            BackColor = t.PanelBg
        };
        pnlLeft.Paint += (s, e) =>
        {
            using var pen = new Pen(t.BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlLeft.Width - 1, pnlLeft.Height - 1);
        };

        var lblGridTitle = new Label
        {
            Text = "CUSTOMER ROSTER",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = t.TextMuted,
            Location = new Point(12, 8),
            Size = new Size(200, 20)
        };

        dgvCustomers = new DataGridView
        {
            Location = new Point(8, 32),
            Size = new Size(584, 360),
            BackgroundColor = t.PanelBg,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            Font = new Font("Segoe UI", 9F),
            GridColor = t.BorderColor,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
        };
        dgvCustomers.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.Current.DgvHeaderBg;
        dgvCustomers.ColumnHeadersDefaultCellStyle.ForeColor = t.AccentCyan;
        dgvCustomers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgvCustomers.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvCustomers.ColumnHeadersHeight = 30;
        dgvCustomers.EnableHeadersVisualStyles = false;
        dgvCustomers.DefaultCellStyle.SelectionBackColor = ThemeManager.Current.DgvSelection;
        dgvCustomers.DefaultCellStyle.SelectionForeColor = Color.White;
        dgvCustomers.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        dgvCustomers.RowTemplate.Height = 28;
        dgvCustomers.AlternatingRowsDefaultCellStyle.BackColor = ThemeManager.Current.DgvRowAlt;
        dgvCustomers.DefaultCellStyle.BackColor = ThemeManager.Current.DgvRowNormal;
        dgvCustomers.DefaultCellStyle.ForeColor = t.TextPrimary;
        dgvCustomers.SelectionChanged += DgvCustomers_SelectionChanged;
        dgvCustomers.DoubleClick += DgvCustomers_DoubleClick;

        pnlLeft.Controls.AddRange(new Control[] { lblGridTitle, dgvCustomers });

        // RIGHT PANEL - Entry Card
        var pnlRight = new Panel
        {
            Location = new Point(625, 10),
            Size = new Size(340, 400),
            BackColor = t.PanelBg
        };
        pnlRight.Paint += (s, e) =>
        {
            using var pen = new Pen(t.BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlRight.Width - 1, pnlRight.Height - 1);
        };

        lblFormTitle = new Label
        {
            Text = "NEW CUSTOMER",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = t.AccentCyan,
            Location = new Point(15, 10),
            Size = new Size(310, 25),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var y = 42;
        AddField("NAME", ref txtName, ref y, pnlRight, t.InputBg, t.InputFg, t.TextPrimary);
        AddField("PHONE", ref txtPhone, ref y, pnlRight, t.InputBg, t.InputFg, t.TextPrimary);
        AddField("EMAIL", ref txtEmail, ref y, pnlRight, t.InputBg, t.InputFg, t.TextPrimary);
        AddField("ADDRESS", ref txtAddress, ref y, pnlRight, t.InputBg, t.InputFg, t.TextPrimary);

        // Credit Limit field
        var lblCredLimit = new Label
        {
            Text = "CREDIT LIMIT",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = t.TextMuted,
            Location = new Point(15, y),
            Size = new Size(80, 16),
            TextAlign = ContentAlignment.MiddleLeft
        };
        txtCreditLimit = new TextBox
        {
            Location = new Point(15, y + 16),
            Size = new Size(140, 26),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = t.InputBg,
            ForeColor = t.InputFg,
            Font = new Font("Segoe UI", 10F),
            TextAlign = HorizontalAlignment.Right
        };
        var lblCredHint = new Label
        {
            Text = "(0 = unlimited)",
            Font = new Font("Segoe UI", 7F),
            ForeColor = ThemeManager.Current.TextMuted,
            Location = new Point(160, y + 19),
            Size = new Size(80, 16)
        };
        pnlRight.Controls.AddRange(new Control[] { lblCredLimit, txtCreditLimit, lblCredHint });
        y += 48;

        chkActive = new CheckBox
        {
            Text = "Active (allow orders)",
            Checked = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = t.InputFg,
            Location = new Point(15, y),
            Size = new Size(200, 20)
        };
        pnlRight.Controls.Add(chkActive);
        y += 28;

        // Buttons
        var btnNew = new Button
        {
            Text = "+ NEW",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(15, y),
            Size = new Size(95, 34),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = t.AccentBlue,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnNew.Click += (_, _) =>
        {
            ClearForm();
            txtName.Focus();
        };

        btnSave = new Button
        {
            Text = "\u2714 SAVE",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(115, y),
            Size = new Size(100, 34),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = t.AccentGreen,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnSave.Click += btnSave_Click;

        var btnCreditHistory = new Button
        {
            Text = "\uD83D\uDCCB CREDIT HISTORY",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Location = new Point(15, y + 40),
            Size = new Size(200, 34),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = t.AccentPurple,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnCreditHistory.Click += (_, _) =>
        {
            if (_selected == null)
            {
                ShowValidationAlert("Select a customer first.");
                return;
            }
            ShowCreditHistory(_selected);
        };

        var btnPurchaseHistory = new Button
        {
            Text = "\uD83D\uDED2 PURCHASE HISTORY",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Location = new Point(15, y + 80),
            Size = new Size(200, 34),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = t.AccentOrange,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnPurchaseHistory.Click += (_, _) =>
        {
            if (_selected == null)
            {
                ShowValidationAlert("Select a customer first.");
                return;
            }
            ShowPurchaseHistory(_selected);
        };

        var btnDelete = new Button
        {
            Text = "\u2716 DELETE",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(15, y + 160),
            Size = new Size(200, 34),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = t.AccentRed,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnDelete.Click += BtnDelete_Click;

        pnlRight.Controls.AddRange(new Control[] { lblFormTitle, btnNew, btnSave, btnCreditHistory, btnPurchaseHistory, btnDelete });

        pnlMain.Controls.AddRange(new Control[] { pnlLeft, pnlRight });
        Controls.Clear();
        Controls.AddRange(new Control[] { pnlMain, pnlMetrics, pnlToolbar });

        Shown += (_, _) => ResizeLayout(pnlLeft, pnlRight, dgvCustomers);
        Resize += (_, _) => ResizeLayout(pnlLeft, pnlRight, dgvCustomers);
    }

    private Label CreateMetricLabel(int x, Color color)
    {
        return new Label
        {
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = color,
            Location = new Point(x, 10),
            Size = new Size(160, 20),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false
        };
    }

    private void AddField(string label, ref TextBox box, ref int y, Panel parent, Color inputBg, Color inputFg, Color labelColor)
    {
        var lbl = new Label
        {
            Text = label,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = labelColor,
            Location = new Point(15, y),
            Size = new Size(80, 16),
            TextAlign = ContentAlignment.MiddleLeft
        };
        box = new TextBox
        {
            Location = new Point(15, y + 16),
            Size = new Size(310, 26),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = inputBg,
            ForeColor = inputFg,
            Font = new Font("Segoe UI", 10F)
        };
        parent.Controls.AddRange(new Control[] { lbl, box });
        y += 48;
    }

    private void ResizeLayout(Panel pnlLeft, Panel pnlRight, DataGridView dgv)
    {
        var margin = 10;
        var toolbarH = 50;
        var metricsH = 40;
        var availH = ClientSize.Height - toolbarH - metricsH - margin * 3;
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

    private void DgvCustomers_SelectionChanged(object? sender, EventArgs e)
    {
        if (dgvCustomers.CurrentRow?.DataBoundItem is Customer c)
        {
            _selected = c;
            txtName.Text = c.Name;
            txtPhone.Text = c.Phone;
            txtEmail.Text = c.Email;
            txtAddress.Text = c.Address;
            txtCreditLimit.Text = c.CreditLimit > 0 ? c.CreditLimit.ToString("N2") : "";
            chkActive.Checked = c.IsActive;
            lblFormTitle.Text = $"EDIT: {c.Name}";
            lblFormTitle.ForeColor = ThemeManager.Current.AccentGreen;
        }
    }

    private void DgvCustomers_DoubleClick(object? sender, EventArgs e)
    {
        if (dgvCustomers.CurrentRow?.DataBoundItem is not Customer c)
        {
            ShowValidationAlert("Select a customer first.");
            return;
        }
        ShowPurchaseHistory(c);
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (dgvCustomers.CurrentRow?.DataBoundItem is not Customer c)
        {
            ShowValidationAlert("Select a customer first.");
            return;
        }

        if (c.CreditBalance > 0)
        {
            MessageBox.Show($"Cannot delete '{c.Name}' — outstanding credit balance of \u20b1{c.CreditBalance:N2}. Settle the balance first.", "Delete Blocked", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            return;
        }
        if (c.LoyaltyPoints > 0)
        {
            MessageBox.Show($"Cannot delete '{c.Name}' — they have {c.LoyaltyPoints} loyalty points on record.", "Delete Blocked", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            return;
        }

        if (MessageBox.Show($"Delete customer '{c.Name}'? This cannot be undone.", "Confirm Delete — Step 1", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        if (MessageBox.Show($"Really delete '{c.Name}'? This is final.", "Confirm Delete — Step 2", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        CustomerService.Delete(c.Id);
        LoadCustomers(txtSearch.Text.Trim());
        ClearForm();
        ShowSuccessAlert($"Customer '{c.Name}' has been deleted.");
    }

    private void ShowCreditHistory(Customer customer)
    {
        var txns = CreditService.GetByCustomer(customer.Id);
        var totalPaid = txns.Where(t => t.Type == "Payment").Sum(t => t.Credit);
        var totalCharged = txns.Where(t => t.Type == "Sale").Sum(t => t.Debit);

        var t = ThemeManager.Current;

        using var form = new Form
        {
            Text = $"Credit History - {customer.Name}",
            Size = new Size(900, 550),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            BackColor = ThemeManager.Current.CanvasBg
        };

        var summaryLabel = new Label
        {
            Text = $"Current Balance: \u20b1{customer.CreditBalance:N2}  |  Total Charged: \u20b1{totalCharged:N2}  |  Total Paid: \u20b1{totalPaid:N2}  |  Transactions: {txns.Count}",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = t.AccentCyan,
            Location = new Point(15, 15),
            Size = new Size(850, 25)
        };

        var dgv = new DataGridView
        {
            Location = new Point(15, 50),
            Size = new Size(855, 430),
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            BackgroundColor = t.PanelBg,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9F),
            GridColor = t.BorderColor,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        dgv.AutoGenerateColumns = false;
        dgv.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.Current.DgvHeaderBg;
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = t.AccentCyan;
        dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgv.ColumnHeadersHeight = 30;
        dgv.EnableHeadersVisualStyles = false;
        dgv.DefaultCellStyle.SelectionBackColor = ThemeManager.Current.DgvSelection;
        dgv.DefaultCellStyle.SelectionForeColor = Color.White;
        dgv.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        dgv.RowTemplate.Height = 28;
        dgv.AlternatingRowsDefaultCellStyle.BackColor = ThemeManager.Current.DgvRowAlt;
        dgv.DefaultCellStyle.BackColor = ThemeManager.Current.DgvRowNormal;
        dgv.DefaultCellStyle.ForeColor = t.TextPrimary;

        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedAt", HeaderText = "DATE", Width = 130 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Type", HeaderText = "TYPE", Width = 75 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "InvoiceNo", HeaderText = "INVOICE", Width = 95 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Description", HeaderText = "DESCRIPTION", Width = 260 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PaymentMethod", HeaderText = "METHOD", Width = 85 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReferenceNo", HeaderText = "REF NO", Width = 95 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Debit", HeaderText = "DEBIT", Width = 85, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", ForeColor = ThemeManager.Current.AccentRed, Alignment = DataGridViewContentAlignment.MiddleRight } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Credit", HeaderText = "CREDIT", Width = 85, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", ForeColor = ThemeManager.Current.AccentGreen, Alignment = DataGridViewContentAlignment.MiddleRight } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Balance", HeaderText = "BALANCE", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = ThemeManager.Current.AccentCyan } });
        dgv.DataSource = txns.OrderBy(t => t.CreatedAt).ToList();

        var btnClose = new Button
        {
            Text = "CLOSE",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(15, 490),
            Size = new Size(100, 32),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = ThemeManager.Current.AccentBlue,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            DialogResult = DialogResult.OK
        };

        form.Controls.AddRange(new Control[] { summaryLabel, dgv, btnClose });
        form.ShowDialog();
    }

    private void ShowPurchaseHistory(Customer customer)
    {
        var sales = SaleService.GetSalesByCustomer(customer.Id);

        var t = ThemeManager.Current;

        using var form = new Form
        {
            Text = $"Purchase History - {customer.Name}",
            Size = new Size(900, 550),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            BackColor = ThemeManager.Current.CanvasBg
        };

        var summaryLabel = new Label
        {
            Text = $"Total Purchases: {sales.Count}  |  Total Spent: \u20b1{sales.Where(s => !s.IsVoided).Sum(s => s.GrandTotal):N2}",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = t.AccentCyan,
            Location = new Point(15, 15),
            Size = new Size(850, 25)
        };

        var dgv = new DataGridView
        {
            Location = new Point(15, 50),
            Size = new Size(855, 430),
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            BackgroundColor = t.PanelBg,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9F),
            GridColor = t.BorderColor,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        dgv.AutoGenerateColumns = false;
        dgv.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.Current.DgvHeaderBg;
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = t.AccentCyan;
        dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgv.ColumnHeadersHeight = 30;
        dgv.EnableHeadersVisualStyles = false;
        dgv.DefaultCellStyle.SelectionBackColor = ThemeManager.Current.DgvSelection;
        dgv.DefaultCellStyle.SelectionForeColor = Color.White;
        dgv.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        dgv.RowTemplate.Height = 28;
        dgv.AlternatingRowsDefaultCellStyle.BackColor = ThemeManager.Current.DgvRowAlt;
        dgv.DefaultCellStyle.BackColor = ThemeManager.Current.DgvRowNormal;
        dgv.DefaultCellStyle.ForeColor = t.TextPrimary;

        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "InvoiceNo", HeaderText = "INVOICE", Width = 110 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SaleDate", HeaderText = "DATE", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "GrandTotal", HeaderText = "TOTAL", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = ThemeManager.Current.AccentCyan, Font = new Font("Segoe UI", 9F, FontStyle.Bold) } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PaymentMethod", HeaderText = "METHOD", Width = 90 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "OrderType", HeaderText = "TYPE", Width = 80 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "STATUS", Width = 80 });
        dgv.DataSource = sales;

        dgv.CellFormatting += (s1, e1) =>
        {
            if (e1.RowIndex < 0 || dgv.Rows[e1.RowIndex].DataBoundItem is not Sale row) return;
            if (row.IsVoided)
                e1.CellStyle!.ForeColor = Color.Gray;
            if (e1.ColumnIndex == dgv.Columns["Status"]?.Index)
            {
                e1.Value = row.IsVoided ? "VOIDED" : "COMPLETED";
                e1.CellStyle!.ForeColor = row.IsVoided ? ThemeManager.Current.AccentRed : ThemeManager.Current.AccentGreen;
                e1.CellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            }
        };

        void ViewReceipt()
        {
            if (dgv.CurrentRow?.DataBoundItem is not Sale sale) return;
            var full = SaleService.GetById(sale.Id);
            if (full == null) return;

            using var itemForm = new Form
            {
                Text = $"Receipt {full.InvoiceNo} - Items",
                Size = new Size(700, 450),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                BackColor = ThemeManager.Current.CanvasBg
            };

            var itemGrid = new DataGridView
            {
                Location = new Point(15, 15),
                Size = new Size(655, 340),
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                BackgroundColor = t.PanelBg,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9F),
                GridColor = t.BorderColor,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            itemGrid.AutoGenerateColumns = false;
            itemGrid.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.Current.DgvHeaderBg;
            itemGrid.ColumnHeadersDefaultCellStyle.ForeColor = t.AccentCyan;
            itemGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemGrid.ColumnHeadersHeight = 30;
            itemGrid.EnableHeadersVisualStyles = false;
            itemGrid.DefaultCellStyle.SelectionBackColor = ThemeManager.Current.DgvSelection;
            itemGrid.DefaultCellStyle.SelectionForeColor = Color.White;
            itemGrid.RowTemplate.Height = 28;
            itemGrid.AlternatingRowsDefaultCellStyle.BackColor = ThemeManager.Current.DgvRowAlt;
            itemGrid.DefaultCellStyle.BackColor = ThemeManager.Current.DgvRowNormal;
            itemGrid.DefaultCellStyle.ForeColor = t.TextPrimary;

            itemGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProductName", HeaderText = "PRODUCT", Width = 250 });
            itemGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UnitName", HeaderText = "UNIT", Width = 60 });
            itemGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Quantity", HeaderText = "QTY", Width = 55, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            itemGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Price", HeaderText = "PRICE", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
            itemGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TotalPrice", HeaderText = "TOTAL", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = ThemeManager.Current.AccentCyan, Font = new Font("Segoe UI", 9F, FontStyle.Bold) } });
            itemGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "IsVoided", HeaderText = "VOIDED", Width = 60 });
            itemGrid.DataSource = full.Items;

            itemGrid.CellFormatting += (_, ev) =>
            {
                if (ev.RowIndex < 0 || itemGrid.Rows[ev.RowIndex].DataBoundItem is not SaleItem si) return;
                if (si.IsVoided) ev.CellStyle!.ForeColor = Color.Gray;
                if (ev.ColumnIndex == itemGrid.Columns["IsVoided"]?.Index)
                {
                    ev.Value = si.IsVoided ? "YES" : "NO";
                    ev.CellStyle!.ForeColor = si.IsVoided ? ThemeManager.Current.AccentRed : ThemeManager.Current.AccentGreen;
                    ev.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
            };

            var effectiveTotal = full.Items.Where(x => !x.IsVoided).Sum(x => x.TotalPrice);
            var totalLabel = new Label
            {
                Text = $"Grand Total: \u20b1{full.GrandTotal:N2}    |    Effective: \u20b1{effectiveTotal:N2}",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = t.AccentCyan,
                Location = new Point(15, 365),
                Size = new Size(655, 25),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            var btnItemClose = new Button
            {
                Text = "CLOSE",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(300, 395),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = ThemeManager.Current.AccentBlue,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom,
                DialogResult = DialogResult.OK
            };

            itemForm.Controls.AddRange(new Control[] { itemGrid, totalLabel, btnItemClose });
            itemForm.ShowDialog();
        }

        dgv.DoubleClick += (_, _) => ViewReceipt();

        var btnReceipt = new Button
        {
            Text = "VIEW RECEIPT",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(120, 15),
            Size = new Size(120, 28),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = ThemeManager.Current.AccentBlue,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnReceipt.Click += (_, _) => ViewReceipt();

        var btnClose = new Button
        {
            Text = "CLOSE",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(15, 490),
            Size = new Size(100, 32),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = ThemeManager.Current.AccentBlue,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            DialogResult = DialogResult.OK
        };

        form.Controls.AddRange(new Control[] { summaryLabel, dgv, btnReceipt, btnClose });
        form.ShowDialog();
    }

    public void ApplyTheme()
    {
        var t = ThemeManager.Current;
        BackColor = t.CanvasBg;
        ForeColor = t.TextPrimary;
    }

    private TextBox txtSearch = null!;
    private DataGridView dgvCustomers = null!;
    private TextBox txtName = null!, txtPhone = null!, txtEmail = null!, txtAddress = null!, txtCreditLimit = null!;
    private Button btnSave = null!;
    private Label lblFormTitle = null!;
    private Label lblMetricTotal = null!, lblMetricCredit = null!, lblMetricDebtors = null!, lblMetricPoints = null!;
    private CheckBox chkActive = null!;
}
