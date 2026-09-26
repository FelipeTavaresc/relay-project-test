namespace Relay.Application.Baseline;

/// <param name="WindowEnd">The anchor: the account's latest local activity date (PRD FR2).</param>
/// <param name="Locations">
/// The row list. A daily count for a location absent here is ignored; a location here with no counts
/// still gets a row of zeroes.
/// </param>
/// <param name="DailyCounts">
/// Counts over the <em>fetch span</em>, which is wider than the window: the baseline for the window's
/// first day looks back 28 days, so the caller must supply
/// <c>WindowEnd − (WindowDays − 1) − 28 … WindowEnd</c>. Supplying only the window silently yields
/// zero baselines for the window's early days (techspec § Known Risks). Rows may arrive in any order
/// and may repeat a <c>(Location, LocalDate)</c> pair.
/// </param>
public sealed record BaselineRequest(
    DateOnly WindowEnd,
    int WindowDays,
    IReadOnlyList<LocationHistory> Locations,
    IReadOnlyList<DailyLocationCount> DailyCounts);
