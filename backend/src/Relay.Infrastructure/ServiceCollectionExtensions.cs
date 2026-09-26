using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Relay.Application.Baseline;
using Relay.Infrastructure.Options;
using Relay.Infrastructure.Queries;

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

        // Scoped, matching the scoped RelayDbContext it depends on. Registered against the interface
        // Relay.Application declares — which is the point of the abstraction: the Api project
        // composes the two without ever naming the implementation or an EF Core type.
        services.AddScoped<IActivityQueries, ActivityQueries>();

        return services;
    }
}
