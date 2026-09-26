namespace Relay.Application.Baseline;

/// <summary>
/// The feature's application service: fetches, widens the fetch span, applies the location filter,
/// and hands the pure calculator its input. Aggregation lives here and in the database, never in a
/// controller and never in the browser (PRD FR12).
/// </summary>
public interface IActivityBaselineService
{
    Task<BaselineOutcome> GetBaselineAsync(BaselineQuery query, CancellationToken cancellationToken);
}
