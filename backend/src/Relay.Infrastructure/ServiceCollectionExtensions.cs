using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Relay.Infrastructure.Options;

namespace Relay.Infrastructure;

/// <summary>
/// The composition seam for persistence. Keeps EF Core types out of <c>Program.cs</c>, so the
/// API project references Infrastructure for wiring only.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRelayPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            // Fail at startup with a clear message rather than on the first request.
            .ValidateOnStart();

        services.AddDbContext<RelayDbContext>((provider, builder) =>
        {
            var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            builder.UseNpgsql(options.ConnectionString);
        });

        return services;
    }
}
