using System.Diagnostics;
using System.Runtime.InteropServices;
using JumongPosV1._01.Data;
using JumongPosV1._01.Forms;
using JumongPosV1._01.Helpers;
using JumongPosV1._01.Services;

namespace JumongPosV1._01;

static class Program
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "JumongPosV1.01_{E8A3B9C1-D2F4-4A7E-9B6C-5F8D1E3A2C7B}", out var createdNew);
        if (!createdNew)
        {
            var current = Process.GetCurrentProcess();
            foreach (var p in Process.GetProcessesByName(current.ProcessName))
            {
                if (p.Id != current.Id)
                {
                    var hWnd = p.MainWindowHandle;
                    if (hWnd == IntPtr.Zero)
                        hWnd = p.Handle;
                    ShowWindowAsync(hWnd, SW_RESTORE);
                    SetForegroundWindow(hWnd);
                    break;
                }
            }
            return;
        }

        try { Console.TreatControlCAsInput = true; } catch { }

        ApplicationConfiguration.Initialize();

        var perfLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "perf.log");
        var perfSw = Stopwatch.StartNew();

        try
        {
            perfSw.Restart();
            DatabaseHelper.Initialize();
            var dbInit = perfSw.ElapsedMilliseconds;

            perfSw.Restart();
            ThemeManager.LoadTheme();
            var themeLoad = perfSw.ElapsedMilliseconds;

            perfSw.Restart();
            AutoBackup();
            var backupTime = perfSw.ElapsedMilliseconds;

            File.AppendAllText(perfLog,
                $"[{TimeHelper.Now:yyyy-MM-dd HH:mm:ss}] DB={dbInit}ms Theme={themeLoad}ms Backup={backupTime}ms{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Database initialization failed:\n{ex.Message}\n\nDB Path: {DatabaseHelper.DbPath}",
                "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.ThreadException += (_, e) => { ErrorLogger.Log("Unhandled", e.Exception); LogCrash(e.Exception); SendErrorEmail(e.Exception); };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            { ErrorLogger.Log("Unhandled", ex); LogCrash(ex); SendErrorEmail(ex); }
        };

        EmailService.FlushQueue();

        perfSw.Restart();
        using var login = new LoginForm();
        if (login.ShowDialog() != DialogResult.OK)
            return;
        var loginTime = perfSw.ElapsedMilliseconds;
        File.AppendAllText(perfLog,
            $"Login={loginTime}ms  Total={perfSw.ElapsedMilliseconds}ms{Environment.NewLine}");

        ErrorLogger.Log("Startup", $"App v{AppVersion.Current} started by {login.CurrentUser?.Username ?? "unknown"}");

        try
        {
            Application.Run(new MainForm(login.CurrentUser!));
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("MainForm.Run", ex);
            LogCrash(ex);
            MessageBox.Show($"Application error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static void LogCrash(Exception ex)
    {
        try
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            var lines = new List<string>
            {
                $"=== Crash at {TimeHelper.Now:yyyy-MM-dd HH:mm:ss} ===",
                $"Machine: {Environment.MachineName}",
                $"Version: v{AppVersion.Current}",
                $"Type: {ex.GetType().FullName}",
                $"Message: {ex.Message}",
                $"Stack: {ex.StackTrace}",
            };
            if (ex.InnerException != null)
            {
                lines.Add($"Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                lines.Add($"Inner Stack: {ex.InnerException.StackTrace}");
            }
            lines.Add("");
            File.AppendAllText(logPath, string.Join(Environment.NewLine, lines));
        }
        catch { }
    }

    static void AutoBackup()
    {
        try
        {
            var dbPath = DatabaseHelper.DbPath;
            if (!File.Exists(dbPath)) return;

            var backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
            Directory.CreateDirectory(backupDir);

            var timestamp = TimeHelper.Now.ToString("yyyyMMdd");
            var backupPath = Path.Combine(backupDir, $"JumongPos_{timestamp}.db");

            if (!File.Exists(backupPath))
                File.Copy(dbPath, backupPath, overwrite: false);

            var files = Directory.GetFiles(backupDir, "JumongPos_*.db")
                .OrderByDescending(f => f)
                .Skip(7)
                .ToList();
            foreach (var f in files) File.Delete(f);
        }
        catch { }
    }

    static void SendErrorEmail(Exception ex)
    {
        try
        {
            var svc = new EmailService();
            if (!svc.IsConfigured) return;

            var body = $@"App Error Report
Machine: {Environment.MachineName}
Time: {TimeHelper.Now:yyyy-MM-dd HH:mm:ss}
Version: v1.01

Exception:
{ex.GetType().FullName}: {ex.Message}

Stack Trace:
{ex.StackTrace}

{(ex.InnerException != null ? $"Inner: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}" : "")}";

            svc.SendErrorReport("App Error", body);
        }
        catch { }
    }
}
