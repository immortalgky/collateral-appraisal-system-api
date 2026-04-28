using Appraisal.Application.Features.Quotations.Shared;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.RespondNegotiation;

public class RespondNegotiationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<RespondNegotiationCommand, RespondNegotiationResult>
{
    public async Task<RespondNegotiationResult> Handle(
        RespondNegotiationCommand command,
        CancellationToken cancellationToken)
    {
        var quotation = await quotationRepository.GetByIdWithNegotiationsAsync(
            command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        // Only the invited ext-company can respond
        var invitation = quotation.Invitations
            .FirstOrDefault(i => i.CompanyId == currentUser.CompanyId);

        if (invitation == null)
            throw new UnauthorizedAccessException("User's company is not invited to this quotation");

        QuotationAccessPolicy.EnsureCanSubmitQuotation(invitation, currentUser);

        var itemDiscounts = command.Items?.ToDictionary(
            i => i.AppraisalId,
            i => i.NegotiatedDiscount);

        quotation.RespondNegotiation(
            command.CompanyQuotationId,
            command.NegotiationId,
            command.Verb,
            command.CounterPrice,
            command.Message,
            currentUser.UserId!.Value,
            itemDiscounts);

        quotationRepository.Update(quotation);

        var companyQuotation = quotation.Quotations.First(q => q.Id == command.CompanyQuotationId);
        var negotiation = companyQuotation.Negotiations.First(n => n.Id == command.NegotiationId);

        // v4: resume ext-respond-negotiation step in quotation child workflow
        outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            ActivityId = "ext-respond-negotiation",
            DecisionTaken = command.Verb,  // Accept | Counter | Reject
            CompletedBy = currentUser.Username ?? currentUser.UserId?.ToString() ?? string.Empty,
            CompanyId = currentUser.CompanyId
        }, correlationId: quotation.Id.ToString());

        return new RespondNegotiationResult(quotation.Id, quotation.Status, negotiation.Status);
    }
}
