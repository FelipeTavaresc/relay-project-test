namespace Relay.Infrastructure.Entities;

/// <summary>
/// One inbound activity event. Maps to <c>activity_events</c> in
/// <c>tasks/prd-activity-baseline/schema.sql</c>.
/// </summary>
public sealed class ActivityEvent
{
    /// <summary>Supplied by the seed, not generated — see the configuration class.</summary>
    public required int Id { get; init; }

    public required int AccountId { get; init; }

    /// <summary>The account's site/branch label. Scoped per account; carries no timezone of its own.</summary>
    public required string Location { get; init; }

    /// <summary><c>call_received</c> | <c>lead_created</c> | <c>appointment_set</c>.</summary>
    public required string EventType { get; init; }

    /// <summary>
    /// UTC instant. Deliberately <see cref="DateTime"/> and not <see cref="DateTimeOffset"/>:
    /// Npgsql maps <c>DateTimeOffset</c> to <c>timestamptz</c>, and the column must be
    /// <c>timestamp without time zone</c> or the <c>AT TIME ZONE</c> day bucketing changes meaning
    /// (techspec § Known Risks). The column type wins over the techspec's CLR-type note.
    /// </summary>
    public required DateTime OccurredAt { get; init; }

    /// <summary>Only meaningful for calls; null otherwise.</summary>
    public int? DurationSeconds { get; init; }

    /// <summary>e.g. <c>connected</c> | <c>missed</c> | <c>voicemail</c>; may be null.</summary>
    public string? Outcome { get; init; }
}
