using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using SillyxPilot.Plugin.Alerting;
using SillyxPilot.Plugin.Network;
using SillyxPilot.Plugin.Vatsim;
using SillyxPilot.Plugin.Web;
using Vatsim.Xpilot.PluginSdk.Events;

namespace SillyxPilot.Plugin;

public sealed class HostApp
{
    private AppConfig _config = new();
    private Store? _store;
    private Alerter? _alerter;
    private VatsimFeed? _vatsimFeed;
    private VatSpy? _vatSpy;
    private DiscordNetwork? _network;
    private DashboardServer? _dashboard;

    private string? _callsign;
    private string? _cid;
    private bool _connected;
    private readonly Dictionary<string, JsonObject> _controllers = new(StringComparer.OrdinalIgnoreCase);
    private SelfAircraft? _self;
    private List<TrafficPilot> _traffic = new();
    private Prediction? _prediction;
    private Timer? _bgTimer;

    public int Port => _dashboard?.Port ?? _config.Port;

    public void Start()
    {
        _config = ConfigStore.Load();
        _store = new Store();
        _alerter = new Alerter(() => _config);
        _vatsimFeed = new VatsimFeed(_config.Vatsim.PollSeconds);
        _vatSpy = new VatSpy();
        _network = new DiscordNetwork(() => _config);

        _vatsimFeed.Updated += (_, snap) => Broadcast("awareness", new JsonObject { ["awareness"] = SnapshotToJson(snap) });
        _vatsimFeed.TrafficUpdated += (_, traffic) => { _traffic = traffic; UpdateMap(); };
        _network.ChatReceived += (_, m) => Broadcast("networkChat", new JsonObject
        {
            ["chat"] = new JsonObject { ["from"] = m.From, ["text"] = m.Text, ["at"] = m.At, ["id"] = m.Id }
        });
        _network.StatusChanged += (_, s) => Broadcast("network", new JsonObject { ["network"] = NetworkStatusToJson(s) });

        _dashboard = new DashboardServer(AppPaths.WwwRoot(), HandleApiGet, HandleApiPost, () => FullSnapshot().ToJsonString());
        _dashboard.Start(_config.Port);

        _vatsimFeed.SetInterval(_config.Vatsim.PollSeconds);

        _bgTimer = new Timer(_ =>
        {
            try { MaybeOpenBrowser(); } catch { }
            try { _network?.Start(); } catch { }
            try { _vatSpy?.Load(); UpdateMap(); } catch { }
        }, null, 250, Timeout.Infinite);
    }

    public string DashboardUrl => $"http://localhost:{Port}";

    private void MaybeOpenBrowser()
    {
        if (!_config.OpenBrowserOnStart) return;
        var url = DashboardUrl;
        try { OpenViaCmd(url); return; } catch { }
        try { OpenViaExplorer(url); return; } catch { }
        try { OpenViaXdg(url); return; } catch { }
        try { OpenViaOpen(url); return; } catch { }
        try { OpenViaShell(url); } catch { }
    }

