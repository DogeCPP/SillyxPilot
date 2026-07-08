using System;
using System.IO;
using System.Net.Http;
using System.Text;

namespace SillyxPilot.Plugin;

public static class Runtime
{

    public static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var handler = new SocketsHttpHandler();
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
    }

    public static string ReadText(string path) => Encoding.UTF8.GetString(File.ReadAllBytes(path));

    public static int Recv(System.Net.Sockets.Socket s, byte[] buf)
    {
        try { return RecvPlain(s, buf); } catch (MissingMethodException) { } catch (TypeLoadException) { }
        try { return RecvOffset(s, buf); } catch (MissingMethodException) { } catch (TypeLoadException) { }
        return RecvFlags(s, buf);
    }

    private static int RecvPlain(System.Net.Sockets.Socket s, byte[] buf) => s.Receive(buf);
    private static int RecvOffset(System.Net.Sockets.Socket s, byte[] buf) => s.Receive(buf, 0, buf.Length, System.Net.Sockets.SocketFlags.None);
    private static int RecvFlags(System.Net.Sockets.Socket s, byte[] buf) => s.Receive(buf, System.Net.Sockets.SocketFlags.None);

    public static void SendAll(System.Net.Sockets.Socket s, byte[] buf)
    {
        try { SendPlain(s, buf); return; } catch (MissingMethodException) { } catch (TypeLoadException) { }
        try { SendOffset(s, buf); return; } catch (MissingMethodException) { } catch (TypeLoadException) { }
        SendFlags(s, buf);
    }

    private static void SendPlain(System.Net.Sockets.Socket s, byte[] buf) => s.Send(buf);
    private static void SendOffset(System.Net.Sockets.Socket s, byte[] buf)
    {
        int sent = 0;
        while (sent < buf.Length) sent += s.Send(buf, sent, buf.Length - sent, System.Net.Sockets.SocketFlags.None);
    }
    private static void SendFlags(System.Net.Sockets.Socket s, byte[] buf) => s.Send(buf, System.Net.Sockets.SocketFlags.None);

    public static string[] SplitChar(string s, char sep)
    {
        var parts = new System.Collections.Generic.List<string>();
        int start = 0;
        while (true)
        {
            int idx = s.IndexOf(sep, start);
            if (idx < 0) { parts.Add(s.Substring(start)); break; }
            parts.Add(s.Substring(start, idx - start));
            start = idx + 1;
        }
        return parts.ToArray();
    }

    public static string Pad(int n, int width)
    {
        var s = n.ToString();
        while (s.Length < width) s = "0" + s;
        return s;
    }

    public static string IsoNow() => Iso(DateTime.UtcNow);

    public static string Iso(DateTime dt)
    {
        dt = dt.ToUniversalTime();
        return Pad(dt.Year, 4) + "-" + Pad(dt.Month, 2) + "-" + Pad(dt.Day, 2) + "T" +
               Pad(dt.Hour, 2) + ":" + Pad(dt.Minute, 2) + ":" + Pad(dt.Second, 2) + "." +
               Pad(dt.Millisecond, 3) + "Z";
    }

    public static bool TryReadText(string path, out string text)
    {
        try { text = ReadText(path); return true; }
        catch { text = ""; return false; }
    }
}
