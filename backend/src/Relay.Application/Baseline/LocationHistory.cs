namespace Relay.Application.Baseline;

/// <param name="FirstLocalDate">
/// What separates "this occurrence never existed" from a genuine zero (PRD FR7 vs FR9).
/// </param>
public sealed record LocationHistory(string Location, DateOnly FirstLocalDate, DateOnly LastLocalDate);
