using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace SillyxPilot.Plugin.Vatsim;

public record FirInfo(string Icao, string Name, string Prefix, string BoundaryId);
public record Boundary(string Id, double MinLon, double MinLat, double MaxLon, double MaxLat, List<List<List<double[]>>> Polygons);
public record AirportInfo(string Name, double Lat, double Lon, string Fir);

public record UpcomingFir(string Icao, string Name, int DistanceNm, int? EtaMin, bool Online, string? Controller, bool Current);
public record WatchingAtc(string Callsign, string Position, string Airport, int DistanceNm);
public record CurrentFirInfo(string Icao, string Name, bool Online, string? Controller);
public record DestInfo(string Icao, string Name);
public record Prediction(CurrentFirInfo? CurrentFir, List<UpcomingFir> Upcoming, List<WatchingAtc> Watching, DestInfo? Dest);

public sealed class VatSpy
{
    private const string DatUrl = "https://raw.githubusercontent.com/vatsimnetwork/vatspy-data-project/master/VATSpy.dat";
    private const string GeoUrl = "https://raw.githubusercontent.com/vatsimnetwork/vatspy-data-project/master/Boundaries.geojson";
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);
    private const double NmPerDeg = 60.0;

    private readonly Dictionary<string, AirportInfo> _airports = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FirInfo> _firs = new();
    private readonly Dictionary<string, FirInfo> _firByBoundary = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Boundary> _boundaries = new();
    public bool Ready { get; private set; }

    private string CacheDir => Path.Combine(AppPaths.SillyDataDir(), "vatspy");
    private string DatFile => Path.Combine(CacheDir, "VATSpy.dat");
    private string GeoFile => Path.Combine(CacheDir, "Boundaries.geojson");

    public void Load()
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var dat = CachedFetch(DatUrl, DatFile);
            var geo = CachedFetch(GeoUrl, GeoFile);
            ParseDat(dat);
            ParseGeo(geo);
            Ready = true;
        }
        catch
        {

        }
    }

    private static string CachedFetch(string url, string file)
    {
        var fresh = File.Exists(file) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(file)) < MaxAge;
        if (fresh) return Runtime.ReadText(file);
        try
        {
            var text = Runtime.Http.GetStringAsync(url).GetAwaiter().GetResult();
            File.WriteAllText(file, text);
            return text;
        }
        catch
        {
            if (File.Exists(file)) return Runtime.ReadText(file);
            throw;
        }
    }

    private void ParseDat(string text)
    {
        string? section = null;
        foreach (var raw in Runtime.SplitChar(text, '\n'))
        {
            var line = raw.Trim().TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith(";")) continue;
            if (line.StartsWith("["))
            {
                section = line[1..^1];
                continue;
            }
            var f = Runtime.SplitChar(line, '|');
            if (section == "Airports" && f.Length >= 6)
            {
                if (double.TryParse(f[2], System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
                    double.TryParse(f[3], System.Globalization.CultureInfo.InvariantCulture, out var lon))
                {
                    _airports[f[0]] = new AirportInfo(f[1], lat, lon, f[5]);
                }
            }
            else if (section == "FIRs" && f.Length >= 4)
            {
                var boundaryId = string.IsNullOrEmpty(f[3]) ? f[0] : f[3];
                var fir = new FirInfo(f[0], f[1], string.IsNullOrEmpty(f[2]) ? f[0] : f[2], boundaryId);
                _firs.Add(fir);
                _firByBoundary[boundaryId] = fir;
            }
        }
    }

    private void ParseGeo(string geoJson)
    {

        var root = JsonNode.Parse(geoJson)?.AsObject();
        if (root?["features"] is not JsonArray features) return;

        foreach (var feat in features.OfType<JsonObject>())
        {
            var id = (feat["properties"] as JsonObject)?["id"]?.GetValue<string>();
            if (string.IsNullOrEmpty(id)) continue;
            if (feat["geometry"] is not JsonObject geom) continue;
            var type = geom["type"]?.GetValue<string>();
            if (geom["coordinates"] is not JsonArray coords) continue;

            var polygons = new List<List<List<double[]>>>();
            double minLon = 180, minLat = 90, maxLon = -180, maxLat = -90;

            void ProcessPolygon(JsonArray polyEl)
            {
                var poly = new List<List<double[]>>();
                foreach (var ringEl in polyEl.OfType<JsonArray>())
                {
                    var ring = new List<double[]>();
                    foreach (var ptEl in ringEl.OfType<JsonArray>())
                    {
                        var lon = ptEl[0]!.GetValue<double>();
                        var lat = ptEl[1]!.GetValue<double>();
                        if (lon < minLon) minLon = lon; if (lon > maxLon) maxLon = lon;
                        if (lat < minLat) minLat = lat; if (lat > maxLat) maxLat = lat;
                        ring.Add(new[] { lon, lat });
                    }
                    poly.Add(ring);
                }
                polygons.Add(poly);
            }

            if (type == "MultiPolygon")
            {
                foreach (var polyEl in coords.OfType<JsonArray>()) ProcessPolygon(polyEl);
            }
            else if (type == "Polygon")
            {
                ProcessPolygon(coords);
            }

            _boundaries.Add(new Boundary(id, minLon, minLat, maxLon, maxLat, polygons));
        }
    }

    private string? FindFirBoundary(double lat, double lon)
    {
        foreach (var b in _boundaries)
        {
            if (lon < b.MinLon || lon > b.MaxLon || lat < b.MinLat || lat > b.MaxLat) continue;
            if (InMultiPolygon(lon, lat, b.Polygons)) return b.Id;
        }
        return null;
    }

    private FirInfo? FirForPoint(double lat, double lon)
    {
        var bid = FindFirBoundary(lat, lon);
        if (bid == null) return null;
        return _firByBoundary.TryGetValue(bid, out var fir) ? fir : new FirInfo(bid, bid, bid, bid);
    }

    private static bool InMultiPolygon(double x, double y, List<List<List<double[]>>> polygons)
    {
        foreach (var poly in polygons)
        {
            if (poly.Count == 0) continue;
            if (InRing(x, y, poly[0]))
            {
                var inHole = false;
                for (int h = 1; h < poly.Count; h++)
                {
                    if (InRing(x, y, poly[h])) { inHole = true; break; }
                }
                if (!inHole) return true;
            }
        }
        return false;
    }

    private static bool InRing(double x, double y, List<double[]> ring)
    {
        var inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var xi = ring[i][0]; var yi = ring[i][1];
            var xj = ring[j][0]; var yj = ring[j][1];
            if (((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi)) inside = !inside;
        }
        return inside;
    }

    public static double GreatCircleNm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return NmPerDeg * ToDeg(2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)));
    }

    private static double ToRad(double d) => d * Math.PI / 180.0;
    private static double ToDeg(double r) => r * 180.0 / Math.PI;

    private static (double lat, double lon) DestPoint(double lat, double lon, double bearingDeg, double distNm)
    {
        const double R = 3440.065;
        var br = ToRad(bearingDeg); var dr = distNm / R;
        var lat1 = ToRad(lat); var lon1 = ToRad(lon);
        var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(dr) + Math.Cos(lat1) * Math.Sin(dr) * Math.Cos(br));
        var lon2 = lon1 + Math.Atan2(Math.Sin(br) * Math.Sin(dr) * Math.Cos(lat1), Math.Cos(dr) - Math.Sin(lat1) * Math.Sin(lat2));
        return (ToDeg(lat2), ((ToDeg(lon2) + 540) % 360) - 180);
    }

    private static (double lat, double lon) InterpolateGc(double lat1, double lon1, double lat2, double lon2, double f)
    {
        var phi1 = ToRad(lat1); var lam1 = ToRad(lon1); var phi2 = ToRad(lat2); var lam2 = ToRad(lon2);
        var dLat = phi2 - phi1; var dLon = lam2 - lam1;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var delta = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        if (delta == 0) return (lat1, lon1);
        var A = Math.Sin((1 - f) * delta) / Math.Sin(delta);
        var B = Math.Sin(f * delta) / Math.Sin(delta);
        var x = A * Math.Cos(phi1) * Math.Cos(lam1) + B * Math.Cos(phi2) * Math.Cos(lam2);
        var y = A * Math.Cos(phi1) * Math.Sin(lam1) + B * Math.Cos(phi2) * Math.Sin(lam2);
        var z = A * Math.Sin(phi1) + B * Math.Sin(phi2);
        return (ToDeg(Math.Atan2(z, Math.Sqrt(x * x + y * y))), ToDeg(Math.Atan2(y, x)));
    }

    private List<(double lat, double lon, double distNm)> SamplePath(SelfAircraft self, AirportInfo? dest)
    {
        var points = new List<(double, double, double)> { (self.Lat, self.Lon, 0) };
        const double stepNm = 40;
        double totalNm; double? bearing = null;
        if (dest != null) totalNm = GreatCircleNm(self.Lat, self.Lon, dest.Lat, dest.Lon);
        else { totalNm = 600; bearing = self.Heading; }

        var steps = Math.Min(30, (int)Math.Ceiling(totalNm / stepNm));
        for (int i = 1; i <= steps; i++)
        {
            var frac = (i * stepNm) / totalNm;
            (double lat, double lon) pt;
            if (dest != null && frac <= 1) pt = InterpolateGc(self.Lat, self.Lon, dest.Lat, dest.Lon, frac);
            else if (dest != null) break;
            else pt = DestPoint(self.Lat, self.Lon, bearing ?? 0, i * stepNm);
            points.Add((pt.lat, pt.lon, i * stepNm));
        }
        return points;
    }

    private string? FirOnline(FirInfo fir, Dictionary<string, string> ctrOnline)
    {
        foreach (var key in new[] { fir.Prefix, fir.Icao })
        {
            if (string.IsNullOrEmpty(key)) continue;
            var upperKey = key.ToUpperInvariant();
            foreach (var (root, cs) in ctrOnline)
            {
                if (root == upperKey || root.StartsWith(upperKey) || upperKey.StartsWith(root)) return cs;
            }
        }
        return null;
    }

    public Prediction? Predict(SelfAircraft? self, List<ControllerInfo> onlineControllers)
    {
        if (!Ready || self == null) return null;

        var ctrOnline = new Dictionary<string, string>();
        var airportAtc = new List<(string icao, string callsign, string position)>();
        foreach (var c in onlineControllers)
        {
            var cs = c.Callsign.ToUpperInvariant();
            var parts = Runtime.SplitChar(cs, '_');
            var suffix = parts[^1];
            var prefix = parts[0];
            if (suffix is "CTR" or "FSS")
            {

                var root = cs.EndsWith("_CTR") || cs.EndsWith("_FSS") ? cs.Substring(0, cs.Length - 4) : cs;
                ctrOnline[root] = cs;
            }
            else if (suffix is "TWR" or "APP" or "DEP" or "GND" or "DEL" or "ATIS")
            {
                airportAtc.Add((prefix, cs, suffix));
            }
        }

        AirportInfo? dest = self.Arr != null && _airports.TryGetValue(self.Arr, out var d) ? d : null;
        var path = SamplePath(self, dest);

        var seen = new HashSet<string>();
        var upcoming = new List<UpcomingFir>();
        FirInfo? currentFir = null;

        foreach (var pt in path)
        {
            var fir = FirForPoint(pt.lat, pt.lon);
            if (fir == null) continue;
            if (pt.distNm == 0) currentFir = fir;
            if (seen.Contains(fir.BoundaryId)) continue;
            seen.Add(fir.BoundaryId);
            var online = FirOnline(fir, ctrOnline);
            var gs = self.Groundspeed;
            upcoming.Add(new UpcomingFir(fir.Icao, fir.Name, (int)Math.Round(pt.distNm),
                gs > 30 ? (int)Math.Round(pt.distNm / gs * 60) : null, online != null, online, pt.distNm == 0));
        }

        var watching = new List<WatchingAtc>();
        foreach (var (icao, callsign, position) in airportAtc)
        {
            if (!_airports.TryGetValue(icao, out var ap)) continue;
            var dNm = GreatCircleNm(self.Lat, self.Lon, ap.Lat, ap.Lon);
            if (dNm <= 150) watching.Add(new WatchingAtc(callsign, position, icao, (int)Math.Round(dNm)));
        }
        watching = watching.OrderBy(w => w.DistanceNm).ToList();

        CurrentFirInfo? currentFirInfo = null;
        if (currentFir != null)
        {
            var online = FirOnline(currentFir, ctrOnline);
            currentFirInfo = new CurrentFirInfo(currentFir.Icao, currentFir.Name, online != null, online);
        }

        return new Prediction(
            currentFirInfo,
            upcoming.Where(u => !u.Current).Take(6).ToList(),
            watching.Take(8).ToList(),
            dest != null ? new DestInfo(self.Arr!, dest.Name) : null);
    }
}
