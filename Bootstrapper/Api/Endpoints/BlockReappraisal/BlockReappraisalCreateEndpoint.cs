using Carter;
using Dapper;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.Reappraisal.CreateBlockReappraisal;
using Request.Contracts.Requests.Dtos;
using Shared.Data;
using Shared.Identity;

namespace Api.Endpoints.BlockReappraisal;

/// <summary>
/// Adds the "Create New Appraisal Request" action to the block-reappraisal screen.
///
/// Route: POST /block-reappraisal/{collateralMasterId}/create
///
/// Lives in the Bootstrapper so it can orchestrate across the Collateral module
/// (ProjectDetails / BlockReappraisalDue) and the Request module (CreateBlockReappraisalCommand)
/// without introducing a module-to-module project reference.
///
/// Double-submit safety: the BlockReappraisalDue row is claimed with a single atomic
/// UPDATE (Pending → Consumed). Only the caller that flips it proceeds to create the Request;
/// concurrent callers see 0 rows affected and are rejected. Not transactional with the Request
/// create — if the create fails, the daily scan re-adds the row while the project is still due.
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
                    ICurrentUserService currentUser,
                    CancellationToken cancellationToken) =>
                {
                    // ── Resolve PrevAppraisalId from the project's latest engagement ──────
                    // ProjectDetails.LastAppraisalId (the old AppraisalSummary owned VO) has been
                    // removed: it was a latest-WRITE-wins cache that an out-of-order replay could
                    // leave pointing at the wrong appraisal. Engagements are immutable, one per
                    // appraisal, so ordering by AppraisalDate gives the genuinely latest round.
                    const string sql = """
                        SELECT TOP 1 e.AppraisalId
                        FROM collateral.CollateralEngagements e
                        JOIN collateral.ProjectDetails pd
                          ON pd.CollateralMasterId = e.CollateralMasterId
                         AND pd.IsDeleted = 0
                        WHERE e.CollateralMasterId = @CollateralMasterId
                        ORDER BY e.AppraisalDate DESC, e.CreatedAt DESC, e.Id DESC
                        """;

                    var connection = connectionFactory.GetOpenConnection();

                    var prevAppraisalId = await connection
                        .QueryFirstOrDefaultAsync<Guid?>(sql, new { CollateralMasterId = collateralMasterId });

                    if (prevAppraisalId is null)
                        return Results.NotFound(new
                        {
                            Detail = "No ProjectDetail or completed appraisal found for the given CollateralMasterId. " +
                                     "The block project must have been appraised at least once before creating a reappraisal."
                        });

                    // ── Atomic double-submit claim ─────────────────────────────────────────
                    // Only the caller that flips Pending → Consumed proceeds; concurrent callers
                    // see 0 rows affected and are rejected. This eliminates the duplicate-Request
                    // race (the command-level in-flight dedupe is blind until the new appraisal
                    // materializes asynchronously). If the create later fails, the daily scan
                    // re-adds the row while the project is still due.
                    const string claimSql = """
                        UPDATE collateral.BlockReappraisalDue
                           SET Status = 'Consumed', UpdatedAt = GETUTCDATE()
                         WHERE CollateralMasterId = @CollateralMasterId AND Status = 'Pending'
                        """;

                    var claimed = await connection.ExecuteAsync(
                        claimSql, new { CollateralMasterId = collateralMasterId });

                    if (claimed == 0)
                        return Results.Ok(new CreateBlockReappraisalResult(
                            CreatedRequestId: null,
                            RequestNumber: null,
                            GroupNumber: string.Empty,
                            Skipped: true,
                            SkipReason: "AlreadyInProgress"));

                    // ── Resolve user from authenticated principal ─────────────────────────
                    // userId/username both resolve to the bank-code login; ICurrentUserService
                    // exposes no separate display-name claim. The bank-code value is load-bearing.
                    var bankCode = currentUser.Username ?? "unknown";
                    var userInfo = new UserInfoDto(UserId: bankCode, Username: bankCode);

                    // ── Send command (Request module handles create + dedupe + snapshot copy) ─
                    var command = new CreateBlockReappraisalCommand(
                        PrevAppraisalId: prevAppraisalId.Value,
                        Requestor: userInfo,
                        Creator: userInfo);

                    var result = await sender.Send(command, cancellationToken);

                    // The due row was already claimed (Consumed) above; a Skipped result (a real
                    // in-flight reappraisal already existed) is still correct — the project should
                    // not remain in the due list either way.
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
                "Returns Skipped=true if the due row is already claimed or a non-terminal reappraisal is in-flight.")
            .WithTags("BlockReappraisal")
            .RequireAuthorization();
    }
}
