namespace Relay.Application.Baseline;

/// <summary>
/// Pure and synchronous by design — no database, no clock, no I/O — so every PRD edge case is a
/// plain unit test (techspec § Development Sequencing, step 3).
/// </summary>
public interface IBaselineCalculator
{
    BaselineReport Evaluate(BaselineRequest request);
}
