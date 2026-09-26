using System.Globalization;
using Relay.Application.Baseline;

namespace Relay.UnitTests;

/// <summary>
/// The composition rules that sit between the queries and the pure calculator: the widened fetch
/// span, the location filter, and the outcomes the endpoint turns into status codes. The queries
/// themselves are faked — what the SQL actually returns is verified against the real database by
/// hand (task 4.8–4.10), because the in-memory provider cannot prove SQL translation.
/// </summary>
public class ActivityBaselineServiceTests
{
    private static readonly DateOnly Anchor = Date("2026-07-27");
    private const string SiteA = "Site A";
    private const string SiteB = "Site B";

    [Theory]
    [InlineData(30, "2026-05-31")]
    [InlineData(60, "2026-05-01")]
    [InlineData(90, "2026-04-01")]
    public async Task GetBaselineAsync_FetchSpan_ReachesBackTwentyEightDaysBeforeTheWindowStart(
        int windowDays,
        string expectedFetchFrom)
    {
        // The window's *first* day needs its own four priors, the oldest 28 days earlier. Asking
        // only for the window is the named silent failure: the early days get a baseline of zero
        // and nothing complains (techspec § Known Risks).
        var queries = new FakeActivityQueries();

        var outcome = await Service(queries)
            .GetBaselineAsync(BaselineQuery.ForAllLocations(1, windowDays), CancellationToken.None);

        var report = Assert.IsType<BaselineOutcome.Ready>(outcome).Report;
        Assert.Equal(Date(expectedFetchFrom), queries.RequestedFrom);
        Assert.Equal(Anchor, queries.RequestedTo);
        Assert.Equal(report.WindowStart.AddDays(-BaselineThresholds.LookbackSpanDays), queries.RequestedFrom);
    }

    [Fact]
    public async Task GetBaselineAsync_FetchSpan_IsWideEnoughToGiveTheFirstWindowDayABaseline()
    {
        // One busy day on the oldest lookback date only. It sits outside the window, so it can
        // reach the report at all only through a fetch span wider than the window.
        var queries = new FakeActivityQueries
        {
            Counts = [new DailyLocationCount(SiteA, Date("2026-05-31"), 40)],
        };

        var outcome = await Service(queries)
            .GetBaselineAsync(new BaselineQuery(1, 30, [SiteA]), CancellationToken.None);

        var row = Assert.Single(Assert.IsType<BaselineOutcome.Ready>(outcome).Report.Locations);
        Assert.Equal(10m, row.BaselineTotal);
    }

    [Fact]
    public async Task GetBaselineAsync_UnknownAccount_ReturnsAccountNotFound()
    {
        var queries = new FakeActivityQueries { Account = null };

        var outcome = await Service(queries)
            .GetBaselineAsync(BaselineQuery.ForAllLocations(999, 30), CancellationToken.None);

        Assert.Equal(999, Assert.IsType<BaselineOutcome.AccountNotFound>(outcome).AccountId);
    }

    [Fact]
    public async Task GetBaselineAsync_LocationTheAccountDoesNotHave_IsRejectedNotIgnored()
    {
        var queries = new FakeActivityQueries();

        var outcome = await Service(queries)
            .GetBaselineAsync(new BaselineQuery(1, 30, [SiteA, "Site ZZ"]), CancellationToken.None);

        var unknown = Assert.IsType<BaselineOutcome.UnknownLocations>(outcome);
        Assert.Equal(["Site ZZ"], unknown.Locations);
    }

    [Fact]
    public async Task GetBaselineAsync_LocationFilter_NarrowsBothTheRowsAndTheSummary()
    {
        var queries = new FakeActivityQueries
        {
            Counts =
            [
                new DailyLocationCount(SiteA, Anchor, 5),
                new DailyLocationCount(SiteB, Anchor, 7),
            ],
        };

        var outcome = await Service(queries)
            .GetBaselineAsync(new BaselineQuery(1, 30, [SiteB]), CancellationToken.None);

        var report = Assert.IsType<BaselineOutcome.Ready>(outcome).Report;
        var row = Assert.Single(report.Locations);
        Assert.Equal(SiteB, row.Location);
        Assert.Equal(7, report.RecentTotal);
    }

    [Fact]
    public async Task GetBaselineAsync_EmptyLocationFilter_MeansEveryLocation()
    {
        var queries = new FakeActivityQueries();

        var outcome = await Service(queries)
            .GetBaselineAsync(BaselineQuery.ForAllLocations(1, 30), CancellationToken.None);

        Assert.Equal(2, Assert.IsType<BaselineOutcome.Ready>(outcome).Report.Locations.Count);
    }

