using Carter;
using Collateral.CollateralMasters.Models;
using Collateral.Data;
using Dapper;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Request.Application.Features.Reappraisal.CreateBlockReappraisal;
using Request.Contracts.Requests.Dtos;
using Shared.Data;
using Shared.Identity;

namespace Api.Endpoints.BlockReappraisal;

/// <summary>
/// Adds the "Create New Appraisal Request" action to the block-reappraisal screen (Phase D).
///
/// Route: POST /block-reappraisal/{collateralMasterId}/create
///
/// Lives in the Bootstrapper so it can orchestrate across the Collateral module
/// (ProjectDetails / BlockReappraisalDue) and the Request module (CreateBlockReappraisalCommand)
/// without introducing a module-to-module project reference.
///
/// Two-save pattern (by design — acceptable):
///   1. Request module: CreateBlockReappraisalCommandHandler saves its own DbContext.
///   2. Collateral module: this endpoint marks BlockReappraisalDue as Consumed after
///      the command succeeds. Not atomic — the daily scan reconciles any partial failures.
/// </summary>
public class BlockReappraisalCreateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/block-reappraisal/{collateralMasterId:guid}/create",
                async (
                    Guid collateralMasterId,
                    ISender sender,
                    ISqlConnectionFactory connectionFactory,
                    CollateralDbContext collateralDbContext,
                    ICurrentUserService currentUser,
                    CancellationToken cancellationToken) =>
                {
                    // ── Resolve PrevAppraisalId from ProjectDetail.AppraisalSummary ──────
                    // AppraisalSummary is an owned value object on ProjectDetail; the column
                    // is ProjectDetails.LastAppraisalId (mapped by EF's owned-entity convention).
                    const string sql = """
                        SELECT pd.LastAppraisalId
                        FROM collateral.ProjectDetails pd
                        WHERE pd.CollateralMasterId = @CollateralMasterId
                          AND pd.IsDeleted = 0
                        """;

                    var prevAppraisalId = await connectionFactory.GetOpenConnection()
                        .QueryFirstOrDefaultAsync<Guid?>(sql, new { CollateralMasterId = collateralMasterId });

                    if (prevAppraisalId is null)
                        return Results.NotFound(new
                        {
                            Detail = "No ProjectDetail or LastAppraisalId found for the given CollateralMasterId. " +
                                     "The block project must have been appraised at least once before creating a reappraisal."
                        });

                    // ── Synchronous double-submit guard ───────────────────────────────────
                    // The due row must still be Pending. A second click after a successful
                    // create finds it Consumed and is rejected here, before another Request is
                    // spawned (the async appraisal-creation window leaves the command-level
                    // in-flight dedupe blind to the very first create).
                    var dueRow = await collateralDbContext.BlockReappraisalDue
                        .FirstOrDefaultAsync(r => r.CollateralMasterId == collateralMasterId, cancellationToken);

                    if (dueRow is null || dueRow.Status != "Pending")
                        return Results.Ok(new CreateBlockReappraisalResult(
                            CreatedRequestId: null,
                            RequestNumber: null,
                            GroupNumber: string.Empty,
                            Skipped: true,
                            SkipReason: "AlreadyInProgress"));

                    // ── Resolve user from authenticated principal ─────────────────────────
                    // userId  = bank-code login (from "name" / preferred_username claim).
                    // username = display name — same source here; no separate display-name claim
                    //            is exposed by ICurrentUserService. Downstream Request module
                    //            stores both fields for audit; the bank-code value is load-bearing.
                    var bankCode = currentUser.Username ?? "unknown";
                    var userInfo = new UserInfoDto(UserId: bankCode, Username: bankCode);

                    // ── Send command (Request module handles create + dedupe + snapshot copy) ─
                    var command = new CreateBlockReappraisalCommand(
                        PrevAppraisalId: prevAppraisalId.Value,
                        Requestor: userInfo,
                        Creator: userInfo);

                    var result = await sender.Send(command, cancellationToken);

                    // ── On success, consume the due row in Collateral DbContext ───────────
                    // Two separate module saves; not atomic — acceptable: the daily scan
                    // reconciles any Pending rows that already have an in-flight request.
                    if (!result.Skipped)
                    {
                        dueRow.MarkConsumed();
                        await collateralDbContext.SaveChangesAsync(cancellationToken);
                    }

                    return Results.Ok(result);
                })
            .WithName("CreateBlockReappraisal")
            .Produces<CreateBlockReappraisalResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Create block reappraisal request")
            .WithDescription(
                "Creates a new reappraisal Request for a due block-project collateral master. " +
                "Copies loan / address / contact / titles / documents from the prior Request. " +
                "Returns Skipped=true if a non-terminal reappraisal is already in-flight.")
            .WithTags("BlockReappraisal")
            .RequireAuthorization();
    }
}
