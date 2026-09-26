namespace Relay.Application.Baseline;

/// <param name="LatestLocalDate">
/// The window anchor: the account's latest local activity date, not wall-clock time — the seed ends
/// 2026-07-27, so a <c>DateTime.UtcNow</c> anchor returns zero everywhere. Null for an account with
/// no activity.
/// </param>
public sealed record AccountContext(int Id, string Name, string Timezone, DateOnly? LatestLocalDate);
