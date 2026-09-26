using Microsoft.EntityFrameworkCore;
using Relay.Application.Baseline;
using Relay.Infrastructure.Queries.Rows;

namespace Relay.Infrastructure.Queries;

/// <summary>
/// The three baseline reads, all of them raw SQL for the same reason.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why raw SQL and not LINQ.</b> Every one of these queries groups by the event's <em>local</em>
/// calendar date: <c>(occurred_at AT TIME ZONE 'UTC' AT TIME ZONE accounts.timezone)::date</c>. The
/// zone is a column, not a constant, so the conversion is per row and EF Core has no LINQ construct
/// that translates to it — the nearest alternatives either pull 7,780 rows into memory or silently
/// substitute a fixed offset, and a fixed offset is wrong for the 8 March 2026 DST transition the
/// window spans (PRD FR11). <c>csharp-dotnet.md</c> § EF Core and data access allows raw SQL exactly
/// here, provided it is parameterised.
/// </para>
/// <para>
/// <b>Parameterisation.</b> Every one uses <c>FromSql</c> with an interpolated string, which wraps
/// each value in a <c>DbParameter</c>. <c>FromSqlRaw</c> is not used anywhere in this file and no SQL
/// is concatenated: the only things interpolated are values, never identifiers.
/// </para>
/// </remarks>
internal sealed class ActivityQueries(RelayDbContext dbContext) : IActivityQueries
{
    /// <summary>
    /// The measured metric (PRD FR1) — the highest-volume type and the only one in scope. Passed as
    /// a parameter, never inlined into the SQL text.
    /// </summary>
    private const string CallReceivedEventType = "call_received";

    public async Task<AccountContext?> GetAccountAsync(int accountId, CancellationToken cancellationToken)
    {
        // LEFT JOIN, not INNER: an account with no activity must still come back, with a null anchor,
        // so the caller can tell "no such account" (404) from "nothing happened yet" (200, empty).
        // Single, not First: the GROUP BY is on the primary key, so more than one row would mean
        // the model is wrong rather than that the caller should get an arbitrary one of them.
        var row = await dbContext.Set<AccountAnchorRow>()
            .FromSql(
                $"""
                 SELECT a.id       AS "Id",
                        a.name     AS "Name",
                        a.timezone AS "Timezone",
                        MAX((e.occurred_at AT TIME ZONE 'UTC' AT TIME ZONE a.timezone)::date)
                            AS "LatestLocalDate"
                 FROM accounts a
                 LEFT JOIN activity_events e
                        ON e.account_id = a.id
                       AND e.event_type = {CallReceivedEventType}
                 WHERE a.id = {accountId}
                 GROUP BY a.id, a.name, a.timezone
                 """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? null
            : new AccountContext(row.Id, row.Name, row.Timezone, row.LatestLocalDate);
    }

    public async Task<IReadOnlyList<LocationHistory>> GetLocationHistoryAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        // Ordered in SQL so the row list does not depend on whatever order the scan happens to
        // produce (csharp-dotnet.md § Tests: no ordering assumptions on an unordered query).
        var rows = await dbContext.Set<LocationHistoryRow>()
            .FromSql(
                $"""
                 SELECT e.location AS "Location",
                        MIN((e.occurred_at AT TIME ZONE 'UTC' AT TIME ZONE a.timezone)::date)
                            AS "FirstLocalDate",
                        MAX((e.occurred_at AT TIME ZONE 'UTC' AT TIME ZONE a.timezone)::date)
                            AS "LastLocalDate"
                 FROM activity_events e
                 JOIN accounts a ON a.id = e.account_id
                 WHERE e.account_id = {accountId}
                   AND e.event_type = {CallReceivedEventType}
                 GROUP BY e.location
                 ORDER BY e.location
                 """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Empty, never null, for an account with no activity.
        return rows
            .Select(row => new LocationHistory(row.Location, row.FirstLocalDate, row.LastLocalDate))
            .ToList();
    }

    public async Task<IReadOnlyList<DailyLocationCount>> GetDailyCallCountsAsync(
        int accountId,
        DateOnly fromLocalDate,
        DateOnly toLocalDate,
        CancellationToken cancellationToken)
    {
        // No DISTINCT. The seed's 12 exact-duplicate rows (9 of them call_received) are counted as
        // they stand — PRD Open Question #2 — and a unit test pins that so it cannot drift.
        //
        // The BETWEEN is on the computed local date and so is not sargable; the index
        // ix_activity_events_account_id_event_type_occurred_at carries the selective part of the
        // WHERE clause, and the dataset is 12,626 rows (techspec § The aggregation query).
        var rows = await dbContext.Set<DailyCallCountRow>()
            .FromSql(
                $"""
                 SELECT e.location AS "Location",
                        (e.occurred_at AT TIME ZONE 'UTC' AT TIME ZONE a.timezone)::date AS "LocalDate",
                        COUNT(*)::int AS "Calls"
                 FROM activity_events e
                 JOIN accounts a ON a.id = e.account_id
                 WHERE e.account_id = {accountId}
                   AND e.event_type = {CallReceivedEventType}
                   AND (e.occurred_at AT TIME ZONE 'UTC' AT TIME ZONE a.timezone)::date
                       BETWEEN {fromLocalDate} AND {toLocalDate}
                 GROUP BY e.location, 2
                 """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(row => new DailyLocationCount(row.Location, row.LocalDate, row.Calls))
            .ToList();
    }
}
