using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relay.Infrastructure.Queries.Rows;

namespace Relay.Infrastructure.Configurations;

/// <summary>
/// Registers a raw-query result shape as a <em>keyless</em> entity type mapped to nothing.
/// </summary>
/// <remarks>
/// <para>
/// <c>FromSql</c> can only start from a <c>DbSet</c>, so a result shape has to be in the model even
/// when it corresponds to no table. <c>ToView(null)</c> is what says "this maps to nothing": the type
/// is queryable, never tracked, never written, and contributes no migration operation — the schema
/// stays exactly what the Task 2.0 migration created.
/// </para>
/// <para>
/// The SQL aliases every column to the property name (<c>AS "LocalDate"</c> and so on) because
/// <c>FromSql</c> matches result-set columns to mapped column names, not to positions.
/// </para>
/// </remarks>
internal sealed class AccountAnchorRowConfiguration : IEntityTypeConfiguration<AccountAnchorRow>
{
    public void Configure(EntityTypeBuilder<AccountAnchorRow> builder)
    {
        builder.HasNoKey();
        builder.ToView(null);
    }
}
