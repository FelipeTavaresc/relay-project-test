using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relay.Infrastructure.Queries.Rows;

namespace Relay.Infrastructure.Configurations;

/// <inheritdoc cref="AccountAnchorRowConfiguration"/>
internal sealed class DailyCallCountRowConfiguration : IEntityTypeConfiguration<DailyCallCountRow>
{
    public void Configure(EntityTypeBuilder<DailyCallCountRow> builder)
    {
        builder.HasNoKey();
        builder.ToView(null);
    }
}
