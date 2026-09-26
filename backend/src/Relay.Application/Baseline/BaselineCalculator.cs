namespace Relay.Application.Baseline;

/// <summary>
/// Zero-fill, the trailing same-weekday average, the volume-aware band, the verdict, the outlier
/// flag, and the ranking — techspec § Baseline algorithm, steps 1–5. No constructor dependencies:
/// no database, no clock, no I/O.
/// </summary>
public sealed class BaselineCalculator : IBaselineCalculator
{
    private const decimal PercentScale = 100m;

    public BaselineReport Evaluate(BaselineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.WindowDays, 1);

        var windowStart = request.WindowEnd.AddDays(-(request.WindowDays - 1));
        var counts = IndexDailyCounts(request.DailyCounts);

        var rows = new List<LocationBaseline>(request.Locations.Count);
        foreach (var location in request.Locations)
        {
            rows.Add(EvaluateLocation(location, windowStart, request.WindowEnd, counts));
        }

        return new BaselineReport(
            windowStart,
            request.WindowEnd,
            rows.Sum(row => row.RecentTotal),
            rows.Sum(row => row.BaselineTotal),
            Rank(rows));
    }

    private static Dictionary<(string Location, DateOnly Date), int> IndexDailyCounts(
        IReadOnlyList<DailyLocationCount> dailyCounts)
    {
        var counts = new Dictionary<(string Location, DateOnly Date), int>(dailyCounts.Count);
        foreach (var row in dailyCounts)
        {
            var key = (row.Location, row.LocalDate);

            // Summed, not overwritten: duplicates are counted as-is (PRD Open Question #2), and
            // summing is also what keeps the result independent of input order (FR10) —
            // last-write-wins would quietly depend on it.
            counts[key] = counts.TryGetValue(key, out var running) ? running + row.Calls : row.Calls;
        }

        return counts;
    }

    private static LocationBaseline EvaluateLocation(
        LocationHistory location,
        DateOnly windowStart,
        DateOnly windowEnd,
        IReadOnlyDictionary<(string Location, DateOnly Date), int> counts)
    {
        var recentTotal = 0;
        var baselineTotal = 0m;
        var hasFullHistory = true;
        var contributions = new Dictionary<DateOnly, decimal>();

        for (var day = windowStart; day <= windowEnd; day = day.AddDays(1))
        {
            recentTotal += CallsOn(counts, location.Location, day);

            var priorSum = 0;
            for (var occurrence = 1; occurrence <= BaselineThresholds.PriorOccurrences; occurrence++)
            {
                var prior = day.AddDays(-BaselineThresholds.DaysPerWeek * occurrence);
                if (prior < location.FirstLocalDate)
                {
                    hasFullHistory = false;
                }

                var calls = CallsOn(counts, location.Location, prior);
                priorSum += calls;
                contributions[prior] = Contribution(contributions, prior) + PerDayShare(calls);
            }

            baselineTotal += PerDayShare(priorSum);
        }

        if (!hasFullHistory)
        {
            // No baseline at all rather than a partial one, so no delta is claimed either.
            return new LocationBaseline(
                location.Location, recentTotal, BaselineTotal: 0m, AbsoluteDelta: 0m,
                PercentDelta: null, BandPercent: null, Verdict.InsufficientHistory,
                BaselineOutlier: null);
        }

        var absoluteDelta = recentTotal - baselineTotal;
        var outlier = FindOutlier(contributions, baselineTotal);

        if (baselineTotal < BaselineThresholds.LowVolumeBaselineFloor)
        {
            return NoPercentageRow(location.Location, recentTotal, baselineTotal, absoluteDelta, outlier);
        }

        // Division guard for PRD FR8. At the documented floor of 10 calls this is unreachable — zero
        // trips LowVolume above, and both report the same row — but it is what makes the percentage
        // below provably safe rather than safe by coincidence of a constant's value.
        if (baselineTotal == 0m)
        {
            return NoPercentageRow(location.Location, recentTotal, baselineTotal, absoluteDelta, outlier);
        }

        var band = Band(baselineTotal);
        var percentDelta = absoluteDelta / baselineTotal * PercentScale;

        return new LocationBaseline(
            location.Location,
            recentTotal,
            baselineTotal,
            absoluteDelta,
            RoundForReport(percentDelta),
            RoundForReport(band),
            DecideVerdict(percentDelta, band),
            outlier);
    }

    /// <summary>Compared unrounded, so the band boundary is exact and not an artefact of rounding.</summary>
    private static Verdict DecideVerdict(decimal percentDelta, decimal bandPercent)
    {
        if (Math.Abs(percentDelta) <= bandPercent)
        {
            return Verdict.Typical;
        }

        return percentDelta > 0m ? Verdict.Above : Verdict.Below;
    }

    /// <summary>
    /// <c>max(10%, 100/sqrt(baseline))</c> — PRD FR5. The root goes through <see cref="double"/>
    /// because <see cref="decimal"/> has none; the loss is far below the two decimals the band is
    /// reported at, and the comparison using it happens before rounding.
    /// </summary>
    private static decimal Band(decimal baselineTotal) =>
        Math.Max(
            BaselineThresholds.MinimumBandPercent,
            BaselineThresholds.BandNumeratorPercent / (decimal)Math.Sqrt((double)baselineTotal));

    /// <summary>
    /// A day's contribution is its call count divided by <see cref="BaselineThresholds.PriorOccurrences"/>
    /// once per window day it is a prior occurrence of, so the contributions sum to exactly the baseline.
    /// </summary>
    private static OutlierDay? FindOutlier(
        IReadOnlyDictionary<DateOnly, decimal> contributions,
        decimal baselineTotal)
    {
        if (baselineTotal <= 0m)
        {
            return null;
        }

        var threshold = baselineTotal * BaselineThresholds.OutlierSharePercent / PercentScale;
        var flagged = contributions.Where(contribution => contribution.Value > threshold).ToList();
        if (flagged.Count == 0)
        {
            return null;
        }

        // Ordered explicitly, earliest date winning a tie, so the flagged day never depends on
        // dictionary enumeration order.
        var top = flagged
            .OrderByDescending(contribution => contribution.Value)
            .ThenBy(contribution => contribution.Key)
            .First();

        return new OutlierDay(top.Key, RoundForReport(top.Value / baselineTotal * PercentScale));
    }

    /// <summary>
    /// Largest absolute percentage deviation first — a spike and a drop rank equally — with the rows
    /// carrying no verdict last (PRD FR14). Ties break on location name so the ranking is total and
    /// the output never depends on input order.
    /// </summary>
    private static IReadOnlyList<LocationBaseline> Rank(IEnumerable<LocationBaseline> rows) =>
        rows.OrderBy(HasNoVerdict)
            .ThenByDescending(row => row.PercentDelta.HasValue ? Math.Abs(row.PercentDelta.Value) : 0m)
            .ThenBy(row => row.Location, StringComparer.Ordinal)
            .ToList();

    private static int HasNoVerdict(LocationBaseline row) =>
        row.Verdict is Verdict.LowVolume or Verdict.InsufficientHistory ? 1 : 0;

    private static LocationBaseline NoPercentageRow(
        string location,
        int recentTotal,
        decimal baselineTotal,
        decimal absoluteDelta,
        OutlierDay? outlier) =>
        new(location, recentTotal, baselineTotal, absoluteDelta,
            PercentDelta: null, BandPercent: null, Verdict.LowVolume, outlier);

    /// <summary>A day with no row is a zero day, not a gap (PRD FR9).</summary>
    private static int CallsOn(
        IReadOnlyDictionary<(string Location, DateOnly Date), int> counts,
        string location,
        DateOnly date) =>
        counts.TryGetValue((location, date), out var calls) ? calls : 0;

    private static decimal Contribution(
        IReadOnlyDictionary<DateOnly, decimal> contributions,
        DateOnly date) =>
        contributions.TryGetValue(date, out var running) ? running : 0m;

    private static decimal PerDayShare(int calls) =>
        calls / (decimal)BaselineThresholds.PriorOccurrences;

    private static decimal RoundForReport(decimal value) =>
        Math.Round(value, BaselineThresholds.ReportedPercentDecimals, MidpointRounding.AwayFromZero);
}