    private static void OpenViaCmd(string url) => Process.Start("cmd", $"/c start \"\" \"{url}\"");
    private static void OpenViaExplorer(string url) => Process.Start("explorer", url);
    private static void OpenViaXdg(string url) => Process.Start("xdg-open", url);
    private static void OpenViaOpen(string url) => Process.Start("open", url);
    private static void OpenViaShell(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    public void Stop()
    {
        try { _bgTimer?.Dispose(); } catch { }
        _vatsimFeed?.Dispose();
        _network?.Dispose();
        _dashboard?.Stop();
    }

    public void OnNetworkConnected(object? sender, NetworkConnectedEventArgs e)
    {
        _callsign = e.Callsign;
        _cid = e.Cid;
        _connected = true;
        Broadcast("status", new JsonObject { ["callsign"] = _callsign, ["cid"] = _cid, ["connectedToVatsim"] = true });
    }

    public void OnNetworkDisconnected(object? sender, EventArgs e)
    {
        _connected = false;
        Broadcast("status", new JsonObject { ["connectedToVatsim"] = false });
    }

    private static string HzToFreqString(int hz)
    {

        int khz = hz / 1000;
        int mhz = khz / 1000;
        int frac = khz % 1000;
        return mhz.ToString() + "." + Runtime.Pad(frac, 3);
    }

    private static string ControllerGroup(string callsign)
    {
        if (callsign.EndsWith("_ATIS", StringComparison.OrdinalIgnoreCase)) return "ATIS";
        if (callsign.EndsWith("_OBS", StringComparison.OrdinalIgnoreCase)) return "Observers";
        var segs = Runtime.SplitChar(callsign, '_');
        var suffix = segs[segs.Length - 1].ToUpperInvariant();
        return suffix switch
        {
            "CTR" or "FSS" => "Center",
            "APP" or "DEP" => "Approach/Departure",
            "TWR" => "Tower",
            "GND" => "Ground",
            "DEL" => "Clearance Delivery",
            "RMP" => "Ramp",
            _ => "Observers",
        };
    }

    private JsonArray ControllersJson() => new JsonArray(
        _controllers.Values.OrderBy(c => c["callsign"]!.GetValue<string>()).Select(c => (JsonNode)c.DeepClone()).ToArray());

    public void OnControllerAdded(object? sender, ControllerAddedEventArgs e)
    {
        var cls = _vatsimFeed?.ClassifySender(e.Callsign);
        _controllers[e.Callsign] = new JsonObject
        {
            ["callsign"] = e.Callsign,
            ["freq"] = HzToFreqString(e.Frequency),
            ["group"] = ControllerGroup(e.Callsign),
            ["rating"] = cls != null && cls.Rating.HasValue ? RatingCode(cls.Rating.Value) : null,
        };
        Broadcast("controllers", new JsonObject { ["controllers"] = ControllersJson() });
    }

    public void OnControllerDeleted(object? sender, ControllerDeletedEventArgs e)
    {
        if (_controllers.Remove(e.Callsign))
            Broadcast("controllers", new JsonObject { ["controllers"] = ControllersJson() });
    }

    public void OnControllerFrequencyChanged(object? sender, ControllerFrequencyChangedEventArgs e)
    {
        if (_controllers.TryGetValue(e.Callsign, out var c))
        {
            c["freq"] = HzToFreqString(e.NewFrequency);
            Broadcast("controllers", new JsonObject { ["controllers"] = ControllersJson() });
        }
    }

    private static readonly Dictionary<int, string> RatingNames = new()
    {
        [-1] = "INAC", [0] = "SUS", [1] = "OBS", [2] = "S1", [3] = "S2", [4] = "S3",
        [5] = "C1", [6] = "C2", [7] = "C3", [8] = "I1", [9] = "I2", [10] = "I3", [11] = "SUP", [12] = "ADM",
    };
    private static string RatingCode(int rating) => RatingNames.TryGetValue(rating, out var s) ? s : rating.ToString();

    public void OnAircraftUpdated(object? sender, AircraftUpdatedEventArgs e)
    {

        if (string.Equals(e.Callsign, _callsign, StringComparison.OrdinalIgnoreCase)) UpdateMap();
    }

    private void UpdateMap()
    {
        try
        {
            if (_vatsimFeed == null || _vatSpy == null) return;
            _self = _vatsimFeed.FindSelf(long.TryParse(_cid, out var cidLong) ? cidLong : null, _callsign);

            var radius = _config.Map.TrafficRadiusNm;
            List<TrafficPilot> nearby;
            if (_self != null)
            {
                nearby = _traffic.Where(p => p.Cid != _self.Cid)
                    .Select(p => (p, dist: VatSpy.GreatCircleNm(_self.Lat, _self.Lon, p.Lat, p.Lon)))
                    .Where(x => x.dist <= radius).OrderBy(x => x.dist).Take(300).Select(x => x.p).ToList();
                _prediction = _vatSpy.Predict(_self, _vatsimFeed.OnlineControllers());
            }
            else
            {
                nearby = _traffic.Take(300).ToList();
                _prediction = null;
            }

            Broadcast("map", new JsonObject { ["map"] = MapToJson(_self, nearby, _prediction) });
        }
        catch {  }
    }

    private static JsonObject MapToJson(SelfAircraft? self, List<TrafficPilot> traffic, Prediction? prediction)
    {
        JsonObject? selfJson = self == null ? null : new JsonObject
        {
            ["cid"] = self.Cid, ["callsign"] = self.Callsign, ["lat"] = self.Lat, ["lon"] = self.Lon,
            ["heading"] = self.Heading, ["altitude"] = self.Altitude, ["groundspeed"] = self.Groundspeed,
            ["dep"] = self.Dep, ["arr"] = self.Arr, ["aircraft"] = self.Aircraft,
        };
        var trafficArr = new JsonArray(traffic.Select(p => (JsonNode)new JsonObject
        {
            ["cid"] = p.Cid, ["callsign"] = p.Callsign, ["lat"] = p.Lat, ["lon"] = p.Lon,
            ["heading"] = p.Heading, ["altitude"] = p.Altitude, ["groundspeed"] = p.Groundspeed,
            ["dep"] = p.Dep, ["arr"] = p.Arr, ["aircraft"] = p.Aircraft,
        }).ToArray());

        JsonObject? predJson = null;
        if (prediction != null)
        {
            predJson = new JsonObject
            {
                ["currentFir"] = prediction.CurrentFir == null ? null : new JsonObject
                {
                    ["icao"] = prediction.CurrentFir.Icao, ["name"] = prediction.CurrentFir.Name,
                    ["online"] = prediction.CurrentFir.Online, ["controller"] = prediction.CurrentFir.Controller,
                },
                ["upcoming"] = new JsonArray(prediction.Upcoming.Select(u => (JsonNode)new JsonObject
                {
                    ["icao"] = u.Icao, ["name"] = u.Name, ["distanceNm"] = u.DistanceNm, ["etaMin"] = u.EtaMin,
                    ["online"] = u.Online, ["controller"] = u.Controller, ["current"] = u.Current,
                }).ToArray()),
                ["watching"] = new JsonArray(prediction.Watching.Select(w => (JsonNode)new JsonObject
                {
                    ["callsign"] = w.Callsign, ["position"] = w.Position, ["airport"] = w.Airport, ["distanceNm"] = w.DistanceNm,
                }).ToArray()),
                ["dest"] = prediction.Dest == null ? null : new JsonObject { ["icao"] = prediction.Dest.Icao, ["name"] = prediction.Dest.Name },
            };
        }

        return new JsonObject { ["self"] = selfJson, ["traffic"] = trafficArr, ["prediction"] = predJson };
    }

    private AlertRule RuleFor(string category) => category switch
    {
        "supervisor" => _config.Alerts.SupervisorPanic,
        "atc" => _config.Alerts.AtcPrivateMessage,
        _ => _config.Alerts.AnyPrivateMessage,
    };

    public void OnPrivateMessageReceived(object? sender, PrivateMessageReceivedEventArgs e)
    {
        var cls = _vatsimFeed?.ClassifySender(e.From) ?? new SenderClassification(false, false, null, null, null, null);
        var category = cls.IsSupervisor ? "supervisor" : cls.IsController ? "atc" : "private";

        var rec = _store!.AddMessage(new JsonObject
        {
            ["kind"] = "private", ["direction"] = "in", ["from"] = e.From, ["to"] = _callsign,
            ["message"] = e.Message, ["senderClass"] = category, ["senderName"] = cls.Name,
            ["cid"] = cls.Cid, ["rating"] = cls.Rating.HasValue ? RatingCode(cls.Rating.Value) : null,
        });
        Broadcast("message", new JsonObject { ["message"] = (JsonNode)rec.DeepClone() });

        var rule = RuleFor(category);
        var title = category switch
        {
            "supervisor" => $"Supervisor is contacting you: {e.From}",
            "atc" => $"ATC private message from {e.From}",
            _ => $"Private message from {e.From}",
        };
        var fields = new List<AlertField>
        {
            new("From", e.From), new("Name", cls.Name ?? "unknown"), new("CID", cls.Cid?.ToString() ?? "unknown"),
            new("Rating", cls.Rating.HasValue ? RatingCode(cls.Rating.Value) : "unknown"),
            new("Your callsign", _callsign ?? "unknown"), new("Message", e.Message),
        };

        var fired = new AlertResult(new List<string>(), new List<string>());
        if (rule.Discord || rule.Email || rule.Desktop || rule.Sound)
            fired = _alerter!.Fire(category, rule, title, e.Message, fields);

        var alert = _store.AddAlert(new JsonObject
        {
            ["category"] = category, ["from"] = e.From, ["message"] = e.Message,
            ["channels"] = new JsonArray(fired.Channels.Select(c => (JsonNode)c).ToArray()),
            ["errors"] = new JsonArray(fired.Errors.Select(c => (JsonNode)c).ToArray()),
            ["desktop"] = rule.Desktop, ["sound"] = rule.Sound,
        });
        Broadcast("alert", new JsonObject { ["alert"] = (JsonNode)alert.DeepClone() });

        var tag = category == "supervisor" ? "PANIC" : "alert";
        Console.WriteLine($"SillyxPilot [{tag}] {category} PM from {e.From}: channels {string.Join(", ", fired.Channels)}");
    }

    public void OnRadioMessageReceived(object? sender, RadioMessageReceivedEventArgs e)
    {
        var freqStr = e.Frequencies.Length > 0 ? HzToFreqString(e.Frequencies[0]) : null;
        var rec = _store!.AddMessage(new JsonObject
        {
            ["kind"] = "radio", ["direction"] = "in", ["from"] = e.From, ["freq"] = freqStr, ["message"] = e.Message,
        });
        Broadcast("message", new JsonObject { ["message"] = (JsonNode)rec.DeepClone() });
    }

    public void OnBroadcastMessageReceived(object? sender, BroadcastMessageReceivedEventArgs e)
    {
        var rec = _store!.AddMessage(new JsonObject
        {
            ["kind"] = "broadcast", ["direction"] = "in", ["from"] = e.From, ["message"] = e.Message,
        });
        Broadcast("message", new JsonObject { ["message"] = (JsonNode)rec.DeepClone() });
    }

    private JsonObject FullSnapshot() => new()
    {
        ["type"] = "snapshot",
        ["callsign"] = _callsign,
        ["cid"] = _cid,
        ["connectedToVatsim"] = _connected,
        ["controllers"] = ControllersJson(),
        ["awareness"] = _vatsimFeed != null ? SnapshotToJson(_vatsimFeed.Snapshot()) : null,
        ["network"] = _network != null ? NetworkStatusToJson(_network.Status()) : null,
        ["map"] = MapToJson(_self, _traffic.Take(300).ToList(), _prediction),
        ["messages"] = new JsonArray(_store!.RecentMessages(300).Select(m => (JsonNode)m.DeepClone()).ToArray()),
        ["alerts"] = new JsonArray(_store.RecentAlerts(100).Select(a => (JsonNode)a.DeepClone()).ToArray()),
    };

    private static JsonObject SnapshotToJson(AwarenessSnapshot s) => new()
    {
        ["updatedAt"] = s.UpdatedAt,
        ["supervisorCount"] = s.SupervisorCount,
        ["supervisors"] = new JsonArray(s.Supervisors.Select(x => (JsonNode)new JsonObject
        {
            ["callsign"] = x.Callsign, ["cid"] = x.Cid, ["name"] = x.Name, ["rating"] = x.Rating,
        }).ToArray()),
        ["atcCount"] = s.AtcCount, ["atisCount"] = s.AtisCount, ["pilotCount"] = s.PilotCount, ["error"] = s.Error,
    };

    private static JsonObject NetworkStatusToJson(NetworkStatus s) => new()
    {
        ["enabled"] = s.Enabled, ["connected"] = s.Connected, ["canReceive"] = s.CanReceive,
        ["reason"] = s.Reason, ["activeCount"] = s.ActiveCount,
        ["activeUsers"] = new JsonArray(s.ActiveUsers.Select(u => (JsonNode)u).ToArray()),
    };

    private void Broadcast(string type, JsonObject extra)
    {
        extra["type"] = type;
        _dashboard?.Broadcast(extra);
    }

    private HttpApiResult HandleApiGet(string path)
    {
        if (path == "/api/state") return HttpApiResult.Ok(FullSnapshot().ToJsonString());
        if (path == "/api/config") return HttpApiResult.Ok(ConfigToJson(_config).ToJsonString());
        if (path.StartsWith("/api/messages"))
        {
            var q = ParseQueryParam(path, "q");
            var msgs = new JsonArray(_store!.Search(q, 400).Select(m => (JsonNode)m.DeepClone()).ToArray());
            return HttpApiResult.Ok(new JsonObject { ["messages"] = msgs }.ToJsonString());
        }
        if (path == "/api/setup/status")
        {
            return HttpApiResult.Ok(new JsonObject
            {
                ["startupMode"] = "plugin",
                ["mail"] = EmbeddedSecrets.HasMailSender(),
                ["bot"] = EmbeddedSecrets.HasBotToken(),
            }.ToJsonString());
        }
        return HttpApiResult.NotFound();
    }

    private static JsonObject ConfigToJson(AppConfig c) => new()
    {
        ["port"] = c.Port,
        ["openBrowserOnStart"] = c.OpenBrowserOnStart,
        ["vatsim"] = new JsonObject { ["pollSeconds"] = c.Vatsim.PollSeconds },
        ["alerts"] = new JsonObject
        {
            ["supervisorPanic"] = RuleToJson(c.Alerts.SupervisorPanic),
            ["atcPrivateMessage"] = RuleToJson(c.Alerts.AtcPrivateMessage),
            ["anyPrivateMessage"] = RuleToJson(c.Alerts.AnyPrivateMessage),
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

    private static JsonObject RuleToJson(AlertRule r) => new()
    {
        ["discord"] = r.Discord, ["email"] = r.Email, ["desktop"] = r.Desktop, ["sound"] = r.Sound,
    };

    private HttpApiResult HandleApiPost(string path, string body)
    {
        JsonNode? bodyNode = null;
        try { bodyNode = JsonNode.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body); } catch {  }
        var bodyObj = bodyNode as JsonObject ?? new JsonObject();

        if (path == "/api/config")
        {
            ApplyConfigPatch(bodyObj);
            ConfigStore.Save(_config);
            _vatsimFeed?.SetInterval(_config.Vatsim.PollSeconds);
            _network?.Reconfigure();
            return HttpApiResult.Ok(new JsonObject { ["ok"] = true, ["config"] = ConfigToJson(_config) }.ToJsonString());
        }
        if (path == "/api/test-alert")
        {
            var results = _alerter!.Test();
            return HttpApiResult.Ok(new JsonObject
            {
                ["ok"] = true,
                ["results"] = new JsonObject(results.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, kv.Value))),
            }.ToJsonString());
        }
        if (path == "/api/network/username")
        {
            var username = bodyObj["username"]?.GetValue<string>();
            var check = BadWords.Check(username);
            if (!check.Ok) return HttpApiResult.Ok(new JsonObject { ["ok"] = false, ["reason"] = check.Reason }.ToJsonString());
            _config.Network.Handle = check.Value!;
            ConfigStore.Save(_config);
            return HttpApiResult.Ok(new JsonObject { ["ok"] = true, ["username"] = check.Value }.ToJsonString());
        }
        if (path == "/api/network/send")
        {
            var text = bodyObj["text"]?.GetValue<string>() ?? "";
            var handle = _config.Network.Handle;
            if (string.IsNullOrEmpty(handle)) return HttpApiResult.Ok(new JsonObject { ["ok"] = false, ["reason"] = "pick a username first" }.ToJsonString());
            var (ok, err) = _network!.Send(handle, text);
            if (ok) Broadcast("networkChat", new JsonObject { ["chat"] = new JsonObject { ["from"] = handle, ["text"] = text, ["at"] = Runtime.IsoNow(), ["self"] = true } });
            return HttpApiResult.Ok(new JsonObject { ["ok"] = ok, ["error"] = err }.ToJsonString());
        }
        return HttpApiResult.NotFound();
    }

