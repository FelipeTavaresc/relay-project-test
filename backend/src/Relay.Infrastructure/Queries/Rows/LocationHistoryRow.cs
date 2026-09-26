namespace Relay.Infrastructure.Queries.Rows;

/// <summary>One row of the location-history query. Keyless query result; mapped to no table.</summary>
internal sealed record LocationHistoryRow(string Location, DateOnly FirstLocalDate, DateOnly LastLocalDate);
