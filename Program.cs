using System.Diagnostics;
using System.Runtime.InteropServices;
using JumongPosV1._01.Data;
using JumongPosV1._01.Forms;
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

        try
        {
            DatabaseHelper.Initialize();
            AutoBackup();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Database initialization failed:\n{ex.Message}\n\nDB Path: {DatabaseHelper.DbPath}",
                "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.ThreadException += (_, e) => SendErrorEmail(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                SendErrorEmail(ex);
        };

        EmailService.FlushQueue();

        using var login = new LoginForm();
        if (login.ShowDialog() != DialogResult.OK)
            return;

        Application.Run(new MainForm(login.CurrentUser!));
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
