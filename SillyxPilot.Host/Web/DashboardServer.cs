using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;

namespace SillyxPilot.Plugin.Web;

public sealed class DashboardServer : IDisposable
{
    private readonly string _wwwroot;
    private readonly Func<string, HttpApiResult> _apiGet;
    private readonly Func<string, string, HttpApiResult> _apiPost;
    private readonly Func<string> _snapshotJson;

    private Socket? _listener;
    private Socket? _listener6;
    private readonly object _sseLock = new();
    private readonly Dictionary<Guid, Socket> _sseClients = new();
    private readonly List<Timer> _workers = new();
    private volatile bool _running;

    public int Port { get; private set; }

    public DashboardServer(string wwwroot, Func<string, HttpApiResult> apiGet,
        Func<string, string, HttpApiResult> apiPost, Func<string> snapshotJson)
    {
        _wwwroot = wwwroot;
        _apiGet = apiGet;
        _apiPost = apiPost;
        _snapshotJson = snapshotJson;
    }

    public void Start(int preferredPort)
    {
        var port = preferredPort;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            try
            {
                var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                s.Bind(new IPEndPoint(IPAddress.Loopback, port));
                s.Listen(64);
                _listener = s;
                Port = port;
                break;
            }
            catch (SocketException)
            {
                port++;
                if (attempt == 11) throw;
            }
        }

        try
        {
            var s6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            s6.Bind(new IPEndPoint(IPAddress.IPv6Loopback, Port));
            s6.Listen(64);
            _listener6 = s6;
        }
        catch { _listener6 = null; }

        _running = true;

