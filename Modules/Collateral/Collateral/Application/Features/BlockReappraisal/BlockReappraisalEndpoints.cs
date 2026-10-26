using Collateral.Application.Features.BlockReappraisal.GetBlockReappraisalDetail;
using Collateral.Application.Features.BlockReappraisal.GetBlockReappraisalDueList;
using Collateral.Application.Features.BlockReappraisal.MarkBlockReappraisalNotRequired;
using Collateral.Application.Features.BlockReappraisal.RestoreBlockReappraisal;

namespace Collateral.Application.Features.BlockReappraisal;

/// <summary>
/// Endpoints for the §3.7 block-reappraisal screen.
/// </summary>
public class BlockReappraisalEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // GET /block-reappraisal — paginated due list
        app.MapGet(
                "/block-reappraisal",
                async (
                    string? status,
                    string? search,
                    DateTime? lastAppraisedDateFrom,
                    DateTime? lastAppraisedDateTo,
                    int? remainingDayMin,
                    int? remainingDayMax,
                    string? sortBy,
                    string? sortDir,
                    int? pageNumber,
                    int? pageSize,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    // PaginationRequest.PageNumber is 0-based (offset = PageNumber * PageSize);
                    // the FE sends a 0-based `pageNumber`, matching the AS400 reappraisal endpoint.
                    var query = new GetBlockReappraisalDueListQuery(
                        Status: status,
                        Search: search,
                        LastAppraisedDateFrom: lastAppraisedDateFrom,
                        LastAppraisedDateTo: lastAppraisedDateTo,
                        RemainingDayMin: remainingDayMin,
                        RemainingDayMax: remainingDayMax,
                        SortBy: sortBy,
                        SortDir: sortDir,
                        PaginationRequest: new PaginationRequest(pageNumber ?? 0, pageSize ?? 20));

                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result.Items);
                })
            .WithName("GetBlockReappraisalDueList")
            .Produces<PaginatedResult<BlockReappraisalDueListItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get block reappraisal due list")
            .WithDescription("Paginated list of block-project collateral masters on the reappraisal due list. status = Pending (default) | Deleted (marked not required).")
            .WithTags("BlockReappraisal")
            .RequireAuthorization();

        // GET /block-reappraisal/{collateralMasterId} — project structure detail
        app.MapGet(
                "/block-reappraisal/{collateralMasterId:guid}",
                async (
                    Guid collateralMasterId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetBlockReappraisalDetailQuery(collateralMasterId);
                    var result = await sender.Send(query, cancellationToken);

                    return result is null
                        ? Results.NotFound()
                        : Results.Ok(result);
                })
            .WithName("GetBlockReappraisalDetail")
            .Produces<BlockReappraisalDetailResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get block reappraisal project detail")
            .WithDescription("Returns the project structure (units/models/towers) for a due-list entry, for the overview chart and units table.")
            .WithTags("BlockReappraisal")
            .RequireAuthorization();

        // POST /block-reappraisal/{collateralMasterId}/opt-out — mark not required
        app.MapPost(
                "/block-reappraisal/{collateralMasterId:guid}/opt-out",
                async (
                    Guid collateralMasterId,
                    MarkBlockReappraisalNotRequiredRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new MarkBlockReappraisalNotRequiredCommand(collateralMasterId, request.Remark);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MarkBlockReappraisalNotRequired")
            .Produces<MarkBlockReappraisalNotRequiredResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Mark block reappraisal not required")
            .WithDescription("Excludes this project from the next reappraisal cycle and removes it from the pending due list.")
            .WithTags("BlockReappraisal")
            .RequireAuthorization();

        // POST /block-reappraisal/{collateralMasterId}/restore — put a Consumed row back to Pending
        app.MapPost(
                "/block-reappraisal/{collateralMasterId:guid}/restore",
                async (
                    Guid collateralMasterId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new RestoreBlockReappraisalCommand(collateralMasterId);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("RestoreBlockReappraisal")
            .Produces<RestoreBlockReappraisalResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Restore block reappraisal to Pending")
            .WithDescription("Puts a Deleted (not required) due-list row back to Pending and clears the project's not-required exclusion.")
            .WithTags("BlockReappraisal")
            .RequireAuthorization();
    }
}
