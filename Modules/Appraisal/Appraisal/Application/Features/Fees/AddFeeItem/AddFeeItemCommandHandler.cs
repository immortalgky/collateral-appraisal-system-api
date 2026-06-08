using Appraisal.Application.Features.Shared;
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
        var fee = await dbContext.AppraisalFees
            .Include(f => f.Items)
            .FirstOrDefaultAsync(f => f.Id == command.FeeId, cancellationToken)
            ?? throw new NotFoundException("AppraisalFee", command.FeeId);

        // Edit lock: reject if any item is awaiting approval (submitted but not resolved)
        if (fee.Items.Any(i => i.ApprovalSubmittedAt.HasValue && i.ApprovalStatus == "Pending"))
            throw new InvalidOperationException(
                "Cannot add a fee item: an approval is currently awaiting review. Wait for the approval to be resolved before making further changes.");

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

        return new AddFeeItemResult(item.Id);
    }
}
