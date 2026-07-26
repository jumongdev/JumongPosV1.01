using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

var baseDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
var dbPath = Path.Combine(baseDir, "JumongPos.db");
if (!File.Exists(dbPath))
{
    var parentDir = Directory.GetParent(baseDir)?.FullName;
    if (parentDir != null)
    {
        var parentDb = Path.Combine(parentDir, "JumongPos.db");
        if (File.Exists(parentDb)) dbPath = parentDb;
    }
}
var apiUrl = "https://admin.jumongdev.com/api";
var storeId = "";
var version = "1.0";

// Read storeId from local DB
try
{
    using var conn = new SQLiteConnection($"Data Source={dbPath}");
    conn.Open();
    using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'StoreId'", conn);
    storeId = cmd.ExecuteScalar()?.ToString() ?? "";
    apiUrl = DatabaseHelperGetSetting(conn, "CloudApiUrl") ?? apiUrl;
    if (!apiUrl.EndsWith("/api")) apiUrl = apiUrl.TrimEnd('/') + "/api";
}
catch (Exception ex)
{
    Console.WriteLine($"DB read error: {ex.Message}");
    Console.WriteLine("Press Enter to exit...");
    Console.ReadLine();
    return;
}

if (string.IsNullOrEmpty(storeId) || storeId == "STORE-DEV-0001")
{
    Console.WriteLine("Dev/demo store — agent not starting.");
    Console.ReadLine();
    return;
}

Console.WriteLine($"Agent v{version} | Store: {storeId}");
Console.WriteLine($"API: {apiUrl}");
Console.WriteLine($"DB: {dbPath}");
Console.WriteLine();

using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

while (true)
{
    try
    {
        // Send heartbeat
        try
        {
            var hb = new StringContent(JsonSerializer.Serialize(new { storeId, version = "1.0", localIp = GetLocalIp(), machineName = Environment.MachineName }), Encoding.UTF8, "application/json");
            await client.PostAsync(apiUrl + "/dashboard/agent/heartbeat", hb);
        }
        catch { }

        // Check for pending commands
        var pollUrl = apiUrl + "/dashboard/agent/poll/" + Uri.EscapeDataString(storeId);
        var resp = await client.GetAsync(pollUrl);
        if (resp.IsSuccessStatusCode)
        {
            var json = await resp.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(json) && json != "{}")
            {
                var cmd = JsonDocument.Parse(json).RootElement;
                var cmdId = cmd.GetProperty("id").GetInt32();
                var type = cmd.GetProperty("type").GetString() ?? "sql";
                var payload = cmd.GetProperty("payload").GetString() ?? "";

                string output = "";
                string error = "";
                try
                {
                    if (type == "sql")
                        output = RunSql(dbPath, payload);
                    else if (type == "invcheck")
                        output = RunInvCheck(dbPath);
                    else if (type == "ps")
                        output = RunPs(payload);
                    else if (type == "readfile")
                        output = File.Exists(payload) ? File.ReadAllText(payload) : "File not found: " + payload;
                }
                catch (Exception ex) { error = ex.Message; }

                // Post result
                var result = new StringContent(JsonSerializer.Serialize(new { storeId, commandId = cmdId, output, error }), Encoding.UTF8, "application/json");
                await client.PostAsync(apiUrl + "/dashboard/agent/result", result);
            }
        }
    }
    catch { }

    await Task.Delay(3000);
}

static string RunSql(string dbPath, string sql)
{
    var sb = new StringBuilder();
    using var conn = new SQLiteConnection($"Data Source={dbPath}");
    conn.Open();
    using var cmd = new SQLiteCommand(sql, conn);
    using var r = cmd.ExecuteReader();
    var cols = new List<string>();
    for (var i = 0; i < r.FieldCount; i++)
        cols.Add(r.GetName(i));
    sb.AppendLine(string.Join("\t", cols));

    var rowCount = 0;
    while (r.Read() && rowCount < 500)
    {
        var vals = new List<string>();
        for (var i = 0; i < r.FieldCount; i++)
            vals.Add((r.GetValue(i) ?? "NULL").ToString()!.Replace("\n", " ").Replace("\r", "").Replace("\t", " "));
        sb.AppendLine(string.Join("\t", vals));
        rowCount++;
    }
    if (rowCount >= 500) sb.AppendLine("... (truncated at 500 rows)");
    return sb.ToString();
}

static string RunInvCheck(string dbPath)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "invcheck.exe"),
            Arguments = "\"" + dbPath + "\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var p = Process.Start(psi);
        if (p == null) return "Failed to start invcheck.exe";
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(5000);
        return output;
    }
    catch (Exception ex) { return "invcheck error: " + ex.Message; }
}

static string RunPs(string script)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -Command \"" + script.Replace("\"", "\\\"") + "\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var p = Process.Start(psi);
        if (p == null) return "Failed to start PowerShell";
        var output = p.StandardOutput.ReadToEnd();
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit(10000);
        return output + (string.IsNullOrEmpty(err) ? "" : "\nERR: " + err);
    }
    catch (Exception ex) { return "PS error: " + ex.Message; }
}

static string GetLocalIp()
{
    try
    {
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        var ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        return ip?.ToString() ?? "";
    }
    catch { return ""; }
}

static string? DatabaseHelperGetSetting(SQLiteConnection conn, string key)
{
    using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = @key", conn);
    cmd.Parameters.AddWithValue("@key", key);
    return cmd.ExecuteScalar()?.ToString();
}
