using Microsoft.AspNetCore.Mvc;

namespace Relay.Api.Contracts;

/// <summary>
/// The query string, bound as written by the client and not yet trusted:
/// <c>?windowDays=30&amp;locations=Site%20A,Site%20B</c>. Both values are nullable so that "absent"
/// is distinguishable from "present and wrong" — the first takes the documented default, the second
/// is rejected (<see cref="BaselineRequestValidation"/>).
/// </summary>
public sealed record ActivityBaselineRequest
{
    /// <summary>One of 30, 60, 90 (PRD FR19). Absent means 30; any other value is rejected, not clamped.</summary>
    [FromQuery(Name = "windowDays")]
    public int? WindowDays { get; init; }

    /// <summary>
    /// Comma-separated location labels, or absent for every location the account has (PRD FR20).
    /// Names must be ones the account actually has; an unknown one is rejected.
    /// </summary>
    [FromQuery(Name = "locations")]
    public string? Locations { get; init; }
}
