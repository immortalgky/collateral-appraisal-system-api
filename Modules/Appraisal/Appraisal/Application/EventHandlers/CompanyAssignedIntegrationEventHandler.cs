using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Appraisal.Application.EventHandlers;

public class CompanyAssignedIntegrationEventHandler(
    IAppraisalRepository appraisalRepository,
    IQuotationRepository quotationRepository,
    IAppraisalUnitOfWork unitOfWork,
    IAssignmentFeeService feeService,
    ILogger<CompanyAssignedIntegrationEventHandler> logger,
    InboxGuard<AppraisalDbContext> inboxGuard
) : IConsumer<CompanyAssignedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CompanyAssignedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId}, CompanyId: {CompanyId}, Method: {Method}",
            nameof(CompanyAssignedIntegrationEvent), message.AppraisalId, message.CompanyId, message.AssignmentMethod);

        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(message.AppraisalId, ct);

        if (appraisal is null)
        {
            logger.LogWarning(
                "Appraisal {AppraisalId} not found for company assignment", message.AppraisalId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        var assignment = appraisal.Assignments
            .Where(a => a.AssignmentStatus.Code != "Rejected" && a.AssignmentStatus.Code != "Cancelled")
            .OrderByDescending(a => a.AssignedAt)
            .FirstOrDefault();

        if (assignment is null)
        {
            logger.LogWarning(
                "No active assignment found for Appraisal {AppraisalId}", message.AppraisalId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        // Promote the assignment to Assigned for all external paths (Manual, RoundRobin, Forced, Quotation).
        // Preserve internal-followup fields: Assign() defaults them to null, so without
        // passing them back we'd clobber values set by InternalFollowupAssignedIntegrationEventHandler
        // if that event was processed first (consumer delivery order is not guaranteed).
        assignment.Assign(
            assignmentType: "External",
            assigneeCompanyId: message.CompanyId.ToString(),
            assignmentMethod: message.AssignmentMethod,
            internalAppraiserId: assignment.InternalAppraiserId,
            internalFollowupMethod: assignment.InternalFollowupAssignmentMethod,
            assignedBy: "System");

        var baseFeeSource = await ResolveFeeSourceAsync(message.AppraisalId, message.AssignmentMethod, ct);

        // Construction Inspection appraisals carry over the prior engagement's fee; the fee service
        // substitutes AssignmentFeeSource.ConstructionInspection when that applies. Mirrors what
        // InternalAssignedIntegrationEventHandler does for the internal path.
        var feeSource = await feeService.ResolveSourceForAppraisalAsync(appraisal, baseFeeSource, ct);

        await feeService.EnsureAssignmentFeeItemsAsync(
            appraisalId: message.AppraisalId,
            assignmentId: assignment.Id,
            source: feeSource,
            ct: ct);

        await appraisalRepository.UpdateAsync(appraisal, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);

        logger.LogInformation(
            "Updated AppraisalAssignment for AppraisalId {AppraisalId}: CompanyId={CompanyId}, Method={Method}",
            message.AppraisalId, message.CompanyId, message.AssignmentMethod);
    }

    /// <summary>
    /// Picks the fee source for the given assignment method.
    /// Quotation: looks up the Finalized QuotationRequest and extracts the winning company's
    /// per-appraisal price. Everything else falls back to the fee-structure tier lookup.
    /// </summary>
    private async Task<AssignmentFeeSource> ResolveFeeSourceAsync(
        Guid appraisalId,
        string assignmentMethod,
        CancellationToken ct)
    {
        if (!string.Equals(assignmentMethod, "Quotation", StringComparison.OrdinalIgnoreCase))
            return new AssignmentFeeSource.TierBased();

        var quotationRequest = await quotationRepository.GetFinalizedByAppraisalIdAsync(appraisalId, ct);

        if (quotationRequest is null)
            throw new BadRequestException(
                $"Cannot use Quotation fee source: no Finalized RFQ contains appraisal '{appraisalId}'. " +
                "Make sure the quotation is finalized with a winner before assigning.");

        var winningQuotation = quotationRequest.Quotations.FirstOrDefault(q => q.IsWinner)
            ?? throw new BadRequestException(
                $"Cannot use Quotation fee source: RFQ '{quotationRequest.QuotationNumber ?? quotationRequest.Id.ToString()}' has no winning company quotation.");

        var item = winningQuotation.Items.FirstOrDefault(i => i.AppraisalId == appraisalId);

        // The fee service expects the ex-VAT amount — VAT is recomputed on top of the AppraisalFeeItem
        // when RecalculateFromItems runs. Modern submissions populate FeeAmount/Discount/VatPercent →
        // use FeeAfterDiscount (ex-VAT). Legacy rows without a fee breakdown only have a single
        // QuotedPrice number; fall back to that or the aggregate total as a last resort.
        decimal amount;
        if (item is not null && item.FeeAmount > 0m)
            amount = item.FeeAfterDiscount;
        else if (item is not null)
            amount = item.QuotedPrice;
        else
            amount = winningQuotation.TotalQuotedPrice;

        if (amount <= 0m)
            throw new BadRequestException(
                $"Cannot use Quotation fee source: winning quotation has no usable price for appraisal '{appraisalId}'.");

        return new AssignmentFeeSource.Quotation(amount, quotationRequest.Id, quotationRequest.QuotationNumber);
    }
}
