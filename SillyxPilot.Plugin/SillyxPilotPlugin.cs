using System;
using System.IO;
using System.Threading;
using Vatsim.Xpilot.PluginSdk;
using Vatsim.Xpilot.PluginSdk.Events;

namespace SillyxPilot.Plugin;

public sealed class SillyxPilotPlugin : IPlugin
{
    public string Name => "SillyxPilot";

    private IBroker? _broker;
    private HostApp? _app;
    private Timer? _startTimer;

    public void Initialize(IBroker broker)
    {
        _broker = broker;

        AppPaths.Init(ResolvePluginDir());

        _app = new HostApp();

        broker.SessionEnded += OnSessionEnded;
        broker.NetworkConnected += (s, e) => Safe(() => _app.OnNetworkConnected(s, e));
        broker.NetworkDisconnected += (s, e) => Safe(() => _app.OnNetworkDisconnected(s, e));
        broker.PrivateMessageReceived += (s, e) => Safe(() => _app.OnPrivateMessageReceived(s, e));
        broker.RadioMessageReceived += (s, e) => Safe(() => _app.OnRadioMessageReceived(s, e));
        broker.BroadcastMessageReceived += (s, e) => Safe(() => _app.OnBroadcastMessageReceived(s, e));
        broker.ControllerAdded += (s, e) => Safe(() => _app.OnControllerAdded(s, e));
        broker.ControllerDeleted += (s, e) => Safe(() => _app.OnControllerDeleted(s, e));
        broker.ControllerFrequencyChanged += (s, e) => Safe(() => _app.OnControllerFrequencyChanged(s, e));
        broker.AircraftUpdated += (s, e) => Safe(() => _app.OnAircraftUpdated(s, e));

        _startTimer = new Timer(_ => StartApp(), null, 200, Timeout.Infinite);

        broker.PostDebugMessage("SillyxPilot loaded. Starting the dashboard...");
    }

    private void StartApp()
    {
        try { WriteDiag(); } catch { }
        try
        {
            _app!.Start();
            try { SelfTest(_app.Port); } catch { }
            try { _broker?.PostDebugMessage($"SillyxPilot ready. Open your dashboard: {_app.DashboardUrl}"); } catch { }
        }
        catch (Exception ex)
        {
            try { _broker?.PostDebugMessage($"SillyxPilot could not start: {ex.Message}"); } catch { }
        }
    }

