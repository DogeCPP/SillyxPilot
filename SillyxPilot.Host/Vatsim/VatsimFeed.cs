using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace SillyxPilot.Plugin.Vatsim;

public record SupervisorInfo(string Callsign, long Cid, string? Name, int Rating);
public record SenderClassification(bool IsSupervisor, bool IsController, int? Rating, string? Name, long? Cid, string? Frequency);
public record TrafficPilot(long Cid, string Callsign, double Lat, double Lon, double Heading,
    double Altitude, double Groundspeed, string? Dep, string? Arr, string? Aircraft);
public record SelfAircraft(long Cid, string Callsign, double Lat, double Lon, double Heading,
    double Altitude, double Groundspeed, string? Dep, string? Arr, string? Route, string? Aircraft);
public record ControllerInfo(string Callsign, int Facility, int Rating, string? Name, long Cid, string? Frequency);
public record AwarenessSnapshot(string UpdatedAt, int SupervisorCount, List<SupervisorInfo> Supervisors,
    int AtcCount, int AtisCount, int ObserverCount, int PilotCount, string? Error);

public sealed class VatsimFeed : IDisposable
{
    private const string FeedUrl = "https://data.vatsim.net/v3/vatsim-data.json";
    private static readonly HashSet<int> SupRatings = new() { 11, 12 };
    private static readonly HashSet<string> PositionSuffixes = new() { "DEL", "GND", "TWR", "APP", "CTR", "FSS" };

    private readonly System.Threading.Timer _timer;

    private Dictionary<string, ControllerInfo> _controllersByCallsign = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, TrafficPilot> _pilotsByCallsign = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<long, TrafficPilot> _pilotsByCid = new();
    private List<SupervisorInfo> _supervisors = new();
    private int _atcCount, _atisCount, _observerCount, _pilotCount;
    private string _updatedAt = "";
    private string? _error;

    public event EventHandler<AwarenessSnapshot>? Updated;
    public event EventHandler<List<TrafficPilot>>? TrafficUpdated;

    public VatsimFeed(int pollSeconds)
    {

        _timer = new System.Threading.Timer(_ => { try { Poll(); } catch { } }, null, Timeout.Infinite, Timeout.Infinite);
        SetInterval(pollSeconds);
    }

    public void SetInterval(int pollSeconds) => _timer.Change(0, Math.Max(15, pollSeconds) * 1000);

    private static string? PositionFromCallsign(string callsign)
    {
        var segs = Runtime.SplitChar(callsign, '_');
        var suffix = segs[segs.Length - 1].ToUpperInvariant();
        if (suffix == "DEP") suffix = "APP";
        if (suffix == "RMP") suffix = "GND";
        return PositionSuffixes.Contains(suffix) ? suffix : null;
    }

    private static bool IsRealAtc(string callsign, int facility)
    {
        if (facility == 0) return false;
        if (callsign.EndsWith("_OBS", StringComparison.OrdinalIgnoreCase)) return false;
        return PositionFromCallsign(callsign) != null;
    }

