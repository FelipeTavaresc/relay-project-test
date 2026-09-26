namespace Relay.Application.Baseline;

/// <param name="BaselineTotal">Zero for InsufficientHistory: a partial average is not a baseline.</param>
/// <param name="PercentDelta">Null wherever a percentage would be a lie (PRD FR6, FR7, FR8).</param>
/// <param name="BandPercent">The band the percentage was judged against; null when no band was used.</param>
public sealed record LocationBaseline(
    string Location,
    int RecentTotal,
    decimal BaselineTotal,
    decimal AbsoluteDelta,
    decimal? PercentDelta,
    decimal? BandPercent,
    Verdict Verdict,
    OutlierDay? BaselineOutlier);
