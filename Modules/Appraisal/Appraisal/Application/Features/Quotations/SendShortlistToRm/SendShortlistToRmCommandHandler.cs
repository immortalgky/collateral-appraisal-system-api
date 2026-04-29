using Appraisal.Application.Features.Quotations.Shared;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.SendShortlistToRm;

public class SendShortlistToRmCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox,
    IQuotationActivityLogger activityLogger)
    : ICommandHandler<SendShortlistToRmCommand, SendShortlistToRmResult>
{
    public async Task<SendShortlistToRmResult> Handle(
        SendShortlistToRmCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        quotation.SendShortlistToRm(currentUser.UserId!.Value);

        var adminRole = currentUser.IsInRole("Admin") ? "Admin" : "IntAdmin";
        activityLogger.Log(quotation.Id, null, null, QuotationActivityNames.ShortlistSentToRm, actionByRole: adminRole);

        quotationRepository.Update(quotation);

        var shortlistedIds = quotation.Quotations
            .Where(q => q.IsShortlisted)
            .Select(q => q.Id)
            .ToArray();

        if (quotation.RmUserId.HasValue)
        {
            var appraisalIds = quotation.Appraisals
                .Select(a => a.AppraisalId)
                .ToArray();

            outbox.Publish(new ShortlistSentToRmIntegrationEvent
            {
                QuotationRequestId = quotation.Id,
                RequestId = quotation.RequestId ?? Guid.Empty,
                RmUserId = quotation.RmUserId.Value,
                ShortlistedCompanyQuotationIds = shortlistedIds,
                AppraisalIds = appraisalIds
            }, correlationId: quotation.Id.ToString());
        }

        // v4: resume admin-review-submissions step in quotation child workflow
        outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            ActivityId = "admin-review-submissions",
            DecisionTaken = "SendToRm",
            CompletedBy = currentUser.Username ?? currentUser.UserId?.ToString() ?? string.Empty
        }, correlationId: quotation.Id.ToString());

        return new SendShortlistToRmResult(
            quotation.Id,
            quotation.Status,
            quotation.ShortlistSentToRmAt!.Value);
    }
}
