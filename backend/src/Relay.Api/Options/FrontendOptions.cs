using System.ComponentModel.DataAnnotations;

namespace Relay.Api.Options;

/// <summary>
/// Where the SPA is served from, bound from configuration rather than hardcoded into the CORS policy.
/// The Angular dev server runs on its own origin (<c>http://localhost:4200</c>) while the API runs on
/// another, so the browser needs the header to talk to it at all.
/// </summary>
public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>
    /// Exact origins, never a wildcard: this is a read-only endpoint with no auth, but an allowlist
    /// costs nothing and keeps the policy honest.
    /// </summary>
    [Required]
    [MinLength(1)]
    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];
}
