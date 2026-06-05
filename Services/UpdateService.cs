using System.Diagnostics;
using System.Net.Http.Json;

namespace JumongPosV1._01.Services;

public static class UpdateService
{
    private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromMinutes(5) };

    public static async Task<(bool available, string? version, string? changes)> CheckUpdate()
    {
        try
        {
            var apiBase = SyncService.ApiUrl.Replace("/api", "");
            var info = await _client.GetFromJsonAsync<UpdateInfo>($"{apiBase}/api/dashboard/version");
            if (info == null) return (false, null, null);

            var currentVer = "1.0.6";
            return (string.Compare(info.Version, currentVer, StringComparison.Ordinal) > 0, info.Version, info.Changes);
        }
        catch { return (false, null, null); }
    }

    public static async Task<bool> DownloadAndUpdate(IProgress<int>? progress = null)
    {
        try
        {
            var apiBase = SyncService.ApiUrl.Replace("/api", "");
            var url = $"{apiBase}/updates/JumongPosV1.01.exe";
            var exePath = Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(exePath)) return false;
            var newPath = exePath + ".new";
            var backupPath = exePath + ".bak";

            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return false;

            var total = response.Content.Headers.ContentLength ?? -1;
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fs = new FileStream(newPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[8192];
            var read = 0;
            long totalRead = 0;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read));
                totalRead += read;
                progress?.Report(total > 0 ? (int)(totalRead * 100 / total) : 50);
            }

            if (File.Exists(backupPath)) File.Delete(backupPath);
            File.Move(exePath, backupPath);
            File.Move(newPath, exePath);

            var batch = Path.Combine(Path.GetTempPath(), "jumong_update.bat");
            File.WriteAllText(batch, $"@echo off{Environment.NewLine}timeout /t 2 /nobreak >nul{Environment.NewLine}del \"{backupPath}\"{Environment.NewLine}start \"\" \"{exePath}\"{Environment.NewLine}del \"%~f0\"");
            Process.Start(new ProcessStartInfo(batch) { UseShellExecute = true });
            Environment.Exit(0);
            return true;
        }
        catch { return false; }
    }

    private class UpdateInfo
    {
        public string Version { get; set; } = "";
        public string BuildDate { get; set; } = "";
        public string Changes { get; set; } = "";
    }
}
