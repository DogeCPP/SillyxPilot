using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SillyxPilot.Plugin;

public sealed class Store
{
    private const int MaxRecent = 1000;
    private readonly string _chatFile;
    private readonly string _alertFile;
    private readonly List<JsonObject> _messages = new();
    private readonly List<JsonObject> _alerts = new();
    private long _nextId;

    public Store()
    {
        var dataDir = Path.Combine(AppPaths.SillyDataDir(), "data");
        Directory.CreateDirectory(dataDir);
        _chatFile = Path.Combine(dataDir, "chatlog.jsonl");
        _alertFile = Path.Combine(dataDir, "alerts.jsonl");
        _messages.AddRange(LoadTail(_chatFile, MaxRecent));
        _alerts.AddRange(LoadTail(_alertFile, 200));
        _nextId = _messages.Concat(_alerts).Select(m => m["id"]?.GetValue<long>() ?? 0).DefaultIfEmpty(0).Max();
    }

    private static List<JsonObject> LoadTail(string file, int max)
    {
        if (!File.Exists(file)) return new List<JsonObject>();
        try
        {

            var lines = Runtime.SplitChar(Runtime.ReadText(file), '\n').Where(l => l.Trim().Length > 0).ToList();
            var tail = lines.Skip(Math.Max(0, lines.Count - max));
            return tail.Select(l => { try { return JsonNode.Parse(l)?.AsObject(); } catch { return null; } })
                .Where(o => o != null).Select(o => o!).ToList();
        }
        catch { return new List<JsonObject>(); }
    }

    public JsonObject AddMessage(JsonObject entry)
    {
        entry["id"] = ++_nextId;
        entry["at"] = Runtime.IsoNow();
        _messages.Add(entry);
        if (_messages.Count > MaxRecent) _messages.RemoveAt(0);
        AppendLine(_chatFile, entry);
        return entry;
    }

    public JsonObject AddAlert(JsonObject entry)
    {
        entry["id"] = ++_nextId;
        entry["at"] = Runtime.IsoNow();
        _alerts.Add(entry);
        if (_alerts.Count > 200) _alerts.RemoveAt(0);
        AppendLine(_alertFile, entry);
        return entry;
    }

    private static void AppendLine(string file, JsonObject entry)
    {
        try { File.AppendAllText(file, entry.ToJsonString() + "\n"); } catch {  }
    }

    public List<JsonObject> RecentMessages(int n = 300) => _messages.Skip(Math.Max(0, _messages.Count - n)).ToList();
    public List<JsonObject> RecentAlerts(int n = 100) => _alerts.Skip(Math.Max(0, _alerts.Count - n)).ToList();

    public List<JsonObject> Search(string? query, int n = 300)
    {
        if (string.IsNullOrWhiteSpace(query)) return RecentMessages(n);
        var q = query.ToLowerInvariant();
        return _messages.Where(m =>
                (m["from"]?.GetValue<string>() ?? "").ToLowerInvariant().Contains(q) ||
                (m["message"]?.GetValue<string>() ?? "").ToLowerInvariant().Contains(q) ||
                (m["kind"]?.GetValue<string>() ?? "").ToLowerInvariant().Contains(q))
            .TakeLast(n).ToList();
    }
}
