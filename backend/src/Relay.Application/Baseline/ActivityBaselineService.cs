namespace Relay.Application.Baseline;

/// <inheritdoc cref="IActivityBaselineService"/>
public sealed class ActivityBaselineService(
    IActivityQueries queries,
    IBaselineCalculator calculator,
    TimeProvider timeProvider) : IActivityBaselineService
{
    public async Task<BaselineOutcome> GetBaselineAsync(
        BaselineQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var account = await queries.GetAccountAsync(query.AccountId, cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
        {
            return new BaselineOutcome.AccountNotFound(query.AccountId);
        }

        var history = await queries.GetLocationHistoryAsync(query.AccountId, cancellationToken)
            .ConfigureAwait(false);

        var unknown = UnknownLocationsIn(query.Locations, history);
        if (unknown.Count > 0)
        {
            return new BaselineOutcome.UnknownLocations(unknown);
        }

        var rows = Filter(history, query.Locations);

        // The anchor is the account's own latest local event date, never wall-clock time: the seed
        // ends 2026-07-27, so `now` would return zero everywhere (techspec § Key Decisions #3).
        var anchor = account.LatestLocalDate ?? TodayIn(account.Timezone);

        // Wider than the window on purpose. The baseline for the window's *first* day looks back 28
        // days, so asking only for the window would silently give the early days a baseline of zero
        // (techspec § System Architecture → Data flow).
        var fetchFrom = anchor
            .AddDays(-(query.WindowDays - 1))
            .AddDays(-BaselineThresholds.LookbackSpanDays);

        var dailyCounts = await queries
            .GetDailyCallCountsAsync(query.AccountId, fetchFrom, anchor, cancellationToken)
            .ConfigureAwait(false);

        var report = calculator.Evaluate(new BaselineRequest(anchor, query.WindowDays, rows, dailyCounts));

        return new BaselineOutcome.Ready(account, query.WindowDays, report);
    }

    private static IReadOnlyList<string> UnknownLocationsIn(
        IReadOnlyList<string> requested,
        IReadOnlyList<LocationHistory> history)
    {
        if (requested.Count == 0)
        {
            return [];
        }

        var known = history.Select(row => row.Location).ToHashSet(StringComparer.Ordinal);
        return requested.Where(name => !known.Contains(name)).ToList();
    }

    private static IReadOnlyList<LocationHistory> Filter(
        IReadOnlyList<LocationHistory> history,
        IReadOnlyList<string> requested)
    {
        if (requested.Count == 0)
        {
            return history;
        }

        var wanted = requested.ToHashSet(StringComparer.Ordinal);
        return history.Where(row => wanted.Contains(row.Location)).ToList();
    }

    /// <summary>
    /// Only reached by an account with no activity at all (account 20), which has no anchor to take
    /// from its data. The window still has to mean something, so it ends today in the account's own
    /// zone — from the injected <see cref="TimeProvider"/>, never <c>DateTime.UtcNow</c>.
    /// </summary>
    private DateOnly TodayIn(string timezone)
    {
        var utcNow = timeProvider.GetUtcNow();
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out var found)
            ? found
            : TimeZoneInfo.Utc;

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, zone).DateTime);
    }
}
