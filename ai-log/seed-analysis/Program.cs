using System.Globalization;
using System.Text.RegularExpressions;

// Throwaway derivation of the account-6 reconciliation fixture straight from seed.sql.
// Uses TimeZoneInfo with the IANA id, i.e. real tz rules, not a hardcoded DST cutover.

const int AccountId = 6;
const string Metric = "call_received";
const int WindowDays = 30;

var seedPath = args[0];
var accountRe = new Regex(@"INSERT INTO accounts \(id, name, industry, timezone, created_at\) VALUES \((\d+), '([^']*)', '([^']*)', '([^']*)'");
var eventRe = new Regex(@"INSERT INTO activity_events \(id, account_id, location, event_type, occurred_at, duration_seconds, outcome\) VALUES \((\d+), (\d+), '([^']*)', '([^']*)', '([^']*)'");

string? tzId = null;
var rows = new List<(string Location, DateTime Utc, string Type)>();

foreach (var line in File.ReadLines(seedPath))
{
    var a = accountRe.Match(line);
    if (a.Success && int.Parse(a.Groups[1].Value) == AccountId) { tzId = a.Groups[4].Value; continue; }

    var e = eventRe.Match(line);
    if (!e.Success || int.Parse(e.Groups[2].Value) != AccountId) continue;
    var utc = DateTime.ParseExact(e.Groups[5].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None);
    rows.Add((e.Groups[3].Value, utc, e.Groups[4].Value));
}

var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId!);
Console.WriteLine($"account {AccountId} tz={tzId} resolved={tz.Id} rows={rows.Count}");

DateOnly Local(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, tz));

var allLatest = rows.Max(r => Local(r.Utc));
var callRows = rows.Where(r => r.Type == Metric).ToList();
var callLatest = callRows.Max(r => Local(r.Utc));
Console.WriteLine($"latest local date: all types={allLatest} call_received={callLatest}");

var anchor = callLatest;
var windowStart = anchor.AddDays(-(WindowDays - 1));
var fetchFrom = windowStart.AddDays(-28);
Console.WriteLine($"window {windowStart}..{anchor}  fetch {fetchFrom}..{anchor}");

var counts = callRows.GroupBy(r => (r.Location, Date: Local(r.Utc)))
                     .ToDictionary(g => g.Key, g => g.Count());

var locations = callRows.Select(r => r.Location).Distinct().OrderBy(l => l, StringComparer.Ordinal).ToList();
Console.WriteLine($"locations={locations.Count}");

Console.WriteLine();
Console.WriteLine("--- fixture rows (location,localDate,calls) within fetch span ---");
foreach (var kv in counts.Where(k => k.Key.Date >= fetchFrom && k.Key.Date <= anchor)
                         .OrderBy(k => k.Key.Location, StringComparer.Ordinal).ThenBy(k => k.Key.Date))
{
    Console.WriteLine($"{kv.Key.Location}|{kv.Key.Date:yyyy-MM-dd}|{kv.Value}");
}

Console.WriteLine();
Console.WriteLine("--- first/last local date per location (full history) ---");
foreach (var loc in locations)
{
    var dates = callRows.Where(r => r.Location == loc).Select(r => Local(r.Utc)).ToList();
    Console.WriteLine($"{loc}|{dates.Min():yyyy-MM-dd}|{dates.Max():yyyy-MM-dd}");
}

// Independent replication of the algorithm, to state the expected figures.
Console.WriteLine();
Console.WriteLine("--- expected per-location results ---");
decimal accRecent = 0, accBaseline = 0;
foreach (var loc in locations)
{
    int recent = 0; decimal baseline = 0;
    var contrib = new Dictionary<DateOnly, decimal>();
    for (var d = windowStart; d <= anchor; d = d.AddDays(1))
    {
        recent += counts.TryGetValue((loc, d), out var c) ? c : 0;
        decimal sum = 0;
        for (var k = 1; k <= 4; k++)
        {
            var p = d.AddDays(-7 * k);
            var pc = counts.TryGetValue((loc, p), out var v) ? v : 0;
            sum += pc;
            contrib[p] = (contrib.TryGetValue(p, out var e) ? e : 0m) + pc / 4m;
        }
        baseline += sum / 4m;
    }
    accRecent += recent; accBaseline += baseline;
    decimal? pct = baseline == 0 ? null : Math.Round((recent - baseline) / baseline * 100m, 2);
    var top = contrib.Where(c => c.Value > 0).OrderByDescending(c => c.Value).FirstOrDefault();
    var share = baseline == 0 ? 0m : Math.Round(top.Value / baseline * 100m, 2);
    var band = baseline == 0 ? 0m : Math.Max(10m, 100m / (decimal)Math.Sqrt((double)baseline));
    Console.WriteLine($"{loc}|recent={recent}|baseline={baseline}|pct={pct}|band={Math.Round(band, 2)}|topDay={top.Key:yyyy-MM-dd}|share={share}");
}
Console.WriteLine($"ACCOUNT|recent={accRecent}|baseline={accBaseline}|pct={Math.Round((accRecent - accBaseline) / accBaseline * 100m, 2)}");
