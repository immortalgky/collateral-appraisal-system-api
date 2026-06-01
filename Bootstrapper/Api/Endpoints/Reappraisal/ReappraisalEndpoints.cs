using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.Reappraisal.DeleteCandidate;
using Request.Application.Features.Reappraisal.GetCandidateById;
using Request.Application.Features.Reappraisal.GetCandidates;
using Request.Application.Features.Reappraisal.InitiateReappraisal;
using Request.Contracts.Requests.Dtos;
using Shared.Pagination;

namespace Api.Endpoints.Reappraisal;

/// <summary>
/// Reappraisal (AS400 Periodical) endpoints.
///
/// GET  /reappraisal/candidates        — paginated list with filters
/// GET  /reappraisal/candidates/{id}   — detail + nearby group candidates
/// POST /reappraisal/initiate          — initiate batch of reappraisal requests
/// DELETE /reappraisal/candidates/{id} — soft-delete candidate
/// </summary>
public class ReappraisalEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // ── GET /reappraisal/candidates ──────────────────────────────────
        app.MapGet("/reappraisal/candidates",
                async (
                    ISender sender,
                    CancellationToken cancellationToken,
                    int pageNumber = 0,
                    int pageSize = 20,
                    string? customerName = null,
                    string? oldAppraisalReportNumber = null,
                    string? cifNumber = null,
                    string? collateralId = null,
                    string? reviewType = null,
                    DateOnly? reviewDateFrom = null,
                    DateOnly? reviewDateTo = null,
                    int? remainingDayFrom = null,
                    int? remainingDayTo = null) =>
                {
                    var query = new GetReappraisalCandidatesQuery(
                        Pagination: new PaginationRequest(pageNumber, pageSize),
                        CustomerName: customerName,
                        OldAppraisalReportNumber: oldAppraisalReportNumber,
                        CifNumber: cifNumber,
                        CollateralId: collateralId,
                        ReviewType: reviewType,
                        ReviewDateFrom: reviewDateFrom,
                        ReviewDateTo: reviewDateTo,
                        RemainingDayFrom: remainingDayFrom,
                        RemainingDayTo: remainingDayTo);

                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result.Items);
                })
            .WithName("GetReappraisalCandidates")
            .Produces<PaginatedResult<ReappraisalCandidateListItem>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Reappraisal")
            .WithSummary("List reappraisal candidates")
            .WithDescription("Returns a paginated list of AS400 COLLATREV reappraisal candidates with optional filters.")
            .AllowAnonymous();

        // ── GET /reappraisal/candidates/{id} ─────────────────────────────
        app.MapGet("/reappraisal/candidates/{id:guid}",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken,
                    decimal radiusKm = 1m) =>
                {
                    var result = await sender.Send(
                        new GetReappraisalCandidateByIdQuery(id, radiusKm),
                        cancellationToken);

                    return result is null
                        ? Results.NotFound()
                        : Results.Ok(result.Candidate);
                })
            .WithName("GetReappraisalCandidateById")
            .Produces<ReappraisalCandidateDetail>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Reappraisal")
            .WithSummary("Get reappraisal candidate detail")
            .WithDescription("Returns full detail for one candidate plus nearby candidates for group selection.")
            .AllowAnonymous();

        // ── POST /reappraisal/initiate ────────────────────────────────────
        app.MapPost("/reappraisal/initiate",
                async (
                    InitiateReappraisalRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new InitiateReappraisalCommand(
                        CandidateIds: request.CandidateIds,
                        NearbyAppraisalIds: request.NearbyAppraisalIds,
                        Requestor: request.Requestor,
                        Creator: request.Creator);

                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("InitiateReappraisal")
            .Produces<InitiateReappraisalResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Reappraisal")
            .WithSummary("Initiate reappraisal requests")
            .WithDescription("Creates one reappraisal request per selected candidate, grouped under a shared group number.")
            .AllowAnonymous();

        // ── DELETE /reappraisal/candidates/{id} ──────────────────────────
        app.MapDelete("/reappraisal/candidates/{id:guid}",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new DeleteReappraisalCandidateCommand(id),
                        cancellationToken);

                    return result.Success
                        ? Results.NoContent()
                        : Results.NotFound();
                })
            .WithName("DeleteReappraisalCandidate")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Reappraisal")
            .WithSummary("Delete reappraisal candidate")
            .WithDescription("Soft-deletes a candidate from the list (Status = Deleted). Does not affect any created requests.")
            .AllowAnonymous();
    }
}

/// <summary>Request body for POST /reappraisal/initiate.</summary>
public record InitiateReappraisalRequest(
    List<Guid> CandidateIds,
    List<Guid> NearbyAppraisalIds,
    UserInfoDto Requestor,
    UserInfoDto Creator
);
