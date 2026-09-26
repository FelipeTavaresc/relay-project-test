using System.Globalization;
using Relay.Application.Baseline;
using Relay.UnitTests.Fixtures;

namespace Relay.UnitTests;

public class BaselineCalculatorTests
{
    private readonly BaselineCalculator _calculator = new();

    // A one-day window keeps the arithmetic checkable by hand: the single window day 2026-07-01 has
    // exactly four priors — 2026-06-24, 06-17, 06-10 and 06-03.
    private static readonly DateOnly SingleDayWindowEnd = Date("2026-07-01");
    private const string Site = "Site A";

    [Fact]
    public void Evaluate_EmptyInput_ReturnsTheWindowWithNoRowsAndZeroTotals()
    {
        var report = _calculator.Evaluate(new BaselineRequest(
            SingleDayWindowEnd, WindowDays: 30, Locations: [], DailyCounts: []));

        Assert.Empty(report.Locations);
        Assert.Equal(0, report.RecentTotal);
        Assert.Equal(0m, report.BaselineTotal);
        Assert.Equal(Date("2026-06-02"), report.WindowStart);
        Assert.Equal(SingleDayWindowEnd, report.WindowEnd);
    }

    [Fact]
    public void Evaluate_LocationWithNoCountsAtAll_StillReturnsARow()
    {
        var report = SingleDayReport(recent: 0);

        var row = Assert.Single(report.Locations);
        Assert.Equal(Site, row.Location);
        Assert.Equal(0, row.RecentTotal);
        Assert.Equal(0m, row.BaselineTotal);
    }

    [Fact]
    public void Evaluate_BaselineFromASingleSourceRow_SpreadsItOverTheFourOccurrences()
    {
        var report = SingleDayReport(recent: 0, ("2026-06-03", 40));

        var row = Assert.Single(report.Locations);
        Assert.Equal(10m, row.BaselineTotal);
    }

    [Fact]
    public void Evaluate_DaysWithNoCalls_CountAsZeroKeepingTheDivisorAtFour()
    {
        // Three of the four priors have four calls each. Averaging only the days that are present
        // would give 4; zero-filling the missing one gives 12/4 = 3 (PRD FR9).
        var report = SingleDayReport(
            recent: 0, ("2026-06-03", 4), ("2026-06-10", 4), ("2026-06-17", 4));

        var row = Assert.Single(report.Locations);
        Assert.Equal(3m, row.BaselineTotal);
    }

    [Fact]
    public void Evaluate_WindowDayWithNoCalls_ContributesZeroToTheRecentTotal()
    {
        var request = new BaselineRequest(
            SingleDayWindowEnd,
            WindowDays: 3,
            [History(Site, "2026-01-01")],
            [Count(Site, "2026-07-01", 5)]);

        var row = Assert.Single(_calculator.Evaluate(request).Locations);

        // 2026-06-29 and 06-30 have no rows and are counted, not skipped.
        Assert.Equal(5, row.RecentTotal);
    }

    [Fact]
    public void Evaluate_BaselineBelowTheLowVolumeFloor_ReturnsLowVolumeWithNoPercentage()
    {
        var report = SingleDayReport(
            recent: 9,
            ("2026-06-03", 4), ("2026-06-10", 4), ("2026-06-17", 4), ("2026-06-24", 4));

        var row = Assert.Single(report.Locations);
        Assert.Equal(Verdict.LowVolume, row.Verdict);
        Assert.Equal(4m, row.BaselineTotal);
        Assert.Equal(5m, row.AbsoluteDelta);
        Assert.Null(row.PercentDelta);
        Assert.Null(row.BandPercent);
    }

    [Fact]
    public void Evaluate_BaselineExactlyAtTheLowVolumeFloor_IsJudgedAgainstTheBand()
    {
        var report = SingleDayReport(
            recent: 10,
            ("2026-06-03", 10), ("2026-06-10", 10), ("2026-06-17", 10), ("2026-06-24", 10));

        var row = Assert.Single(report.Locations);
        Assert.Equal(10m, row.BaselineTotal);
        Assert.Equal(Verdict.Typical, row.Verdict);
        Assert.Equal(0m, row.PercentDelta);
        Assert.Equal(31.62m, row.BandPercent);
    }

