namespace Relay.Application.Baseline;

/// <summary>
/// The data the baseline needs, declared by the layer that consumes it. The implementation lives in
/// <c>Relay.Infrastructure</c>, so the dependency arrow points inward and this project never sees an
/// EF Core type (<c>csharp-dotnet.md</c> § Project layout).
/// </summary>
public interface IActivityQueries
{
    /// <summary>
    /// The account and its anchor. Null when no such account exists, so the caller can answer 404
    /// without an exception — a missing account is an expected outcome, not an exceptional one.
    /// </summary>
    Task<AccountContext?> GetAccountAsync(int accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Distinct locations with their first/last local activity date. Sources the row list, and the
    /// first date is what separates "this occurrence never existed" from a genuine zero (FR7 vs FR9).
    /// Empty for an account with no activity, never null.
    /// </summary>
    Task<IReadOnlyList<LocationHistory>> GetLocationHistoryAsync(
        int accountId,
        CancellationToken cancellationToken);

    /// <param name="fromLocalDate">
    /// The start of the <em>fetch span</em>, not of the window. The baseline for the window's first
    /// day looks back 28 days, so this must be
    /// <c>anchor − (windowDays − 1) − <see cref="BaselineThresholds.LookbackSpanDays"/></c>.
    /// </param>
    Task<IReadOnlyList<DailyLocationCount>> GetDailyCallCountsAsync(
        int accountId,
        DateOnly fromLocalDate,
        DateOnly toLocalDate,
        CancellationToken cancellationToken);
}
