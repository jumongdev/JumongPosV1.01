using System.Diagnostics;
using System.Text.Json;
using JumongPosV1._01.Helpers;

namespace JumongPosV1._01.Services;

public static class UpdateService
{
    private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromMinutes(15) };
    private const string Repo = "jumongdev/JumongPosV1.01";

    public static async Task<(bool available, string? version, string? changes, string? downloadUrl)> CheckUpdate()
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repo}/releases/latest");
            req.Headers.UserAgent.ParseAdd("JumongPOS/1.0");
            var resp = await _client.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var version = tag.TrimStart('v');
            var body = doc.RootElement.GetProperty("body").GetString() ?? "";
            var downloadUrl = $"https://github.com/{Repo}/releases/download/{tag}/JumongPosV1.01.exe";

            var currentVer = AppVersion.Current;
            return (string.Compare(version, currentVer, StringComparison.Ordinal) > 0, version, body, downloadUrl);
        }
        catch (Exception ex) { ErrorLogger.Log("UpdateService.Check", ex); return (false, null, null, null); }
    }

    public static async Task<bool> DownloadAndUpdate(string downloadUrl, IProgress<int>? progress = null)
    {
        try
        {
            var url = downloadUrl;
            var exePath = Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(exePath)) return false;
            var exeDir = Path.GetDirectoryName(exePath) ?? "";
            var newPath = Path.Combine(Path.GetTempPath(), "JumongPosV1.01_update.exe");

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
            fs.Close();

            if (total > 0 && totalRead < total)
            {
                if (File.Exists(newPath)) File.Delete(newPath);
                return false;
            }

            if (totalRead < 1_000_000)
            {
                if (File.Exists(newPath)) File.Delete(newPath);
                return false;
            }

            // Write batch: waits, copies new exe over old, starts new exe, cleans up
            var batch = Path.Combine(Path.GetTempPath(), "jumong_update.bat");
            File.WriteAllText(batch, 
                "@echo off\r\n" +
                "ping 127.0.0.1 -n 3 >nul\r\n" +
                "copy /y \"" + newPath + "\" \"" + exePath + "\"\r\n" +
                "if %errorlevel% equ 0 (\r\n" +
                "  del \"" + newPath + "\" 2>nul\r\n" +
                "  start \"\" \"" + exePath + "\"\r\n" +
                ")\r\n" +
                "del \"%~f0\" 2>nul\r\n");
            Process.Start(new ProcessStartInfo(batch) { UseShellExecute = true });
            Process.GetCurrentProcess().Kill();
            return true;
        }
        catch (Exception ex) { ErrorLogger.Log("UpdateService.Download", ex); return false; }
    }
}
