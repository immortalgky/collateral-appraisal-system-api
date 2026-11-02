using Appraisal.Application.Features.Assignments.AssignmentEmails;
using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Appraisal.Application.EventHandlers;

// Co-located on the partitioned "appraisal-sync" endpoint
// (Program.cs) with the internal-assignment handlers so all assignment events for one appraisal
// are serialized, eliminating the cross-event-type field-clobber race on AppraisalAssignment.
[ExcludeFromConfigureEndpoints]
public class CompanyAssignedIntegrationEventHandler(
    IAppraisalRepository appraisalRepository,
    IQuotationRepository quotationRepository,
    IAppraisalUnitOfWork unitOfWork,
    IAssignmentFeeService feeService,
    ILogger<CompanyAssignedIntegrationEventHandler> logger,
    InboxGuard<AppraisalDbContext> inboxGuard,
    AssignmentEmailDispatcher emailDispatcher
) : IConsumer<CompanyAssignedIntegrationEvent>
{
    public Task Consume(ConsumeContext<CompanyAssignedIntegrationEvent> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, _ => HandleAsync(context), context.CancellationToken);

    private async Task HandleAsync(ConsumeContext<CompanyAssignedIntegrationEvent> context)
    {
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
            return;
        }

        var assignment = appraisal.Assignments.Current();

        if (assignment is null)
        {
            logger.LogWarning(
                "No active assignment found for Appraisal {AppraisalId}", message.AppraisalId);
            return;
        }

        // Read before Assign() flips it: a row that was Pending is a genuine (re)assignment, so a system base left by
        // an earlier source is re-priced; any other re-fire must not rewrite a base that may already be billed.
        var wasPending = assignment.AssignmentStatus == AssignmentStatus.Pending;
        // A company the row did not hold is a new engagement even on a row left Assigned (the automatic route-back
        // through initial-routing, when round-robin lands elsewhere): it gets its email (#622, user 2026-11-01).
        var isNewCompany = !(Guid.TryParse(assignment.AssigneeCompanyId, out var heldCompanyId) && heldCompanyId == message.CompanyId);

        // Promote the assignment to Assigned for all external paths (Manual, RoundRobin, Forced, Quotation).
        // Preserve internal-followup fields: Assign() defaults them to null, so without
        // passing them back we'd clobber values set by InternalFollowupAssignedIntegrationEventHandler
        // on an earlier pass (a route-back keeps the row), or if a dead-lettered CompanyAssigned is redriven after it.
        assignment.Assign(
            assignmentType: "External",
            assigneeCompanyId: message.CompanyId.ToString(),
            assignmentMethod: message.AssignmentMethod,
            internalAppraiserId: assignment.InternalAppraiserId,
            internalFollowupMethod: assignment.InternalFollowupAssignmentMethod,
            // The admin who pressed Assign, recorded on the row by AssignAppraisalCommandHandler before the workflow
            // resumed (Assign() would otherwise stamp "System" over them).
            assignedBy: assignment.AssignedBy);

        // One answer for both the fee billed and the fee the email names.
        var isQuotation = string.Equals(message.AssignmentMethod, AssignmentDefaultValues.MethodQuotation, StringComparison.OrdinalIgnoreCase);
        var baseFeeSource = await ResolveFeeSourceAsync(message.AppraisalId, message.CompanyId, isQuotation, assignment, ct);

        // Construction Inspection appraisals carry over the prior engagement's fee; the fee service
        // substitutes AssignmentFeeSource.ConstructionInspection when that applies. Mirrors what
        // InternalAssignedIntegrationEventHandler does for the internal path.
        var feeSource = await feeService.ResolveSourceForAppraisalAsync(appraisal, baseFeeSource, ct);

        await feeService.EnsureAssignmentFeeItemsAsync(
            appraisalId: message.AppraisalId,
            assignmentId: assignment.Id,
            source: feeSource,
            repriceSystemBase: wasPending,
            ct: ct);

        // EXT (#622): a genuine assignment (the row was Pending, or the company is new) that nobody chose an email for
        // gets the bank template, sent by the follow-up consumer once the officer is known. A route-back replay of the
        // company the row holds adds nothing. The email never holds up the assignment.
        if (wasPending || isNewCompany)
        {
            try
            {
                await emailDispatcher.AddExtTemplateAsync(assignment, message.CompanyId, isQuotation, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Assignment email for AppraisalId {AppraisalId} was not prepared; the assignment goes ahead without it",
                    message.AppraisalId);
            }
        }

        // Kept on purpose: assignment events only touch AppraisalAssignment rows, so this stamps Appraisals.UpdatedAt
        // (read by GetAppraisalStatus and vw_MisCasReport).
        await appraisalRepository.UpdateAsync(appraisal, ct);
        await unitOfWork.SaveChangesAsync(ct);

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
        Guid companyId,
        bool isQuotation,
        AppraisalAssignment assignment,
        CancellationToken ct)
    {
        if (!isQuotation)
            return new AssignmentFeeSource.TierBased();

        // Resolve the Finalized RFQ for this appraisal. Look up by appraisal id — this is
        // authoritative regardless of whether QuotationRequestId was previously backfilled
        // on the assignment. (The backfill below is a defensive cache for legacy callers.)
        var quotationRequest = await quotationRepository.GetFinalizedByAppraisalIdAsync(appraisalId, companyId, ct);

        if (quotationRequest is null)
            throw new BadRequestException(
                $"Cannot use Quotation fee source: the latest finalized RFQ for appraisal '{appraisalId}' was not won by company '{companyId}' (or none exists). " +
                "Make sure the quotation is finalized with this company as the winner before assigning.");

        // Whether the winner's price is usable is judged by the fee service, in the assignment's own transaction: an
        // admin's own base (01) outranks the quotation and needs no price, otherwise an unusable one is logged and
        // no base fee is created (never a 01 at 0, never a throw after the workflow has moved on). So an unusable
        // price is passed on as null (with the reason), never decided here.
        var price = QuotationFeePrice.Resolve(quotationRequest, appraisalId, companyId);

        // Backfill the linkage so future reads don't need the appraisal-id fallback path.
        if (assignment.QuotationRequestId != quotationRequest.Id)
            assignment.SetQuotationRequestId(quotationRequest.Id);

        return new AssignmentFeeSource.Quotation(price.Amount, quotationRequest.Id, quotationRequest.QuotationNumber, price.Error);
    }
}
