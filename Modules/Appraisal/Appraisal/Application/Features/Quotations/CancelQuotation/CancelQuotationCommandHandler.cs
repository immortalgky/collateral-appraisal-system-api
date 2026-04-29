using Appraisal.Application.Features.Quotations.Shared;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.CancelQuotation;

public class CancelQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox,
    IQuotationActivityLogger activityLogger)
    : ICommandHandler<CancelQuotationCommand, CancelQuotationResult>
{
    public async Task<CancelQuotationResult> Handle(
        CancelQuotationCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        var wasAlreadyCancelled = quotation.Status == "Cancelled";
        quotation.Cancel(command.Reason);

        var adminRole = currentUser.IsInRole("Admin") ? "Admin" : "IntAdmin";
        activityLogger.Log(quotation.Id, null, null, QuotationActivityNames.QuotationCancelled, command.Reason, actionByRole: adminRole);

        quotationRepository.Update(quotation);

        if (!wasAlreadyCancelled)
        {
            var invitedCompanyIds = quotation.Invitations.Select(i => i.CompanyId).ToArray();
            outbox.Publish(new QuotationCancelledIntegrationEvent
            {
                QuotationRequestId = quotation.Id,
                TaskExecutionId = quotation.TaskExecutionId,
                Reason = command.Reason,
                InvitedCompanyIds = invitedCompanyIds,
                RmUserId = quotation.RmUserId
            }, correlationId: quotation.Id.ToString());

            // v4: cancel the quotation child workflow via the current active step
            // The consumer resolves which activity is currently active and calls CancelWorkflowAsync.
            outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
            {
                QuotationRequestId = quotation.Id,
                ActivityId = "cancel",  // sentinel: consumer will call CancelWorkflowAsync
                DecisionTaken = "Cancel",
                CompletedBy = currentUser.Username ?? currentUser.UserId?.ToString() ?? string.Empty
            }, correlationId: quotation.Id.ToString());
        }

        return new CancelQuotationResult(quotation.Id, quotation.Status);
    }
}
