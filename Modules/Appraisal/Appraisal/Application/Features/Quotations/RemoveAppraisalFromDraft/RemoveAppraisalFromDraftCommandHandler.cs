using Appraisal.Application.Features.Quotations.Shared;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.Features.Quotations.RemoveAppraisalFromDraft;

public class RemoveAppraisalFromDraftCommandHandler(
    ISqlConnectionFactory connectionFactory,
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RemoveAppraisalFromDraftCommand, RemoveAppraisalFromDraftResult>
{
    public async Task<RemoveAppraisalFromDraftResult> Handle(
        RemoveAppraisalFromDraftCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation request '{command.QuotationRequestId}' not found.");

        var adminUsername = currentUser.Username
            ?? throw new UnauthorizedAccessException("Cannot resolve current user username from token");
        var adminUserId = currentUser.UserId; // for integration event only

        // Only the quotation owner can remove appraisals
        if (quotation.RequestedBy != adminUsername)
            throw new UnauthorizedAccessException("You can only modify your own Draft quotation.");

        if (quotation.Status != "Draft")
            throw new BadRequestException($"Cannot remove appraisal from quotation in status '{quotation.Status}'.");

        // Recompute BankingSegment from the set that will remain AFTER this removal, and set it
        // BEFORE calling RemoveAppraisal below. If this is the last appraisal, RemoveAppraisal
        // auto-cancels the quotation (QuotationRequest.cs) — Status stops being "Draft" the
        // moment that happens, inside that same call — so SetBankingSegment's Draft-only guard
        // would reject a call made after it, leaving a stale, non-empty cached value on a now
        // appraisal-less Cancelled quotation. Setting it first, while still Draft, correctly
        // clears it to [] in that case too.
        var remainingAppraisalIds = quotation.Appraisals
            .Where(a => a.AppraisalId != command.AppraisalId)
            .Select(a => a.AppraisalId)
            .ToArray();
        var appraisalSegments = await SegmentCoverage.LoadAppraisalSegmentSetAsync(
            connectionFactory.GetOpenConnection(), remainingAppraisalIds);
        quotation.SetBankingSegment(appraisalSegments.ToList());

        // Domain guard: appraisal must be present on this quotation
        try
        {
            quotation.RemoveAppraisal(command.AppraisalId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not part of this quotation"))
        {
            throw new NotFoundException($"Appraisal '{command.AppraisalId}' was not found on quotation '{command.QuotationRequestId}'.");
        }

        // Remove the matching display item (no-op if item was never created)
        quotation.RemoveItem(command.AppraisalId);

        // Remove shared-document selections keyed to this appraisal.
        // Only possible while still Draft — if the quotation was just auto-cancelled (last appraisal
        // removed), SetSharedDocuments would reject the call, so we skip it. The docs are effectively
        // orphaned but harmless on a Cancelled quotation.
        if (quotation.Status == "Draft")
        {
            var remainingDocs = quotation.SharedDocuments
                .Where(d => d.AppraisalId != command.AppraisalId)
                .Select(d => (d.AppraisalId, d.DocumentId, d.Level))
                .ToList();

            if (remainingDocs.Count != quotation.SharedDocuments.Count)
                quotation.SetSharedDocuments(remainingDocs, adminUsername, dateTimeProvider.ApplicationNow);
        }

        quotationRepository.Update(quotation);

        outbox.Publish(new AppraisalRemovedFromQuotationIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            AppraisalId = command.AppraisalId,
            AdminUserId = adminUserId ?? Guid.Empty
        }, correlationId: quotation.Id.ToString());

        return new RemoveAppraisalFromDraftResult(
            quotation.Id,
            quotation.TotalAppraisals,
            quotation.Status);
    }
}