    [Fact]
    public void Evaluate_PriorOccurrenceBeforeTheLocationExisted_ReturnsInsufficientHistory()
    {
        // The location's first activity is 2026-06-10, so the 06-03 prior never existed.
        var request = new BaselineRequest(
            SingleDayWindowEnd,
            WindowDays: 1,
            [History(Site, "2026-06-10")],
            [Count(Site, "2026-07-01", 7), Count(Site, "2026-06-10", 40),
             Count(Site, "2026-06-17", 40), Count(Site, "2026-06-24", 40)]);

        var row = Assert.Single(_calculator.Evaluate(request).Locations);
        Assert.Equal(Verdict.InsufficientHistory, row.Verdict);
        Assert.Equal(7, row.RecentTotal);
        Assert.Equal(0m, row.BaselineTotal);
        Assert.Equal(0m, row.AbsoluteDelta);
        Assert.Null(row.PercentDelta);
        Assert.Null(row.BandPercent);
        Assert.Null(row.BaselineOutlier);
    }

    [Fact]
    public void Evaluate_OldestPriorOccurrenceExactlyOnFirstLocalDate_IsSufficientHistory()
    {
        var request = new BaselineRequest(
            SingleDayWindowEnd,
            WindowDays: 1,
            [History(Site, "2026-06-03")],
            [Count(Site, "2026-06-03", 40)]);

        var row = Assert.Single(_calculator.Evaluate(request).Locations);
        Assert.NotEqual(Verdict.InsufficientHistory, row.Verdict);
        Assert.Equal(10m, row.BaselineTotal);
    }

    [Fact]
    public void Evaluate_ZeroBaseline_ReportsAbsoluteDeltaOnlyWithoutDividing()
    {
        var report = SingleDayReport(recent: 7);

        var row = Assert.Single(report.Locations);
        Assert.Equal(0m, row.BaselineTotal);
        Assert.Equal(7m, row.AbsoluteDelta);
        Assert.Null(row.PercentDelta);
        Assert.Null(row.BandPercent);
        Assert.Null(row.BaselineOutlier);

        // The low-volume floor of 10 calls subsumes a zero baseline, so this is the verdict a zero
        // lands on: both rules report the same row and neither divides.
        Assert.Equal(Verdict.LowVolume, row.Verdict);
    }

    // Four equal priors mean the baseline equals callsPerPriorDay. Baseline 16 gives a band of max(10, 100/4) = 25% exactly; baseline 400 gives max(10, 5) = 10%,
    // so the floor governs. Both are exact in decimal, which is what makes the boundary assertable.
    [Theory]
    [InlineData(16, 20, "25.00", "25.00", Verdict.Typical)]
    [InlineData(16, 21, "31.25", "25.00", Verdict.Above)]
    [InlineData(16, 12, "-25.00", "25.00", Verdict.Typical)]
    [InlineData(16, 11, "-31.25", "25.00", Verdict.Below)]
    [InlineData(400, 440, "10.00", "10.00", Verdict.Typical)]
    [InlineData(400, 441, "10.25", "10.00", Verdict.Above)]
    [InlineData(400, 360, "-10.00", "10.00", Verdict.Typical)]
    [InlineData(400, 359, "-10.25", "10.00", Verdict.Below)]
    public void Evaluate_PercentDeltaAtOrBeyondTheBand_ReturnsTheExpectedVerdict(
        int callsPerPriorDay,
        int recentCalls,
        string expectedPercentDelta,
        string expectedBandPercent,
        Verdict expectedVerdict)
    {
        var report = SingleDayReport(
            recentCalls,
            ("2026-06-03", callsPerPriorDay), ("2026-06-10", callsPerPriorDay),
            ("2026-06-17", callsPerPriorDay), ("2026-06-24", callsPerPriorDay));

        var row = Assert.Single(report.Locations);
        Assert.Equal(Decimal(expectedPercentDelta), row.PercentDelta);
        Assert.Equal(Decimal(expectedBandPercent), row.BandPercent);
        Assert.Equal(expectedVerdict, row.Verdict);
    }

