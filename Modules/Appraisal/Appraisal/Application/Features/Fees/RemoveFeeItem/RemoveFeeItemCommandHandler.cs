using Appraisal.Application.Features.Shared;
using Appraisal.Domain.Appraisals;
using Shared.Identity;
using Workflow.Contracts.FeeAppointmentApprovals;

namespace Appraisal.Application.Features.Fees.RemoveFeeItem;

public class RemoveFeeItemCommandHandler(AppraisalDbContext dbContext, ISender sender, ICurrentUserService currentUser) : ICommandHandler<RemoveFeeItemCommand>
{
    public async Task<Unit> Handle(RemoveFeeItemCommand command, CancellationToken cancellationToken)
    {
        var fee = await dbContext.AppraisalFees
            .Include(fi => fi.Items)
            .FirstOrDefaultAsync(f => f.Id == command.FeeId, cancellationToken);
        if (fee is null)
            throw new NotFoundException("Fee", command.FeeId);

        var existing = fee.Items.FirstOrDefault(i => i.Id == command.FeeItemId)
            ?? throw new NotFoundException($"Fee item {command.FeeItemId} not found");

        var isAdminStage = await FeeAdminStage.IsAdminStageAsync(dbContext, fee.AssignmentId, cancellationToken);
        FeeAdminStage.EnsureNoApprovalAwaitingReview(fee, isAdminStage, "remove");
        FeeAdminStage.EnsureCanRemove(existing, isAdminStage);

        if (isAdminStage)
        {
            // Only admin items (or the system base, refused above) get here, and those never entered the approval
            // policy, so there is nothing to re-evaluate.
            var requestedAbsorb = fee.BankAbsorbAmount;
            fee.RemoveItem(command.FeeItemId);
            fee.RestoreRequestedBankAbsorb(requestedAbsorb);
            return Unit.Value;
        }

        fee.RemoveItem(command.FeeItemId);

        // Compute cumulative total of active USER-entered items (draft/pending; excludes
        // finalised Approved/Rejected) after removal — mirrors ReevaluateAddedFees predicate.
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

        return Unit.Value;
    }
}
