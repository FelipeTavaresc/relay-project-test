namespace Relay.Application.Baseline;

/// <summary>
/// The tuning constants of the baseline algorithm. Server-side by decision: PRD § Out of Scope
/// excludes a configuration UI, so each one is documented here with the reasoning that produced it.
/// </summary>
public static class BaselineThresholds
{
    public const int DaysPerWeek = 7;

    /// <summary>
    /// Prior occurrences of the same weekday that make up a day's baseline (PRD FR3). Four gives a
    /// month of like-for-like history; the seed runs 78–97 events/day on weekdays against 23–25 at
    /// weekends, so comparing a Monday to anything but other Mondays is noise.
    /// </summary>
    public const int PriorOccurrences = 4;

    /// <summary>
    /// How far the lookback reaches back from any window day. The caller must widen its fetch span
    /// by this much, or the window's early days silently get a baseline of zero.
    /// </summary>
    public const int LookbackSpanDays = PriorOccurrences * DaysPerWeek;

    /// <summary>
    /// Baselines below this many calls return <see cref="Verdict.LowVolume"/> (PRD FR6). Chosen so
    /// no 30-day row in the seed is suppressed — the smallest baseline there is 11.25 — while still
    /// protecting a tiny site from a confident −40% caused by two missing calls (PRD Open Question #3).
    /// </summary>
    public const decimal LowVolumeBaselineFloor = 10m;

    /// <summary>Floor of the band: below this it would be tighter than ordinary weekly variation.</summary>
    public const decimal MinimumBandPercent = 10m;

    /// <summary>
    /// Numerator of the volume-aware term <c>100/sqrt(baseline)</c>. The band widens where counts
    /// are small because at a baseline of 4 calls a single call is 25%: a flat ±10% band labelled
    /// 67–84% of locations unusual and destroyed the signal the feature exists to provide (PRD FR5).
    /// </summary>
    public const decimal BandNumeratorPercent = 100m;

    /// <summary>
    /// A single source day supplying more than this share of a baseline raises an
    /// <see cref="OutlierDay"/>. Empirical: at 25% the flag fires on exactly account 6's 15
    /// locations at all three windows and on nothing else, where the highest share anywhere else is
    /// 20% (30d), 14% (60d), 9% (90d). An even contribution across a 30-day window is ~3%, so this
    /// is roughly 7× an even share (techspec § Key Decisions #1).
    /// </summary>
    public const decimal OutlierSharePercent = 25m;

    /// <summary>
    /// Decimal places for reported percentages. Applied only when shaping output — the band
    /// comparison runs unrounded so the boundary is exact.
    /// </summary>
    public const int ReportedPercentDecimals = 2;
}