    [Fact]
    public void Evaluate_RowsInADifferentOrder_ProducesAnIdenticalReport()
    {
        var ordered = Account6Fixture.Request();
        var shuffled = ordered with
        {
            Locations = [.. ordered.Locations.Reverse()],
            DailyCounts = [.. ordered.DailyCounts
                .OrderByDescending(count => count.LocalDate)
                .ThenByDescending(count => count.Calls)],
        };

        var fromOrdered = _calculator.Evaluate(ordered);
        var fromShuffled = _calculator.Evaluate(shuffled);

        Assert.Equal(fromOrdered.RecentTotal, fromShuffled.RecentTotal);
        Assert.Equal(fromOrdered.BaselineTotal, fromShuffled.BaselineTotal);
        Assert.Equal(fromOrdered.Locations, fromShuffled.Locations);
    }

    [Fact]
    public void Evaluate_DuplicateRows_AreCountedAsIsNotDeduplicated()
    {
        var request = new BaselineRequest(
            SingleDayWindowEnd,
            WindowDays: 1,
            [History(Site, "2026-01-01")],
            [Count(Site, "2026-07-01", 5), Count(Site, "2026-07-01", 5),
             Count(Site, "2026-06-03", 4), Count(Site, "2026-06-03", 4)]);

        var row = Assert.Single(_calculator.Evaluate(request).Locations);

        // Deduplicating would give 5 and 1; counting as-is gives 10 and 2 (PRD Open Question #2).
        Assert.Equal(10, row.RecentTotal);
        Assert.Equal(2m, row.BaselineTotal);
    }

    [Fact]
    public void Evaluate_TiedPercentDeltas_RanksByLocationName()
    {
        var report = _calculator.Evaluate(new BaselineRequest(
            SingleDayWindowEnd,
            WindowDays: 1,
            [History("Site Z", "2026-01-01"), History("Site A", "2026-01-01")],
            [.. EqualPriors("Site Z", 16), .. EqualPriors("Site A", 16),
             Count("Site Z", "2026-07-01", 21), Count("Site A", "2026-07-01", 21)]));

        Assert.Equal(new[] { "Site A", "Site Z" }, report.Locations.Select(row => row.Location).ToArray());
    }

    [Fact]
    public void Evaluate_Ranking_TreatsASpikeAndADropAsEquallyDeviant()
    {
        var report = _calculator.Evaluate(new BaselineRequest(
            SingleDayWindowEnd,
            WindowDays: 1,
            [History("Site Mid", "2026-01-01"), History("Site Up", "2026-01-01"),
             History("Site Down", "2026-01-01")],
            [.. EqualPriors("Site Mid", 16), .. EqualPriors("Site Up", 16), .. EqualPriors("Site Down", 16),
             Count("Site Mid", "2026-07-01", 20), Count("Site Up", "2026-07-01", 21),
             Count("Site Down", "2026-07-01", 11)]));

        // ±31.25% outrank +25%, and the tie between them breaks on name.
        Assert.Equal(
            new[] { "Site Down", "Site Up", "Site Mid" },
            report.Locations.Select(row => row.Location).ToArray());
        Assert.Equal(
            new[] { Verdict.Below, Verdict.Above, Verdict.Typical },
            report.Locations.Select(row => row.Verdict).ToArray());
    }

    [Fact]
    public void Evaluate_Ranking_PutsLowVolumeAndInsufficientHistoryLast()
    {
        var report = _calculator.Evaluate(new BaselineRequest(
            SingleDayWindowEnd,
            WindowDays: 1,
            // "Site B" is judged and sits at 0% deviation; the other two carry no percentage and must
            // still rank behind it rather than tying with it at zero.
            [History("Site A", "2026-06-24"), History("Site B", "2026-01-01"),
             History("Site C", "2026-01-01")],
            [.. EqualPriors("Site A", 40), .. EqualPriors("Site B", 10), .. EqualPriors("Site C", 1),
             Count("Site B", "2026-07-01", 10)]));

        Assert.Equal(new[] { "Site B", "Site A", "Site C" }, report.Locations.Select(row => row.Location).ToArray());
        Assert.Equal(
            new[] { Verdict.Typical, Verdict.InsufficientHistory, Verdict.LowVolume },
            report.Locations.Select(row => row.Verdict).ToArray());
    }

