using System.Globalization;

namespace Relay.Api.Contracts;

/// <summary>
/// The result of validating a query string: a closed set, so the controller answers both cases
/// exhaustively instead of throwing. Out-of-range input is an expected outcome, not an exception
/// (<c>csharp-dotnet.md</c> § Error handling).
/// </summary>
public abstract record BaselineRequestValidation
{
    /// <summary>The window lengths the feature offers (PRD FR19). Anything else is rejected, not clamped.</summary>
    public static readonly IReadOnlyList<int> AllowedWindowDays = [30, 60, 90];

    /// <summary>Applied when <c>windowDays</c> is absent altogether (PRD FR22's documented default).</summary>
    public const int DefaultWindowDays = 30;

    private const char LocationSeparator = ',';

    private BaselineRequestValidation()
    {
    }

    public static BaselineRequestValidation Validate(ActivityBaselineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var windowDays = request.WindowDays ?? DefaultWindowDays;
        if (!AllowedWindowDays.Contains(windowDays))
        {
            return new Invalid(string.Create(
                CultureInfo.InvariantCulture,
                $"windowDays must be one of {string.Join(", ", AllowedWindowDays)}; {windowDays} is not allowed."));
        }

        return new Valid(windowDays, ParseLocations(request.Locations));
    }

    /// <summary>
    /// An absent, empty, or comma-only filter means "every location", which is the same thing the
    /// application layer takes an empty list to mean. Names are kept in the order given, trimmed, and
    /// de-duplicated; whether each one exists is the application layer's call, since only it knows
    /// what the account has.
    /// </summary>
    private static IReadOnlyList<string> ParseLocations(string? locations)
    {
        if (string.IsNullOrWhiteSpace(locations))
        {
            return [];
        }

        return locations
            .Split(LocationSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public sealed record Valid(int WindowDays, IReadOnlyList<string> Locations) : BaselineRequestValidation;

    /// <param name="Detail">Names the bound that was broken — it becomes the <c>ProblemDetails.Detail</c>.</param>
    public sealed record Invalid(string Detail) : BaselineRequestValidation;
}
