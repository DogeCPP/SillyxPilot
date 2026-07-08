using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SillyxPilot.Plugin.Alerting;

namespace SillyxPilot.Plugin.Network;

public record NetworkChatMessage(string From, string Text, string At, string? Id, bool Backfill);
public record NetworkStatus(bool Enabled, bool Connected, bool CanReceive, string? Reason, int ActiveCount, List<string> ActiveUsers);

public sealed class DiscordNetwork : IDisposable
{
    private const string Api = "https://discord.com/api/v10";
    private static readonly Regex MsgRe = new(@"^\[([^\]]{1,40})\]\s+Said:\s+\(([\s\S]*)\)\s*$", RegexOptions.Compiled);

    private readonly Func<AppConfig> _getConfig;
    private System.Threading.Timer? _timer;
    private string? _channelId;
    private string? _lastMessageId;
    private readonly Dictionary<string, DateTime> _recentAuthors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _sentFingerprints = new();
    private bool _connected;
    private string? _reason;

    public event EventHandler<NetworkChatMessage>? ChatReceived;
    public event EventHandler<NetworkStatus>? StatusChanged;

    public DiscordNetwork(Func<AppConfig> getConfig) => _getConfig = getConfig;

    public void Start() => Init();
    public void Stop() => _timer?.Dispose();

    public void Reconfigure()
    {
        _timer?.Dispose();
        _channelId = null; _lastMessageId = null; _connected = false;
        Init();
    }

    private void Init()
    {
        var net = _getConfig().Network;
        if (!net.Enabled || string.IsNullOrEmpty(net.WebhookUrl))
        {
            _reason = "disabled";
            RaiseStatus();
            return;
        }

        try
        {
            var json = Runtime.Http.GetStringAsync(net.WebhookUrl).GetAwaiter().GetResult();
            _channelId = JsonNode.Parse(json)?["channel_id"]?.GetValue<string>();
        }
        catch (Exception ex) { _reason = $"webhook lookup: {ex.Message}"; }

        if (!EmbeddedSecrets.HasBotToken())
        {
            _reason = "no bot token (send-only)";
            _connected = false;
            RaiseStatus();
            return;
        }
        if (string.IsNullOrEmpty(_channelId)) { RaiseStatus(); SchedulePoll(); return; }

        Poll(true);
        SchedulePoll();
    }

    private void SchedulePoll()
    {
        var ms = Math.Max(3, _getConfig().Network.PollSeconds) * 1000;
        _timer?.Dispose();
        _timer = new System.Threading.Timer(_ => { try { Poll(false); } catch { } }, null, ms, ms);
    }

    private void Poll(bool backfill)
    {
        if (string.IsNullOrEmpty(_channelId) || !EmbeddedSecrets.HasBotToken()) return;
        var token = EmbeddedSecrets.Get().DiscordBotToken;
        var url = $"{Api}/channels/{_channelId}/messages?limit={(backfill ? 25 : 50)}";
        if (!backfill && _lastMessageId != null) url += $"&after={_lastMessageId}";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bot", token);
            using var res = Runtime.Http.SendAsync(req).GetAwaiter().GetResult();
            if (!res.IsSuccessStatusCode)
            {
                _connected = false;
                _reason = (int)res.StatusCode switch
                {
                    401 => "bot token invalid",
                    403 => "bot lacks channel access",
                    _ => $"HTTP {(int)res.StatusCode}",
                };
                RaiseStatus();
                return;
            }

            _connected = true; _reason = null;
            var json = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (JsonNode.Parse(json) is not JsonArray messages || messages.Count == 0) { RaiseStatus(); return; }

            var newestId = messages[0]?["id"]?.GetValue<string>();
            if (newestId != null && (_lastMessageId == null || ulong.Parse(newestId) > ulong.Parse(_lastMessageId)))
                _lastMessageId = newestId;

            foreach (var m in messages.OfType<JsonObject>().Reverse())
            {
                var parsed = ParseMessage(m);
                if (parsed == null) continue;
                _recentAuthors[parsed.Value.from] = DateTime.UtcNow;
                if (!backfill && IsOwnEcho(parsed.Value.text)) continue;
                var id = m["id"]?.GetValue<string>();
                var ts = m["timestamp"]?.GetValue<string>() ?? Runtime.IsoNow();
                ChatReceived?.Invoke(this, new NetworkChatMessage(parsed.Value.from, parsed.Value.text, ts, id, backfill));
            }
            PruneAuthors();
            RaiseStatus();
        }
        catch (Exception ex)
        {
            _connected = false; _reason = ex.Message;
            RaiseStatus();
        }
    }

    private static (string from, string text)? ParseMessage(JsonObject m)
    {
        var content = m["content"]?.GetValue<string>() ?? "";
        var match = MsgRe.Match(content.Trim());
        if (match.Success) return (match.Groups[1].Value.Trim(), match.Groups[2].Value);

        var author = m["author"] as JsonObject;
        var isBot = author?["bot"]?.GetValue<bool>() ?? false;
        if (isBot) return null;
        if (string.IsNullOrWhiteSpace(content)) return null;
        return (author?["username"]?.GetValue<string>() ?? "discord", content);
    }

    private bool IsOwnEcho(string text)
    {
        var key = text.Trim();
        if (_sentFingerprints.TryGetValue(key, out var t) && (DateTime.UtcNow - t).TotalSeconds < 30)
        {
            _sentFingerprints.Remove(key);
            return true;
        }
        return false;
    }

    private void PruneAuthors()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        foreach (var name in _recentAuthors.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList())
            _recentAuthors.Remove(name);
    }

    public (bool ok, string? error) Send(string name, string message)
    {
        var net = _getConfig().Network;
        if (string.IsNullOrEmpty(net.WebhookUrl)) return (false, "no webhook configured");
        var text = message.Length > 1500 ? message[..1500] : message;
        var content = $"[{name}] Said: ({text})";
        _sentFingerprints[text.Trim()] = DateTime.UtcNow;

        try
        {
            var body = new JsonObject { ["content"] = content, ["allowed_mentions"] = new JsonObject { ["parse"] = new JsonArray() } };
            using var sc = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using var res = Runtime.Http.PostAsync(net.WebhookUrl, sc).GetAwaiter().GetResult();
            if (!res.IsSuccessStatusCode) return (false, $"HTTP {(int)res.StatusCode}");
            _recentAuthors[name] = DateTime.UtcNow;
            return (true, null);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private void RaiseStatus() => StatusChanged?.Invoke(this, Status());

    public NetworkStatus Status() => new(
        _getConfig().Network.Enabled, _connected, EmbeddedSecrets.HasBotToken(), _reason,
        _recentAuthors.Count, _recentAuthors.Keys.ToList());

    public void Dispose() => _timer?.Dispose();
}