    [Fact]
    public void Evaluate_SourceDayAboveTheOutlierShare_IsFlagged()
    {
        // Baseline 11.25; 06-03 contributes 12/4 = 3, a 26.67% share against a 2.8125 threshold.
        var report = SingleDayReport(
            recent: 11,
            ("2026-06-03", 12), ("2026-06-10", 11), ("2026-06-17", 11), ("2026-06-24", 11));

        var row = Assert.Single(report.Locations);
        var outlier = row.BaselineOutlier;
        Assert.NotNull(outlier);
        Assert.Equal(Date("2026-06-03"), outlier.Date);
        Assert.Equal(26.67m, outlier.SharePercent);
    }

    [Fact]
    public void Evaluate_SourceDayExactlyAtTheOutlierShare_IsNotFlagged()
    {
        // Four equal priors: each contributes exactly 25%, and the rule is strictly more than 25%.
        var report = SingleDayReport(
            recent: 11,
            ("2026-06-03", 11), ("2026-06-10", 11), ("2026-06-17", 11), ("2026-06-24", 11));

        var row = Assert.Single(report.Locations);
        Assert.Equal(11m, row.BaselineTotal);
        Assert.Null(row.BaselineOutlier);
    }

    [Fact]
    public void Evaluate_DataOnlyOnTheOldestLookbackDay_StillGivesTheFirstWindowDayABaseline()
    {
        // The fetch-span trap (techspec § Known Risks). 2026-05-31 is 28 days before the window's
        // first day, 2026-06-28, and is a prior occurrence for that day alone. It is the only data in
        // the request, so an implementation that fetched or looked back only as far as the window
        // start would report a baseline of zero here and fail silently in production.
        var request = new BaselineRequest(
            Account6Fixture.WindowEnd,
            Account6Fixture.WindowDays,
            [History(Site, "2026-01-01")],
            [Count(Site, "2026-05-31", 48)]);

        var row = Assert.Single(_calculator.Evaluate(request).Locations);
        Assert.Equal(Account6Fixture.WindowStart, Date("2026-06-28"));
        Assert.Equal(12m, row.BaselineTotal);
        Assert.Equal(-12m, row.AbsoluteDelta);
        Assert.Equal(-100m, row.PercentDelta);
        Assert.Equal(Verdict.Below, row.Verdict);

        // The same request with that one pre-window row dropped collapses to nothing, which is what
        // the narrowed-fetch-span bug looks like.
        var narrowed = request with { DailyCounts = [] };
        Assert.Equal(0m, Assert.Single(_calculator.Evaluate(narrowed).Locations).BaselineTotal);
    }

