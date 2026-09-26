using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Relay.Infrastructure;

/// <summary>
/// Used by <c>dotnet ef</c> only — never by the running application, which builds its context from
/// <see cref="Relay.Infrastructure.Options.DatabaseOptions"/>. Having it here means
/// <c>dotnet ef database update</c> needs no environment setup beyond the compose <c>db</c> service.
/// </summary>
internal sealed class DesignTimeRelayDbContextFactory : IDesignTimeDbContextFactory<RelayDbContext>
{
    /// <summary>Matches the <c>db</c> service in the repository-root <c>docker-compose.yml</c>.</summary>
    private const string LocalDevelopmentConnectionString =
        "Host=localhost;Port=5432;Database=relay;Username=relay;Password=relay";

    private const string ConnectionStringVariable = "RELAY_DB_CONNECTION";

    public RelayDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringVariable) is { Length: > 0 } fromEnvironment
                ? fromEnvironment
                : LocalDevelopmentConnectionString;

        var options = new DbContextOptionsBuilder<RelayDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RelayDbContext(options);
    }
}
