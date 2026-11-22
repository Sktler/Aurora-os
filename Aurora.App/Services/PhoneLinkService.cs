using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aurora.App.Models;

namespace Aurora.App.Services
{
    /// <summary>
    /// Opt-in Wi-Fi link for a phone. Serves a small chat web page on the local network.
    /// A phone pairs by scanning a QR code or typing a PIN; chat only, no tools or computer control.
    /// </summary>
    public sealed class PhoneLinkService : IDisposable
    {
        public const int DefaultPort = 5151;
        private const int MaxFailures = 5;
        private static readonly TimeSpan PinLifetime = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(1);
        private const int MaxBodyBytes = 16 * 1024;

        private readonly Func<string, IReadOnlyList<ChatMessage>, Task<string>> _chat;
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _sessions = new();
        private readonly object _pinLock = new();
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private string _pin = "";
        private DateTimeOffset _pinExpires;
        private int _failures;
        private DateTimeOffset _lockedUntil;

        public PhoneLinkService(Func<string, IReadOnlyList<ChatMessage>, Task<string>> chat) => _chat = chat;

        public bool IsRunning => _listener != null;
        public int Port { get; private set; }
        public event EventHandler? PairedDevicesChanged;
        public int PairedDeviceCount => _sessions.Count;

        public static IReadOnlyList<string> GetLanAddresses() =>
            NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a) && !a.ToString().StartsWith("169.254."))
                .Select(a => a.ToString())
                .Distinct()
                .ToList();

        public string CurrentPin
        {
            get
            {
                lock (_pinLock)
                {
                    if (DateTimeOffset.UtcNow >= _pinExpires) RotatePinLocked();
                    return _pin;
                }
            }
        }

        public string? PairingUrl(string host) =>
            IsRunning ? $"http://{host}:{Port}/#pin={CurrentPin}" : null;

        public void Start(int port = DefaultPort)
        {
            if (_listener != null) return;
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            _listener = listener;
            Port = port;
            _cts = new CancellationTokenSource();
            lock (_pinLock) RotatePinLocked();
            _ = AcceptLoopAsync(listener, _cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
            _listener = null;
            _sessions.Clear();
            lock (_pinLock) { _pin = ""; }
            PairedDevicesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RevokeAll()
        {
            _sessions.Clear();
            PairedDevicesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => Stop();

        private void RotatePinLocked()
        {
            _pin = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            _pinExpires = DateTimeOffset.UtcNow + PinLifetime;
        }

        /// <summary>Returns a session token when the PIN matches, otherwise null. Wrong guesses are rate limited.</summary>
        public string? TryPair(string pin)
        {
            lock (_pinLock)
            {
                if (!IsRunning || DateTimeOffset.UtcNow < _lockedUntil) return null;
                if (DateTimeOffset.UtcNow >= _pinExpires) RotatePinLocked();
                var ok = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(_pin), Encoding.UTF8.GetBytes(pin ?? ""));
                if (!ok)
                {
                    if (++_failures >= MaxFailures)
                    {
                        _failures = 0;
                        _lockedUntil = DateTimeOffset.UtcNow + LockoutDuration;
                        RotatePinLocked();
                    }
                    return null;
                }
                _failures = 0;
                RotatePinLocked();
            }
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _sessions[token] = new List<ChatMessage>();
            PairedDevicesChanged?.Invoke(this, EventArgs.Empty);
            return token;
        }

        private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(ct); }
                catch { return; }
                _ = Task.Run(() => HandleAsync(client, ct), ct);
            }
        }

        private async Task HandleAsync(TcpClient client, CancellationToken ct)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = client.SendTimeout = 60000;
                    var stream = client.GetStream();
                    var (method, path, headers, body) = await ReadRequestAsync(stream, ct);
                    var (status, contentType, payload) = await RouteAsync(method, path, headers, body);
                    var bytes = Encoding.UTF8.GetBytes(payload);
                    var head = $"HTTP/1.1 {status}\r\nContent-Type: {contentType}; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(head), ct);
                    await stream.WriteAsync(bytes, ct);
                }
                catch { }
            }
        }

        private static async Task<(string Method, string Path, Dictionary<string, string> Headers, string Body)> ReadRequestAsync(NetworkStream stream, CancellationToken ct)
        {
            var buffer = new List<byte>();
            var one = new byte[1];
            while (buffer.Count < 8192)
            {
                if (await stream.ReadAsync(one, ct) == 0) throw new IOException();
                buffer.Add(one[0]);
                var n = buffer.Count;
                if (n >= 4 && buffer[n - 4] == '\r' && buffer[n - 3] == '\n' && buffer[n - 2] == '\r' && buffer[n - 1] == '\n') break;
            }
            var lines = Encoding.ASCII.GetString(buffer.ToArray()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            var first = lines[0].Split(' ');
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines.Skip(1))
            {
                var i = line.IndexOf(':');
                if (i > 0) headers[line[..i].Trim()] = line[(i + 1)..].Trim();
            }
            var body = "";
            if (headers.TryGetValue("Content-Length", out var len) && int.TryParse(len, out var length) && length > 0)
            {
                if (length > MaxBodyBytes) throw new IOException("Body too large");
                var data = new byte[length];
                var read = 0;
                while (read < length)
                {
                    var r = await stream.ReadAsync(data.AsMemory(read), ct);
                    if (r == 0) throw new IOException();
                    read += r;
                }
                body = Encoding.UTF8.GetString(data);
            }
            return (first[0], first.Length > 1 ? first[1] : "/", headers, body);
        }

        private async Task<(string Status, string ContentType, string Body)> RouteAsync(string method, string path, Dictionary<string, string> headers, string body)
        {
            path = path.Split('?')[0];
            if (method == "GET" && path == "/") return ("200 OK", "text/html", PhoneLinkPage.Html);
            if (method != "POST") return ("404 Not Found", "text/plain", "Not found");

            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                if (path == "/pair")
                {
                    var pin = doc.RootElement.TryGetProperty("pin", out var p) ? p.GetString() : "";
                    var token = TryPair(pin ?? "");
                    return token == null
                        ? ("403 Forbidden", "application/json", "{\"error\":\"Wrong or expired PIN, or too many attempts. Try again in a minute.\"}")
                        : ("200 OK", "application/json", JsonSerializer.Serialize(new { token }));
                }
                if (path == "/chat")
                {
                    var auth = headers.TryGetValue("Authorization", out var a) ? a : "";
                    var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth[7..].Trim() : "";
                    if (!_sessions.TryGetValue(token, out var history))
                        return ("401 Unauthorized", "application/json", "{\"error\":\"Not paired.\"}");
                    var message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "";
                    if (string.IsNullOrWhiteSpace(message)) return ("400 Bad Request", "application/json", "{\"error\":\"Empty message.\"}");
                    if (message.Length > 4000) message = message[..4000];
                    string reply;
                    List<ChatMessage> snapshot;
                    lock (history) snapshot = history.ToList();
                    reply = await _chat(message, snapshot);
                    lock (history)
                    {
                        history.Add(new ChatMessage { Role = "user", Content = message });
                        history.Add(new ChatMessage { Role = "assistant", Content = reply });
                        if (history.Count > 20) history.RemoveRange(0, history.Count - 20);
                    }
                    return ("200 OK", "application/json", JsonSerializer.Serialize(new { reply }));
                }
            }
            catch (JsonException) { return ("400 Bad Request", "application/json", "{\"error\":\"Bad request.\"}"); }
            catch (Exception ex) { return ("500 Internal Server Error", "application/json", JsonSerializer.Serialize(new { error = ex.Message })); }
            return ("404 Not Found", "text/plain", "Not found");
        }
    }

    internal static class PhoneLinkPage
    {
        public const string Html = """
<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Aurora</title>
<style>
body{margin:0;font-family:Segoe UI,system-ui,sans-serif;background:#0B0E14;color:#fff;display:flex;flex-direction:column;height:100vh}
header{padding:14px 16px;font-size:18px;font-weight:600;background:#12161F}
#log{flex:1;overflow:auto;padding:12px}
.m{max-width:85%;margin:6px 0;padding:10px 12px;border-radius:12px;white-space:pre-wrap;line-height:1.35}
.u{background:#4FD8E8;color:#0B0E14;margin-left:auto}.a{background:#171C28}
form{display:flex;gap:8px;padding:10px;background:#12161F}
input{flex:1;font-size:16px;padding:12px;border-radius:8px;border:1px solid #4FD8E8;background:#171C28;color:#fff}
button{font-size:16px;padding:12px 16px;border-radius:8px;border:0;background:#4FD8E8;color:#0B0E14;font-weight:600}
#pair{padding:24px;text-align:center}#pair input{width:70%;text-align:center;letter-spacing:6px;font-size:24px}
#err{color:#F0745C;margin-top:10px}
</style></head><body>
<header>Aurora</header>
<div id="pair"><p>Enter the 6-digit PIN shown on your PC.</p>
<input id="pin" inputmode="numeric" maxlength="6" autocomplete="off"><br><br><button id="go">Connect</button><div id="err"></div></div>
<div id="chat" style="display:none;flex:1;flex-direction:column;min-height:0"><div id="log"></div>
<form id="f"><input id="t" placeholder="Message Aurora" autocomplete="off"><button>Send</button></form></div>
<script>
let token=sessionStorage.getItem('t');
const $=id=>document.getElementById(id);
function show(){ $('pair').style.display='none'; $('chat').style.display='flex'; }
function add(c,t){const d=document.createElement('div');d.className='m '+c;d.textContent=t;$('log').appendChild(d);$('log').scrollTop=1e9;return d}
async function pair(pin){
  const r=await fetch('/pair',{method:'POST',body:JSON.stringify({pin})});
  const j=await r.json();
  if(!r.ok){$('err').textContent=j.error||'Could not connect.';return}
  token=j.token;sessionStorage.setItem('t',token);history.replaceState(null,'',location.pathname);show();add('a','Connected. Ask me anything.');
}
$('go').onclick=()=>pair($('pin').value.trim());
$('f').onsubmit=async e=>{e.preventDefault();const t=$('t').value.trim();if(!t)return;$('t').value='';add('u',t);const w=add('a','…');
  try{const r=await fetch('/chat',{method:'POST',headers:{Authorization:'Bearer '+token},body:JSON.stringify({message:t})});
  const j=await r.json();w.textContent=j.reply||j.error||'No reply.';if(r.status==401){sessionStorage.removeItem('t');location.reload()}}
  catch{w.textContent='Lost connection to your PC.'}};
const m=location.hash.match(/pin=(\d{6})/);
if(token)show();else if(m)pair(m[1]);
</script></body></html>
""";
    }
}