    [Fact]
    public void Evaluate_Summary_SumsTheLocationColumns()
    {
        var report = _calculator.Evaluate(new BaselineRequest(
            SingleDayWindowEnd,
            WindowDays: 1,
            [History("Site A", "2026-01-01"), History("Site B", "2026-01-01")],
            [.. EqualPriors("Site A", 16), .. EqualPriors("Site B", 40),
             Count("Site A", "2026-07-01", 20), Count("Site B", "2026-07-01", 30)]));

        Assert.Equal(50, report.RecentTotal);
        Assert.Equal(56m, report.BaselineTotal);
        Assert.Equal(report.Locations.Sum(row => row.RecentTotal), report.RecentTotal);
        Assert.Equal(report.Locations.Sum(row => row.BaselineTotal), report.BaselineTotal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Evaluate_WindowDaysBelowOne_Throws(int windowDays)
    {
        var request = new BaselineRequest(SingleDayWindowEnd, windowDays, [], []);

        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Evaluate(request));
    }

    [Fact]
    public void Evaluate_Account6At30Days_ReconcilesWithTheSeedDerivedTotals()
    {
        var report = _calculator.Evaluate(Account6Fixture.Request());

        Assert.Equal(Account6Fixture.WindowStart, report.WindowStart);
        Assert.Equal(Account6Fixture.WindowEnd, report.WindowEnd);
        Assert.Equal(175, report.RecentTotal);
        Assert.Equal(311.75m, report.BaselineTotal);
        Assert.Equal(15, report.Locations.Count);
    }

    [Fact]
    public void Evaluate_Account6At30Days_RanksSiteOFirstAsTheLargestDeviation()
    {
        var report = _calculator.Evaluate(Account6Fixture.Request());

        var first = report.Locations[0];
        Assert.Equal("Site O", first.Location);
        Assert.Equal(9, first.RecentTotal);
        Assert.Equal(21.25m, first.BaselineTotal);
        Assert.Equal(-12.25m, first.AbsoluteDelta);
        Assert.Equal(-57.65m, first.PercentDelta);
        Assert.Equal(21.69m, first.BandPercent);
        Assert.Equal(Verdict.Below, first.Verdict);
    }

    [Theory]
    [InlineData("Site A", 14, "22.50", "-37.78")]
    [InlineData("Site B", 13, "24.00", "-45.83")]
    [InlineData("Site C", 20, "27.00", "-25.93")]
    [InlineData("Site D", 8, "17.25", "-53.62")]
    [InlineData("Site E", 18, "28.00", "-35.71")]
    [InlineData("Site F", 8, "18.50", "-56.76")]
    [InlineData("Site G", 9, "17.50", "-48.57")]
    [InlineData("Site H", 13, "22.50", "-42.22")]
    [InlineData("Site I", 12, "20.00", "-40.00")]
    [InlineData("Site J", 9, "17.00", "-47.06")]
    [InlineData("Site K", 13, "19.50", "-33.33")]
    [InlineData("Site L", 14, "25.25", "-44.55")]
    [InlineData("Site M", 7, "15.25", "-54.10")]
    [InlineData("Site N", 8, "16.25", "-50.77")]
    [InlineData("Site O", 9, "21.25", "-57.65")]
    public void Evaluate_Account6At30Days_ReconcilesEachLocation(
        string location,
        int expectedRecentTotal,
        string expectedBaselineTotal,
        string expectedPercentDelta)
    {
        var report = _calculator.Evaluate(Account6Fixture.Request());

        var row = report.Locations.Single(candidate => candidate.Location == location);
        Assert.Equal(expectedRecentTotal, row.RecentTotal);
        Assert.Equal(Decimal(expectedBaselineTotal), row.BaselineTotal);
        Assert.Equal(Decimal(expectedPercentDelta), row.PercentDelta);
        Assert.Equal(Verdict.Below, row.Verdict);
    }

    [Fact]
    public void Evaluate_Account6At30Days_FlagsTheAnomalousDayOnEveryLocation()
    {
        // techspec § Key Decisions #1: at 25% the flag fires on exactly these 15 locations, all of
        // them pointing at 2026-06-03. This pins the decision to report the anomaly, not smooth it.
        var report = _calculator.Evaluate(Account6Fixture.Request());

        Assert.All(report.Locations, row =>
        {
            var outlier = row.BaselineOutlier;
        Assert.NotNull(outlier);
            Assert.Equal(Account6Fixture.AnomalyDate, outlier.Date);
            Assert.True(
                outlier.SharePercent > BaselineThresholds.OutlierSharePercent,
                $"{row.Location} share {outlier.SharePercent} should exceed the threshold");
        });
    }

    private BaselineReport SingleDayReport(int recent, params (string Date, int Calls)[] priors)
    {
        var counts = priors.Select(prior => Count(Site, prior.Date, prior.Calls)).ToList();
        if (recent > 0)
        {
            counts.Add(new DailyLocationCount(Site, SingleDayWindowEnd, recent));
        }

        return _calculator.Evaluate(new BaselineRequest(
            SingleDayWindowEnd, WindowDays: 1, [History(Site, "2026-01-01")], counts));
    }

    private static IEnumerable<DailyLocationCount> EqualPriors(string location, int callsPerDay) =>
        new[] { "2026-06-03", "2026-06-10", "2026-06-17", "2026-06-24" }
            .Select(date => Count(location, date, callsPerDay));

    private static LocationHistory History(string location, string firstLocalDate) =>
        new(location, Date(firstLocalDate), Date("2026-12-31"));

    private static DailyLocationCount Count(string location, string localDate, int calls) =>
        new(location, Date(localDate), calls);

    private static DateOnly Date(string value) =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static decimal Decimal(string value) =>
        decimal.Parse(value, CultureInfo.InvariantCulture);
}
