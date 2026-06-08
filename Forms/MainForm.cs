using System.Data.SQLite;
using System.Timers;
using JumongPosV1._01.Data;
using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class MainForm : Form
{
    private readonly User _currentUser;
    private readonly CustomerDisplayForm _customerDisplay;
    private System.Windows.Forms.Timer? _schedulerTimer;
    private string? _lastDailyReportSent;

    public MainForm(User user)
    {
        _currentUser = user;
        InitializeComponent();

        var screenIdx = 0;
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'PosScreenIndex'", conn);
            var val = cmd.ExecuteScalar()?.ToString();
            if (int.TryParse(val, out var idx) && idx >= 0 && idx < Screen.AllScreens.Length)
                screenIdx = idx;
        }
        catch { }

        if (screenIdx != 0 || Screen.AllScreens.Length > 1)
        {
            StartPosition = FormStartPosition.Manual;
            Location = Screen.AllScreens[screenIdx].WorkingArea.Location;
            WindowState = FormWindowState.Maximized;
        }

        _customerDisplay = new CustomerDisplayForm();
        var showDisplay = true;
        try
        {
            using var conn2 = DatabaseHelper.GetConnection();
            conn2.Open();
            using var cmd2 = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'EnableCustomerDisplay'", conn2);
            var dispVal = cmd2.ExecuteScalar()?.ToString();
            showDisplay = dispVal != "false";
        }
        catch { }
        if (showDisplay) _customerDisplay.Show();

        StartEmailScheduler();
        StartSyncRetry();
        DebugHelper.AddFormLabel(this);
    }

    private void StartSyncRetry()
    {
        var syncTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        syncTimer.Tick += async (_, _) => { try { await SyncService.RetryFailedAsync(); } catch { } };
        syncTimer.Start();
    }

    private void StartEmailScheduler()
    {
        try
        {
            var scheduleHour = 20;
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'EmailScheduleHour'", conn);
            var val = cmd.ExecuteScalar()?.ToString();
            if (int.TryParse(val, out var h) && h >= 0 && h <= 23) scheduleHour = h;

            using var lastSentCmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'LastDailyReportSent'", conn);
            _lastDailyReportSent = lastSentCmd.ExecuteScalar()?.ToString();

            _schedulerTimer = new System.Windows.Forms.Timer { Interval = 60000 };
            _schedulerTimer.Tick += (_, _) =>
            {
                var now = DateTime.Now;
                var todayStr = now.ToString("yyyy-MM-dd");
                if (now.Hour == scheduleHour && _lastDailyReportSent != todayStr)
                {
                    SendScheduledReport();
                    _lastDailyReportSent = todayStr;
                    UpsertSetting("LastDailyReportSent", todayStr);
                }
            };
            _schedulerTimer.Start();
        }
        catch { }
    }

    private void SendScheduledReport()
    {
        try
        {
            var (totalSales, totalCash, totalEWallet, totalCredit, totalVoided, creditPayCash, creditPayEWallet, totalExpenses) = DailyCloseService.GetShiftTotals();
            var expenses = ExpenseService.GetExpensesForCurrentShift();
            var gcashTxns = DailyCloseService.GetGcashTransactionsSinceLastClose();
            var creditCustomers = DailyCloseService.GetCreditCustomersSinceLastClose();
            var creditPayments = DailyCloseService.GetCreditPaymentsSinceLastClose();
            var cashierName = "Auto Scheduled Report";

            var emailSvc = new EmailService();
            if (emailSvc.IsConfigured)
                emailSvc.SendEndShiftReport(totalSales, totalCash, totalEWallet, totalCredit, totalVoided, 0, 0, cashierName, totalExpenses, expenses, gcashTxns, creditCustomers, creditPayments, 0, 0, 0, 0, 0, 0, 0);
        }
        catch { }
    }

    private static void UpsertSetting(string key, string value)
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("UPDATE OR IGNORE Settings SET Value = @val WHERE Key = @key", conn);
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@val", value);
            cmd.ExecuteNonQuery();
            using var ins = new SQLiteCommand("INSERT OR IGNORE INTO Settings (Key, Value) VALUES (@key, @val)", conn);
            ins.Parameters.AddWithValue("@key", key);
            ins.Parameters.AddWithValue("@val", value);
            ins.ExecuteNonQuery();
        }
        catch { }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _schedulerTimer?.Stop();
        _schedulerTimer?.Dispose();
        _customerDisplay?.Close();
        base.OnFormClosed(e);
    }

    private void btnPOS_Click(object? sender, EventArgs e)
    {
        using var form = new SalesForm(_currentUser, _customerDisplay);
        form.ShowDialog();
        _customerDisplay.SetIdleMode(true);
        _customerDisplay.ClearOrder();
    }

    private void btnProducts_Click(object? sender, EventArgs e)
    {
        using var form = new ProductsForm(_currentUser, null);
        form.ShowDialog();
    }

    private void btnCustomers_Click(object? sender, EventArgs e)
    {
        using var form = new CustomersForm(_currentUser);
        form.ShowDialog();
    }

    private void btnReports_Click(object? sender, EventArgs e)
    {
        using var form = new ReportsForm(_currentUser);
        form.ShowDialog();
    }

    private void btnUsers_Click(object? sender, EventArgs e)
    {
        using var form = new UsersForm(_currentUser);
        form.ShowDialog();
    }

    private void btnInventory_Click(object? sender, EventArgs e)
    {
        using var form = new StockReceivingForm(_currentUser);
        form.ShowDialog();
    }

    private void btnExpenses_Click(object? sender, EventArgs e)
    {
        using var form = new ExpensesForm(_currentUser);
        form.ShowDialog();
    }

    private void btnCredit_Click(object? sender, EventArgs e)
    {
        using var form = new CreditManagementForm(_currentUser);
        form.ShowDialog();
    }

    private void btnEndShift_Click(object? sender, EventArgs e)
    {
        using var form = new EndShiftForm(_currentUser);
        form.ShowDialog();
    }

    private void btnSettings_Click(object? sender, EventArgs e)
    {
        using var form = new SettingsForm(_currentUser);
        form.ShowDialog();
    }

    private void btnLogout_Click(object? sender, EventArgs e)
    {
        Close();
    }

    private void InitializeComponent()
    {
        var darkBg = Color.FromArgb(30, 30, 45);
        var cardBg = Color.FromArgb(40, 40, 58);
        var accent = Color.FromArgb(72, 126, 176);
        var textColor = Color.White;
        var hoverBg = Color.FromArgb(55, 55, 78);

        BackColor = darkBg;
        Text = $"Jumong POS v{AppVersion.Current}";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        ForeColor = textColor;

        var title = new Label
        {
            Text = "JUMONG POS",
            Font = new Font("Segoe UI", 24F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 180, 255),
            Location = new Point(0, 25),
            Size = new Size(520, 40),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var userInfo = new Label
        {
            Text = string.IsNullOrEmpty(_currentUser.FullName)
                ? $"Logged in as: {_currentUser.Username}  ({_currentUser.Role})"
                : $"Logged in as: {_currentUser.FullName}  ({_currentUser.Role})",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(150, 150, 170),
            Location = new Point(0, 65),
            Size = new Size(520, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var divider = new Panel
        {
            Location = new Point(60, 95),
            Size = new Size(400, 1),
            BackColor = Color.FromArgb(60, 60, 80)
        };

        btnPOS = CreateMenuButton("POS / Sales", 0, 120, btnPOS_Click, cardBg, textColor, hoverBg);
        btnProducts = CreateMenuButton("Products", 0, 175, btnProducts_Click, cardBg, textColor, hoverBg);
        btnCustomers = CreateMenuButton("Customers", 0, 230, btnCustomers_Click, cardBg, textColor, hoverBg);
        btnReports = CreateMenuButton("Reports", 0, 285, btnReports_Click, cardBg, textColor, hoverBg);
        btnCredit = CreateMenuButton("Credit Management", 0, 340, btnCredit_Click, cardBg, textColor, hoverBg);
        btnInventory = CreateMenuButton("Inventory", 0, 395, btnInventory_Click, cardBg, textColor, hoverBg);
        btnExpenses = CreateMenuButton("Expenses", 0, 450, btnExpenses_Click, cardBg, textColor, hoverBg);
        btnUsers = CreateMenuButton("User Management", 0, 505, btnUsers_Click, cardBg, textColor, hoverBg);
        btnUsers.Visible = _currentUser.Role == "Admin";
        btnEndShift = CreateMenuButton("End Shift", 0, 560, btnEndShift_Click, cardBg, textColor, hoverBg);
        btnSettings = CreateMenuButton("Settings", 0, 615, btnSettings_Click, cardBg, textColor, hoverBg);
        btnLogout = CreateMenuButton("Logout", 0, 670, btnLogout_Click, Color.FromArgb(50, 35, 35), Color.FromArgb(231, 76, 60), Color.FromArgb(70, 45, 45));

        Controls.AddRange(new Control[] { title, userInfo, divider, btnPOS, btnProducts, btnCustomers, btnReports, btnCredit, btnInventory, btnExpenses, btnUsers, btnEndShift, btnSettings, btnLogout });
    }

    private Button CreateMenuButton(string text, int x, int y, EventHandler click, Color bg, Color fg, Color hoverBg)
    {
        var btn = new Button
        {
            Text = "    " + text,
            Location = new Point(60, y),
            Size = new Size(400, 48),
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = hoverBg },
            BackColor = bg,
            ForeColor = fg,
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            Padding = new Padding(20, 0, 0, 0)
        };
        btn.FlatAppearance.BorderColor = bg;
        btn.Click += click;
        return btn;
    }

    private Button btnPOS = null!;
    private Button btnProducts = null!;
    private Button btnCustomers = null!;
    private Button btnReports = null!;
    private Button btnUsers = null!;
    private Button btnCredit = null!;
    private Button btnInventory = null!;
    private Button btnExpenses = null!;
    private Button btnEndShift = null!;
    private Button btnSettings = null!;
    private Button btnLogout = null!;
}
