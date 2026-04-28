using Appraisal.Application.Services;
using Appraisal.Domain.Quotations;

namespace Appraisal.Application.Features.Assignments.AssignAppraisal;

public class AssignAppraisalCommandHandler(
    IAppraisalRepository appraisalRepository,
    IQuotationRepository quotationRepository,
    IAssignmentFeeService feeService)
    : ICommandHandler<AssignAppraisalCommand, AssignAppraisalResult>
{
    public async Task<AssignAppraisalResult> Handle(
        AssignAppraisalCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        AppraisalAssignment resolvedAssignment;

        var pendingAssignment =
            appraisal.Assignments.FirstOrDefault(a => a.AssignmentStatus == AssignmentStatus.Pending);
        if (pendingAssignment is not null)
        {
            pendingAssignment.Assign(
                command.AssignmentType,
                command.AssigneeUserId,
                command.AssigneeCompanyId,
                command.AssignmentMethod,
                command.InternalAppraiserId,
                command.InternalFollowupAssignmentMethod,
                assignedBy: command.AssignedBy);
            resolvedAssignment = pendingAssignment;
        }
        else
        {
            resolvedAssignment = appraisal.Assign(
                command.AssignmentType,
                command.AssigneeUserId,
                command.AssigneeCompanyId,
                command.AssignmentMethod,
                command.InternalAppraiserId,
                command.InternalFollowupAssignmentMethod,
                assignedBy: command.AssignedBy);
        }

        var feeSource = await ResolveFeeSourceAsync(
            command, resolvedAssignment, cancellationToken);

        await feeService.EnsureAssignmentFeeItemsAsync(
            appraisalId: command.AppraisalId,
            assignmentId: resolvedAssignment.Id,
            source: feeSource,
            ct: cancellationToken);

        return new AssignAppraisalResult(resolvedAssignment.Id);
    }

    /// <summary>
    /// Pick the fee source from the chosen AssignmentMethod. "Quotation" pulls the
    /// per-appraisal price agreed during the RFQ; everything else falls back to the
    /// fee structure tier lookup.
    /// </summary>
    private async Task<AssignmentFeeSource> ResolveFeeSourceAsync(
        AssignAppraisalCommand command,
        AppraisalAssignment assignment,
        CancellationToken ct)
    {
        if (!string.Equals(command.AssignmentMethod, "Quotation", StringComparison.OrdinalIgnoreCase))
            return new AssignmentFeeSource.TierBased();

        // Resolve the Finalized RFQ for this appraisal. We try the linkedId stamped at
        // finalize time first (cheapest), but ALWAYS fall back to the appraisal-id lookup
        // when that path comes up empty — race conditions, manual assignment-row swaps,
        // and pre-link legacy data can all leave the linkage missing or stale.
        QuotationRequest? quotationRequest = null;

        if (assignment.QuotationRequestId is { } linkedId)
            quotationRequest = await quotationRepository.GetByIdAsync(linkedId, ct);

        if (quotationRequest is null || quotationRequest.Status != "Finalized")
            quotationRequest = await quotationRepository.GetFinalizedByAppraisalIdAsync(command.AppraisalId, ct);

        if (quotationRequest is null)
            throw new BadRequestException(
                $"Cannot use Quotation fee source: no Finalized RFQ contains appraisal '{command.AppraisalId}'. " +
                "Make sure the quotation is finalized with a winner before assigning.");

        var winningQuotation = quotationRequest.Quotations.FirstOrDefault(q => q.IsWinner)
            ?? throw new BadRequestException(
                $"Cannot use Quotation fee source: RFQ '{quotationRequest.QuotationNumber ?? quotationRequest.Id.ToString()}' has no winning company quotation.");

        var item = winningQuotation.Items.FirstOrDefault(i => i.AppraisalId == command.AppraisalId);

        // The fee service expects the *ex-VAT* amount — VAT is recomputed on top of the
        // AppraisalFeeItem when RecalculateFromItems runs. Modern submissions populate
        // FeeAmount/Discount/VatPercent → use FeeAfterDiscount (ex-VAT). Legacy rows
        // without a fee breakdown only have a single QuotedPrice number; fall back to
        // that or the aggregate total as a last resort.
        decimal amount;
        if (item is not null && item.FeeAmount > 0m)
            amount = item.FeeAfterDiscount;
        else if (item is not null)
            amount = item.QuotedPrice;
        else
            amount = winningQuotation.TotalQuotedPrice;

        if (amount <= 0m)
            throw new BadRequestException(
                $"Cannot use Quotation fee source: winning quotation has no usable price for appraisal '{command.AppraisalId}'.");

        // Backfill the linkage on this assignment so future reads don't need the
        // appraisal-id fallback path.
        if (assignment.QuotationRequestId != quotationRequest.Id)
            assignment.SetQuotationRequestId(quotationRequest.Id);

        return new AssignmentFeeSource.Quotation(amount, quotationRequest.Id, quotationRequest.QuotationNumber);
    }
}
