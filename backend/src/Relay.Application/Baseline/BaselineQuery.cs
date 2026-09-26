namespace Relay.Application.Baseline;

/// <param name="Locations">
/// The optional location filter (PRD FR20). Empty means every location the account has. Names are
/// matched exactly against what the account actually has; an unrecognised one is rejected rather
/// than silently dropped, so a stale URL cannot quietly change what the numbers mean.
/// </param>
public sealed record BaselineQuery(int AccountId, int WindowDays, IReadOnlyList<string> Locations)
{
    public static BaselineQuery ForAllLocations(int accountId, int windowDays) =>
        new(accountId, windowDays, []);
}
