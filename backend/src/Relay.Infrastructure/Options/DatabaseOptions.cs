using System.ComponentModel.DataAnnotations;

namespace Relay.Infrastructure.Options;

/// <summary>
/// The database connection, bound from configuration rather than hardcoded. Supplied by
/// <c>appsettings.Development.json</c> locally and by the <c>Database__ConnectionString</c>
/// environment variable in Docker Compose.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; init; } = string.Empty;
}
