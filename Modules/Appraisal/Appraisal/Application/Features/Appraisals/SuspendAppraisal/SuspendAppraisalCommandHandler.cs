using Shared.Time;

namespace Appraisal.Application.Features.Appraisals.SuspendAppraisal;

public class SuspendAppraisalCommandHandler(
    IAppraisalRepository appraisalRepository,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<SuspendAppraisalCommand, SuspendAppraisalResult>
{
    public async Task<SuspendAppraisalResult> Handle(SuspendAppraisalCommand command, CancellationToken cancellationToken)
    {
        var actionBy = currentUser.UserCode ?? currentUser.Username ?? "unknown";
        var actionAt = dateTimeProvider.ApplicationNow;

        // Row-locked until commit, so a concurrent request (e.g. a double click) sees this one's result and
        // is rejected by the domain guard instead of writing a second log row.
        var appraisal = await appraisalRepository.GetByIdForUpdateAsync(command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // Suspend/Reactivate enforce "Completed only" and "not already in that state" (400 when violated).
        if (command.IsSuspended)
        {
            appraisal.Suspend();
            appraisalRepository.AddSuspensionLog(
                AppraisalSuspensionLog.ForSuspend(appraisal.Id, command.Reason, actionAt, actionBy));
        }
        else
        {
            appraisal.Reactivate();
            appraisalRepository.AddSuspensionLog(
                AppraisalSuspensionLog.ForReactivate(appraisal.Id, command.Reason, actionAt, actionBy));
        }

        await appraisalRepository.SaveChangesAsync(cancellationToken);

        return new SuspendAppraisalResult(appraisal.Id, command.IsSuspended, actionAt, actionBy);
    }
}
