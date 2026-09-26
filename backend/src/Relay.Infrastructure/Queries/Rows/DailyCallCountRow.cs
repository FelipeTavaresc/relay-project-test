namespace Relay.Infrastructure.Queries.Rows;

/// <summary>One row of the daily-count aggregation. Keyless query result; mapped to no table.</summary>
internal sealed record DailyCallCountRow(string Location, DateOnly LocalDate, int Calls);
