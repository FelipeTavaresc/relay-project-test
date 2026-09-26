using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relay.Infrastructure.Entities;

namespace Relay.Infrastructure.Configurations;

/// <summary>
/// Mirrors the <c>activity_events</c> table in <c>tasks/prd-activity-baseline/schema.sql</c>,
/// plus the one index the schema does not carry: the composite the baseline aggregation filters on.
/// </summary>
internal sealed class ActivityEventConfiguration : IEntityTypeConfiguration<ActivityEvent>
{
    /// <summary>
    /// Covers the aggregation's WHERE clause: <c>account_id = @p AND event_type = 'call_received'</c>,
    /// with <c>occurred_at</c> trailing so the scan is range-ordered. The query's date predicate is on
    /// the computed local date and therefore not sargable (techspec § The aggregation query); this
    /// index carries the selective part of it.
    /// </summary>
    private const string AggregationIndexName = "ix_activity_events_account_id_event_type_occurred_at";

    public void Configure(EntityTypeBuilder<ActivityEvent> builder)
    {
        builder.ToTable("activity_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            // The seed supplies explicit ids; an identity column would fight it.
            .ValueGeneratedNever();

        builder.Property(e => e.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(e => e.Location)
            .HasColumnName("location")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(e => e.OccurredAt)
            .HasColumnName("occurred_at")
            // Load-bearing. `timestamp without time zone` holding UTC is what makes
            // `occurred_at AT TIME ZONE 'UTC' AT TIME ZONE accounts.timezone` mean what the
            // aggregation needs it to mean. With timestamptz the first conversion is a no-op and
            // the day bucketing silently changes (techspec § Known Risks).
            .HasColumnType("timestamp without time zone")
            .IsRequired();

        builder.Property(e => e.DurationSeconds)
            .HasColumnName("duration_seconds");

        builder.Property(e => e.Outcome)
            .HasColumnName("outcome")
            .HasMaxLength(40);

        // No navigation property: nothing in this feature traverses the relationship, and the
        // aggregation is raw SQL. The constraint still belongs in the schema.
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.AccountId, e.EventType, e.OccurredAt })
            .HasDatabaseName(AggregationIndexName);
    }
}
