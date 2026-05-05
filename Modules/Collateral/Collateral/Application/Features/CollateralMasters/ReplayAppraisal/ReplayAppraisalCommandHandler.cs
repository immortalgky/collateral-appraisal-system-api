using Collateral.CollateralMasters.Exceptions;
using Shared.Identity;

namespace Collateral.Application.Features.CollateralMasters.ReplayAppraisal;

/// <summary>
/// Synchronously replays the upsert for a single appraisal and writes a BackfillReport row.
/// Admin-only. Used after upstream data is corrected to recover from SkippedMissingKey or Error.
/// </summary>
public class ReplayAppraisalCommandHandler(
    ICollateralMasterUpsertService upsertService,
    CollateralDbContext db,
    ICurrentUserService currentUser,
    ILogger<ReplayAppraisalCommandHandler> logger
) : ICommandHandler<ReplayAppraisalCommand, ReplayAppraisalResult>
{
    public async Task<ReplayAppraisalResult> Handle(
        ReplayAppraisalCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("IntAdmin"))
            throw new UnauthorizedAccessException("Only Admin users can replay appraisals.");

        var appraisalId = command.AppraisalId;
        string status;
        string? message = null;

        try
        {
            await upsertService.ProcessAppraisalAsync(appraisalId, cancellationToken);
            status = "Processed";
            logger.LogInformation("ReplayAppraisal: Processed AppraisalId={AppraisalId}", appraisalId);
        }
        catch (MissingIdentityKeyException ex)
        {
            status = "SkippedMissingKey";
            message = ex.Message;
            logger.LogWarning(
                "ReplayAppraisal: SkippedMissingKey AppraisalId={AppraisalId} Reason={Reason}",
                appraisalId, ex.Message);
        }
        catch (Exception ex)
        {
            status = "Error";
            var full = ex.ToString();
            message = full.Length > 1000 ? full[..1000] : full;
            logger.LogError(ex, "ReplayAppraisal: Error AppraisalId={AppraisalId}", appraisalId);
        }

        // Write the report row in the same handler's DbContext (same scope)
        var report = new CollateralBackfillReport(appraisalId, status, message);
        db.CollateralBackfillReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);

        return new ReplayAppraisalResult(appraisalId, status, message);
    }
}
