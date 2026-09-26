namespace Relay.Application.Baseline;

/// <summary>
/// A source day supplying a disproportionate share of a baseline. Reported, never smoothed away:
/// both robust estimators prototyped against the seed performed worse (techspec § Key Decisions #1).
/// </summary>
public sealed record OutlierDay(DateOnly Date, decimal SharePercent);
