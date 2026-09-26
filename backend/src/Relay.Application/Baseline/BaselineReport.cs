namespace Relay.Application.Baseline;

/// <param name="BaselineTotal">
/// The sum of the rows' baselines, so the summary line is always the visible column added up (FR16).
/// </param>
public sealed record BaselineReport(
    DateOnly WindowStart,
    DateOnly WindowEnd,
    int RecentTotal,
    decimal BaselineTotal,
    IReadOnlyList<LocationBaseline> Locations)
{
    private const decimal PercentScale = 100m;

    public decimal AbsoluteDelta => RecentTotal - BaselineTotal;

    /// <summary>
    /// Null when there is nothing to divide by — an account with no activity at all (account 20)
    /// reports totals of zero and no percentage, never a division (PRD FR8). Derived here rather
    /// than at the API boundary: arithmetic belongs in this layer, not in a controller (PRD FR12).
    /// Unrounded; rounding for display happens where the response is shaped.
    /// </summary>
    public decimal? PercentDelta =>
        BaselineTotal == 0m ? null : AbsoluteDelta / BaselineTotal * PercentScale;
}
