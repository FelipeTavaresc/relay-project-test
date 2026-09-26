namespace Relay.Application.Baseline;

public sealed record DailyLocationCount(string Location, DateOnly LocalDate, int Calls);
