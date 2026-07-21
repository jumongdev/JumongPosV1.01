using JumongPosV1._01.Helpers;
using JumongPosV1._01.Data;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public class EndShiftForm : Form
{
    private readonly User _currentUser;
    private decimal _totalSales, _totalCash, _totalEWallet, _totalCredit, _totalVoided, _creditPayCash, _creditPayEWallet, _totalExpenses, _totalCostSold, _totalStockReceivedCost;
    private decimal _openingBalance;
    private bool _denominationsEntered = false;
    private Label lblTotal1000 = null!, lblTotal500 = null!, lblTotal200 = null!, lblTotal100 = null!, lblTotal50 = null!, lblTotal20 = null!, lblTotalCoins = null!;

    public EndShiftForm(User user)
    {
        _currentUser = user;
        InitializeComponent();
        LoadTotals();
        Recalc();

        _openingBalance = ShiftSessionService.GetOpeningBalance();
        lblOpeningCash.Text = _openingBalance.ToString("N2");

        var cashierName = string.IsNullOrEmpty(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName;
        AuditLogService.Log("EndShiftAccessed", "EndShift", "", $"Cashier {cashierName} opened End Shift form", cashierName);
        DebugHelper.AddFormLabel(this);
    }

    private void LoadTotals()
    {
        (_totalSales, _totalCash, _totalEWallet, _totalCredit, _totalVoided, _creditPayCash, _creditPayEWallet, _totalExpenses, _totalCostSold, _totalStockReceivedCost) = DailyCloseService.GetShiftTotals();
        lblDate.Text = TimeHelper.Now.ToString("MMMM dd, yyyy  hh:mm tt");
        var cashierName = string.IsNullOrEmpty(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName;
        lblCashierName.Text = cashierName;
        lblTotalExpenses.Text = _totalExpenses.ToString("N2");
    }

    private void Recalc()
    {
        var t1000 = (int)num1000.Value * 1000m;
        var t500 = (int)num500.Value * 500m;
        var t200 = (int)num200.Value * 200m;
        var t100 = (int)num100.Value * 100m;
        var t50 = (int)num50.Value * 50m;
        var t20 = (int)num20.Value * 20m;
        var coins = txtCoins.Value;
        lblTotal1000.Text = "= ₱" + t1000.ToString("N2");
        lblTotal500.Text = "= ₱" + t500.ToString("N2");
        lblTotal200.Text = "= ₱" + t200.ToString("N2");
        lblTotal100.Text = "= ₱" + t100.ToString("N2");
        lblTotal50.Text = "= ₱" + t50.ToString("N2");
        lblTotal20.Text = "= ₱" + t20.ToString("N2");
        lblTotalCoins.Text = "= ₱" + coins.ToString("N2");
        var cashOnHand = t1000 + t500 + t200 + t100 + t50 + t20 + coins;
        lblCashOnHand.Text = cashOnHand.ToString("N2");
    }

    private void Denom_ValueChanged(object? sender, EventArgs e)
    {
        Recalc();
        _denominationsEntered = true;
    }

    private void btnClose_Click(object? sender, EventArgs e)
{
    var cashOnHand = (int)num1000.Value * 1000m + (int)num500.Value * 500m + (int)num200.Value * 200m + (int)num100.Value * 100m + (int)num50.Value * 50m + (int)num20.Value * 20m + txtCoins.Value;
    
    if (!_denominationsEntered)
    {
        MessageBox.Show("Please enter all cash denomination counts before ending the shift.\n\nCount every bill and coin in the drawer.", "Cash Count Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
    }

    if (cashOnHand == 0m && _totalCash + _creditPayCash > 0m)
    {
        MessageBox.Show("Cash on hand cannot be zero when there were cash transactions this shift.\n\nPlease recount the drawer.", "Cash Count Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
    }

    var cashierName = string.IsNullOrEmpty(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName;
    var confirmMsg = $@"End Shift Confirmation — {cashierName}
Are you sure you want to finalize your shift count? You cannot alter this submission afterwards.";

    if (MessageBox.Show(confirmMsg, "Confirm End Shift", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

    LoadTotals();

    var expenses = ExpenseService.GetExpensesForCurrentShift();
    var gcashTxns = DailyCloseService.GetGcashTransactionsSinceLastClose();
    var creditCustomers = DailyCloseService.GetCreditCustomersSinceLastClose();
    var creditPayments = DailyCloseService.GetCreditPaymentsSinceLastClose();

    var diff = cashOnHand + _totalExpenses - _totalCash - _creditPayCash - _openingBalance;
    var now = TimeHelper.Now;

    // Compute total inventory cost (SUM of stock_qty * cost for all products)
    var totalInvCost = 0m;
    try
    {
        using var invConn = DatabaseHelper.GetConnection();
        invConn.Open();
        using var invCmd = new System.Data.SQLite.SQLiteCommand("SELECT COALESCE(SUM(StockQty * Cost), 0) FROM Products", invConn);
        totalInvCost = Convert.ToDecimal(invCmd.ExecuteScalar());
    }
    catch { }

    var dc = new DailyClose 
    { 
        CloseDate = now.ToString("yyyy-MM-dd HH:mm:ss"), 
        CreatedAt = now.ToString("yyyy-MM-dd HH:mm:ss"),
        TotalSales = _totalSales, 
        TotalCash = _totalCash, 
        TotalEWallet = _totalEWallet, 
        TotalCredit = _totalCredit, 
        TotalVoided = _totalVoided, 
        TotalExpenses = _totalExpenses,
        TotalInventoryCost = totalInvCost,
        TotalCostSold = _totalCostSold,
        TotalStockReceivedCost = _totalStockReceivedCost,
        OpeningCash = _openingBalance,
        UserId = _currentUser.Id, 
        UserName = cashierName, 
        Denom1000 = (int)num1000.Value, 
        Denom500 = (int)num500.Value, 
        Denom200 = (int)num200.Value, 
        Denom100 = (int)num100.Value, 
        Denom50 = (int)num50.Value,
        Denom20 = (int)num20.Value,
        DenomCoins = txtCoins.Value,
        CashOnHand = cashOnHand, 
        Difference = diff, 
        Notes = txtNotes.Text.Trim() 
    };

    var error = DailyCloseService.SaveClose(dc);
    if (error != null) 
    { 
        MessageBox.Show($"Error saving: {error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); 
        return; 
    }
    
    ShiftSessionService.EndSession();

    var printErrorMsg = "";
    var prevInv = DailyCloseService.GetLastInventoryCost();
    try
    {
        PrinterService.PrintAuditEndShiftReport(cashOnHand, diff, cashierName, now, txtNotes.Text.Trim(), _totalSales, _totalCash, _totalEWallet, _totalCredit, _totalVoided, expenses, gcashTxns, creditCustomers, creditPayments,
            (int)num1000.Value, (int)num500.Value, (int)num200.Value, (int)num100.Value, (int)num50.Value, (int)num20.Value, txtCoins.Value,
            totalInvCost, _totalCostSold, _totalStockReceivedCost, prevInv);
    }
    catch (Exception printEx)
    {
        printErrorMsg = $"\n\nPrint error: {printEx.Message}";
    }

    var emailErrorMsg = "";
    try
    {
        var emailSvc = new EmailService();
        if (emailSvc.IsConfigured)
        {
            var ee = emailSvc.SendEndShiftReport(_totalSales, _totalCash, _totalEWallet, _totalCredit, _totalVoided, cashOnHand, diff, cashierName, _totalExpenses, expenses, gcashTxns, creditCustomers, creditPayments,
                (int)num1000.Value, (int)num500.Value, (int)num200.Value, (int)num100.Value, (int)num50.Value, (int)num20.Value, txtCoins.Value,
                totalInvCost, _totalCostSold, _totalStockReceivedCost, prevInv);
            if (ee != null) emailErrorMsg = $"\n\nEmail error: {ee}";
        }
    }
    catch (Exception emailEx)
    {
        emailErrorMsg = $"\n\nEmail error: {emailEx.Message}";
    }

    var finalMsg = "Shift ended successfully. Your count has been recorded." + printErrorMsg + emailErrorMsg;
    MessageBox.Show(finalMsg, "Shift Complete", MessageBoxButtons.OK, emailErrorMsg != "" ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

    num1000.Value = num500.Value = num200.Value = num100.Value = num50.Value = num20.Value = 0;
    txtCoins.Value = 0;
    txtNotes.Clear();
    _denominationsEntered = false;
    LoadTotals();
    Recalc();
}

    private void btnPrintReport_Click(object? sender, EventArgs e)
{
    var cashierName = string.IsNullOrEmpty(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName;
    var cashOnHand = (int)num1000.Value * 1000m + (int)num500.Value * 500m + (int)num200.Value * 200m + (int)num100.Value * 100m + (int)num50.Value * 50m + (int)num20.Value * 20m + txtCoins.Value;
    
    LoadTotals();
    
    var diff = cashOnHand + _totalExpenses - _totalCash - _creditPayCash - _openingBalance;
    
    var expenses = ExpenseService.GetExpensesForCurrentShift();
    var gcashTxns = DailyCloseService.GetGcashTransactionsSinceLastClose();
    var creditCustomers = DailyCloseService.GetCreditCustomersSinceLastClose();
    var creditPayments = DailyCloseService.GetCreditPaymentsSinceLastClose();
    var totalInvCost = 0m;
    try { using var invConn = DatabaseHelper.GetConnection(); invConn.Open(); using var invCmd = new System.Data.SQLite.SQLiteCommand("SELECT COALESCE(SUM(StockQty * Cost), 0) FROM Products", invConn); totalInvCost = Convert.ToDecimal(invCmd.ExecuteScalar()); } catch { }
        PrinterService.PrintAuditEndShiftReport(cashOnHand, diff, cashierName, TimeHelper.Now, txtNotes.Text.Trim(), _totalSales, _totalCash, _totalEWallet, _totalCredit, _totalVoided, expenses, gcashTxns, creditCustomers, creditPayments,
            (int)num1000.Value, (int)num500.Value, (int)num200.Value, (int)num100.Value, (int)num50.Value, (int)num20.Value, txtCoins.Value,
            totalInvCost, _totalCostSold, _totalStockReceivedCost, DailyCloseService.GetLastInventoryCost());
}

private void btnEmail_Click(object? sender, EventArgs e)
{
    var cashierName = string.IsNullOrEmpty(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName;
    var cashOnHand = (int)num1000.Value * 1000m + (int)num500.Value * 500m + (int)num200.Value * 200m + (int)num100.Value * 100m + (int)num50.Value * 50m + (int)num20.Value * 20m + txtCoins.Value;
    
    LoadTotals();
    
    var diff = cashOnHand + _totalExpenses - _totalCash - _creditPayCash - _openingBalance;
    
    var expenses = ExpenseService.GetExpensesForCurrentShift();
    var gcashTxns = DailyCloseService.GetGcashTransactionsSinceLastClose();
    var creditCustomers = DailyCloseService.GetCreditCustomersSinceLastClose();
    var creditPayments = DailyCloseService.GetCreditPaymentsSinceLastClose();
    var totalInvCost = 0m;
    try { using var invConn = DatabaseHelper.GetConnection(); invConn.Open(); using var invCmd = new System.Data.SQLite.SQLiteCommand("SELECT COALESCE(SUM(StockQty * Cost), 0) FROM Products", invConn); totalInvCost = Convert.ToDecimal(invCmd.ExecuteScalar()); } catch { }

    var emailSvc = new EmailService();
    if (!emailSvc.IsConfigured) 
    { 
        MessageBox.Show("Email not configured. Go to Settings to set SMTP details.", "Email Not Configured", MessageBoxButtons.OK, MessageBoxIcon.Warning); 
        return; 
    }
    
        var error = emailSvc.SendEndShiftReport(_totalSales, _totalCash, _totalEWallet, _totalCredit, _totalVoided, cashOnHand, diff, cashierName, _totalExpenses, expenses, gcashTxns, creditCustomers, creditPayments,
            (int)num1000.Value, (int)num500.Value, (int)num200.Value, (int)num100.Value, (int)num50.Value, (int)num20.Value, txtCoins.Value,
            totalInvCost, _totalCostSold, _totalStockReceivedCost, DailyCloseService.GetLastInventoryCost());
    if (error != null) 
    {
        MessageBox.Show($"Email error: {error}", "Email Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    else 
    {
        MessageBox.Show("End shift report sent successfully.", "Email Sent", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

    private void btnHistory_Click(object? sender, EventArgs e)
    {
        var t2 = ThemeManager.Current;
        var canvasBg = t2.CanvasBg;
        var panelBg = t2.PanelBg;
        var neonTitle = t2.AccentCyan;
        var borderColor = t2.BorderColor;
        var accentBlue = t2.AccentBlue;
        var isAdmin = _currentUser.Role == "Admin";

        using var form = new Form { Text = "Shift History", WindowState = FormWindowState.Maximized, StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.Sizable, MaximizeBox = true, BackColor = canvasBg };
        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };
        var lblTitle = new Label { Text = "\uD83D\uDCCB SHIFT HISTORY", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(250, 28) };
        pnlToolbar.Controls.Add(lblTitle);

        var dgv = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = panelBg, BorderStyle = BorderStyle.None, GridColor = t2.DgvGrid, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, Font = new Font("Segoe UI", 9F), ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = t2.DgvHeaderBg, ForeColor = neonTitle, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, ColumnHeadersHeight = 30, EnableHeadersVisualStyles = false, DefaultCellStyle = new DataGridViewCellStyle { BackColor = t2.DgvRowNormal, ForeColor = t2.TextPrimary, SelectionBackColor = t2.DgvSelection, SelectionForeColor = Color.White }, RowTemplate = { Height = 28 }, AlternatingRowsDefaultCellStyle = { BackColor = t2.DgvRowAlt } };
        dgv.DataSource = DailyCloseService.GetHistory();

        var financialColumns = new[] { "TotalSales", "TotalCash", "TotalEWallet", "TotalCredit", "TotalVoided", "Difference", "Denom1000", "Denom500", "Denom200", "Denom100", "Denom50", "Denom20", "DenomCoins", "CashOnHand" };
        foreach (DataGridViewColumn col in dgv.Columns)
        {
            if (financialColumns.Contains(col.DataPropertyName) && !isAdmin)
                col.Visible = false;
        }

        var btnReprint = new Button { Text = "REPRINT SELECTED", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(20, 10), Size = new Size(140, 30), BackColor = accentBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnReprint.Click += (_, __) =>
        {
            if (dgv.CurrentRow?.DataBoundItem is not DailyClose dc) { MessageBox.Show("Select a shift to reprint.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            var since = DailyCloseService.GetPreviousCloseTime(dc.Id);
            var closeDate = DateTime.Parse(dc.CloseDate);
            var gcashTxns = DailyCloseService.GetGcashTransactionsBetween(since, closeDate);
            var creditCustomers = DailyCloseService.GetCreditCustomersBetween(since, closeDate);
            var creditPayments = DailyCloseService.GetCreditPaymentsBetween(since, closeDate);
            var expenses = ExpenseService.GetExpensesBetween(since, closeDate);
            var cashOnHand = dc.Denom1000 * 1000m + dc.Denom500 * 500m + dc.Denom200 * 200m + dc.Denom100 * 100m + dc.Denom50 * 50m + dc.Denom20 * 20m + dc.DenomCoins;
            var prevInv2 = since != null ? DailyCloseService.GetLastInventoryCost() : 0m;
            PrinterService.PrintAuditEndShiftReport(cashOnHand, dc.Difference, dc.UserName, closeDate, dc.Notes, dc.TotalSales, dc.TotalCash, dc.TotalEWallet, dc.TotalCredit, dc.TotalVoided, expenses, gcashTxns, creditCustomers, creditPayments,
                dc.Denom1000, dc.Denom500, dc.Denom200, dc.Denom100, dc.Denom50, dc.Denom20, dc.DenomCoins,
                dc.TotalInventoryCost, dc.TotalCostSold, dc.TotalStockReceivedCost, prevInv2);
        };
        var btnTrends = new Button { Text = "\uD83D\uDCCA TRENDS", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(170, 10), Size = new Size(100, 30), BackColor = t2.AccentGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnTrends.Click += (_, __) =>
        {
            var data = DailyCloseService.GetShiftComparison();
            using var tf = new Form { Text = "Shift Comparison — OVER/SHORT Trends", WindowState = FormWindowState.Maximized, StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.Sizable, BackColor = canvasBg };
            var tt = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
            tt.Paint += (s, ev) => { using var pen = new Pen(borderColor, 1); ev.Graphics.DrawLine(pen, 0, tt.Height - 1, tt.Width, tt.Height - 1); };
            var tl = new Label { Text = "\uD83D\uDCCA SHIFT COMPARISON — DAILY TRENDS (60 days)", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(500, 28) };
            tt.Controls.Add(tl);
            var td = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = panelBg, BorderStyle = BorderStyle.None, GridColor = t2.DgvGrid, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Font = new Font("Segoe UI", 9F), ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = t2.DgvHeaderBg, ForeColor = neonTitle, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, ColumnHeadersHeight = 30, EnableHeadersVisualStyles = false, DefaultCellStyle = new DataGridViewCellStyle { BackColor = t2.DgvRowNormal, ForeColor = t2.TextPrimary, SelectionBackColor = t2.DgvSelection, SelectionForeColor = Color.White }, RowTemplate = { Height = 28 }, AlternatingRowsDefaultCellStyle = { BackColor = t2.DgvRowAlt } };
            td.AutoGenerateColumns = false;
            td.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Date", HeaderText = "DATE", Width = 120 });
            td.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ShiftCount", HeaderText = "SHIFTS", Width = 80 });
            td.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TotalSales", HeaderText = "TOTAL SALES", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", ForeColor = t2.TextPrimary } });
            td.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TotalExpenses", HeaderText = "EXPENSES", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", ForeColor = t2.AccentOrange } });
            td.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AvgVariance", HeaderText = "AVG OVER/SHORT", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Format = "+0.00;-0.00;0", Font = new Font("Segoe UI", 10F, FontStyle.Bold) } });
            td.CellFormatting += (s, ev) => { if (ev.ColumnIndex == 4 && ev.Value is decimal d && ev.CellStyle != null) { ev.CellStyle.ForeColor = d >= 0 ? t2.AccentGreen : t2.AccentRed; ev.Value = (d >= 0 ? "+" : "") + d.ToString("N2"); } };
            td.DataSource = data.Select(x => new { Date = x.Date, ShiftCount = x.ShiftCount, TotalSales = x.TotalSales, TotalExpenses = x.TotalExpenses, AvgVariance = x.AvgVariance }).ToList();
            var tc = new Button { Text = "CLOSE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(20, 10), Size = new Size(100, 30), BackColor = accentBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            tc.Click += (_, __) => tf.Close();
            var tp = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = canvasBg };
            tp.Controls.Add(tc);
            tf.Controls.AddRange(new Control[] { td, tp, tt });
            tf.ShowDialog();
        };
        var btnVariance = new Button { Text = "\uD83D\uDCCB DAILY O/S", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(280, 10), Size = new Size(110, 30), BackColor = t2.AccentOrange, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnVariance.Click += (_, __) =>
        {
            var data = DailyCloseService.GetDailyVariance();
            using var vf = new Form { Text = "Daily Over/Short Tracker", WindowState = FormWindowState.Maximized, StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.Sizable, BackColor = canvasBg };
            var vt = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
            vt.Paint += (s, ev) => { using var pen = new Pen(borderColor, 1); ev.Graphics.DrawLine(pen, 0, vt.Height - 1, vt.Width, vt.Height - 1); };
            var vl = new Label { Text = "\uD83D\uDCCB DAILY OVER/SHORT TRACKER (90 days)", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(500, 28) };
            vt.Controls.Add(vl);
            var vd = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = panelBg, BorderStyle = BorderStyle.None, GridColor = t2.DgvGrid, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Font = new Font("Segoe UI", 9F), ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = t2.DgvHeaderBg, ForeColor = neonTitle, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, ColumnHeadersHeight = 30, EnableHeadersVisualStyles = false, DefaultCellStyle = new DataGridViewCellStyle { BackColor = t2.DgvRowNormal, ForeColor = t2.TextPrimary, SelectionBackColor = t2.DgvSelection, SelectionForeColor = Color.White }, RowTemplate = { Height = 28 }, AlternatingRowsDefaultCellStyle = { BackColor = t2.DgvRowAlt } };
            vd.AutoGenerateColumns = false;
            vd.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Date", HeaderText = "DATE", Width = 100 });
            vd.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Shifts", HeaderText = "SFT", Width = 50 });
            vd.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Cashiers", HeaderText = "CASHIER(S)", Width = 140 });
            vd.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TotalSales", HeaderText = "SALES", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", ForeColor = t2.TextPrimary } });
            vd.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TotalCash", HeaderText = "CASH", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            vd.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TotalEWallet", HeaderText = "E-WALLET", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            vd.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CashCounted", HeaderText = "IN HAND", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", ForeColor = t2.AccentCyan, Font = new Font("Segoe UI", 9F, FontStyle.Bold) } });
            vd.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TotalVariance", HeaderText = "O/S", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "+0.00;-0.00;0", Font = new Font("Segoe UI", 10F, FontStyle.Bold) } });
            vd.CellFormatting += (s, ev) =>
            {
                if (ev.ColumnIndex == 7 && ev.Value is decimal d && ev.CellStyle != null)
                {
                    if (d > 10) { ev.CellStyle.ForeColor = t2.AccentGreen; ev.Value = "+" + d.ToString("N2"); }
                    else if (d < -10) { ev.CellStyle.ForeColor = t2.AccentRed; ev.Value = d.ToString("N2"); }
                    else { ev.CellStyle.ForeColor = t2.TextMuted; ev.Value = d >= 0 ? "+" + d.ToString("N2") : d.ToString("N2"); }
                }
            };
            var totalVariance = data.Sum(x => x.TotalVariance);
            var displayData = data.Select(x => new
            {
                x.Date, x.Shifts, x.Cashiers, x.TotalSales, x.TotalCash, x.TotalEWallet, CashCounted = x.CashCounted,
                TotalVariance = x.TotalVariance
            }).ToList();
            displayData.Add(new
            {
                Date = "CUMULATIVE", Shifts = data.Sum(x => x.Shifts), Cashiers = "",
                TotalSales = data.Sum(x => x.TotalSales), TotalCash = data.Sum(x => x.TotalCash),
                TotalEWallet = data.Sum(x => x.TotalEWallet), CashCounted = data.Sum(x => x.CashCounted),
                TotalVariance = totalVariance
            });
            vd.DataSource = displayData;
            var vc = new Button { Text = "CLOSE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(20, 10), Size = new Size(100, 30), BackColor = accentBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            vc.Click += (_, __) => vf.Close();
            var vp = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = canvasBg };
            vp.Controls.Add(vc);
            vf.Controls.AddRange(new Control[] { vd, vp, vt });
            vf.ShowDialog();
        };
        var btnClose = new Button { Text = "CLOSE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(170, 10), Size = new Size(100, 30), BackColor = t2.AccentGrey, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnClose.Click += (_, __) => form.Close();
        var pnlBtn = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = canvasBg };
        pnlBtn.Controls.AddRange(new Control[] { btnReprint, btnTrends, btnVariance, btnClose });
        form.Controls.AddRange(new Control[] { dgv, pnlBtn, pnlToolbar });
        form.ShowDialog();
    }

    private void btnExpenses_Click(object? sender, EventArgs e)
    {
        using var form = new ExpensesForm(_currentUser);
        form.ShowDialog();
        LoadTotals();
    }

    private void InitializeComponent()
    {
        var t = ThemeManager.Current;
        var canvasBg = t.CanvasBg;
        var panelBg = t.PanelBg;
        var inputBg = t.InputBg;
        var inputFg = t.InputFg;
        var neonTitle = t.AccentCyan;
        var dimText = t.TextMuted;
        var borderColor = t.BorderColor;
        var accentBlue = t.AccentBlue;
        var accentGreen = t.AccentGreen;
        var accentRed = t.AccentRed;
        var accentOrange = t.AccentOrange;
        var accentPurple = t.AccentPurple;

        BackColor = canvasBg;
        Text = "End Shift";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };
        var lblPageTitle = new Label { Text = "\uD83D\uDD12 END SHIFT", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(250, 28) };
        lblDate = new Label { Font = new Font("Segoe UI", 10F), ForeColor = dimText, Location = new Point(300, 12), Size = new Size(300, 25), TextAlign = ContentAlignment.MiddleRight, AutoSize = false };
        pnlToolbar.Controls.AddRange(new Control[] { lblPageTitle, lblDate });

        var pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = canvasBg };
        var margin = 10;
        var y = margin;

        // ── SHIFT SUMMARY CARD ──
        var pnlSummary = new Panel { Location = new Point(margin, y), Size = new Size(100, 135), BackColor = panelBg };
        pnlSummary.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlSummary.Width - 1, pnlSummary.Height - 1); };
        var lblSumTitle = new Label { Text = "SHIFT SUMMARY", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, 10), Size = new Size(200, 18) };
        var sy = 32;
        AddSummaryRow(pnlSummary, 15, ref sy, "Shift Date/Time:", lblDate = MakeValueLabel());
        AddSummaryRow(pnlSummary, 15, ref sy, "Active Cashier:", lblCashierName = MakeValueLabel());
        AddSummaryRow(pnlSummary, 15, ref sy, "Opening Balance:", lblOpeningCash = MakeValueLabel(accentGreen));
        AddSummaryRow(pnlSummary, 15, ref sy, "Shift Expenses:", lblTotalExpenses = MakeValueLabel(accentRed));
        pnlSummary.Controls.Add(lblSumTitle);
        y += 145;

        // ── DENOMINATION CARD ──
        var pnlDenom = new Panel { Location = new Point(margin, y), Size = new Size(100, 250), BackColor = panelBg };
        pnlDenom.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlDenom.Width - 1, pnlDenom.Height - 1); };
        var lblDenomTitle = new Label { Text = "CASH DENOMINATION", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, 10), Size = new Size(200, 18) };
        var dy = 32;
        AddDenomRow(pnlDenom, ref dy, "\u20b11,000", num1000 = MakeNum(), lblTotal1000 = MakeTotalLabel());
        AddDenomRow(pnlDenom, ref dy, "\u20b1500", num500 = MakeNum(), lblTotal500 = MakeTotalLabel());
        AddDenomRow(pnlDenom, ref dy, "\u20b1200", num200 = MakeNum(), lblTotal200 = MakeTotalLabel());
        AddDenomRow(pnlDenom, ref dy, "\u20b1100", num100 = MakeNum(), lblTotal100 = MakeTotalLabel());
        AddDenomRow(pnlDenom, ref dy, "\u20b150", num50 = MakeNum(), lblTotal50 = MakeTotalLabel());
        AddDenomRow(pnlDenom, ref dy, "\u20b120", num20 = MakeNum(), lblTotal20 = MakeTotalLabel());
        var lblCoins = new Label { Text = "Coins:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(15, dy), Size = new Size(60, 25) };
        var lblXcoins = new Label { Text = "x", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = dimText, Location = new Point(78, dy), Size = new Size(20, 25), TextAlign = ContentAlignment.MiddleCenter };
        txtCoins = new NumericUpDown { DecimalPlaces = 2, Location = new Point(100, dy), Size = new Size(80, 25), Maximum = 99999, BackColor = inputBg, ForeColor = inputFg };
        txtCoins.ValueChanged += Denom_ValueChanged;
        lblTotalCoins = MakeTotalLabel();
        lblTotalCoins.Location = new Point(190, dy);
        pnlDenom.Controls.AddRange(new Control[] { lblCoins, lblXcoins, txtCoins, lblTotalCoins });
        dy += 28;
        var line = new Panel { Location = new Point(15, dy), Size = new Size(460, 1), BackColor = borderColor };
        pnlDenom.Controls.Add(line);
        dy += 10;
        AddSummaryRow(pnlDenom, 15, ref dy, "Total Cash on Hand:", lblCashOnHand = MakeValueLabel(accentBlue));
        lblCashOnHand.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        pnlDenom.Controls.Add(lblDenomTitle);
        y += 260;

        // ── NOTES & BUTTONS ──
        var pnlActions = new Panel { Location = new Point(margin, y), Size = new Size(100, 50), BackColor = canvasBg };
        var lblNotesLabel = new Label { Text = "Notes:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(0, 12), Size = new Size(60, 25) };
        txtNotes = new TextBox { Location = new Point(60, 10), Size = new Size(250, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 9F) };
        btnClose = new Button { Text = "\uD83D\uDD12 END SHIFT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(320, 5), Size = new Size(120, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentRed, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnClose.Click += btnClose_Click;
        btnHistory = new Button { Text = "\uD83D\uDCCB HISTORY", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(445, 5), Size = new Size(110, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnHistory.Click += btnHistory_Click;
        btnExpenses = new Button { Text = "\uD83D\uDCB8 EXPENSES", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(0, 45), Size = new Size(110, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentOrange, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnExpenses.Click += btnExpenses_Click;
        btnPrintReport = new Button { Text = "\uD83D\uDDA8\uFE0F PRINT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(115, 45), Size = new Size(110, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = t.AccentBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnPrintReport.Click += btnPrintReport_Click;
        btnEmail = new Button { Text = "\uD83D\uDCE7 EMAIL", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(230, 45), Size = new Size(100, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = accentPurple, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnEmail.Click += btnEmail_Click;
        pnlActions.Controls.AddRange(new Control[] { lblNotesLabel, txtNotes, btnClose, btnHistory, btnExpenses, btnPrintReport, btnEmail });

        pnlMain.Controls.AddRange(new Control[] { pnlSummary, pnlDenom, pnlActions });
        Controls.Clear();
        Controls.AddRange(new Control[] { pnlMain, pnlToolbar });

        Shown += (_, _) => ResizeLayout(pnlSummary, pnlDenom, pnlActions);
        Resize += (_, _) => ResizeLayout(pnlSummary, pnlDenom, pnlActions);
    }

    private void ResizeLayout(Panel pnlSummary, Panel pnlDenom, Panel pnlActions)
    {
        var margin = 10;
        var w = ClientSize.Width - margin * 3;
        
        var sumH = 135;
        var denomH = 280;
        var actionsH = margin * 9;
        
        var availW = w;
        pnlSummary.Location = new Point(margin, margin);
        pnlSummary.Size = new Size(availW, sumH);
        
        pnlDenom.Location = new Point(margin, sumH + margin * 2);
        pnlDenom.Size = new Size(availW, denomH);
        
        pnlActions.Location = new Point(margin, sumH + denomH + margin * 3);
        pnlActions.Size = new Size(availW, actionsH);
    }

    private Label MakeValueLabel(Color? color = null) => new() { Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = color ?? ThemeManager.Current.TextPrimary, Location = new Point(360, 0), Size = new Size(110, 22), TextAlign = ContentAlignment.MiddleRight };

    private void AddSummaryRow(Panel parent, int x, ref int y, string label, Label value)
    {
        var lbl = new Label { Text = label, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = ThemeManager.Current.TextMuted, Location = new Point(x, y), Size = new Size(200, 22) };
        value.Location = new Point(parent.Width - 160, y);
        value.Size = new Size(130, 22);
        value.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        parent.Controls.AddRange(new Control[] { lbl, value });
        y += 24;
    }

    private NumericUpDown MakeNum()
    {
        var n = new NumericUpDown { Maximum = 9999, Width = 100, BackColor = ThemeManager.Current.InputBg, ForeColor = ThemeManager.Current.TextPrimary };
        n.ValueChanged += Denom_ValueChanged;
        return n;
    }

    private Label MakeTotalLabel() => new Label { Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = ThemeManager.Current.AccentCyan, Size = new Size(100, 25), TextAlign = ContentAlignment.MiddleLeft };

    private void AddDenomRow(Panel parent, ref int y, string label, NumericUpDown num, Label lblTotal)
    {
        var lblName = new Label { Text = label, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = ThemeManager.Current.TextMuted, Location = new Point(15, y), Size = new Size(60, 25), TextAlign = ContentAlignment.MiddleLeft };
        var lblX = new Label { Text = "x", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = ThemeManager.Current.TextMuted, Location = new Point(78, y), Size = new Size(20, 25), TextAlign = ContentAlignment.MiddleCenter };
        num.Location = new Point(100, y);
        num.Size = new Size(80, 25);
        lblTotal.Location = new Point(190, y);
        parent.Controls.AddRange(new Control[] { lblName, lblX, num, lblTotal });
        y += 28;
    }

    public void ApplyTheme()
    {
        var t = ThemeManager.Current;
        BackColor = t.CanvasBg;
        ForeColor = t.TextPrimary;
    }

    private Label lblDate = null!, lblCashierName = null!, lblTotalExpenses = null!;
    private Label lblOpeningCash = null!;
    private Label lblCashOnHand = null!;
    private NumericUpDown num1000 = null!, num500 = null!, num200 = null!, num100 = null!, num50 = null!, num20 = null!;
    private NumericUpDown txtCoins = null!;
    private TextBox txtNotes = null!;
    private Button btnClose = null!, btnHistory = null!, btnPrintReport = null!, btnEmail = null!, btnExpenses = null!;
}
