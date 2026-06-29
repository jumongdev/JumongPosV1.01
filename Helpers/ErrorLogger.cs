using System.Text;

using JumongPosV1._01.Services;

namespace JumongPosV1._01.Helpers;

public static class ErrorLogger
{
    private static readonly string LogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "error.log");

    private static readonly object _lock = new();

    public static void Log(string source, Exception ex)
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{TimeHelper.Now:yyyy-MM-dd HH:mm:ss}] {source}");
            sb.AppendLine($"  Type: {ex.GetType().FullName}");
            sb.AppendLine($"  Message: {ex.Message}");
            sb.AppendLine($"  Stack: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                sb.AppendLine($"  Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                sb.AppendLine($"  Inner Stack: {ex.InnerException.StackTrace}");
            }
            sb.AppendLine();
            File.AppendAllText(LogPath, sb.ToString());
        }
    }

    public static void Log(string source, string message)
    {
        lock (_lock)
        {
            var line = $"[{TimeHelper.Now:yyyy-MM-dd HH:mm:ss}] {source}: {message}{Environment.NewLine}";
            File.AppendAllText(LogPath, line);
        }
    }
}
