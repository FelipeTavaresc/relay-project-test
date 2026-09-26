namespace Relay.Api.Contracts;

/// <summary>
/// The "one day is doing most of the work" note. Reported, never smoothed away — it is what makes
/// account 6's "every location is below typical" reading explainable (techspec § Key Decisions #1).
/// </summary>
public sealed record BaselineOutlierResponse(DateOnly Date, decimal SharePercent);
