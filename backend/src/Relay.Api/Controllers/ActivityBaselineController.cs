using System.Net.Mime;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Relay.Api.Contracts;
using Relay.Application.Baseline;

namespace Relay.Api.Controllers;

/// <summary>
/// The feature's only endpoint. Binds, validates, delegates, shapes the response — no LINQ over
/// entities, no aggregation, no business rule (PRD FR12, <c>csharp-dotnet.md</c> § API surface).
/// The account id in the route is an addressing mechanism, not a tenant picker: authentication and
/// multi-account switching are both out of scope, and the client holds the account it looks at.
/// </summary>
[ApiController]
[Route("api/v1/accounts/{accountId:int}/activity-baseline")]
public sealed class ActivityBaselineController(
    IActivityBaselineService baselineService,
    ILogger<ActivityBaselineController> logger) : ControllerBase
{
    private const string NotAllowedTitle = "Invalid window";
    private const string UnknownLocationTitle = "Unknown location";

    /// <summary>
    /// Recent activity per location against its own same-weekday baseline, ranked by how far from
    /// normal it is.
    /// </summary>
    /// <response code="200">The report. Empty locations and zero totals for an account with no activity.</response>
    /// <response code="400">windowDays outside {30, 60, 90}, or a location name the account does not have.</response>
    /// <response code="404">No such account.</response>
    [HttpGet]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType<ActivityBaselineResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Results<Ok<ActivityBaselineResponse>, BadRequest<ProblemDetails>, NotFound>> GetAsync(
        int accountId,
        [FromQuery] ActivityBaselineRequest request,
        CancellationToken cancellationToken)
        => BaselineRequestValidation.Validate(request) switch
        {
            BaselineRequestValidation.Valid valid =>
                await ServeAsync(accountId, valid, cancellationToken).ConfigureAwait(false),
            BaselineRequestValidation.Invalid invalid =>
                RejectWindow(accountId, request.WindowDays, invalid),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request, "Unmapped validation result."),
        };

    private async Task<Results<Ok<ActivityBaselineResponse>, BadRequest<ProblemDetails>, NotFound>> ServeAsync(
        int accountId,
        BaselineRequestValidation.Valid request,
        CancellationToken cancellationToken)
    {
        var query = new BaselineQuery(accountId, request.WindowDays, request.Locations);
        var outcome = await baselineService.GetBaselineAsync(query, cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            BaselineOutcome.Ready ready => Serve(query, ready),
            BaselineOutcome.UnknownLocations unknown => RejectLocations(query, unknown),
            BaselineOutcome.AccountNotFound => RejectAccount(query),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unmapped baseline outcome."),
        };
    }

    private Ok<ActivityBaselineResponse> Serve(BaselineQuery query, BaselineOutcome.Ready ready)
    {
        logger.LogInformation(
            "Baseline served for account {AccountId}: {WindowDays} days, {LocationCount} locations",
            query.AccountId,
            query.WindowDays,
            ready.Report.Locations.Count);

        return TypedResults.Ok(BaselineResponseMapper.ToResponse(ready));
    }

    /// <summary>
    /// Logs what the caller actually sent, including a null <c>windowDays</c>, so a rejected request
    /// can be reconstructed from the log line alone.
    /// </summary>
    private BadRequest<ProblemDetails> RejectWindow(
        int accountId,
        int? windowDays,
        BaselineRequestValidation.Invalid invalid)
    {
        logger.LogWarning(
            "Baseline window rejected for account {AccountId}: {WindowDays} days",
            accountId,
            windowDays);

        return Reject(NotAllowedTitle, invalid.Detail);
    }

    private BadRequest<ProblemDetails> RejectLocations(
        BaselineQuery query,
        BaselineOutcome.UnknownLocations unknown)
    {
        var names = string.Join(", ", unknown.Locations);

        logger.LogWarning(
            "Baseline locations rejected for account {AccountId}: {WindowDays} days, unknown {Locations}",
            query.AccountId,
            query.WindowDays,
            names);

        return Reject(
            UnknownLocationTitle,
            $"Account {query.AccountId} has no location named {names}.");
    }

    private NotFound RejectAccount(BaselineQuery query)
    {
        logger.LogWarning(
            "Baseline requested for unknown account {AccountId}: {WindowDays} days",
            query.AccountId,
            query.WindowDays);

        return TypedResults.NotFound();
    }

    /// <summary>
    /// A rejection names the bound it broke, so the caller can fix the request without guessing —
    /// which is the point of rejecting rather than silently clamping.
    /// </summary>
    private BadRequest<ProblemDetails> Reject(string title, string detail) =>
        TypedResults.BadRequest(new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = StatusCodes.Status400BadRequest,
            Instance = HttpContext.Request.Path,
        });
}
