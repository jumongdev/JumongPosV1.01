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
    private readonly InventoryWebServer _invServer = new(port: 5002, pin: "1234");

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

        var storeId = SyncService.StoreId;
        foreach (Control c in Controls)
            if (c is Panel card && card.Controls.Count > 0 && card.Controls[1] is Label lbl && lbl.Text == "Wholesale")
                card.Visible = storeId == "STORE-20260602-7159" || storeId == "STORE-DEV-0001";

        StartEmailScheduler();
        StartSyncRetry();
        StartTransferPoll();
        DebugHelper.AddFormLabel(this);
        Load += (_, _) => StartInventoryServer();
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
                foreach (Control c in Controls)
                    if (c is Panel card && card.Controls.Count >= 2 && card.Controls[1] is Label lbl && lbl.Text == "Incoming Stock")
                        lbl.Text = count > 0 ? $"Incoming Stock ({count})" : "Incoming Stock";
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
            var (totalSales, totalCash, totalEWallet, totalCredit, totalVoided, creditPayCash, creditPayEWallet, totalExpenses, _, _) = DailyCloseService.GetShiftTotals();
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
        BackColor = Color.FromArgb(15, 15, 35);
        var topPanel = Controls[^1] as Panel;
        if (topPanel != null)
        {
            topPanel.BackColor = t.SidebarBg;
            foreach (Control cc in topPanel.Controls)
            {
                if (cc is Label lbl)
                {
                    if (lbl.Text == "JUMONG POS")
                        lbl.ForeColor = t.SidebarTitleAccent;
                    else
                        lbl.ForeColor = t.SidebarUserInfo;
                }
            }
        }
        foreach (Control c in Controls)
        {
            if (c is Panel card && card.Controls.Count >= 2)
            {
                card.BackColor = t.SidebarCardBg;
                card.Invalidate();
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
            else if (f is InventoryHistoryForm ihf) ihf.ApplyTheme();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _schedulerTimer?.Stop();
        _schedulerTimer?.Dispose();
        _transferTimer?.Stop();
        _transferTimer?.Dispose();
        _invServer.Stop();
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

    private void btnWhSell_Click(object? sender, EventArgs e)
    {
        try
        {
            ErrorLogger.Log("btnWhSell", "Opening WarehouseSellForm...");
            using var form = new WarehouseSellForm(_currentUser);
            form.ShowDialog();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("btnWhSell", ex);
            MessageBox.Show($"Error: {ex.Message}", "Wholesale Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnInventoryCount_Click(object? sender, EventArgs e)
    {
        using var form = new InventoryHistoryForm(_currentUser);
        form.ShowDialog();
    }

    private void StartInventoryServer()
    {
        try
        {
            _invServer.Start();
            var ip = _invServer.Url;
            _lblConnStatus.Text = $"\u25CF Server: {ip}";
            _lblConnStatus.ForeColor = _connGreen;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("MainForm.StartInventoryServer", ex);
        }
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

    private Panel MakeCard(string icon, string label, EventHandler click)
    {
        var t = ThemeManager.Current;
        var p = new Panel
        {
            Size = new Size(220, 160),
            BackColor = t.SidebarCardBg,
            Cursor = Cursors.Hand
        };
        p.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(60, 60, 90), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };
        var lblIcon = new Label
        {
            Text = icon,
            Font = new Font("Segoe UI", 36F),
            ForeColor = t.SidebarTitleAccent,
            Location = new Point(0, 30),
            Size = new Size(220, 60),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        var lblText = new Label
        {
            Text = label,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = t.SidebarFg,
            Location = new Point(0, 100),
            Size = new Size(220, 40),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        p.Controls.Add(lblIcon);
        p.Controls.Add(lblText);
        p.Click += click;
        lblIcon.Click += click;
        lblText.Click += click;
        p.MouseEnter += (_, _) => p.BackColor = t.SidebarHoverBg;
        p.MouseLeave += (_, _) => p.BackColor = t.SidebarCardBg;
        return p;
    }

    private void InitializeComponent()
    {
        var t = ThemeManager.Current;
        var darkBg = t.SidebarBg;
        var cardBg = t.SidebarCardBg;
        var textColor = t.SidebarFg;

        BackColor = Color.FromArgb(15, 15, 35);
        Text = $"Jumong POS v{AppVersion.Current}";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        ForeColor = textColor;

        var topPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(ClientSize.Width, 110),
            BackColor = darkBg,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var title = new Label
        {
            Text = $"JUMONG POS    v{AppVersion.Current}",
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = t.SidebarTitleAccent,
            Location = new Point(30, 20),
            Size = new Size(600, 40),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var userInfo = new Label
        {
            Text = string.IsNullOrEmpty(_currentUser.FullName)
                ? $"{_currentUser.Username}  ({_currentUser.Role})"
                : $"{_currentUser.FullName}  ({_currentUser.Role})",
            Font = new Font("Segoe UI", 9F),
            ForeColor = t.SidebarUserInfo,
            Location = new Point(30, 60),
            Size = new Size(280, 20),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _lblConnStatus = new Label
        {
            Text = "Checking API...",
            Font = new Font("Segoe UI", 8F),
            ForeColor = t.SidebarUserInfo,
            Location = new Point(30, 82),
            Size = new Size(280, 18),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var items = new (string icon, string label, EventHandler click, bool admin)[]
        {
            ("\uD83D\uDCC5", "POS / Sales", btnPOS_Click, false),
            ("\uD83D\uDCE6", "Products", btnProducts_Click, false),
            ("\uD83D\uDC65", "Customers", btnCustomers_Click, false),
            ("\uD83D\uDCCA", "Reports", btnReports_Click, false),
            ("\uD83D\uDCB3", "Credit", btnCredit_Click, false),
            ("\uD83D\uDCE6", "Inventory", btnInventory_Click, false),
            ("\uD83C\uDFEA", "Wholesale", btnWhSell_Click, false),
            ("\uD83D\uDCCA", "Inventory Count", btnInventoryCount_Click, false),
            ("\uD83D\uDCE6", "Incoming Stock", btnOnlineOrders_Click, false),
            ("\uD83D\uDCB8", "Expenses", btnExpenses_Click, false),
            ("\uD83D\uDC64", "Users", btnUsers_Click, true),
            ("\uD83D\uDD14", "End Shift", btnEndShift_Click, false),
            ("\u2699\uFE0F", "Settings", btnSettings_Click, false),
            ("\uD83D\uDEAA", "Logout", btnLogout_Click, false),
        };

        var cardW = 220;
        var cardH = 160;
        var gapX = 20;
        var gapY = 20;
        var cols = 4;
        var startX = 30;
        var startY = 130;
        var idx = 0;

        foreach (var (icon, label, click, admin) in items)
        {
            if (admin && _currentUser.Role != "Admin") { idx++; continue; }
            var col = idx % cols;
            var row = idx / cols;
            var card = MakeCard(icon, label, click);
            card.Location = new Point(startX + col * (cardW + gapX), startY + row * (cardH + gapY));
            if (label == "Wholesale") card.Visible = false;
            Controls.Add(card);
            idx++;
        }

        Controls.Add(topPanel);
        topPanel.Controls.Add(title);
        topPanel.Controls.Add(userInfo);
        topPanel.Controls.Add(_lblConnStatus);

        _ = CheckApiConnectionAsync();
        _connTimer = new System.Windows.Forms.Timer { Interval = 10000 };
        _connTimer.Tick += async (_, _) => await CheckApiConnectionAsync();
        _connTimer.Start();

        Resize += (_, _) =>
        {
            topPanel.Width = ClientSize.Width;
        };

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
        var (ok, err) = await SyncService.CheckConnectionAsync();
        var serverUrl = _invServer.IsRunning ? _invServer.Url : "";
        var invText = string.IsNullOrEmpty(serverUrl) ? "" : $" | INV: {serverUrl}";
        _lblConnStatus.Text = (ok ? "● Cloud: OK" : "● Cloud: OFF " + (err ?? "")) + invText;
        _lblConnStatus.ForeColor = ok ? _connGreen : _connRed;
    }

}
