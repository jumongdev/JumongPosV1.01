using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Services;

internal class InventoryWebServer : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private readonly int _port;
    private readonly string _pin;
    private readonly string _localIp;

    public bool IsRunning => _listener != null;
    public int Port => _port;
    public string Url => $"http://{_localIp}:{_port}";

    public InventoryWebServer(int port = 5002, string pin = "1234")
    {
        _port = port;
        _pin = pin;
        _localIp = GetLocalIpAddress();
    }

    public void Start()
    {
        if (_listener != null) return;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _listenTask = ListenAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        _listener = null;
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch { }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var buffer = new byte[8192];
                var offset = 0;

                while (offset < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
                    if (read == 0) return;
                    offset += read;
                    var raw = Encoding.UTF8.GetString(buffer, 0, offset);
                    if (raw.Contains("\r\n\r\n") || raw.Contains("\n\n"))
                        break;
                }

                var request = Encoding.UTF8.GetString(buffer, 0, offset);
                var (statusCode, contentType, body) = await ProcessRequestAsync(request);
                var bodyBytes = Encoding.UTF8.GetBytes(body);

                var header = $"HTTP/1.1 {statusCode} {(statusCode == 200 ? "OK" : statusCode == 404 ? "Not Found" : "Bad Request")}\r\n" +
                    $"Content-Type: {contentType}\r\n" +
                    $"Content-Length: {bodyBytes.Length}\r\n" +
                    "Connection: close\r\n" +
                    "Access-Control-Allow-Origin: *\r\n" +
                    "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                    "Access-Control-Allow-Headers: Content-Type\r\n" +
                    "\r\n";

                var headerBytes = Encoding.UTF8.GetBytes(header);
                await stream.WriteAsync(headerBytes, ct);
                await stream.WriteAsync(bodyBytes, ct);
                await stream.FlushAsync(ct);
            }
        }
        catch { }
    }

    private async Task<(int statusCode, string contentType, string body)> ProcessRequestAsync(string rawRequest)
    {
        try
        {
            var lines = rawRequest.Split('\n');
            if (lines.Length < 1) return (400, "application/json", JsonError("Bad request"));

            var requestLine = lines[0].Trim();
            var parts = requestLine.Split(' ');
            if (parts.Length < 2) return (400, "application/json", JsonError("Bad request"));

            var method = parts[0].ToUpper();
            var path = parts[1];

            var bodyStart = rawRequest.IndexOf("\r\n\r\n");
            if (bodyStart < 0) bodyStart = rawRequest.IndexOf("\n\n");
            var body = bodyStart > 0 ? rawRequest[(bodyStart + (rawRequest[bodyStart + 2] == '\n' ? 4 : 2))..].Trim() : "";

            var pathParts = path.Split('?');
            var route = pathParts[0].TrimEnd('/');

            if (method == "OPTIONS")
                return (200, "text/plain", "");

            if (route == "" || route == "/" || route == "/index.html")
                return (200, "text/html; charset=utf-8", GetHtml());

            if (route == "/api/auth" && method == "POST")
            {
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                var pin = data.GetProperty("pin").GetString();
                if (pin == _pin)
                    return (200, "application/json", JsonOk(new { success = true, token = "authorized" }));
                return (400, "application/json", JsonError("Invalid PIN"));
            }

            if (route == "/api/session/start" && method == "POST")
            {
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                var countedBy = data.GetProperty("countedBy").GetString() ?? "Unknown";
                var sessionId = InventoryService.StartSession(countedBy);
                var session = InventoryService.GetSession(sessionId);
                return (200, "application/json", JsonOk(new { sessionId, countedBy, startedAt = session?.StartedAt ?? "" }));
            }

            if (route == "/api/session/end" && method == "POST")
            {
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                var sessionId = data.GetProperty("sessionId").GetString() ?? "";
                var error = InventoryService.EndSession(sessionId);
                if (error != null) return (400, "application/json", JsonError(error));
                var counts = InventoryService.GetSessionCounts(sessionId);
                var report = InventoryService.GetSessionReport(sessionId);
                return (200, "application/json", JsonOk(new { success = true, sessionId, counts = counts.Select(c => new
                {
                    c.Id, c.ProductId, c.Barcode, c.ProductName,
                    c.SystemQty, c.ActualQty, c.Variance, c.Adjusted
                }), report }));
            }

            if (route == "/api/session/active" && method == "GET")
            {
                var session = InventoryService.GetActiveSession();
                if (session == null)
                    return (200, "application/json", JsonOk(new { active = false }));
                var counts = InventoryService.GetSessionCounts(session.SessionId);
                return (200, "application/json", JsonOk(new
                {
                    active = true,
                    session.SessionId,
                    session.CountedBy,
                    session.StartedAt,
                    session.TotalItems,
                    session.ItemsWithVariance,
                    counts = counts.Select(c => new
                    {
                        c.Id, c.ProductId, c.Barcode, c.ProductName,
                        c.SystemQty, c.ActualQty, c.Variance, c.Adjusted
                    })
                }));
            }

            if (route.StartsWith("/api/product/") && method == "GET")
            {
                var barcode = Uri.UnescapeDataString(route["/api/product/".Length..]);
                var product = InventoryService.GetProductByBarcode(barcode);
                if (product == null)
                    return (404, "application/json", JsonError("Product not found"));
                return (200, "application/json", JsonOk(new { product.Id, product.Name, product.Barcode, product.Category, product.Price, product.StockQty }));
            }

            if (route == "/api/count" && method == "POST")
            {
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                var sessionId = data.GetProperty("sessionId").GetString() ?? "";
                var productId = data.GetProperty("productId").GetInt32();
                var barcode = data.GetProperty("barcode").GetString() ?? "";
                var productName = data.GetProperty("productName").GetString() ?? "";
                var systemQty = data.GetProperty("systemQty").GetInt32();
                var actualQty = data.GetProperty("actualQty").GetInt32();
                var countedBy = data.GetProperty("countedBy").GetString() ?? "";

                var id = InventoryService.SubmitCount(sessionId, productId, barcode, productName, systemQty, actualQty, countedBy);
                var variance = actualQty - systemQty;
                return (200, "application/json", JsonOk(new { success = true, id, productId, productName, barcode, systemQty, actualQty, variance }));
            }

            var matchSession = Regex.Match(route, @"^/api/session/([A-Z0-9]+)$");
            if (matchSession.Success && method == "GET")
            {
                var sessionId = matchSession.Groups[1].Value;
                var session = InventoryService.GetSession(sessionId);
                if (session == null) return (404, "application/json", JsonError("Session not found"));
                var counts = InventoryService.GetSessionCounts(sessionId);
                return (200, "application/json", JsonOk(new
                {
                    session.SessionId,
                    session.CountedBy,
                    session.StartedAt,
                    session.EndedAt,
                    session.Status,
                    session.TotalItems,
                    session.ItemsWithVariance,
                    counts = counts.Select(c => new
                    {
                        c.Id, c.ProductId, c.Barcode, c.ProductName,
                        c.SystemQty, c.ActualQty, c.Variance, c.Adjusted
                    })
                }));
            }

            var matchReport = Regex.Match(route, @"^/api/session/([A-Z0-9]+)/report$");
            if (matchReport.Success && method == "GET")
            {
                var sessionId = matchReport.Groups[1].Value;
                var report = InventoryService.GetSessionReport(sessionId);
                return (200, "application/json", JsonOk(new { report }));
            }

            if (route == "/api/server/info" && method == "GET")
            {
                return (200, "application/json", JsonOk(new
                {
                    ip = _localIp,
                    port = _port,
                    storeName = SyncService.StoreName,
                    connected = true
                }));
            }

            return (404, "application/json", JsonError("Not found"));
        }
        catch (Exception ex)
        {
            return (500, "application/json", JsonError($"Server error: {ex.Message}"));
        }
    }

    private static string JsonOk(object data)
    {
        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    private static string JsonError(string message)
    {
        return JsonSerializer.Serialize(new { error = message });
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch { }
        return "127.0.0.1";
    }

    private static string GetHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
<title>Inventory Count</title>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#0f172a;color:#e2e8f0;min-height:100vh;padding:16px}
.hidden{display:none!important}
.container{max-width:480px;margin:0 auto}
.card{background:#1e293b;border-radius:12px;padding:20px;margin-bottom:16px}
.input-group{margin-bottom:16px}
.input-group label{display:block;font-size:13px;color:#94a3b8;margin-bottom:6px;font-weight:600;text-transform:uppercase;letter-spacing:.5px}
.input-group input{width:100%;padding:14px 16px;background:#0f172a;border:2px solid #334155;border-radius:10px;color:#e2e8f0;font-size:18px;outline:none;transition:border-color .2s}
.input-group input:focus{border-color:#38bdf8}
.input-group input::placeholder{color:#475569}
.btn{width:100%;padding:14px;border:none;border-radius:10px;font-size:16px;font-weight:700;cursor:pointer;transition:all .2s}
.btn-primary{background:#2563eb;color:#fff}
.btn-success{background:#16a34a;color:#fff}
.btn-danger{background:#dc2626;color:#fff}
.btn-outline{background:transparent;border:2px solid #334155;color:#e2e8f0}
.product-card{background:linear-gradient(135deg,#1e3a5f,#1e293b);border-radius:12px;padding:20px;margin-bottom:16px}
.product-name{font-size:20px;font-weight:700;margin-bottom:4px}
.product-barcode{font-size:13px;color:#64748b;margin-bottom:16px}
.stock-row{display:flex;justify-content:space-between;align-items:center;padding:8px 0;border-top:1px solid #334155}
.stock-label{font-size:14px;color:#94a3b8}
.stock-value{font-size:18px;font-weight:700}
.stock-positive{color:#4ade80}
.stock-negative{color:#f87171}
.stock-zero{color:#94a3b8}
.count-input{font-size:32px!important;text-align:center;font-weight:700;padding:20px!important}
.header{text-align:center;padding:20px 0}
.header h1{font-size:24px;font-weight:800;color:#38bdf8}
.header p{color:#64748b;font-size:14px;margin-top:4px}
.stats-grid{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-bottom:16px}
.stat-card{background:#0f172a;border-radius:8px;padding:16px;text-align:center}
.stat-number{font-size:28px;font-weight:800}
.stat-label{font-size:12px;color:#64748b;margin-top:4px;text-transform:uppercase;letter-spacing:.5px}
.count-item{display:flex;justify-content:space-between;align-items:center;padding:10px 0;border-bottom:1px solid #1e293b}
.count-item:last-child{border-bottom:none}
.count-item-name{flex:1;font-size:14px}
.count-item-name small{display:block;color:#64748b;font-size:11px}
.count-item-nums{text-align:right;margin-left:12px}
.count-item-nums .sys{font-size:11px;color:#64748b}
.count-item-nums .act{font-size:15px;font-weight:600}
.toast{position:fixed;bottom:24px;left:50%;transform:translateX(-50%);background:#1e293b;color:#e2e8f0;padding:12px 24px;border-radius:10px;font-size:14px;font-weight:600;box-shadow:0 8px 32px rgba(0,0,0,.4);z-index:100;transition:all .3s;max-width:90%}
.toast-error{border-left:4px solid #dc2626}
.toast-success{border-left:4px solid #16a34a}
.barcode-input-wrap{position:relative}
.barcode-input-wrap input{padding-right:50px}
.barcode-icon{position:absolute;right:16px;top:50%;transform:translateY(-50%);font-size:24px;opacity:.5}
.summary-text{font-size:13px;color:#94a3b8;line-height:1.6;white-space:pre-wrap;font-family:'Courier New',monospace;background:#0f172a;padding:16px;border-radius:8px;max-height:300px;overflow-y:auto;margin-top:12px}
.bottom-nav{position:fixed;bottom:0;left:0;right:0;background:#1e293b;border-top:1px solid #334155;display:flex;padding:8px 16px;gap:8px;z-index:50}
.bottom-nav .btn{font-size:13px;padding:10px}
.pb-nav{padding-bottom:72px}
</style>
</head>
<body>
<div class=""container"">
<div class=""header"">
    <h1>Inventory Count</h1>
    <p id=""statusText"">Connecting...</p>
</div>
<div id=""loginScreen"">
    <div class=""card"">
        <div class=""input-group"">
            <label>PIN Code</label>
            <input type=""password"" id=""pinInput"" placeholder=""Enter PIN"" maxlength=""10"" inputmode=""numeric"">
        </div>
        <button class=""btn btn-primary"" onclick=""login()"">Login</button>
    </div>
</div>
<div id=""mainScreen"" class=""hidden"">
    <div class=""stats-grid"">
        <div class=""stat-card"">
            <div class=""stat-number"" id=""statCounted"">0</div>
            <div class=""stat-label"">Counted</div>
        </div>
        <div class=""stat-card"">
            <div class=""stat-number"" id=""statVariance"">0</div>
            <div class=""stat-label"">Variance</div>
        </div>
    </div>
    <div class=""card"">
        <div class=""input-group barcode-input-wrap"">
            <label>Scan Barcode</label>
            <input type=""text"" id=""barcodeInput"" placeholder=""Scan or type barcode..."" inputmode=""numeric"" autocomplete=""off"">
            <span class=""barcode-icon"">#</span>
        </div>
    </div>
    <div id=""productCard"" class=""hidden"">
        <div class=""product-card"">
            <div class=""product-name"" id=""prodName""></div>
            <div class=""product-barcode"" id=""prodBarcode""></div>
            <div class=""stock-row"">
                <span class=""stock-label"">System Stock</span>
                <span class=""stock-value"" id=""prodSystemStock"">0</span>
            </div>
            <div class=""stock-row"" style=""border-bottom:none"">
                <span class=""stock-label"">Actual Count</span>
                <span class=""stock-value"">
                    <input type=""number"" id=""actualInput"" class=""count-input"" min=""0"" value="""" placeholder=""0"" inputmode=""numeric"">
                </span>
            </div>
            <div style=""margin-top:16px"">
                <button class=""btn btn-success"" onclick=""submitCount()"">Save Count</button>
            </div>
        </div>
    </div>
    <div id=""noProduct"" class=""hidden card"" style=""text-align:center;padding:40px 20px"">
        <p style=""color:#94a3b8;font-size:16px"">Product not found</p>
        <p style=""color:#64748b;font-size:13px;margin-top:4px"">Scan again or check barcode</p>
    </div>
    <div class=""card pb-nav"">
        <h3 style=""font-size:16px;margin-bottom:12px;color:#94a3b8"">Counted Items</h3>
        <div id=""countedList""></div>
    </div>
</div>
<div id=""endScreen"" class=""hidden"">
    <div class=""card"">
        <h2 style=""font-size:20px;margin-bottom:16px"">Session Complete</h2>
        <div class=""stats-grid"">
            <div class=""stat-card"">
                <div class=""stat-number"" id=""endTotal"">0</div>
                <div class=""stat-label"">Total Items</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-number"" id=""endVariance"">0</div>
                <div class=""stat-label"">With Variance</div>
            </div>
        </div>
        <div class=""summary-text"" id=""endReport""></div>
        <div style=""margin-top:16px;display:flex;gap:8px"">
            <button class=""btn btn-success"" style=""flex:1"" onclick=""adjustAndClose()"">Adjust & Close</button>
            <button class=""btn btn-outline"" style=""flex:1"" onclick=""copyReport()"">Copy</button>
        </div>
        <button class=""btn btn-outline"" style=""margin-top:8px"" onclick=""backToMain()"">Back</button>
    </div>
</div>
<div class=""bottom-nav hidden"" id=""bottomNav"">
    <button class=""btn btn-outline"" style=""flex:1"" onclick=""endSession()"">End Session</button>
    <button class=""btn btn-outline"" style=""flex:1"" onclick=""refreshSession()"">Refresh</button>
</div>
</div>
<script>
let state={token:null,sessionId:null,countedBy:null,currentProduct:null,counts:[]};
document.addEventListener('DOMContentLoaded',function(){
document.getElementById('barcodeInput').addEventListener('keydown',function(e){
if(e.key==='Enter'){e.preventDefault();var b=this.value.trim();if(b)lookupProduct(b);}});
document.getElementById('actualInput').addEventListener('keydown',function(e){
if(e.key==='Enter'){e.preventDefault();submitCount();}});
document.getElementById('pinInput').addEventListener('keydown',function(e){
if(e.key==='Enter')login();});
checkActiveSession();});
function toast(m,t){var e=document.createElement('div');e.className='toast toast-'+(t||'success');e.textContent=m;document.body.appendChild(e);setTimeout(function(){e.remove();},3000);}
async function api(path,method,body){try{var o={method:method||'GET',headers:{}};if(body){o.headers['Content-Type']='application/json';o.body=JSON.stringify(body);}var r=await fetch(path,o);return await r.json();}catch(e){toast('Connection error','error');return null;}}
async function login(){var p=document.getElementById('pinInput').value.trim();if(!p){toast('Enter PIN','error');return;}var r=await api('/api/auth','POST',{pin:p});if(r&&r.success){state.token=r.token;document.getElementById('loginScreen').classList.add('hidden');document.getElementById('mainScreen').classList.remove('hidden');document.getElementById('bottomNav').classList.remove('hidden');document.getElementById('statusText').textContent='Connected';focusBarcode();checkActiveSession();}else{toast('Invalid PIN','error');}}
async function checkActiveSession(){var r=await api('/api/session/active');if(r&&r.active){state.sessionId=r.sessionId;state.countedBy=r.countedBy;state.counts=r.counts||[];updateStats();renderCountedList();document.getElementById('statusText').textContent='Session: '+r.sessionId;}else{startNewSession();}}
async function startNewSession(){var n=prompt('Enter your name:');if(!n){toast('Name required','error');return;}var r=await api('/api/session/start','POST',{countedBy:n});if(r&&r.sessionId){state.sessionId=r.sessionId;state.countedBy=n;state.counts=[];updateStats();renderCountedList();document.getElementById('statusText').textContent='Session: '+r.sessionId;toast('Session started!');}}
async function lookupProduct(barcode){if(!barcode)return;var r=await api('/api/product/'+encodeURIComponent(barcode));if(r&&r.id){state.currentProduct=r;document.getElementById('prodName').textContent=r.name;document.getElementById('prodBarcode').textContent=r.barcode+' | '+(r.category||'');document.getElementById('prodSystemStock').textContent=r.stockQty;var e=state.counts.find(function(c){return c.productId===r.id;});var inp=document.getElementById('actualInput');if(e){inp.value=e.actualQty;}else{inp.value=r.stockQty;}inp.focus();inp.select();document.getElementById('productCard').classList.remove('hidden');document.getElementById('noProduct').classList.add('hidden');}else{document.getElementById('productCard').classList.add('hidden');document.getElementById('noProduct').classList.remove('hidden');setTimeout(function(){document.getElementById('noProduct').classList.add('hidden');},2000);}document.getElementById('barcodeInput').value='';}
async function submitCount(){if(!state.currentProduct||!state.sessionId)return;var a=parseInt(document.getElementById('actualInput').value);if(isNaN(a)||a<0){toast('Enter valid count','error');return;}var p=state.currentProduct;var r=await api('/api/count','POST',{sessionId:state.sessionId,productId:p.id,barcode:p.barcode,productName:p.name,systemQty:p.stockQty,actualQty:a,countedBy:state.countedBy});if(r&&r.success){var i=state.counts.findIndex(function(c){return c.productId===p.id;});var e={productId:p.id,productName:p.name,barcode:p.barcode,systemQty:p.stockQty,actualQty:a,variance:a-p.stockQty,adjusted:false};if(i>=0)state.counts[i]=e;else state.counts.push(e);updateStats();renderCountedList();document.getElementById('productCard').classList.add('hidden');focusBarcode();toast('Saved: '+p.name);}else{toast('Failed to save','error');}}
function updateStats(){document.getElementById('statCounted').textContent=state.counts.length;var w=state.counts.filter(function(c){return c.variance!==0;}).length;document.getElementById('statVariance').textContent=w;document.getElementById('statVariance').style.color=w>0?'#f87171':'#4ade80';}
function renderCountedList(){var l=document.getElementById('countedList');if(state.counts.length===0){l.innerHTML='<p style=""color:#64748b;font-size:13px;text-align:center;padding:20px"">No items counted yet</p>';return;}var h='';state.counts.forEach(function(c){var v=c.variance>0?'stock-positive':(c.variance<0?'stock-negative':'stock-zero');var s=c.variance>0?'+':'';h+='<div class=""count-item""><div class=""count-item-name"">'+c.productName+'<small>'+(c.barcode||'')+'</small></div><div class=""count-item-nums""><div class=""sys"">Sys: '+c.systemQty+'</div><div class=""act '+v+'"">'+c.actualQty+' ('+s+c.variance+')</div></div></div>';});l.innerHTML=h;}
function focusBarcode(){setTimeout(function(){document.getElementById('barcodeInput').focus();},100);}
async function endSession(){if(!state.sessionId||state.counts.length===0){toast('Nothing to end','error');return;}if(!confirm('End this session and adjust stock?'))return;var r=await api('/api/session/end','POST',{sessionId:state.sessionId});if(r&&r.success){document.getElementById('mainScreen').classList.add('hidden');document.getElementById('endScreen').classList.remove('hidden');document.getElementById('bottomNav').classList.add('hidden');document.getElementById('endTotal').textContent=r.counts?r.counts.length:0;document.getElementById('endVariance').textContent=r.counts?r.counts.filter(function(c){return c.variance!==0;}).length:0;document.getElementById('endReport').textContent=r.report||'';toast('Stock adjusted!');}else{toast(r?r.error:'Failed','error');}}
function backToMain(){document.getElementById('endScreen').classList.add('hidden');document.getElementById('mainScreen').classList.remove('hidden');document.getElementById('bottomNav').classList.remove('hidden');state.sessionId=null;state.counts=[];state.currentProduct=null;checkActiveSession();focusBarcode();}
function copyReport(){var r=document.getElementById('endReport').textContent;if(!r)return;if(navigator.clipboard){navigator.clipboard.writeText(r).then(function(){toast('Copied!');});}else{var t=document.createElement('textarea');t.value=r;document.body.appendChild(t);t.select();document.execCommand('copy');t.remove();toast('Copied!');}}
async function refreshSession(){if(!state.sessionId)return;var r=await api('/api/session/'+state.sessionId);if(r&&r.counts){state.counts=r.counts;updateStats();renderCountedList();toast('Refreshed!');}}
function adjustAndClose(){backToMain();}
</script>
</body>
</html>";
    }
}
