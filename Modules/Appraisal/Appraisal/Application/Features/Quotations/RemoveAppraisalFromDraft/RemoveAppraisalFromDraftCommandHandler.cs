using Appraisal.Application.Features.Quotations.Shared;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.RemoveAppraisalFromDraft;

public class RemoveAppraisalFromDraftCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<RemoveAppraisalFromDraftCommand, RemoveAppraisalFromDraftResult>
{
    public async Task<RemoveAppraisalFromDraftResult> Handle(
        RemoveAppraisalFromDraftCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation request '{command.QuotationRequestId}' not found.");

        var adminId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot resolve current user ID from token");

        if (quotation.RequestedBy != adminId)
            throw new UnauthorizedAccessException("You can only modify your own Draft quotation.");

        // Domain method handles Draft-only guard and auto-cancel when last appraisal removed
        quotation.RemoveAppraisal(command.AppraisalId);

        var autoCancelled = quotation.Status == "Cancelled";

        quotationRepository.Update(quotation);

        // M1: always publish the removal event
        outbox.Publish(new AppraisalRemovedFromQuotationIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            AppraisalId = command.AppraisalId,
            AdminUserId = adminId
        }, correlationId: quotation.Id.ToString());

        // M1: when removing the last appraisal auto-cancels the quotation, also publish the cancel event
        if (autoCancelled)
        {
            var invitedCompanyIds = quotation.Invitations
                .Select(i => i.CompanyId)
                .ToArray();

            outbox.Publish(new QuotationCancelledIntegrationEvent
            {
                QuotationRequestId = quotation.Id,
                TaskExecutionId = quotation.TaskExecutionId,
                Reason = "Last appraisal removed",
                InvitedCompanyIds = invitedCompanyIds,
                RmUserId = quotation.RmUserId
            }, correlationId: quotation.Id.ToString());
        }

        return new RemoveAppraisalFromDraftResult(
            quotation.Id,
            quotation.TotalAppraisals,
            autoCancelled);
    }
}
