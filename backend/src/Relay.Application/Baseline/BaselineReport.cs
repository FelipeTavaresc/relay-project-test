namespace Relay.Application.Baseline;

/// <param name="BaselineTotal">
/// The sum of the rows' baselines, so the summary line is always the visible column added up (FR16).
/// </param>
public sealed record BaselineReport(
    DateOnly WindowStart,
    DateOnly WindowEnd,
    int RecentTotal,
    decimal BaselineTotal,
    IReadOnlyList<LocationBaseline> Locations);
