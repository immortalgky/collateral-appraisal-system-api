using Appraisal.Application.Features.Shared;
using Appraisal.Domain.Appraisals;
using Shared.Identity;
using Workflow.Contracts.FeeAppointmentApprovals;

namespace Appraisal.Application.Features.Fees.UpdateFeeItem;

public class UpdateFeeItemCommandHandler(AppraisalDbContext dbContext, ISender sender, ICurrentUserService currentUser)
    : ICommandHandler<UpdateFeeItemCommand, UpdateFeeItemResult>
{
    public async Task<UpdateFeeItemResult> Handle(UpdateFeeItemCommand command, CancellationToken cancellationToken)
    {
        var fee = await dbContext.AppraisalFees
            .Include(fi => fi.Items)
            .FirstOrDefaultAsync(f => f.Id == command.FeeId, cancellationToken);

        if (fee is null)
            throw new NotFoundException("AppraisalFee", command.FeeId);

        var existing = fee.Items.FirstOrDefault(i => i.Id == command.FeeItemId)
            ?? throw new NotFoundException($"Fee item {command.FeeItemId} not found");
        var isAdminStage = await FeeAdminStage.IsAdminStageAsync(dbContext, fee.AssignmentId, cancellationToken);
        FeeAdminStage.EnsureNoApprovalAwaitingReview(fee, isAdminStage, "update");

        FeeAdminStage.EnsureCanUpdate(fee, existing, command.FeeCode, isAdminStage);

        var requestedAbsorb = fee.BankAbsorbAmount;
        var item = fee.UpdateItem(command.FeeItemId, command.FeeCode, command.FeeDescription, command.FeeAmount);

        // An admin's edit before handover makes the item the admin's own: billable at once, no approval.
        if (isAdminStage)
        {
            item.MarkAsAdminSet();
            fee.RestoreRequestedBankAbsorb(requestedAbsorb);
            return new UpdateFeeItemResult(item.Id, item.FeeCode, item.FeeDescription, item.FeeAmount);
        }

        // Compute cumulative total of active USER-entered items (draft/pending; excludes
        // finalised Approved/Rejected) — mirrors ReevaluateAddedFees predicate.
        var cumulativeTotal = fee.Items
            .Where(i => i.IsActiveAddedFee)
            .Sum(i => i.FeeAmount);

        var requestSource = currentUser.ToFeeApprovalRequestSource();

        // Evaluate policy at edit time (read-only cross-module query)
        var verdict = await sender.Send(
            new EvaluateFeeAppointmentApprovalQuery(
                command.AppraisalId,
                RequestSource: requestSource,
                ProposedAppointmentDate: null,
                RescheduleCount: null,
                CumulativeAddedFeeTotal: cumulativeTotal),
            cancellationToken);

        // Re-evaluate the whole set of company-added items as a unit
        fee.ReevaluateAddedFees(verdict.FeesRequireApproval);

        return new UpdateFeeItemResult(item.Id, item.FeeCode, item.FeeDescription, item.FeeAmount);
    }
}
