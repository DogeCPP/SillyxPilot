using System.IO;
using System.Text.Json.Nodes;

namespace SillyxPilot.Plugin;

public class AlertRule
{
    public bool Discord { get; set; }
    public bool Email { get; set; }
    public bool Desktop { get; set; } = true;
    public bool Sound { get; set; } = true;
}

public class AlertsConfig
{
    public AlertRule SupervisorPanic { get; set; } = new() { Discord = true, Email = true };
    public AlertRule AtcPrivateMessage { get; set; } = new() { Email = true };
    public AlertRule AnyPrivateMessage { get; set; } = new();
    public int DedupeSeconds { get; set; } = 120;
}

public class DiscordAlertConfig { public string WebhookUrl { get; set; } = ""; }
public class EmailConfig { public bool Enabled { get; set; } public string Address { get; set; } = ""; }
public class NetworkConfig
{
    public bool Enabled { get; set; } = true;
    public string WebhookUrl { get; set; } = "";
    public string Handle { get; set; } = "";
    public int PollSeconds { get; set; } = 5;
}
public class MapConfig { public double TrafficRadiusNm { get; set; } = 400; }
public class VatsimConfig { public int PollSeconds { get; set; } = 60; }

public class AppConfig
{
    public int Port { get; set; } = 3000;
    public bool OpenBrowserOnStart { get; set; } = true;
    public string CallsignOverride { get; set; } = "";
    public VatsimConfig Vatsim { get; set; } = new();
    public AlertsConfig Alerts { get; set; } = new();
    public DiscordAlertConfig Discord { get; set; } = new();
    public EmailConfig Email { get; set; } = new();
    public NetworkConfig Network { get; set; } = new();
    public MapConfig Map { get; set; } = new();
}

public static class ConfigStore
{
    private static string ConfigPath => Path.Combine(AppPaths.SillyDataDir(), "config.json");
    private static AppConfig? _cache;

    public static AppConfig Load()
    {
        if (_cache != null) return _cache;
        if (File.Exists(ConfigPath) && Runtime.TryReadText(ConfigPath, out var text))
        {
            try { _cache = FromJson(JsonNode.Parse(text)?.AsObject()); }
            catch { _cache = new AppConfig(); }
        }
        else
        {
            _cache = new AppConfig();
            Save(_cache);
        }
        return _cache!;
    }

    public static void Save(AppConfig cfg)
    {
        _cache = cfg;
        Directory.CreateDirectory(AppPaths.SillyDataDir());
        File.WriteAllText(ConfigPath, ToJson(cfg).ToJsonString());
    }

    private static bool B(JsonObject? o, string k, bool d) => o?[k]?.GetValue<bool>() ?? d;
    private static int I(JsonObject? o, string k, int d) => o?[k]?.GetValue<int>() ?? d;
    private static double D(JsonObject? o, string k, double d) => o?[k]?.GetValue<double>() ?? d;
    private static string S(JsonObject? o, string k, string d) => o?[k]?.GetValue<string>() ?? d;

    private static AlertRule RuleFrom(JsonObject? o, AlertRule def) => o == null ? def : new AlertRule
    {
        Discord = B(o, "discord", def.Discord),
        Email = B(o, "email", def.Email),
        Desktop = B(o, "desktop", def.Desktop),
        Sound = B(o, "sound", def.Sound),
    };

    private static AppConfig FromJson(JsonObject? root)
    {
        var c = new AppConfig();
        if (root == null) return c;
        c.Port = I(root, "port", c.Port);
        c.OpenBrowserOnStart = B(root, "openBrowserOnStart", c.OpenBrowserOnStart);
        c.CallsignOverride = S(root, "callsignOverride", c.CallsignOverride);
        if (root["vatsim"] is JsonObject v) c.Vatsim.PollSeconds = I(v, "pollSeconds", c.Vatsim.PollSeconds);
        if (root["alerts"] is JsonObject a)
        {
            c.Alerts.SupervisorPanic = RuleFrom(a["supervisorPanic"] as JsonObject, c.Alerts.SupervisorPanic);
            c.Alerts.AtcPrivateMessage = RuleFrom(a["atcPrivateMessage"] as JsonObject, c.Alerts.AtcPrivateMessage);
            c.Alerts.AnyPrivateMessage = RuleFrom(a["anyPrivateMessage"] as JsonObject, c.Alerts.AnyPrivateMessage);
            c.Alerts.DedupeSeconds = I(a, "dedupeSeconds", c.Alerts.DedupeSeconds);
        }
        if (root["discord"] is JsonObject d) c.Discord.WebhookUrl = S(d, "webhookUrl", c.Discord.WebhookUrl);
        if (root["email"] is JsonObject e) { c.Email.Enabled = B(e, "enabled", c.Email.Enabled); c.Email.Address = S(e, "address", c.Email.Address); }
        if (root["network"] is JsonObject n)
        {
            c.Network.Enabled = B(n, "enabled", c.Network.Enabled);
            c.Network.WebhookUrl = S(n, "webhookUrl", c.Network.WebhookUrl);
            c.Network.Handle = S(n, "handle", c.Network.Handle);
            c.Network.PollSeconds = I(n, "pollSeconds", c.Network.PollSeconds);
        }
        if (root["map"] is JsonObject m) c.Map.TrafficRadiusNm = D(m, "trafficRadiusNm", c.Map.TrafficRadiusNm);
        return c;
    }

    private static JsonObject RuleTo(AlertRule r) => new()
    {
        ["discord"] = r.Discord, ["email"] = r.Email, ["desktop"] = r.Desktop, ["sound"] = r.Sound,
    };

    public static JsonObject ToJson(AppConfig c) => new()
    {
        ["port"] = c.Port,
        ["openBrowserOnStart"] = c.OpenBrowserOnStart,
        ["callsignOverride"] = c.CallsignOverride,
        ["vatsim"] = new JsonObject { ["pollSeconds"] = c.Vatsim.PollSeconds },
        ["alerts"] = new JsonObject
        {
            ["supervisorPanic"] = RuleTo(c.Alerts.SupervisorPanic),
            ["atcPrivateMessage"] = RuleTo(c.Alerts.AtcPrivateMessage),
            ["anyPrivateMessage"] = RuleTo(c.Alerts.AnyPrivateMessage),
            ["dedupeSeconds"] = c.Alerts.DedupeSeconds,
        },
        ["discord"] = new JsonObject { ["webhookUrl"] = c.Discord.WebhookUrl },
        ["email"] = new JsonObject { ["enabled"] = c.Email.Enabled, ["address"] = c.Email.Address },
        ["network"] = new JsonObject
        {
            ["enabled"] = c.Network.Enabled, ["webhookUrl"] = c.Network.WebhookUrl,
            ["handle"] = c.Network.Handle, ["pollSeconds"] = c.Network.PollSeconds,
        },
        ["map"] = new JsonObject { ["trafficRadiusNm"] = c.Map.TrafficRadiusNm },
    };
}
