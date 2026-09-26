namespace Relay.Application.Baseline;

public enum Verdict
{
    Above,
    Typical,
    Below,

    /// <summary>Baseline below the low-volume floor: no verdict, no percentage (PRD FR6).</summary>
    LowVolume,

    /// <summary>
    /// A prior same-weekday occurrence predates the location, so the baseline would be a partial
    /// average (PRD FR7).
    /// </summary>
    InsufficientHistory,
}
