using System.Globalization;
using System.Reflection;
using Relay.Application.Baseline;

namespace Relay.UnitTests.Fixtures;

/// <summary>
/// The account 6 reconciliation fixture: real seed-derived daily counts, so the figures in
/// techspec § Testing Approach are checked by a test rather than by eye (PRD Goal 3).
/// </summary>
internal static class Account6Fixture
{
    public const int WindowDays = 30;

    public static readonly DateOnly WindowEnd = new(2026, 7, 27);
    public static readonly DateOnly WindowStart = new(2026, 6, 28);

    /// <summary>The planted anomaly that inflates every one of account 6's baselines.</summary>
    public static readonly DateOnly AnomalyDate = new(2026, 6, 3);

    public static BaselineRequest Request() =>
        new(WindowEnd, WindowDays, LocationHistories(), DailyCounts());

    public static IReadOnlyList<LocationHistory> LocationHistories() =>
        DataRows("account-6-location-history.csv")
            .Select(fields => new LocationHistory(fields[0], ParseDate(fields[1]), ParseDate(fields[2])))
            .ToList();

    public static IReadOnlyList<DailyLocationCount> DailyCounts() =>
        DataRows("account-6-daily-call-counts.csv")
            .Select(fields => new DailyLocationCount(
                fields[0],
                ParseDate(fields[1]),
                int.Parse(fields[2], CultureInfo.InvariantCulture)))
            .ToList();

    private static DateOnly ParseDate(string value) =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IReadOnlyList<string[]> DataRows(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(fileName, StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Fixture '{fileName}' is not embedded.");
        using var reader = new StreamReader(stream);

        var rows = new List<string[]>();
        var headerSeen = false;
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (!headerSeen)
            {
                headerSeen = true;
                continue;
            }

            rows.Add(line.Split(','));
        }

        return rows;
    }
}