    [Fact]
    public async Task GetBaselineAsync_AccountWithNoActivity_IsAnEmptyReportNotAnError()
    {
        // Account 20 in the seed. A valid query that matched nothing is a 200 (PRD FR17).
        var queries = new FakeActivityQueries
        {
            Account = new AccountContext(20, "Quiet Harbor Spa", "America/Chicago", LatestLocalDate: null),
            History = [],
        };

        var outcome = await Service(queries)
            .GetBaselineAsync(BaselineQuery.ForAllLocations(20, 30), CancellationToken.None);

        var report = Assert.IsType<BaselineOutcome.Ready>(outcome).Report;
        Assert.Empty(report.Locations);
        Assert.Equal(0, report.RecentTotal);
        Assert.Equal(0m, report.BaselineTotal);
    }

    [Fact]
    public async Task GetBaselineAsync_AccountWithNoActivity_AnchorsOnTheClockInTheAccountsOwnZone()
    {
        // No data means no anchor to take from the data, so the window has to come from somewhere.
        // 02:30 UTC on the 27th is still 21:30 on the 26th in Chicago (UTC−5 in September): the
        // account's own zone decides the date, and the clock is injected, not DateTime.UtcNow.
        var queries = new FakeActivityQueries
        {
            Account = new AccountContext(20, "Quiet Harbor Spa", "America/Chicago", LatestLocalDate: null),
            History = [],
        };
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 27, 2, 30, 0, TimeSpan.Zero));

        var outcome = await Service(queries, clock)
            .GetBaselineAsync(BaselineQuery.ForAllLocations(20, 30), CancellationToken.None);

        var report = Assert.IsType<BaselineOutcome.Ready>(outcome).Report;
        Assert.Equal(Date("2026-09-26"), report.WindowEnd);
        Assert.Equal(Date("2026-08-28"), report.WindowStart);
    }

    [Fact]
    public async Task GetBaselineAsync_AnchorsOnTheAccountsOwnLatestLocalDate_NotTheClock()
    {
        // Account 8's latest is three days behind account 6's, and the seed ends long before
        // "today" — a wall-clock anchor returns zero everywhere (techspec § Key Decisions #3).
        var queries = new FakeActivityQueries
        {
            Account = new AccountContext(8, "Lakeside Physio", "America/Chicago", Date("2026-07-24")),
        };

        var outcome = await Service(queries)
            .GetBaselineAsync(BaselineQuery.ForAllLocations(8, 30), CancellationToken.None);

        var report = Assert.IsType<BaselineOutcome.Ready>(outcome).Report;
        Assert.Equal(Date("2026-07-24"), report.WindowEnd);
        Assert.Equal(Date("2026-06-25"), report.WindowStart);
        Assert.Equal(Date("2026-05-28"), queries.RequestedFrom);
    }

    private static ActivityBaselineService Service(
        IActivityQueries queries,
        TimeProvider? timeProvider = null) =>
        new(queries, new BaselineCalculator(), timeProvider ?? new FixedTimeProvider(DateTimeOffset.UnixEpoch));

    private static DateOnly Date(string value) =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// Records the span the service asked for, which is what most of these tests are about.
    /// </summary>
    private sealed class FakeActivityQueries : IActivityQueries
    {
        public AccountContext? Account { get; init; } =
            new(1, "Summit Auto Group", "America/Chicago", Anchor);

        public IReadOnlyList<LocationHistory> History { get; init; } =
        [
            new(SiteA, Date("2026-02-01"), Anchor),
            new(SiteB, Date("2026-02-01"), Anchor),
        ];

        public IReadOnlyList<DailyLocationCount> Counts { get; init; } = [];

        public DateOnly? RequestedFrom { get; private set; }

        public DateOnly? RequestedTo { get; private set; }

        public Task<AccountContext?> GetAccountAsync(int accountId, CancellationToken cancellationToken) =>
            Task.FromResult(Account);

        public Task<IReadOnlyList<LocationHistory>> GetLocationHistoryAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            Task.FromResult(History);

        public Task<IReadOnlyList<DailyLocationCount>> GetDailyCallCountsAsync(
            int accountId,
            DateOnly fromLocalDate,
            DateOnly toLocalDate,
            CancellationToken cancellationToken)
        {
            RequestedFrom = fromLocalDate;
            RequestedTo = toLocalDate;
            return Task.FromResult(Counts);
        }
    }
}
