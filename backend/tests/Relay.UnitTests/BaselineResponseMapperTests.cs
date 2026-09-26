using Relay.Api.Contracts;
using Relay.Application.Baseline;
using Relay.UnitTests.Fixtures;

namespace Relay.UnitTests;

/// <summary>
/// The boundary mapping: the wire contract in techspec § API Endpoints, the snake_case verdict
/// strings, the rows where a percentage must be absent rather than zero, and the optional outlier.
/// Checked here rather than through HTTP because this is where the shape is decided — the endpoint's
/// status codes are exercised by hand against the real database (task 5.9).
/// </summary>
public class BaselineResponseMapperTests
{
    private static readonly AccountContext Account6 =
        new(6, "Metro Collision Centers", "America/New_York", Account6Fixture.WindowEnd);

    [Fact]
    public void ToResponse_Account6At30Days_CarriesTheAccountAndWindowUnchanged()
    {
        var response = BaselineResponseMapper.ToResponse(Account6Report());

        Assert.Equal(6, response.AccountId);
        Assert.Equal("Metro Collision Centers", response.AccountName);
        Assert.Equal("America/New_York", response.Timezone);
        Assert.Equal(30, response.WindowDays);
        Assert.Equal(Account6Fixture.WindowStart, response.WindowStart);
        Assert.Equal(Account6Fixture.WindowEnd, response.WindowEnd);
        Assert.Equal(15, response.Locations.Count);
    }

    [Fact]
    public void ToResponse_Account6At30Days_ReconcilesTheSummaryLine()
    {
        var summary = BaselineResponseMapper.ToResponse(Account6Report()).Summary;

        Assert.Equal(175, summary.RecentTotal);
        Assert.Equal(311.75m, summary.BaselineTotal);
        Assert.Equal(-136.75m, summary.AbsoluteDelta);
        Assert.Equal(-43.87m, summary.PercentDelta);
    }

    [Fact]
    public void ToResponse_Account6At30Days_ShapesSiteOAsTheSamplePayload()
    {
        var response = BaselineResponseMapper.ToResponse(Account6Report());

        var siteO = response.Locations[0];
        Assert.Equal("Site O", siteO.Location);
        Assert.Equal(9, siteO.RecentTotal);
        Assert.Equal(21.25m, siteO.BaselineTotal);
        Assert.Equal(-12.25m, siteO.AbsoluteDelta);
        Assert.Equal(-57.65m, siteO.PercentDelta);
        Assert.Equal(21.69m, siteO.BandPercent);
        Assert.Equal("below", siteO.Verdict);
    }

    [Fact]
    public void ToResponse_Account6At30Days_FlagsTheAnomalousSourceDayOnEveryLocation()
    {
        var response = BaselineResponseMapper.ToResponse(Account6Report());

        Assert.All(response.Locations, row =>
        {
            var outlier = Assert.IsType<BaselineOutlierResponse>(row.BaselineOutlier);
            Assert.Equal(Account6Fixture.AnomalyDate, outlier.Date);
            Assert.True(outlier.SharePercent > BaselineThresholds.OutlierSharePercent);
        });
    }

    [Theory]
    [InlineData(Verdict.Above, "above")]
    [InlineData(Verdict.Typical, "typical")]
    [InlineData(Verdict.Below, "below")]
    [InlineData(Verdict.LowVolume, "low_volume")]
    [InlineData(Verdict.InsufficientHistory, "insufficient_history")]
    public void ToResponse_Verdict_SerialisesAsTheSnakeCaseWireValue(Verdict verdict, string expected)
    {
        var response = BaselineResponseMapper.ToResponse(ReportOf(Row("Site A", verdict, percentDelta: 12m)));

        Assert.Equal(expected, response.Locations[0].Verdict);
    }

    [Theory]
    [InlineData(Verdict.LowVolume)]
    [InlineData(Verdict.InsufficientHistory)]
    public void ToResponse_RowWithoutAVerdict_OmitsBothPercentages(Verdict verdict)
    {
        var row = Row("Site A", verdict, percentDelta: null);

        var mapped = BaselineResponseMapper.ToResponse(ReportOf(row)).Locations[0];

        Assert.Null(mapped.PercentDelta);
        Assert.Null(mapped.BandPercent);
        Assert.Equal(-4m, mapped.AbsoluteDelta);
    }

    [Fact]
    public void ToResponse_RowWithoutAnOutlier_OmitsTheOutlierBlock()
    {
        var mapped = BaselineResponseMapper.ToResponse(ReportOf(Row("Site A", Verdict.Typical, 2m))).Locations[0];

        Assert.Null(mapped.BaselineOutlier);
    }

    [Fact]
    public void ToResponse_AccountWithNoActivity_IsAnEmptyReportRatherThanADivision()
    {
        // Account 20 (Quiet Harbor Spa) has zero events. A valid query that matched nothing is a 200
        // with zero totals and no percentage, never an error and never a divide-by-zero (PRD FR17, FR8).
        var quiet = new AccountContext(20, "Quiet Harbor Spa", "America/Chicago", LatestLocalDate: null);
        var report = new BaselineReport(
            Account6Fixture.WindowStart, Account6Fixture.WindowEnd, 0, 0m, []);

        var response = BaselineResponseMapper.ToResponse(new BaselineOutcome.Ready(quiet, 30, report));

        Assert.Empty(response.Locations);
        Assert.Equal(0, response.Summary.RecentTotal);
        Assert.Equal(0m, response.Summary.BaselineTotal);
        Assert.Equal(0m, response.Summary.AbsoluteDelta);
        Assert.Null(response.Summary.PercentDelta);
    }

    [Fact]
    public void ToResponse_Percentages_AreRoundedToTwoDecimalsAtTheBoundary()
    {
        // The calculator ranks and decides on unrounded values; only the response is rounded, so a
        // row cannot change verdict or position because of how it is printed.
        var row = new LocationBaseline(
            "Site A", 10, 8m, 2m, PercentDelta: 25.005m, BandPercent: 35.355m,
            Verdict.Typical, new OutlierDay(Account6Fixture.AnomalyDate, 26.666m));

        var mapped = BaselineResponseMapper.ToResponse(ReportOf(row)).Locations[0];

        Assert.Equal(25.01m, mapped.PercentDelta);
        Assert.Equal(35.36m, mapped.BandPercent);
        Assert.Equal(26.67m, Assert.IsType<BaselineOutlierResponse>(mapped.BaselineOutlier).SharePercent);
    }

    private static BaselineOutcome.Ready Account6Report() =>
        new(Account6, Account6Fixture.WindowDays, new BaselineCalculator().Evaluate(Account6Fixture.Request()));

    private static LocationBaseline Row(string location, Verdict verdict, decimal? percentDelta) =>
        new(location, RecentTotal: 8, BaselineTotal: 12m, AbsoluteDelta: -4m,
            percentDelta, BandPercent: percentDelta.HasValue ? 28.87m : null, verdict,
            BaselineOutlier: null);

    private static BaselineOutcome.Ready ReportOf(LocationBaseline row) =>
        new(Account6,
            Account6Fixture.WindowDays,
            new BaselineReport(
                Account6Fixture.WindowStart, Account6Fixture.WindowEnd,
                row.RecentTotal, row.BaselineTotal, [row]));
}