    private static void SelfTest(int port)
    {
        string result;
        try
        {
            using var s = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            s.Connect(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, port));
            Runtime.SendAll(s, System.Text.Encoding.UTF8.GetBytes("GET /api/config HTTP/1.1\r\nHost: localhost\r\n\r\n"));
            var buf = new byte[2048];

            if (!s.Poll(8_000_000, System.Net.Sockets.SelectMode.SelectRead))
            {
                result = "SelfTest FAILED: no response within 8s (server accepted but never answered)";
                File.AppendAllText(Path.Combine(AppPaths.SillyDataDir(), "diag.txt"), result + "\n");
                return;
            }
            var n = Runtime.Recv(s, buf);
            var headLen = n > 40 ? 40 : n;
            var head = n > 0 ? System.Text.Encoding.UTF8.GetString(buf, 0, headLen) : "(empty)";
            var nl = head.IndexOf('\n');
            var firstLine = (nl >= 0 ? head.Substring(0, nl) : head).TrimEnd('\r');
            result = "SelfTest GET /api/config: " + n + " bytes, starts '" + firstLine + "'";
        }
        catch (Exception ex)
        {
            result = $"SelfTest FAILED: {ex.GetType().Name} {ex.Message}";
        }
        try { File.AppendAllText(Path.Combine(AppPaths.SillyDataDir(), "diag.txt"), result + "\n"); } catch { }
    }

    private static void WriteDiag()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("SillyxPilot runtime probe\n");
        Probe(sb, "Encoding.UTF8", () => { var _ = System.Text.Encoding.UTF8.GetString(new byte[] { 65 }); });
        Probe(sb, "Encoding.ASCII", () => { var _ = System.Text.Encoding.ASCII.GetString(new byte[] { 65 }); });
        Probe(sb, "Socket.Poll", () =>
        {
            using var s = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            s.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
            s.Listen(1);
            var _ = s.Poll(1000, System.Net.Sockets.SelectMode.SelectRead);
        });
        Probe(sb, "Split(string)", () => { var _ = "a\r\nb".Split("\r\n"); });
        Probe(sb, "Split(char)", () => { var _ = "a b".Split(' '); });
        Probe(sb, "Guid.NewGuid", () => { var _ = Guid.NewGuid(); });
        Probe(sb, "Uri.UnescapeDataString", () => { var _ = Uri.UnescapeDataString("a%20b"); });

        Probe(sb, "UTF8.GetString(b,i,n)", () => { var _ = System.Text.Encoding.UTF8.GetString(new byte[] { 65, 66 }, 0, 2); });
        Probe(sb, "StartsWith(string)", () => { var _ = "abc".StartsWith("a"); });
        Probe(sb, "StartsWith(cmp)", () => { var _ = "abc".StartsWith("A", StringComparison.OrdinalIgnoreCase); });
        Probe(sb, "EndsWith(string)", () => { var _ = "abc".EndsWith("c"); });
        Probe(sb, "string.Equals(cmp)", () => { var _ = string.Equals("a", "A", StringComparison.OrdinalIgnoreCase); });
        Probe(sb, "TrimEnd(char)", () => { var _ = "a\r".TrimEnd('\r'); });
        Probe(sb, "Trim()", () => { var _ = " a ".Trim(); });
        Probe(sb, "int.TryParse", () => { var _ = int.TryParse("5", out var __); });
        Probe(sb, "Path.GetFullPath", () => { var _ = Path.GetFullPath(AppPaths.SillyDataDir()); });
        Probe(sb, "Path.GetExtension", () => { var _ = Path.GetExtension("a.css"); });
        Probe(sb, "ToLowerInvariant", () => { var _ = "A".ToLowerInvariant(); });
        Probe(sb, "ToUpperInvariant", () => { var _ = "a".ToUpperInvariant(); });
        Probe(sb, "IndexOf(char)", () => { var _ = "a:b".IndexOf(':'); });
        Probe(sb, "range slice", () => { var s = "abc"; var _ = s[1..]; });
        Probe(sb, "File.Exists", () => { var _ = File.Exists("nope-not-here"); });
        Probe(sb, "Socket.ReceiveTimeout", () =>
        {
            using var s = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            s.ReceiveTimeout = 1000;
        });

        Probe(sb, "Send/Recv overloads", () =>
        {
            using var l = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            l.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
            l.Listen(1);
            using var c = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            c.Connect(l.LocalEndPoint!);
            using var a = l.Accept();
            var one = new byte[] { 42 };
            var buf = new byte[4];
            Probe(sb, "  Send(byte[])", () => { c.Send(one); });
            Probe(sb, "  Recv(byte[])", () => { var _ = a.Receive(buf); });
            Probe(sb, "  Send(b,i,n,flags)", () => { c.Send(one, 0, 1, System.Net.Sockets.SocketFlags.None); });
            Probe(sb, "  Recv(b,i,n,flags)", () => { var _ = a.Receive(buf, 0, buf.Length, System.Net.Sockets.SocketFlags.None); });
        });
        Probe(sb, "LINQ Skip/ToList<byte>", () => { var l = new System.Collections.Generic.List<byte> { 1, 2, 3 }; var _ = l.Skip(1).ToList(); });
        Probe(sb, "List<byte>.ToArray", () => { var l = new System.Collections.Generic.List<byte> { 1, 2 }; var _ = l.ToArray(); });
        Probe(sb, "DateTime.UtcNow", () => { var _ = DateTime.UtcNow; });
        Probe(sb, "DateTime.AddSeconds", () => { var _ = DateTime.UtcNow.AddSeconds(5); });
        Probe(sb, "DateTime op<", () => { var a = DateTime.UtcNow; var b = a.AddSeconds(1); var _ = a < b; });
        Probe(sb, "Math.Min(int,int)", () => { var _ = Math.Min(1, 2); });
        Probe(sb, "Math.Max(int,int)", () => { var _ = Math.Max(1, 2); });
        Probe(sb, "File.ReadAllBytes", () =>
        {
            var f = Path.Combine(AppPaths.SillyDataDir(), "probe.tmp");
            File.WriteAllText(f, "x");
            var _ = File.ReadAllBytes(f);
            File.Delete(f);
        });
        File.WriteAllText(Path.Combine(AppPaths.SillyDataDir(), "diag.txt"), sb.ToString());
    }

    private static void Probe(System.Text.StringBuilder sb, string name, Action probe)
    {
        try { probe(); sb.Append(name).Append(": OK\n"); }
        catch (Exception ex) { sb.Append(name).Append(": FAIL ").Append(ex.GetType().Name).Append('\n'); }
    }

    private static string ResolvePluginDir()
    {
        try
        {
            var loc = typeof(SillyxPilotPlugin).Assembly.Location;
            var dir = Path.GetDirectoryName(loc);
            if (!string.IsNullOrEmpty(dir)) return dir!;
        }
        catch {  }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "org.vatsim.xpilot", "Plugins");
    }

    private void OnSessionEnded(object? sender, EventArgs e)
    {
        try { _startTimer?.Dispose(); } catch { }
        try { _app?.Stop(); } catch { }
    }

    private static void Safe(Action action)
    {
        try { action(); } catch { }
    }
}