    private void Poll()
    {
        try
        {
            var text = Runtime.Http.GetStringAsync(FeedUrl).GetAwaiter().GetResult();
            var root = JsonNode.Parse(text)?.AsObject();
            Ingest(root);
            _error = null;
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        try { Updated?.Invoke(this, Snapshot()); } catch { }
        try { TrafficUpdated?.Invoke(this, TrafficList()); } catch { }
    }

    private static string? Str(JsonNode? n) => n?.GetValue<string>();
    private static double Dbl(JsonNode? n) { try { return n?.GetValue<double>() ?? 0; } catch { return 0; } }
    private static int Int(JsonNode? n) { try { return n?.GetValue<int>() ?? 0; } catch { return 0; } }
    private static long Lng(JsonNode? n) { try { return n?.GetValue<long>() ?? 0; } catch { return 0; } }

    private void Ingest(JsonObject? root)
    {
        var controllers = new Dictionary<string, ControllerInfo>(StringComparer.OrdinalIgnoreCase);
        var supervisors = new List<SupervisorInfo>();
        int atc = 0, observers = 0;

        if (root?["controllers"] is JsonArray ctrlArr)
        {
            foreach (var c in ctrlArr.OfType<JsonObject>())
            {
                var callsign = Str(c["callsign"]) ?? "";
                var facility = Int(c["facility"]);
                var rating = Int(c["rating"]);
                var cid = Lng(c["cid"]);
                controllers[callsign] = new ControllerInfo(callsign, facility, rating, Str(c["name"]), cid, Str(c["frequency"]));
                if (SupRatings.Contains(rating)) supervisors.Add(new SupervisorInfo(callsign, cid, Str(c["name"]), rating));
                if (facility == 0 || callsign.EndsWith("_OBS", StringComparison.OrdinalIgnoreCase)) observers++;
                else if (IsRealAtc(callsign, facility)) atc++;
            }
        }

        var pilotsByCallsign = new Dictionary<string, TrafficPilot>(StringComparer.OrdinalIgnoreCase);
        var pilotsByCid = new Dictionary<long, TrafficPilot>();
        int pilotCount = 0;
        if (root?["pilots"] is JsonArray pilotArr)
        {
            foreach (var p in pilotArr.OfType<JsonObject>())
            {
                pilotCount++;
                var fp = p["flight_plan"] as JsonObject;
                var tp = new TrafficPilot(
                    Lng(p["cid"]), Str(p["callsign"]) ?? "", Dbl(p["latitude"]), Dbl(p["longitude"]),
                    Dbl(p["heading"]), Dbl(p["altitude"]), Dbl(p["groundspeed"]),
                    Str(fp?["departure"]), Str(fp?["arrival"]), Str(fp?["aircraft_short"]));
                pilotsByCallsign[tp.Callsign] = tp;
                pilotsByCid[tp.Cid] = tp;
            }
        }

        int atisCount = (root?["atis"] as JsonArray)?.Count ?? 0;

        _controllersByCallsign = controllers;
        _pilotsByCallsign = pilotsByCallsign;
        _pilotsByCid = pilotsByCid;
        _supervisors = supervisors;
        _atcCount = atc;
        _atisCount = atisCount;
        _observerCount = observers;
        _pilotCount = pilotCount;
        _updatedAt = Str(root?["general"]?["update_timestamp"]) ?? Runtime.IsoNow();
    }

    public SenderClassification ClassifySender(string callsign)
    {
        if (_controllersByCallsign.TryGetValue(callsign, out var ctrl))
            return new SenderClassification(SupRatings.Contains(ctrl.Rating), true, ctrl.Rating, ctrl.Name, ctrl.Cid, ctrl.Frequency);
        var sup = _supervisors.FirstOrDefault(s => string.Equals(s.Callsign, callsign, StringComparison.OrdinalIgnoreCase));
        if (sup != null) return new SenderClassification(true, true, sup.Rating, sup.Name, sup.Cid, null);
        if (_pilotsByCallsign.TryGetValue(callsign, out var p)) return new SenderClassification(false, false, null, null, p.Cid, null);
        return new SenderClassification(false, false, null, null, null, null);
    }

    public SelfAircraft? FindSelf(long? cid, string? callsign)
    {
        TrafficPilot? p = null;
        if (cid.HasValue) _pilotsByCid.TryGetValue(cid.Value, out p);
        if (p == null && !string.IsNullOrEmpty(callsign)) _pilotsByCallsign.TryGetValue(callsign, out p);
        if (p == null) return null;
        return new SelfAircraft(p.Cid, p.Callsign, p.Lat, p.Lon, p.Heading, p.Altitude, p.Groundspeed, p.Dep, p.Arr, null, p.Aircraft);
    }

    public List<ControllerInfo> OnlineControllers() => _controllersByCallsign.Values.ToList();

    public AwarenessSnapshot Snapshot() =>
        new(_updatedAt, _supervisors.Count, _supervisors, _atcCount, _atisCount, _observerCount, _pilotCount, _error);

    public List<TrafficPilot> TrafficList() => _pilotsByCallsign.Values.ToList();

    public void Dispose() => _timer.Dispose();
}
