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

        // Edit lock: reject if any item is awaiting approval (submitted but not resolved)
        if (fee.Items.Any(i => i.ApprovalSubmittedAt.HasValue && i.ApprovalStatus == "Pending"))
            throw new InvalidOperationException(
                "Cannot remove a fee item: an approval is currently awaiting review. Wait for the approval to be resolved before making further changes.");

        fee.RemoveItem(command.FeeItemId);

        // Compute cumulative total of active USER-entered items (draft/pending; excludes
        // finalised Approved/Rejected) after removal — mirrors ReevaluateAddedFees predicate.
        var cumulativeTotal = fee.Items
            .Where(i => i.IsActiveAddedFee)
            .Sum(i => i.FeeAmount);

        var requestSource = currentUser.IsExternal
            ? FeeApprovalRequestSource.External
            : FeeApprovalRequestSource.Internal;

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
