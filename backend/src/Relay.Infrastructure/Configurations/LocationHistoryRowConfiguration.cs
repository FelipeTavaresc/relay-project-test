using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relay.Infrastructure.Queries.Rows;

namespace Relay.Infrastructure.Configurations;

/// <inheritdoc cref="AccountAnchorRowConfiguration"/>
internal sealed class LocationHistoryRowConfiguration : IEntityTypeConfiguration<LocationHistoryRow>
{
    public void Configure(EntityTypeBuilder<LocationHistoryRow> builder)
    {
        builder.HasNoKey();
        builder.ToView(null);
    }
}