    private void ApplyConfigPatch(JsonObject patch)
    {
        if (patch["port"] != null) _config.Port = patch["port"]!.GetValue<int>();
        if (patch["openBrowserOnStart"] != null) _config.OpenBrowserOnStart = patch["openBrowserOnStart"]!.GetValue<bool>();
        if (patch["vatsim"]?["pollSeconds"] != null) _config.Vatsim.PollSeconds = patch["vatsim"]!["pollSeconds"]!.GetValue<int>();
        if (patch["discord"]?["webhookUrl"] != null) _config.Discord.WebhookUrl = patch["discord"]!["webhookUrl"]!.GetValue<string>();
        if (patch["email"] is JsonObject em)
        {
            if (em["enabled"] != null) _config.Email.Enabled = em["enabled"]!.GetValue<bool>();
            if (em["address"] != null) _config.Email.Address = em["address"]!.GetValue<string>();
        }
        if (patch["network"] is JsonObject net)
        {
            if (net["enabled"] != null) _config.Network.Enabled = net["enabled"]!.GetValue<bool>();
            if (net["webhookUrl"] != null) _config.Network.WebhookUrl = net["webhookUrl"]!.GetValue<string>();
            if (net["handle"] != null) _config.Network.Handle = net["handle"]!.GetValue<string>();
        }
        if (patch["map"]?["trafficRadiusNm"] != null) _config.Map.TrafficRadiusNm = patch["map"]!["trafficRadiusNm"]!.GetValue<double>();
        ApplyRulePatch(patch["alerts"]?["supervisorPanic"] as JsonObject, _config.Alerts.SupervisorPanic);
        ApplyRulePatch(patch["alerts"]?["atcPrivateMessage"] as JsonObject, _config.Alerts.AtcPrivateMessage);
        ApplyRulePatch(patch["alerts"]?["anyPrivateMessage"] as JsonObject, _config.Alerts.AnyPrivateMessage);
        if (patch["alerts"]?["dedupeSeconds"] != null) _config.Alerts.DedupeSeconds = patch["alerts"]!["dedupeSeconds"]!.GetValue<int>();
    }

    private static void ApplyRulePatch(JsonObject? patch, AlertRule rule)
    {
        if (patch == null) return;
        if (patch["discord"] != null) rule.Discord = patch["discord"]!.GetValue<bool>();
        if (patch["email"] != null) rule.Email = patch["email"]!.GetValue<bool>();
        if (patch["desktop"] != null) rule.Desktop = patch["desktop"]!.GetValue<bool>();
        if (patch["sound"] != null) rule.Sound = patch["sound"]!.GetValue<bool>();
    }

    private static string? ParseQueryParam(string pathWithQuery, string name)
    {
        var qIdx = pathWithQuery.IndexOf('?');
        if (qIdx < 0) return null;
        var query = pathWithQuery[(qIdx + 1)..];
        foreach (var pair in Runtime.SplitChar(query, '&'))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0) continue;
            var key = Uri.UnescapeDataString(pair[..eq]);
            if (key == name) return Uri.UnescapeDataString(pair[(eq + 1)..]);
        }
        return null;
    }
}
