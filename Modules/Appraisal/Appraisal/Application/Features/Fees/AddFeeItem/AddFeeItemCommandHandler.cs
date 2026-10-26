using Appraisal.Application.Features.Shared;
using Appraisal.Domain.Appraisals;
using Shared.Identity;
using Workflow.Contracts.FeeAppointmentApprovals;

namespace Appraisal.Application.Features.Fees.AddFeeItem;

public class AddFeeItemCommandHandler(AppraisalDbContext dbContext, ISender sender, ICurrentUserService currentUser)
    : ICommandHandler<AddFeeItemCommand, AddFeeItemResult>
{
    public async Task<AddFeeItemResult> Handle(
        AddFeeItemCommand command,
        CancellationToken cancellationToken)
    {
        // No fee id = "the appraisal's awaiting-assignment fee", created on first use (a request submitted
        // without fee data has no fee row to address yet). Only the admin stage can do that. An id that was given
        // is always looked up as such, so an all-zero one is a 404, not that.
        var fee = command.FeeId is not { } feeId
            ? await FeeAdminStage.FindOrCreateAdminStageFeeAsync(dbContext, command.AppraisalId, cancellationToken)
            : await dbContext.AppraisalFees
                .Include(f => f.Items)
                .FirstOrDefaultAsync(f => f.Id == feeId, cancellationToken)
                ?? throw new NotFoundException("AppraisalFee", feeId);

        var isAdminStage = await FeeAdminStage.IsAdminStageAsync(dbContext, fee.AssignmentId, cancellationToken);
        FeeAdminStage.EnsureNoApprovalAwaitingReview(fee, isAdminStage, "add");

        // Before the case is handed over the admin owns the fee: the item is the admin's, billable at once, and
        // never goes through the approval policy. Editing the base is simply an admin-owned FeeCode 01 item.
        if (isAdminStage)
        {
            var requestedAbsorb = fee.BankAbsorbAmount;
            AppraisalFeeItem adminItem;
            if (command.FeeCode == AppraisalFee.BaseFeeCode && FeeAdminStage.SystemBaseToOverride(fee) is { } systemBase)
            {
                // The admin's 01 replaces the system's (same as editing it): convert it rather than refuse.
                adminItem = fee.UpdateItem(systemBase.Id, command.FeeCode, command.FeeDescription, command.FeeAmount);
            }
            else
            {
                FeeAdminStage.EnsureNoOtherBaseItem(fee, command.FeeCode, exceptItemId: null);
                adminItem = fee.AddItem(command.FeeCode, command.FeeDescription, command.FeeAmount);
            }

            adminItem.MarkAsAdminSet();
            // The partial total just re-clamped the absorb; assignment re-applies the requested one.
            fee.RestoreRequestedBankAbsorb(requestedAbsorb);

            if (command.FeeCode == AppraisalFee.BaseFeeCode)
                await FeeAdminStage.EnsureSingleBaseItemAsync(dbContext, fee, cancellationToken);

            return new AddFeeItemResult(adminItem.Id, fee.Id);
        }

        FeeAdminStage.EnsureUserItemIsNotBase(command.FeeCode);

        // Add the item and immediately mark it as user-entered so ReevaluateAddedFees can
        // distinguish it from the base appraisal fee and other system-created items.
        var item = fee.AddItem(command.FeeCode, command.FeeDescription, command.FeeAmount);
        item.MarkAsUserAdded();

        // Compute cumulative total of active USER-entered items (draft/pending; excludes
        // finalised Approved/Rejected). Matches ReevaluateAddedFees and the raise handler.
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

        return new AddFeeItemResult(item.Id, fee.Id);
    }
}
