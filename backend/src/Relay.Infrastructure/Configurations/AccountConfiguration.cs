using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relay.Infrastructure.Entities;

namespace Relay.Infrastructure.Configurations;

/// <summary>
/// Mirrors the <c>accounts</c> table in <c>tasks/prd-activity-baseline/schema.sql</c> exactly:
/// same table name, same snake_case column names, same lengths and nullability, so the provided
/// seed INSERTs load without modification.
/// </summary>
internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            // The seed supplies explicit ids; an identity column would fight it.
            .ValueGeneratedNever();

        builder.Property(a => a.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(a => a.Industry)
            .HasColumnName("industry")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(a => a.Timezone)
            .HasColumnName("timezone")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            // UTC, as the seed states. Pinned rather than left to Npgsql's Kind-based default
            // so the stored type cannot drift to timestamptz.
            .HasColumnType("timestamp without time zone")
            .IsRequired();
    }
}
