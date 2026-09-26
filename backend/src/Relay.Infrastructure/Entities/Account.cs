namespace Relay.Infrastructure.Entities;

/// <summary>
/// A Relay customer. Maps to <c>accounts</c> in <c>tasks/prd-activity-baseline/schema.sql</c>.
/// </summary>
/// <remarks>
/// This is a persistence entity and never crosses the API boundary; the response DTOs are
/// separate types in <c>Relay.Api</c>.
/// </remarks>
public sealed class Account
{
    /// <summary>Supplied by the seed, not generated — see the configuration class.</summary>
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Industry { get; init; }

    /// <summary>
    /// IANA zone name, e.g. <c>America/Chicago</c>. The only timezone the schema carries, and the
    /// one the day-bucketing query passes straight to <c>AT TIME ZONE</c> (PRD FR11).
    /// </summary>
    public required string Timezone { get; init; }

    /// <summary>UTC. Stored as <c>timestamp without time zone</c>; see the configuration class.</summary>
    public required DateTime CreatedAt { get; init; }
}
