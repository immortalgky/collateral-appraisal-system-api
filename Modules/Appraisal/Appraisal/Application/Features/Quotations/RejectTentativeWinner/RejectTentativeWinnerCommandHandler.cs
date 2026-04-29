using Appraisal.Application.Features.Quotations.Shared;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.RejectTentativeWinner;

public class RejectTentativeWinnerCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox,
    IQuotationActivityLogger activityLogger)
    : ICommandHandler<RejectTentativeWinnerCommand, RejectTentativeWinnerResult>
{
    public async Task<RejectTentativeWinnerResult> Handle(
        RejectTentativeWinnerCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        if (!quotation.TentativeWinnerQuotationId.HasValue)
            throw new BadRequestException("No tentative winner to reject");

        var tentativeWinnerId = quotation.TentativeWinnerQuotationId.Value;
        quotation.RejectTentativeWinner(tentativeWinnerId, command.Reason);

        var rejectedQuotation = quotation.Quotations.First(q => q.Id == tentativeWinnerId);
        var adminRole = currentUser.IsInRole("Admin") ? "Admin" : "IntAdmin";
        activityLogger.Log(
            quotation.Id,
            tentativeWinnerId,
            rejectedQuotation.CompanyId,
            QuotationActivityNames.TentativeWinnerRejected,
            command.Reason,
            actionByRole: adminRole);

        quotationRepository.Update(quotation);

        // v4: resume admin-finalize step in quotation child workflow (RejectTentative path → loopback to rm-pick-winner)
        outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            ActivityId = "admin-finalize",
            DecisionTaken = "RejectTentative",
            CompletedBy = currentUser.Username ?? currentUser.UserId?.ToString() ?? string.Empty
        }, correlationId: quotation.Id.ToString());

        return new RejectTentativeWinnerResult(quotation.Id, quotation.Status);
    }
}
