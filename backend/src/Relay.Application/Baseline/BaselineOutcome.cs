namespace Relay.Application.Baseline;

/// <summary>
/// What a baseline request can result in. A closed set — the private constructor means no case can
/// be added outside this file — so the controller maps outcomes to status codes exhaustively rather
/// than catching exceptions. A missing account and an unknown location name are expected results,
/// not exceptional ones (<c>csharp-dotnet.md</c> § Error handling).
/// </summary>
public abstract record BaselineOutcome
{
    private BaselineOutcome()
    {
    }

    /// <summary>
    /// The report, and the account context the caller needs to describe it. Also the answer for an
    /// account with no activity at all: an empty row list and zero totals is a valid 200 (PRD FR17).
    /// </summary>
    public sealed record Ready(AccountContext Account, int WindowDays, BaselineReport Report)
        : BaselineOutcome;

    /// <summary>No such account — the caller's 404.</summary>
    public sealed record AccountNotFound(int AccountId) : BaselineOutcome;

    /// <summary>Location names the account does not have — the caller's 400, naming them.</summary>
    public sealed record UnknownLocations(IReadOnlyList<string> Locations) : BaselineOutcome;
}
