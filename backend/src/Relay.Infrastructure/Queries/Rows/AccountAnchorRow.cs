namespace Relay.Infrastructure.Queries.Rows;

/// <summary>
/// One row of the account-anchor query. A keyless query result, not an entity: it is the shape the
/// raw SQL returns, and it is mapped to no table (see <c>QueryRowConfiguration</c>).
/// </summary>
/// <param name="LatestLocalDate">
/// Null for an account that has no matching events at all — the LEFT JOIN keeps the account row.
/// </param>
internal sealed record AccountAnchorRow(int Id, string Name, string Timezone, DateOnly? LatestLocalDate);
