using Appraisal.Application.Features.Quotations.Shared;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.RecallShortlist;

public class RecallShortlistCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<RecallShortlistCommand, RecallShortlistResult>
{
    public async Task<RecallShortlistResult> Handle(
        RecallShortlistCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        quotation.RecallShortlist(currentUser.UserId!.Value);

        quotationRepository.Update(quotation);

        // v4: resume rm-pick-winner step with RecallShortlist (loopback to admin-review-submissions)
        outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            ActivityId = "rm-pick-winner",
            DecisionTaken = "RecallShortlist",
            CompletedBy = currentUser.UserId?.ToString() ?? string.Empty
        }, correlationId: quotation.Id.ToString());

        return new RecallShortlistResult(quotation.Id, quotation.Status);
    }
}
