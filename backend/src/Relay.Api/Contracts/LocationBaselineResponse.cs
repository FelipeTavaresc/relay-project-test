namespace Relay.Api.Contracts;

/// <summary>One table row: PRD FR13, in the ranked order the application layer produced (FR14).</summary>
/// <param name="Location">The location label as the seed carries it; no timezone of its own.</param>
/// <param name="RecentTotal">Calls at this location inside the window.</param>
/// <param name="BaselineTotal">The trailing same-weekday average, summed over the window's days.</param>
/// <param name="AbsoluteDelta">Recent minus baseline. Reported even where a percentage is not.</param>
/// <param name="PercentDelta">Null for <c>low_volume</c>, <c>insufficient_history</c>, and a zero baseline.</param>
/// <param name="BandPercent">The band the percentage was judged against; null when no band was used.</param>
/// <param name="Verdict">One of <c>above</c>, <c>typical</c>, <c>below</c>, <c>low_volume</c>, <c>insufficient_history</c>.</param>
/// <param name="BaselineOutlier">
/// Present when one source day supplies a disproportionate share of this baseline. Absent for most rows.
/// </param>
public sealed record LocationBaselineResponse(
    string Location,
    int RecentTotal,
    decimal BaselineTotal,
    decimal AbsoluteDelta,
    decimal? PercentDelta,
    decimal? BandPercent,
    string Verdict,
    BaselineOutlierResponse? BaselineOutlier);
