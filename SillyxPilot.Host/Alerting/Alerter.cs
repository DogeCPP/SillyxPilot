using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace SillyxPilot.Plugin.Alerting;

public record AlertField(string Name, string Value);
public record AlertResult(List<string> Channels, List<string> Errors);

public sealed class Alerter
{
    private readonly Func<AppConfig> _getConfig;
    private readonly Dictionary<string, DateTime> _lastFired = new();

    public Alerter(Func<AppConfig> getConfig) => _getConfig = getConfig;

    private bool Dedupe(string key, int seconds)
    {
        var now = DateTime.UtcNow;
        if (_lastFired.TryGetValue(key, out var last) && (now - last).TotalSeconds < seconds) return false;
        _lastFired[key] = now;
        return true;
    }

    public AlertResult Fire(string category, AlertRule rule, string title, string description, List<AlertField> fields)
    {
        var cfg = _getConfig();
        if (!Dedupe($"{category}:{title}", cfg.Alerts.DedupeSeconds))
            return new AlertResult(new List<string>(), new List<string>());

        var channels = new List<string>();
        var errors = new List<string>();

        if (rule.Discord && !string.IsNullOrEmpty(cfg.Discord.WebhookUrl))
        {
            try { PostDiscord(cfg.Discord.WebhookUrl, category, title, description, fields); channels.Add("discord"); }
            catch (Exception ex) { errors.Add($"discord: {ex.Message}"); }
        }

        if (rule.Email && cfg.Email.Enabled && !string.IsNullOrEmpty(cfg.Email.Address))
            errors.Add("email: not available in xPilot plugin runtime");

        if (rule.Desktop) channels.Add("desktop");
        if (rule.Sound) channels.Add("sound");

        return new AlertResult(channels, errors);
    }

    private static int ColorFor(string category) => category switch
    {
        "supervisor" => 0xff3b30,
        "atc" => 0x00b0d8,
        "private" => 0x4a90ff,
        _ => 0xf5a623,
    };

    private static void PostDiscord(string webhookUrl, string category, string title, string description, List<AlertField> fields)
    {
        var embed = new JsonObject
        {
            ["title"] = title,
            ["description"] = description,
            ["color"] = ColorFor(category),
            ["fields"] = new JsonArray(fields.Select(f => (JsonNode)new JsonObject
            {
                ["name"] = f.Name, ["value"] = f.Value, ["inline"] = true,
            }).ToArray()),
            ["timestamp"] = Runtime.IsoNow(),
            ["footer"] = new JsonObject { ["text"] = "SillyPilot Awareness System" },
        };
        var body = new JsonObject
        {
            ["username"] = "SillyxPilot",
            ["embeds"] = new JsonArray(embed),
        };
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var res = Runtime.Http.PostAsync(webhookUrl, content).GetAwaiter().GetResult();
        if (!res.IsSuccessStatusCode) throw new Exception($"HTTP {(int)res.StatusCode}");
    }

    public Dictionary<string, string> Test()
    {
        var cfg = _getConfig();
        var results = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(cfg.Discord.WebhookUrl))
        {
            try
            {
                PostDiscord(cfg.Discord.WebhookUrl, "default", "SillyxPilot test alert",
                    "If you can read this, Discord alerts work.", new List<AlertField> { new("Status", "OK") });
                results["discord"] = "ok";
            }
            catch (Exception ex) { results["discord"] = ex.Message; }
        }
        if (cfg.Email.Enabled) results["email"] = "not available in xPilot plugin runtime";
        return results;
    }
}
