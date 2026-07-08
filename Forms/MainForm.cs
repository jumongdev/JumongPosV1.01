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
    private readonly CustomerDisplayForm? _customerDisplay;
    private System.Windows.Forms.Timer? _schedulerTimer;
    private System.Windows.Forms.Timer? _transferTimer;
    private System.Windows.Forms.Timer? _connTimer;
    private static int _lastTransferCount;
    private Label _lblConnStatus = null!;

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

        try
        {
            using var conn3 = DatabaseHelper.GetConnection();
            conn3.Open();
            using var cmd3 = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'EnableOnlineOrders'", conn3);
            btnOnlineOrders.Visible = cmd3.ExecuteScalar()?.ToString() != "false";
            if (btnOnlineOrders.Visible) btnOnlineOrders.Text = "    Incoming Stock";
        }
        catch { }
        LayoutMenuButtons();

        StartEmailScheduler();
        StartSyncRetry();
        if (btnOnlineOrders.Visible) StartTransferPoll();
        DebugHelper.AddFormLabel(this);
        Load += (_, _) => LayoutMenuButtons();
    }

    private void StartSyncRetry()
    {
        var syncTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        syncTimer.Tick += async (_, _) => { try { await SyncService.RetryFailedAsync(); } catch (Exception ex) { ErrorLogger.Log("MainForm.StartSyncRetry", ex); } };
        syncTimer.Start();
    }

    private void StartTransferPoll()
    {
        _transferTimer = new System.Windows.Forms.Timer { Interval = 15000 };
        _transferTimer.Tick += async (_, _) =>
        {
            try
            {
                var transfers = await SyncService.GetPendingTransfersAsync();
                var count = transfers?.Count ?? 0;
                if (count > _lastTransferCount)
                {
                    var newCount = count - _lastTransferCount;
                    var icon = new NotifyIcon
                    {
                        Icon = SystemIcons.Information,
                        Visible = true,
                        BalloonTipTitle = "Incoming Stock",
                        BalloonTipText = $"{newCount} new transfer(s) ready. Open to receive."
                    };
                    icon.BalloonTipClicked += (_, _) =>
                    {
                        icon.Dispose();
                        btnOnlineOrders_Click(null!, EventArgs.Empty);
                    };
                    icon.BalloonTipClosed += (_, _) => icon.Dispose();
                    icon.ShowBalloonTip(5000);
                }
                _lastTransferCount = count;
                if (btnOnlineOrders != null)
                    btnOnlineOrders.Text = count > 0
                        ? $"    Incoming Stock ({count})"
                        : "    Incoming Stock";
            }
            catch (Exception ex) { ErrorLogger.Log("MainForm.StartTransferPoll", ex); }
        };
        _transferTimer.Start();
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
                var now = TimeHelper.Now;
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
            if (!emailSvc.IsConfigured) return;

            var error = emailSvc.SendEndShiftReport(totalSales, totalCash, totalEWallet, totalCredit, totalVoided, 0, 0, cashierName, totalExpenses, expenses, gcashTxns, creditCustomers, creditPayments, 0, 0, 0, 0, 0, 0, 0);
            if (error != null)
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scheduled_report_errors.log");
                File.AppendAllText(logPath, $"{TimeHelper.Now:yyyy-MM-dd HH:mm:ss} - Scheduled report failed: {error}{Environment.NewLine}");
            }
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scheduled_report_errors.log");
            File.AppendAllText(logPath, $"{TimeHelper.Now:yyyy-MM-dd HH:mm:ss} - Scheduled report exception: {ex.Message}{Environment.NewLine}");
        }
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

    public void ApplyTheme()
    {
        var t = ThemeManager.Current;
        BackColor = t.SidebarBg;
        ForeColor = Color.White;

        foreach (var c in Controls)
        {
            if (c is Label lbl)
            {
                if (lbl.Text == "JUMONG POS")
                    lbl.ForeColor = t.SidebarTitleAccent;
                else if (lbl.Text.StartsWith("Logged in as"))
                    lbl.ForeColor = t.SidebarUserInfo;
            }
            if (c is Panel p && p.Size.Height == 1 && p.Size.Width == 400)
                p.BackColor = t.SidebarDivider;
        }

        foreach (var btn in _menuButtons)
        {
            if (btn == btnLogout)
            {
                btn.BackColor = t.SidebarLogoutBg;
                btn.ForeColor = t.SidebarLogoutFg;
                btn.FlatAppearance.MouseOverBackColor = t.SidebarLogoutHover;
            }
            else
            {
                btn.BackColor = t.SidebarCardBg;
                btn.ForeColor = t.SidebarFg;
                btn.FlatAppearance.MouseOverBackColor = t.SidebarHoverBg;
            }
        }
    }

    public static void ApplyThemeToChildren()
    {
        var openForms = Application.OpenForms;
        for (var i = 0; i < openForms.Count; i++)
        {
            var f = openForms[i];
            if (f is MainForm mf) mf.ApplyTheme();
            else if (f is SalesForm sf) sf.ApplyTheme();
            else if (f is ProductsForm pf) pf.ApplyTheme();
            else if (f is ReportsForm rf) rf.ApplyTheme();
            else if (f is SettingsForm setf) setf.ApplyTheme();
            else if (f is StockReceivingForm srf) srf.ApplyTheme();
            else if (f is StockMovementForm smf) smf.ApplyTheme();
            else if (f is EndShiftForm esf) esf.ApplyTheme();
            else if (f is ExpensesForm ef) ef.ApplyTheme();
            else if (f is UsersForm uf) uf.ApplyTheme();
            else if (f is CreditManagementForm cmf) cmf.ApplyTheme();
            else if (f is CustomersForm cf) cf.ApplyTheme();
            else if (f is PendingOrdersForm pof) pof.ApplyTheme();
            else if (f is VoidLogForm vlf) vlf.ApplyTheme();
            else if (f is ProductUnitsForm puf) puf.ApplyTheme();
            else if (f is LoginForm lf) lf.ApplyTheme();
            else if (f is PaymentForm pmf) pmf.ApplyTheme();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _schedulerTimer?.Stop();
        _schedulerTimer?.Dispose();
        _transferTimer?.Stop();
        _transferTimer?.Dispose();
        base.OnFormClosed(e);
    }

    private void btnPOS_Click(object? sender, EventArgs e)
    {
        using var form = new SalesForm(_currentUser);
        form.ShowDialog();
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

    private void btnOnlineOrders_Click(object? sender, EventArgs e)
    {
        using var form = new PendingOrdersForm(_currentUser);
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
        var t = ThemeManager.Current;
        var darkBg = t.SidebarBg;
        var cardBg = t.SidebarCardBg;
        var accent = t.AccentBlue;
        var textColor = t.SidebarFg;
        var hoverBg = t.SidebarHoverBg;

        BackColor = darkBg;
        Text = $"Jumong POS v{AppVersion.Current}";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        ForeColor = textColor;

        var title = new Label
        {
            Text = "JUMONG POS",
            Font = new Font("Segoe UI", 24F, FontStyle.Bold),
            ForeColor = t.SidebarTitleAccent,
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
            ForeColor = t.SidebarUserInfo,
            Location = new Point(0, 65),
            Size = new Size(520, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var divider = new Panel
        {
            Location = new Point(60, 95),
            Size = new Size(400, 1),
            BackColor = t.SidebarDivider
        };

        btnPOS = CreateMenuButton("POS / Sales", 0, 120, btnPOS_Click, cardBg, textColor, hoverBg);
        btnProducts = CreateMenuButton("Products", 0, 175, btnProducts_Click, cardBg, textColor, hoverBg);
        btnCustomers = CreateMenuButton("Customers", 0, 230, btnCustomers_Click, cardBg, textColor, hoverBg);
        btnReports = CreateMenuButton("Reports", 0, 285, btnReports_Click, cardBg, textColor, hoverBg);
        btnCredit = CreateMenuButton("Credit Management", 0, 340, btnCredit_Click, cardBg, textColor, hoverBg);
        btnInventory = CreateMenuButton("Inventory", 0, 395, btnInventory_Click, cardBg, textColor, hoverBg);
        btnOnlineOrders = CreateMenuButton("Incoming Stock", 0, 450, btnOnlineOrders_Click, cardBg, textColor, hoverBg);
        btnExpenses = CreateMenuButton("Expenses", 0, 505, btnExpenses_Click, cardBg, textColor, hoverBg);
        btnUsers = CreateMenuButton("User Management", 0, 560, btnUsers_Click, cardBg, textColor, hoverBg);
        btnUsers.Visible = _currentUser.Role == "Admin";
        btnEndShift = CreateMenuButton("End Shift", 0, 615, btnEndShift_Click, cardBg, textColor, hoverBg);
        btnSettings = CreateMenuButton("Settings", 0, 670, btnSettings_Click, cardBg, textColor, hoverBg);
        btnLogout = CreateMenuButton("Logout", 0, 725, btnLogout_Click, t.SidebarLogoutBg, t.SidebarLogoutFg, t.SidebarLogoutHover);

        _lblConnStatus = new Label
        {
            Text = "Checking API...",
            Font = new Font("Segoe UI", 8F),
            ForeColor = t.SidebarUserInfo,
            Location = new Point(60, 780),
            Size = new Size(400, 20),
            TextAlign = ContentAlignment.MiddleLeft
        };

        Controls.AddRange(new Control[] { title, userInfo, divider, btnPOS, btnProducts, btnCustomers, btnReports, btnCredit, btnInventory, btnOnlineOrders, btnExpenses, btnUsers, btnEndShift, btnSettings, btnLogout, _lblConnStatus });

        _menuButtons = new Button[] { btnPOS, btnProducts, btnCustomers, btnReports, btnCredit, btnInventory, btnOnlineOrders, btnExpenses, btnUsers, btnEndShift, btnSettings, btnLogout };
        LayoutMenuButtons();

        _ = CheckApiConnectionAsync();
        _connTimer = new System.Windows.Forms.Timer { Interval = 10000 };
        _connTimer.Tick += async (_, _) => await CheckApiConnectionAsync();
        _connTimer.Start();

        // Delay user sync to avoid slowing down app startup
        var syncTimer = new System.Windows.Forms.Timer { Interval = 8000 };
        syncTimer.Tick += async (_, _) =>
        {
            syncTimer.Stop();
            var storeId = SyncService.StoreId;
            if (string.IsNullOrEmpty(storeId) || storeId == "STORE-DEV-0001") return;
            try { await SyncService.DownloadUsersAsync(storeId); } catch { }
        };
        syncTimer.Start();
    }

    private static readonly Color _connGreen = Color.FromArgb(0, 200, 83);
    private static readonly Color _connRed = Color.FromArgb(255, 82, 82);
    private async Task CheckApiConnectionAsync()
    {
        var connected = await SyncService.CheckConnectionAsync();
        _lblConnStatus.Text = connected ? "● Connected" : "● Disconnected";
        _lblConnStatus.ForeColor = connected ? _connGreen : _connRed;
    }

    private void LayoutMenuButtons()
    {
        var y = 120;
        var step = 55;
        foreach (var btn in _menuButtons)
        {
            if (btn.Visible)
            {
                btn.Location = new Point(60, y);
                y += step;
            }
        }
    }

    private Button[] _menuButtons = null!;

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
    private Button btnOnlineOrders = null!;
    private Button btnExpenses = null!;
    private Button btnEndShift = null!;
    private Button btnSettings = null!;
    private Button btnLogout = null!;
}
