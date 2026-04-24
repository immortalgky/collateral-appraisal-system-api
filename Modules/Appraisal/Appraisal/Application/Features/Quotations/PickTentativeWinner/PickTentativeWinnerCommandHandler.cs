using Appraisal.Application.Features.Quotations.Shared;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.PickTentativeWinner;

public class PickTentativeWinnerCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<PickTentativeWinnerCommand, PickTentativeWinnerResult>
{
    public async Task<PickTentativeWinnerResult> Handle(
        PickTentativeWinnerCommand command,
        CancellationToken cancellationToken)
    {
        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        // RM can only pick for their own request; Admin can override
        var role = currentUser.IsInRole("RequestMaker") ? "RM" : "Admin";
        QuotationAccessPolicy.EnsureRmOrAdmin(quotation, currentUser);

        quotation.PickTentativeWinner(
            command.CompanyQuotationId,
            currentUser.UserId!.Value,
            role);

        // Store RM negotiation recommendation on aggregate for admin-finalize step to read
        quotation.SetRmNegotiationRecommendation(command.RequestNegotiation, command.NegotiationNote);

        quotationRepository.Update(quotation);

        var pickedQuotation = quotation.Quotations.First(q => q.Id == command.CompanyQuotationId);

        outbox.Publish(new TentativeWinnerPickedIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            RequestId = quotation.RequestId ?? Guid.Empty,
            CompanyId = pickedQuotation.CompanyId,
            CompanyQuotationId = command.CompanyQuotationId,
            PickedBy = currentUser.UserId!.Value,
            Role = role
        }, correlationId: quotation.Id.ToString());

        // v4: resume rm-pick-winner step in quotation child workflow
        outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            ActivityId = "rm-pick-winner",
            DecisionTaken = "Pick",
            CompletedBy = currentUser.UserId?.ToString() ?? string.Empty,
            TentativeWinnerCompanyQuotationId = command.CompanyQuotationId,
            TentativeWinnerCompanyId = pickedQuotation.CompanyId,
            RmRequestsNegotiation = command.RequestNegotiation,
            RmNegotiationNote = command.NegotiationNote
        }, correlationId: quotation.Id.ToString());

        return new PickTentativeWinnerResult(quotation.Id, command.CompanyQuotationId, quotation.Status);
    }
}
