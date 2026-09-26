using Microsoft.EntityFrameworkCore;
using Relay.Infrastructure.Entities;

namespace Relay.Infrastructure;

/// <summary>
/// The only EF Core context in the solution. Owns the schema for <c>accounts</c> and
/// <c>activity_events</c>; <c>Relay.Application</c> never sees this type.
/// </summary>
public sealed class RelayDbContext(DbContextOptions<RelayDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuration lives in IEntityTypeConfiguration<T> classes, not inline here and not in
        // attributes on the entities (csharp-dotnet.md § EF Core and data access).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RelayDbContext).Assembly);
    }
}
