namespace Relay.Api.Contracts;

/// <summary>
/// The account-level line: "is the account normal?" (PRD FR16). Always the visible column added up,
/// so the table and the summary can never tell different stories.
/// </summary>
/// <param name="RecentTotal">Calls across every returned location inside the window.</param>
/// <param name="BaselineTotal">What those locations' same-weekday history predicted for the window.</param>
/// <param name="AbsoluteDelta">Recent minus baseline. Negative means quieter than normal.</param>
/// <param name="PercentDelta">Null when the baseline is zero — no percentage rather than a lie (FR8).</param>
public sealed record BaselineSummaryResponse(
    int RecentTotal,
    decimal BaselineTotal,
    decimal AbsoluteDelta,
    decimal? PercentDelta);
