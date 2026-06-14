using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class CreditManagementForm : Form
{
    private enum ViewMode { TodayDebt, AllDebt, OverLimit, PaymentHistory }
    private ViewMode _viewMode = ViewMode.AllDebt;
    private Customer? _selectedCustomer;
    private List<Customer> _customers = new();
    private Dictionary<int, decimal> _todayBalances = new();
    private DateTime _dateFrom = TimeHelper.Today.AddDays(-30);
    private DateTime _dateTo = TimeHelper.Today;
    private string _paymentFilter = "All";

    private readonly User? _currentUser;

    public CreditManagementForm(User? user = null)
    {
        _currentUser = user;
        InitializeComponent();
        RefreshDashboard();
        RefreshCustomerList();
        DebugHelper.AddFormLabel(this);
    }

    private void RefreshDashboard()
    {
        var totalReceivables = CreditService.GetTotalReceivables();
        var aging = CreditService.GetAgingSummary();
        var custCount = CreditService.GetAllDebtCustomers().Count;

        lblTotalReceivables.Text = $"RECEIVABLES: \u20b1{totalReceivables:N2}";
        lblDebtorsCount.Text = $"DEBTORS: {custCount}";
        lblCurrent.Text = $"CURRENT: \u20b1{aging.Current:N2}";
        lblD30.Text = $"1-30D: \u20b1{aging.D30:N2}";
        lblD60.Text = $"31-60D: \u20b1{aging.D60:N2}";
        lblD90.Text = $"61-90D: \u20b1{aging.D90:N2}";
        lblD90Plus.Text = $"90+D: \u20b1{aging.D90Plus:N2}";

        lblCurrent.ForeColor = aging.Current > 0 ? Color.FromArgb(46, 204, 113) : Color.FromArgb(140, 140, 170);
        lblD30.ForeColor = aging.D30 > 0 ? Color.FromArgb(243, 156, 18) : Color.FromArgb(140, 140, 170);
        lblD60.ForeColor = aging.D60 > 0 ? Color.FromArgb(230, 126, 34) : Color.FromArgb(140, 140, 170);
        lblD90.ForeColor = aging.D90 > 0 ? Color.FromArgb(231, 76, 60) : Color.FromArgb(140, 140, 170);
        lblD90Plus.ForeColor = aging.D90Plus > 0 ? Color.FromArgb(192, 57, 43) : Color.FromArgb(140, 140, 170);
    }

    private void RefreshCustomerList()
    {
        lstCustomers.DataSource = null;
        lstCustomers.DisplayMember = "";
        _customers.Clear();
        _todayBalances.Clear();

        var keyword = txtSearchCustomer.Text.Trim();
        List<Customer> source = _viewMode switch
        {
            ViewMode.TodayDebt => CreditService.GetTodayDebtCustomers().Select(x => x.Customer).ToList(),
            ViewMode.AllDebt => string.IsNullOrEmpty(keyword) ? CreditService.GetAllDebtCustomers() : CreditService.SearchCreditCustomers(keyword),
            ViewMode.OverLimit => CreditService.GetCustomersWithCredit().Where(x => x.Customer.IsOverLimit).Select(x => x.Customer).ToList(),
            _ => CreditService.GetAllDebtCustomers()
        };

        if (_viewMode == ViewMode.TodayDebt)
        {
            var items = CreditService.GetTodayDebtCustomers();
            var displayList = new List<string>();
            foreach (var (c, bal) in items)
            {
                if (!string.IsNullOrEmpty(keyword) && !c.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) && !c.Phone.Contains(keyword)) continue;
                _customers.Add(c);
                _todayBalances[c.Id] = bal;
                var limitText = c.HasCreditLimit ? $" | Limit: \u20b1{c.CreditLimit:N2}" : "";
                displayList.Add($"{c.Name} | {c.DisplayPhone} (Today: \u20b1{bal:N2}){limitText}");
            }
            lstCustomers.DataSource = displayList;
        }
        else
        {
            _customers = source;
            lstCustomers.DataSource = _customers.Select(c =>
            {
                var limitText = c.HasCreditLimit ? $" | Limit: \u20b1{c.CreditLimit:N2}" : "";
                var overText = c.IsOverLimit ? " [OVER LIMIT]" : "";
                return $"{c.Name} | {c.DisplayPhone} (Credit: \u20b1{c.CreditBalance:N2}){limitText}{overText}";
            }).ToList();
        }

        _selectedCustomer = null;
        lblCustomerInfo.Text = "";
        dgvTrans.DataSource = null;
    }

    private void lstCustomers_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (lstCustomers.SelectedIndex < 0 || lstCustomers.SelectedIndex >= _customers.Count) return;
        var c = _customers[lstCustomers.SelectedIndex];
        _selectedCustomer = c;

        var todayText = _todayBalances.TryGetValue(c.Id, out var tb) ? $" | Today: \u20b1{tb:N2}" : "";
        var limitText = c.HasCreditLimit ? $" | Limit: \u20b1{c.CreditLimit:N2}" : "";
        var availText = c.HasCreditLimit ? $" | Available: \u20b1{c.AvailableCredit:N2}" : "";
        var overText = c.IsOverLimit ? " [OVER LIMIT!]" : "";
        lblCustomerInfo.Text = $"{c.Name} | Balance: \u20b1{c.CreditBalance:N2}{todayText}{limitText}{availText}{overText}";
        lblCustomerInfo.ForeColor = c.IsOverLimit ? Color.FromArgb(231, 76, 60) : Color.FromArgb(0, 245, 255);
        LoadTransactions(c.Id);
    }

    private void LoadTransactions(int customerId)
    {
        dgvTrans.AutoGenerateColumns = false;
        dgvTrans.Columns.Clear();
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedAt", HeaderText = "DATE", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm", ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Type", HeaderText = "TYPE", Width = 70, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(0, 245, 255) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "InvoiceNo", HeaderText = "INVOICE", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Description", HeaderText = "DESCRIPTION", Width = 180, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(230, 230, 245) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PaymentMethod", HeaderText = "METHOD", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReferenceNo", HeaderText = "REF NO", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Debit", HeaderText = "DEBIT", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.FromArgb(231, 76, 60) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Credit", HeaderText = "CREDIT", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.FromArgb(46, 204, 113) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Balance", HeaderText = "BALANCE", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(0, 245, 255) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AgingBucket", HeaderText = "AGING", Width = 80 });

        dgvTrans.DataSource = CreditService.GetByCustomer(customerId, _dateFrom, _dateTo);
        dgvTrans.RowHeadersVisible = false;
        dgvTrans.BackgroundColor = Color.FromArgb(20, 20, 40);
        dgvTrans.BorderStyle = BorderStyle.None;
        dgvTrans.GridColor = Color.FromArgb(40, 40, 70);
        dgvTrans.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 25, 50);
        dgvTrans.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(0, 245, 255);
        dgvTrans.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgvTrans.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvTrans.ColumnHeadersHeight = 30;
        dgvTrans.EnableHeadersVisualStyles = false;
        dgvTrans.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 40, 80);
        dgvTrans.DefaultCellStyle.SelectionForeColor = Color.White;
        dgvTrans.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        dgvTrans.RowTemplate.Height = 28;
        dgvTrans.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(15, 15, 32);
        dgvTrans.DefaultCellStyle.BackColor = Color.FromArgb(22, 22, 45);
        dgvTrans.DefaultCellStyle.ForeColor = Color.FromArgb(230, 230, 245);

        dgvTrans.CellFormatting += (_, args) =>
        {
            if (args.RowIndex < 0) return;
            if (dgvTrans.Rows[args.RowIndex].DataBoundItem is CreditTransaction t)
            {
                if (args.ColumnIndex == dgvTrans.Columns["AgingBucket"]?.Index)
                {
                    args.Value = t.AgingBucket;
                    if (t.AgingDays > 90) args.CellStyle!.ForeColor = Color.FromArgb(192, 57, 43);
                    else if (t.AgingDays > 60) args.CellStyle!.ForeColor = Color.FromArgb(231, 76, 60);
                    else if (t.AgingDays > 30) args.CellStyle!.ForeColor = Color.FromArgb(230, 126, 34);
                }
            }
        };
    }

    private void btnPayCredit_Click(object? sender, EventArgs e)
    {
        if (_selectedCustomer == null) return;

        var todayBalance = _todayBalances.TryGetValue(_selectedCustomer.Id, out var tb) ? tb : 0m;
        var defaultAmt = todayBalance > 0 ? todayBalance : _selectedCustomer.CreditBalance;

        using var payForm = new Form
        {
            Text = "Receive Payment",
            Size = new Size(420, 380),
            FormBorderStyle = FormBorderStyle.Sizable,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(10, 10, 26)
        };

        var pnlBg = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 10, 26) };
        var neonCyan = Color.FromArgb(0, 245, 255);
        var dimText = Color.FromArgb(140, 140, 170);
        var inputBg = Color.FromArgb(30, 30, 55);
        var inputFg = Color.FromArgb(230, 230, 245);
        var panelBg = Color.FromArgb(20, 20, 40);
        var borderColor = Color.FromArgb(40, 40, 70);

        var limitText = _selectedCustomer.HasCreditLimit ? $" | Limit: \u20b1{_selectedCustomer.CreditLimit:N2}" : "";
        var lblInfo = new Label
        {
            Text = $"{_selectedCustomer.Name} | Balance: \u20b1{_selectedCustomer.CreditBalance:N2} | Today: \u20b1{todayBalance:N2}{limitText}",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = neonCyan,
            Location = new Point(15, 12),
            Size = new Size(380, 22)
        };

        var lblCash = new Label { Text = "Cash:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, 45), Size = new Size(80, 20) };
        var txtCash = new TextBox { Font = new Font("Segoe UI", 12F), Location = new Point(100, 42), Size = new Size(160, 25), TextAlign = HorizontalAlignment.Right, BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg };

        var lblEw = new Label { Text = "E-Wallet:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, 77), Size = new Size(80, 20) };
        var txtEw = new TextBox { Font = new Font("Segoe UI", 12F), Location = new Point(100, 74), Size = new Size(160, 25), TextAlign = HorizontalAlignment.Right, BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg };

        var lblTotal = new Label
        {
            Text = "Total: \u20b10.00",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = neonCyan,
            Location = new Point(15, 107),
            Size = new Size(200, 25)
        };

        var lblEwRef = new Label { Text = "Ref No:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, 137), Size = new Size(80, 20) };
        var txtEwRef = new TextBox { Font = new Font("Segoe UI", 10F), Location = new Point(100, 135), Size = new Size(280, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg };

        var lblNotes = new Label { Text = "Notes:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, 167), Size = new Size(80, 20) };
        var txtNotes = new TextBox { Font = new Font("Segoe UI", 9F), Location = new Point(100, 165), Size = new Size(280, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg };

        void UpdateTotal()
        {
            decimal.TryParse(txtCash.Text, out var cash);
            decimal.TryParse(txtEw.Text, out var ew);
            lblTotal.Text = $"Total: \u20b1{cash + ew:N2}";
        }

        txtCash.TextChanged += (_, _) => UpdateTotal();
        txtEw.TextChanged += (_, _) => UpdateTotal();

        txtCash.Text = defaultAmt.ToString("N2");
        UpdateTotal();

        var btnOk = new Button
        {
            Text = "RECEIVE PAYMENT",
            Location = new Point(80, 210),
            Size = new Size(250, 40),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.FromArgb(46, 204, 113),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK
        };

        var btnCancel = new Button
        {
            Text = "CANCEL",
            Location = new Point(80, 255),
            Size = new Size(250, 35),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.FromArgb(149, 165, 166),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel
        };

        pnlBg.Controls.AddRange(new Control[] { lblInfo, lblCash, txtCash, lblEw, txtEw, lblTotal, lblEwRef, txtEwRef, lblNotes, txtNotes, btnOk, btnCancel });
        payForm.Controls.Add(pnlBg);
        payForm.AcceptButton = btnOk;
        payForm.CancelButton = btnCancel;

        if (payForm.ShowDialog() == DialogResult.OK)
        {
            if (!decimal.TryParse(txtCash.Text, out var cashAmt)) cashAmt = 0;
            if (!decimal.TryParse(txtEw.Text, out var ewAmt)) ewAmt = 0;
            var total = cashAmt + ewAmt;

            if (total <= 0) { MessageBox.Show("Enter a valid payment amount.", "Invalid", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (total > _selectedCustomer.CreditBalance) { MessageBox.Show($"Total exceeds balance of \u20b1{_selectedCustomer.CreditBalance:N2}.", "Overpayment", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            var refNo = txtEwRef.Text.Trim();
            var notes = txtNotes.Text.Trim();
            var custId = _selectedCustomer.Id;
            var userName = _currentUser != null && !string.IsNullOrEmpty(_currentUser.FullName)
                ? _currentUser.FullName : _currentUser?.Username ?? "";
            var userId = _currentUser?.Id ?? 0;
            if (cashAmt > 0)
            {
                var desc = $"Payment - Cash | {_selectedCustomer.Name}";
                if (!string.IsNullOrEmpty(notes)) desc += $" | {notes}";
                CreditService.AddTransaction(custId, null, "Payment", desc, cashAmt, "Cash", "", userId, userName);
            }
            if (ewAmt > 0)
            {
                var desc = $"Payment - E-Wallet | {_selectedCustomer.Name}";
                if (!string.IsNullOrEmpty(notes)) desc += $" | {notes}";
                CreditService.AddTransaction(custId, null, "Payment", desc, ewAmt, "E-Wallet", refNo, userId, userName);
            }

            _selectedCustomer = CustomerService.GetById(custId);
            var newBal = _selectedCustomer?.CreditBalance ?? 0m;
            LoadTransactions(custId);
            RefreshCustomerList();
            RefreshDashboard();
            MessageBox.Show($"Payment of \u20b1{total:N2} recorded.\nRemaining balance: \u20b1{newBal:N2}", "Payment Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void btnStatement_Click(object? sender, EventArgs e)
    {
        if (_selectedCustomer == null) return;
        var stmt = CreditService.GenerateStatement(_selectedCustomer.Id, _dateFrom, _dateTo);
        if (string.IsNullOrEmpty(stmt)) return;

        using var viewForm = new Form
        {
            Text = $"Statement of Account - {_selectedCustomer.Name}",
            Size = new Size(700, 500),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            BackColor = Color.FromArgb(10, 10, 26)
        };

        var txtStmt = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 10F),
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 40),
            ForeColor = Color.FromArgb(230, 230, 245),
            BorderStyle = BorderStyle.None,
            Text = stmt
        };

        var pnlBtn = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.FromArgb(10, 10, 26) };
        var btnPrint = new Button { Text = "PRINT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(180, 10), Size = new Size(100, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(72, 126, 176), ForeColor = Color.White, Cursor = Cursors.Hand };
        btnPrint.Click += (_, _) =>
        {
            var doc = new System.Drawing.Printing.PrintDocument();
            doc.PrintPage += (s, args) =>
            {
                var font = new Font("Consolas", 10F);
                var lines = stmt.Split('\n');
                if (args.Graphics == null) return;
                float y = args.MarginBounds.Top;
                foreach (var line in lines)
                {
                    args.Graphics.DrawString(line, font, Brushes.Black, args.MarginBounds.Left, y);
                    y += font.GetHeight();
                    if (y > args.MarginBounds.Bottom) break;
                }
            };
            using var dlg = new PrintDialog { Document = doc };
            if (dlg.ShowDialog() == DialogResult.OK) doc.Print();
        };
        var btnCopy = new Button { Text = "COPY", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(290, 10), Size = new Size(100, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(149, 165, 166), ForeColor = Color.White, Cursor = Cursors.Hand };
        btnCopy.Click += (_, _) => Clipboard.SetText(stmt);
        var btnClose = new Button { Text = "CLOSE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(400, 10), Size = new Size(100, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(231, 76, 60), ForeColor = Color.White, Cursor = Cursors.Hand, DialogResult = DialogResult.OK };
        pnlBtn.Controls.AddRange(new Control[] { btnPrint, btnCopy, btnClose });
        viewForm.Controls.AddRange(new Control[] { txtStmt, pnlBtn });
        viewForm.ShowDialog();
    }

    private void btnSetLimit_Click(object? sender, EventArgs e)
    {
        if (_selectedCustomer == null) return;

        using var limitForm = new Form
        {
            Text = "Set Credit Limit",
            Size = new Size(340, 200),
            FormBorderStyle = FormBorderStyle.Sizable,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(10, 10, 26)
        };

        var neonCyan = Color.FromArgb(0, 245, 255);
        var dimText = Color.FromArgb(140, 140, 170);
        var inputBg = Color.FromArgb(30, 30, 55);
        var inputFg = Color.FromArgb(230, 230, 245);

        var lbl = new Label { Text = $"Credit limit for {_selectedCustomer.Name}:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = neonCyan, Location = new Point(15, 15), Size = new Size(300, 20) };
        var txtLimit = new TextBox { Font = new Font("Segoe UI", 12F), Location = new Point(15, 42), Size = new Size(200, 25), TextAlign = HorizontalAlignment.Right, BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Text = _selectedCustomer.CreditLimit > 0 ? _selectedCustomer.CreditLimit.ToString("N2") : "" };
        var lblHint = new Label { Text = "(Leave blank or 0 for no limit)", Font = new Font("Segoe UI", 8F), ForeColor = dimText, Location = new Point(15, 72), Size = new Size(220, 18) };
        var btnOk = new Button { Text = "SAVE", Location = new Point(110, 100), Size = new Size(100, 30), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(72, 126, 176), ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand };
        limitForm.Controls.AddRange(new Control[] { lbl, txtLimit, lblHint, btnOk });
        limitForm.AcceptButton = btnOk;

        if (limitForm.ShowDialog() == DialogResult.OK)
        {
            decimal.TryParse(txtLimit.Text, out var limit);
            _selectedCustomer.CreditLimit = limit;
            CustomerService.Save(_selectedCustomer);
            RefreshCustomerList();
            RefreshDashboard();
            var msg = limit > 0 ? $"Credit limit set to \u20b1{limit:N2}" : "Credit limit removed";
            MessageBox.Show(msg, "Credit Limit", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ToggleView(ViewMode mode)
    {
        _viewMode = mode;
        var active = Color.FromArgb(72, 126, 176);
        var inactive = Color.FromArgb(30, 30, 55);
        btnTodayDebt.BackColor = mode == ViewMode.TodayDebt ? active : inactive;
        btnAllDebt.BackColor = mode == ViewMode.AllDebt ? active : inactive;
        btnOverLimit.BackColor = mode == ViewMode.OverLimit ? active : inactive;
        btnPaymentHistory.BackColor = mode == ViewMode.PaymentHistory ? active : inactive;

        if (mode == ViewMode.PaymentHistory) { ShowCustomerView(); LoadPaymentHistory(); }
        else { ShowCustomerView(); RefreshCustomerList(); }
    }

    private void ApplyDateFilter()
    {
        if (_viewMode == ViewMode.PaymentHistory) LoadPaymentHistory();
        else if (_selectedCustomer != null) LoadTransactions(_selectedCustomer.Id);
    }

    private void LoadPaymentHistory()
    {
        lstCustomers.Visible = false;
        lblCustLabel.Visible = false;
        btnPayCredit.Visible = false;
        btnStatement.Visible = false;
        btnSetLimit.Visible = false;
        lblCustomerInfo.Visible = false;
        cmbPaymentFilter.Visible = true;
        lblPaySummary.Visible = true;

        var payments = CreditService.GetAllPayments(_dateFrom, _dateTo, _paymentFilter == "All" ? null : _paymentFilter, txtSearchCustomer.Text.Trim());
        dgvTrans.AutoGenerateColumns = false;
        dgvTrans.Columns.Clear();
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedAt", HeaderText = "DATE/TIME", Width = 130, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm", ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CustomerName", HeaderText = "CUSTOMER", Width = 150, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(230, 230, 245) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Description", HeaderText = "DESCRIPTION", Width = 250, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PaymentMethod", HeaderText = "METHOD", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReferenceNo", HeaderText = "REF NO", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvTrans.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Credit", HeaderText = "AMOUNT", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(46, 204, 113) } });

        dgvTrans.DataSource = payments;
        dgvTrans.RowHeadersVisible = false;
        dgvTrans.BackgroundColor = Color.FromArgb(20, 20, 40);
        dgvTrans.BorderStyle = BorderStyle.None;
        dgvTrans.GridColor = Color.FromArgb(40, 40, 70);
        dgvTrans.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 25, 50);
        dgvTrans.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(0, 245, 255);
        dgvTrans.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgvTrans.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvTrans.ColumnHeadersHeight = 30;
        dgvTrans.EnableHeadersVisualStyles = false;
        dgvTrans.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 40, 80);
        dgvTrans.DefaultCellStyle.SelectionForeColor = Color.White;
        dgvTrans.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        dgvTrans.RowTemplate.Height = 28;
        dgvTrans.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(15, 15, 32);
        dgvTrans.DefaultCellStyle.BackColor = Color.FromArgb(22, 22, 45);
        dgvTrans.DefaultCellStyle.ForeColor = Color.FromArgb(230, 230, 245);

        var summary = CreditService.GetPaymentSummary(_dateFrom, _dateTo);
        lblPaySummary.Text = $"TOTAL: \u20b1{summary.TotalPayments:N2}  |  CASH: \u20b1{summary.TotalCash:N2}  |  E-WALLET: \u20b1{summary.TotalEWallet:N2}  |  OTHER: \u20b1{summary.TotalOther:N2}  |  RECORDS: {payments.Count}";
        lblPaySummary.ForeColor = Color.FromArgb(46, 204, 113);
    }

    private void ShowCustomerView()
    {
        lstCustomers.Visible = true;
        lblCustLabel.Visible = true;
        btnPayCredit.Visible = true;
        btnStatement.Visible = true;
        btnSetLimit.Visible = true;
        lblCustomerInfo.Visible = true;
        cmbPaymentFilter.Visible = false;
        lblPaySummary.Visible = false;
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
        var accentPurple = Color.FromArgb(155, 89, 182);

        BackColor = canvasBg;
        Text = "Credit Management";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        // ── TOP TOOLBAR ──
        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };

        var lblPageTitle = new Label { Text = "\uD83D\uDCB3 CREDIT MANAGEMENT", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(250, 28) };
        lblTotalReceivables = new Label { Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(600, 12), Size = new Size(300, 25), TextAlign = ContentAlignment.MiddleRight, AutoSize = false };
        lblDebtorsCount = new Label { Font = new Font("Segoe UI", 9F), ForeColor = dimText, Location = new Point(900, 12), Size = new Size(120, 25), TextAlign = ContentAlignment.MiddleRight, AutoSize = false };
        pnlToolbar.Controls.AddRange(new Control[] { lblPageTitle, lblTotalReceivables, lblDebtorsCount });

        // ── METRICS BAR (Aging) ──
        var pnlMetrics = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = canvasBg };
        lblCurrent = new Label { Font = new Font("Segoe UI", 8F, FontStyle.Bold), Location = new Point(15, 5), Size = new Size(130, 18), TextAlign = ContentAlignment.MiddleLeft };
        lblD30 = new Label { Font = new Font("Segoe UI", 8F, FontStyle.Bold), Location = new Point(155, 5), Size = new Size(130, 18), TextAlign = ContentAlignment.MiddleLeft };
        lblD60 = new Label { Font = new Font("Segoe UI", 8F, FontStyle.Bold), Location = new Point(295, 5), Size = new Size(130, 18), TextAlign = ContentAlignment.MiddleLeft };
        lblD90 = new Label { Font = new Font("Segoe UI", 8F, FontStyle.Bold), Location = new Point(435, 5), Size = new Size(130, 18), TextAlign = ContentAlignment.MiddleLeft };
        lblD90Plus = new Label { Font = new Font("Segoe UI", 8F, FontStyle.Bold), Location = new Point(575, 5), Size = new Size(130, 18), TextAlign = ContentAlignment.MiddleLeft };
        pnlMetrics.Controls.AddRange(new Control[] { lblCurrent, lblD30, lblD60, lblD90, lblD90Plus });

        // ── MAIN PANEL ──
        var pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = canvasBg };
        var margin = 10;

        // VIEW MODE BUTTONS
        var pnlViewMode = new Panel { Location = new Point(margin, margin), Size = new Size(500, 35), BackColor = panelBg };
        pnlViewMode.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlViewMode.Width - 1, pnlViewMode.Height - 1); };
        btnTodayDebt = new Button { Text = "TODAY", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(30, 30, 55), ForeColor = Color.White, Location = new Point(5, 3), Size = new Size(90, 28), Cursor = Cursors.Hand };
        btnTodayDebt.Click += (_, _) => ToggleView(ViewMode.TodayDebt);
        btnAllDebt = new Button { Text = "ALL DEBTORS", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentBlue, ForeColor = Color.White, Location = new Point(100, 3), Size = new Size(100, 28), Cursor = Cursors.Hand };
        btnAllDebt.Click += (_, _) => ToggleView(ViewMode.AllDebt);
        btnOverLimit = new Button { Text = "OVER LIMIT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(30, 30, 55), ForeColor = Color.White, Location = new Point(205, 3), Size = new Size(100, 28), Cursor = Cursors.Hand };
        btnOverLimit.Click += (_, _) => ToggleView(ViewMode.OverLimit);
        btnPaymentHistory = new Button { Text = "PAYMENTS", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = Color.FromArgb(30, 30, 55), ForeColor = Color.White, Location = new Point(310, 3), Size = new Size(100, 28), Cursor = Cursors.Hand };
        btnPaymentHistory.Click += (_, _) => ToggleView(ViewMode.PaymentHistory);
        pnlViewMode.Controls.AddRange(new Control[] { btnTodayDebt, btnAllDebt, btnOverLimit, btnPaymentHistory });

        // SEARCH & DATE FILTER
        txtSearchCustomer = new TextBox { Font = new Font("Segoe UI", 9F), Location = new Point(margin, 48), Size = new Size(180, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg };
        txtSearchCustomer.TextChanged += (_, _) => { if (_viewMode == ViewMode.PaymentHistory) LoadPaymentHistory(); else RefreshCustomerList(); };

        lblDateFrom = new Label { Text = "From:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(195, 50), Size = new Size(35, 20) };
        dtpFrom = new DateTimePicker { Location = new Point(233, 48), Size = new Size(110, 25), Format = DateTimePickerFormat.Short, Value = _dateFrom, BackColor = inputBg, ForeColor = inputFg };
        dtpFrom.ValueChanged += (_, _) => { _dateFrom = dtpFrom.Value.Date; ApplyDateFilter(); };

        lblDateTo = new Label { Text = "To:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(353, 50), Size = new Size(25, 20) };
        dtpTo = new DateTimePicker { Location = new Point(381, 48), Size = new Size(110, 25), Format = DateTimePickerFormat.Short, Value = _dateTo, BackColor = inputBg, ForeColor = inputFg };
        dtpTo.ValueChanged += (_, _) => { _dateTo = dtpTo.Value.Date; ApplyDateFilter(); };

        cmbPaymentFilter = new ComboBox { Font = new Font("Segoe UI", 9F), Location = new Point(501, 48), Size = new Size(100, 25), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = inputBg, ForeColor = inputFg, FlatStyle = FlatStyle.Flat, Visible = false };
        cmbPaymentFilter.Items.AddRange(new object[] { "All", "Cash", "E-Wallet" });
        cmbPaymentFilter.SelectedIndex = 0;
        cmbPaymentFilter.SelectedIndexChanged += (_, _) => { _paymentFilter = cmbPaymentFilter.Text; LoadPaymentHistory(); };

        // LEFT PANEL - Customer List
        var pnlLeft = new Panel { Location = new Point(margin, 85), Size = new Size(300, 400), BackColor = panelBg };
        pnlLeft.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlLeft.Width - 1, pnlLeft.Height - 1); };
        lblCustLabel = new Label { Text = "CUSTOMERS", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(12, 8), Size = new Size(200, 18) };
        lstCustomers = new ListBox { Font = new Font("Segoe UI", 9F), Location = new Point(8, 30), Size = new Size(284, 200), BorderStyle = BorderStyle.None, BackColor = panelBg, ForeColor = inputFg };
        lstCustomers.SelectedIndexChanged += lstCustomers_SelectedIndexChanged;

        btnPayCredit = new Button { Text = "\uD83D\uDCB5 RECEIVE PAYMENT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentGreen, ForeColor = Color.White, Location = new Point(8, 245), Size = new Size(284, 35), Cursor = Cursors.Hand };
        btnPayCredit.Click += btnPayCredit_Click;
        btnStatement = new Button { Text = "\uD83D\uDCC4 STATEMENT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentBlue, ForeColor = Color.White, Location = new Point(8, 285), Size = new Size(284, 35), Cursor = Cursors.Hand };
        btnStatement.Click += btnStatement_Click;
        btnSetLimit = new Button { Text = "\u2699\uFE0F SET CREDIT LIMIT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentPurple, ForeColor = Color.White, Location = new Point(8, 325), Size = new Size(284, 35), Cursor = Cursors.Hand };
        btnSetLimit.Click += btnSetLimit_Click;
        pnlLeft.Controls.AddRange(new Control[] { lblCustLabel, lstCustomers, btnPayCredit, btnStatement, btnSetLimit });

        // RIGHT PANEL - Transactions
        var pnlRight = new Panel { Location = new Point(320, 85), Size = new Size(500, 400), BackColor = panelBg };
        pnlRight.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlRight.Width - 1, pnlRight.Height - 1); };
        lblCustomerInfo = new Label { Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(12, 8), Size = new Size(476, 22), TextAlign = ContentAlignment.MiddleLeft, AutoSize = false };
        lblPaySummary = new Label { Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = accentGreen, Location = new Point(12, 8), Size = new Size(476, 22), TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, Visible = false };
        dgvTrans = new DataGridView { Location = new Point(8, 34), Size = new Size(484, 358), SelectionMode = DataGridViewSelectionMode.FullRowSelect, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, MultiSelect = false, Font = new Font("Segoe UI", 9F), CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal };
        pnlRight.Controls.AddRange(new Control[] { lblCustomerInfo, lblPaySummary, dgvTrans });

        pnlMain.Controls.AddRange(new Control[] { pnlViewMode, txtSearchCustomer, lblDateFrom, dtpFrom, lblDateTo, dtpTo, cmbPaymentFilter, pnlLeft, pnlRight });
        Controls.AddRange(new Control[] { pnlToolbar, pnlMetrics, pnlMain });

        Resize += (_, _) => ResizeLayout(pnlLeft, pnlRight, dgvTrans, lstCustomers, pnlViewMode, txtSearchCustomer, lblDateFrom, dtpFrom, lblDateTo, dtpTo, cmbPaymentFilter);
    }

    private void ResizeLayout(Panel pnlLeft, Panel pnlRight, DataGridView dgv, ListBox lst, Panel pnlViewMode, TextBox txtSearch, Label lblDF, DateTimePicker dtpF, Label lblDT, DateTimePicker dtpT, ComboBox cmbPF)
    {
        var margin = 10;
        var toolbarH = 50;
        var metricsH = 30;
        var viewModeH = 35;
        var searchH = 35;
        var topOffset = toolbarH + metricsH + margin * 4 + viewModeH + searchH;
        var availH = ClientSize.Height - toolbarH - metricsH - margin * 5 - viewModeH - searchH;
        var availW = ClientSize.Width - margin * 3;
        var leftW = 300;
        var rightW = availW - leftW;

        pnlViewMode.Location = new Point(margin, toolbarH + metricsH + margin * 2);
        pnlViewMode.Size = new Size(availW, viewModeH);

        txtSearch.Location = new Point(margin, toolbarH + metricsH + margin * 3 + viewModeH);
        lblDF.Location = new Point(195, toolbarH + metricsH + margin * 3 + viewModeH + 2);
        dtpF.Location = new Point(233, toolbarH + metricsH + margin * 3 + viewModeH);
        lblDT.Location = new Point(353, toolbarH + metricsH + margin * 3 + viewModeH + 2);
        dtpT.Location = new Point(381, toolbarH + metricsH + margin * 3 + viewModeH);
        cmbPF.Location = new Point(501, toolbarH + metricsH + margin * 3 + viewModeH);

        pnlLeft.Location = new Point(margin, topOffset);
        pnlLeft.Size = new Size(leftW, availH);
        pnlRight.Location = new Point(leftW + margin * 2, topOffset);
        pnlRight.Size = new Size(rightW, availH);

        lst.Location = new Point(8, 30);
        lst.Size = new Size(leftW - 16, availH - 150);
        
        btnPayCredit.Location = new Point(8, lst.Bottom + 8);
        btnPayCredit.Size = new Size(leftW - 16, 35);
        btnStatement.Location = new Point(8, btnPayCredit.Bottom + 5);
        btnStatement.Size = new Size(leftW - 16, 35);
        btnSetLimit.Location = new Point(8, btnStatement.Bottom + 5);
        btnSetLimit.Size = new Size(leftW - 16, 35);

        dgv.Location = new Point(8, 34);
        dgv.Size = new Size(rightW - 16, availH - 42);
    }

    private ListBox lstCustomers = null!;
    private Button btnPayCredit = null!, btnStatement = null!, btnSetLimit = null!;
    private Button btnTodayDebt = null!, btnAllDebt = null!, btnOverLimit = null!, btnPaymentHistory = null!;
    private Label lblCustomerInfo = null!, lblTotalReceivables = null!, lblDebtorsCount = null!;
    private Label lblCurrent = null!, lblD30 = null!, lblD60 = null!, lblD90 = null!, lblD90Plus = null!;
    private Label lblPaySummary = null!, lblCustLabel = null!;
    private DataGridView dgvTrans = null!;
    private DateTimePicker dtpFrom = null!, dtpTo = null!;
    private Label lblDateFrom = null!, lblDateTo = null!;
    private TextBox txtSearchCustomer = null!;
    private ComboBox cmbPaymentFilter = null!;
}
