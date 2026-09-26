namespace Relay.Api.Contracts;

/// <summary>
/// The wire contract of <c>GET /api/v1/accounts/{accountId}/activity-baseline</c>, shaped exactly as
/// techspec § API Endpoints. A type of its own rather than the domain <c>BaselineReport</c> or an EF
/// entity: the boundary is allowed to change independently of either
/// (<c>csharp-dotnet.md</c> § API surface).
/// </summary>
public sealed record ActivityBaselineResponse(
    int AccountId,
    string AccountName,
    string Timezone,
    int WindowDays,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    BaselineSummaryResponse Summary,
    IReadOnlyList<LocationBaselineResponse> Locations);
