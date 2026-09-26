using Relay.Application.Baseline;

namespace Relay.Api.Contracts;

/// <summary>
/// Domain records → wire contract. The only place the two shapes meet, and the only place the
/// <see cref="Verdict"/> enum is turned into the snake_case strings the payload publishes — one
/// switch over a closed set, in one place (<c>csharp-dotnet.md</c> § Design principles).
/// No arithmetic beyond display rounding: the numbers arrive already computed (PRD FR12).
/// </summary>
public static class BaselineResponseMapper
{
    public static ActivityBaselineResponse ToResponse(BaselineOutcome.Ready outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var report = outcome.Report;

        return new ActivityBaselineResponse(
            outcome.Account.Id,
            outcome.Account.Name,
            outcome.Account.Timezone,
            outcome.WindowDays,
            report.WindowStart,
            report.WindowEnd,
            new BaselineSummaryResponse(
                report.RecentTotal,
                report.BaselineTotal,
                report.AbsoluteDelta,
                RoundPercent(report.PercentDelta)),
            report.Locations.Select(ToResponse).ToList());
    }

    private static LocationBaselineResponse ToResponse(LocationBaseline row) =>
        new(row.Location,
            row.RecentTotal,
            row.BaselineTotal,
            row.AbsoluteDelta,
            RoundPercent(row.PercentDelta),
            RoundPercent(row.BandPercent),
            ToWireValue(row.Verdict),
            row.BaselineOutlier is { } outlier
                ? new BaselineOutlierResponse(outlier.Date, RoundPercent(outlier.SharePercent))
                : null);

    private static string ToWireValue(Verdict verdict) => verdict switch
    {
        Verdict.Above => "above",
        Verdict.Typical => "typical",
        Verdict.Below => "below",
        Verdict.LowVolume => "low_volume",
        Verdict.InsufficientHistory => "insufficient_history",
        _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "Unmapped verdict."),
    };

    /// <summary>
    /// Display rounding, and only here: the band comparison and the ranking in
    /// <see cref="BaselineCalculator"/> both run on unrounded values, so a row never changes verdict
    /// or position because of how it is printed.
    /// </summary>
    private static decimal? RoundPercent(decimal? percent) =>
        percent.HasValue ? RoundPercent(percent.Value) : null;

    private static decimal RoundPercent(decimal percent) =>
        Math.Round(percent, BaselineThresholds.ReportedPercentDecimals, MidpointRounding.AwayFromZero);
}