        for (int i = 0; i < 6; i++)
            _workers.Add(new Timer(_ => AcceptLoop(_listener), null, 0, Timeout.Infinite));
        if (_listener6 != null)
            for (int i = 0; i < 6; i++)
                _workers.Add(new Timer(_ => AcceptLoop(_listener6), null, 0, Timeout.Infinite));
    }

    private void AcceptLoop(Socket? listener)
    {
        while (_running && listener != null)
        {
            Socket client;
            try { client = listener.Accept(); }
            catch { break; }

            try { HandleConnection(client); } catch { CloseQuiet(client); }
        }
    }

    private static bool WaitReadable(Socket s, int slices)
    {
        try { return WaitReadableCore(s, slices); }
        catch { return true; }
    }

    private static bool WaitReadableCore(Socket s, int slices)
    {
        for (int i = 0; i < slices; i++)
        {
            if (s.Poll(250_000, SelectMode.SelectRead)) return true;
        }
        return false;
    }

    private void HandleConnection(Socket socket)
    {
        try
        {
            var req = ReadRequest(socket);
            if (req == null) { CloseQuiet(socket); return; }

            if (req.Path == "/events")
            {
                HandleSse(socket);
                return;
            }

            if (req.Path.StartsWith("/api/"))
            {
                HandleApi(socket, req);
            }
            else
            {
                ServeStatic(socket, req.Path);
            }
        }
        catch (Exception ex)
        {

            CloseQuiet(socket);

            try
            {
                File.AppendAllText(Path.Combine(AppPaths.SillyDataDir(), "diag.txt"),
                    "HandleConnection error: " + ex + "\n");
            }
            catch { }
        }
    }

    private sealed class Request
    {
        public string Method = "GET";
        public string Path = "/";
        public string Body = "";
    }

    private static Request? ReadRequest(Socket socket)
    {
        var buffer = new byte[16384];
        var received = new List<byte>();
        int headerEnd = -1;
        const int deadlineSlices = 20;

        while (headerEnd < 0)
        {

            if (!WaitReadable(socket, deadlineSlices)) return null;
            int n;
            try { n = SillyxPilot.Plugin.Runtime.Recv(socket, buffer); } catch { return null; }
            if (n <= 0) return null;
            for (int i = 0; i < n; i++) received.Add(buffer[i]);
            headerEnd = FindDoubleCrlf(received);
            if (received.Count > 1_000_000) return null;
        }

        var headerText = Encoding.UTF8.GetString(received.ToArray(), 0, headerEnd);

        var lines = SillyxPilot.Plugin.Runtime.SplitChar(headerText, '\n');
        var parts = SillyxPilot.Plugin.Runtime.SplitChar(lines[0].TrimEnd('\r'), ' ');
        if (parts.Length < 2) return null;

        var req = new Request { Method = parts[0], Path = DecodePath(parts[1]) };

        int contentLength = 0;
        for (int li = 1; li < lines.Length; li++)
        {
            var line = lines[li].TrimEnd('\r');
            var idx = line.IndexOf(':');
            if (idx < 0) continue;
            var key = line[..idx].Trim();
            var val = line[(idx + 1)..].Trim();
            if (string.Equals(key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                int.TryParse(val, out contentLength);
        }

        var bodyBytes = new List<byte>();
        for (int i = headerEnd + 4; i < received.Count; i++) bodyBytes.Add(received[i]);
        while (bodyBytes.Count < contentLength)
        {
            if (!WaitReadable(socket, deadlineSlices)) break;
            int n;
            try { n = SillyxPilot.Plugin.Runtime.Recv(socket, buffer); } catch { break; }
            if (n <= 0) break;
            for (int i = 0; i < n; i++) bodyBytes.Add(buffer[i]);
        }
        req.Body = Encoding.UTF8.GetString(bodyBytes.ToArray(), 0, bodyBytes.Count);
        return req;
    }

    private static int FindDoubleCrlf(List<byte> b)
    {
        for (int i = 0; i + 3 < b.Count; i++)
            if (b[i] == 13 && b[i + 1] == 10 && b[i + 2] == 13 && b[i + 3] == 10) return i;
        return -1;
    }

    private static string DecodePath(string raw)
    {
        var q = raw.IndexOf('?');
        var pathPart = q >= 0 ? raw[..q] : raw;
        var query = q >= 0 ? raw[q..] : "";
        return Uri.UnescapeDataString(pathPart) + query;
    }

    private void HandleApi(Socket socket, Request req)
    {
        HttpApiResult result;
        try
        {
            result = req.Method == "POST" ? _apiPost(req.Path, req.Body) : _apiGet(req.Path);
        }
        catch
        {
            result = new HttpApiResult(500, "{\"error\":\"server error\"}");
        }

        WriteResponse(socket, result.StatusCode, "application/json", Encoding.UTF8.GetBytes(result.Body ?? "{}"));
        CloseQuiet(socket);
    }

    private static readonly Dictionary<string, string> Mime = new()
    {
        [".html"] = "text/html; charset=utf-8", [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8", [".json"] = "application/json",
        [".svg"] = "image/svg+xml", [".ico"] = "image/x-icon", [".png"] = "image/png",
        [".wav"] = "audio/wav", [".woff2"] = "font/woff2",
    };

    private void ServeStatic(Socket socket, string pathWithQuery)
    {
        var qi = pathWithQuery.IndexOf('?');
        var path = qi >= 0 ? pathWithQuery.Substring(0, qi) : pathWithQuery;
        var rel = path == "/" ? "/index.html" : path;
        string full;

        if (rel.StartsWith("/sounds/"))
        {
            full = Path.Combine(AppPaths.XpilotSoundsDir(), rel["/sounds/".Length..]);
        }
        else
        {

            var relNoSlash = rel;
            while (relNoSlash.Length > 0 && relNoSlash[0] == '/') relNoSlash = relNoSlash.Substring(1);
            full = Path.GetFullPath(Path.Combine(_wwwroot, relNoSlash));
            if (!full.StartsWith(_wwwroot, StringComparison.OrdinalIgnoreCase))
            {
                WriteResponse(socket, 403, "text/plain", Encoding.UTF8.GetBytes("forbidden"));
                CloseQuiet(socket);
                return;
            }
        }

        if (!File.Exists(full))
        {
            WriteResponse(socket, 404, "text/plain", Encoding.UTF8.GetBytes("not found"));
            CloseQuiet(socket);
            return;
        }

        var ext = Path.GetExtension(full).ToLowerInvariant();
        var mime = Mime.TryGetValue(ext, out var m) ? m : "application/octet-stream";
        WriteResponse(socket, 200, mime, File.ReadAllBytes(full));
        CloseQuiet(socket);
    }

    private void HandleSse(Socket socket)
    {
        var headers =
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/event-stream\r\n" +
            "Cache-Control: no-cache\r\n" +
            "Connection: keep-alive\r\n" +
            "Access-Control-Allow-Origin: *\r\n\r\n";
        try { SillyxPilot.Plugin.Runtime.SendAll(socket, Encoding.UTF8.GetBytes(headers)); }
        catch { CloseQuiet(socket); return; }

        var id = Guid.NewGuid();
        lock (_sseLock) { _sseClients[id] = socket; }

        try { SendSse(socket, _snapshotJson()); }
        catch { lock (_sseLock) { _sseClients.Remove(id); } CloseQuiet(socket); }
    }

    private static void SendSse(Socket socket, string json)
    {

        var frame = "data: " + json + "\n\n";
        SillyxPilot.Plugin.Runtime.SendAll(socket, Encoding.UTF8.GetBytes(frame));
    }

    public void Broadcast(JsonObject payload)
    {
        var json = payload.ToJsonString();
        List<KeyValuePair<Guid, Socket>> clients;
        lock (_sseLock) { clients = _sseClients.ToList(); }
        var dead = new List<Guid>();
        foreach (var kv in clients)
        {
            try { SendSse(kv.Value, json); }
            catch { dead.Add(kv.Key); CloseQuiet(kv.Value); }
        }
        if (dead.Count > 0)
            lock (_sseLock) { foreach (var id in dead) _sseClients.Remove(id); }
    }

    private static void WriteResponse(Socket socket, int status, string contentType, byte[] body)
    {
        var reason = status switch { 200 => "OK", 400 => "Bad Request", 403 => "Forbidden", 404 => "Not Found", 500 => "Internal Server Error", _ => "OK" };
        var head =
            $"HTTP/1.1 {status} {reason}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Connection: close\r\n\r\n";
        try
        {
            SillyxPilot.Plugin.Runtime.SendAll(socket, Encoding.UTF8.GetBytes(head));
            if (body.Length > 0) SillyxPilot.Plugin.Runtime.SendAll(socket, body);
        }
        catch {  }
    }

    private static void CloseQuiet(Socket socket)
    {
        try { socket.Shutdown(SocketShutdown.Both); } catch { }
        try { socket.Close(); } catch { }
    }

    public void Stop()
    {
        _running = false;
        foreach (var w in _workers) { try { w.Dispose(); } catch { } }
        _workers.Clear();
        lock (_sseLock)
        {
            foreach (var kv in _sseClients) CloseQuiet(kv.Value);
            _sseClients.Clear();
        }
        try { _listener?.Close(); } catch { }
        try { _listener6?.Close(); } catch { }
    }

    public void Dispose() => Stop();
}

public record HttpApiResult(int StatusCode, string? Body)
{
    public static HttpApiResult Ok(string body) => new(200, body);
    public static HttpApiResult NotFound() => new(404, "{\"error\":\"not found\"}");
    public static HttpApiResult BadRequest(string msg) =>
        new(400, new JsonObject { ["error"] = msg }.ToJsonString());
}
