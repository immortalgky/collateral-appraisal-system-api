using Appraisal.Application.Features.Quotations.Shared;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.CloseQuotation;

public class CloseQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<CloseQuotationCommand, CloseQuotationResult>
{
    public async Task<CloseQuotationResult> Handle(
        CloseQuotationCommand command,
        CancellationToken cancellationToken)
    {
        // Admin or SYSTEM (background service) can close
        // SYSTEM callers will not have a userId, so we allow anonymous system calls
        // but when there is an authenticated user, it must be Admin
        if (currentUser.IsAuthenticated)
            QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        var wasAlreadyClosed = quotation.Status == "UnderAdminReview";
        quotation.Close();
        quotationRepository.Update(quotation);

        // Only publish the event if we actually transitioned (idempotency guard)
        if (!wasAlreadyClosed)
        {
            outbox.Publish(new QuotationSubmissionsClosedIntegrationEvent
            {
                QuotationRequestId = quotation.Id,
                RequestId = quotation.RequestId ?? Guid.Empty,
                AdminUserIds = []   // Notification handler will broadcast to admin group
            }, correlationId: quotation.Id.ToString());
        }

        return new CloseQuotationResult(quotation.Id, quotation.Status);
    }
}
