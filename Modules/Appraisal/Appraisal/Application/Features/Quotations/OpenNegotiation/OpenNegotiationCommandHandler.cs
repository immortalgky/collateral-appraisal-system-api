using Appraisal.Application.Features.Quotations.Shared;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.OpenNegotiation;

public class OpenNegotiationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<OpenNegotiationCommand, OpenNegotiationResult>
{
    public async Task<OpenNegotiationResult> Handle(
        OpenNegotiationCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdWithNegotiationsAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        quotation.StartNegotiation(
            command.CompanyQuotationId,
            command.ProposedPrice,
            currentUser.UserId!.Value,
            command.Message);

        quotationRepository.Update(quotation);

        // Find the negotiation just created on the company quotation
        var companyQuotation = quotation.Quotations.First(q => q.Id == command.CompanyQuotationId);
        var latestNegotiation = companyQuotation.Negotiations.OrderByDescending(n => n.NegotiationRound).First();

        // v4: resume admin-finalize step in quotation child workflow (OpenNegotiation path)
        outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            ActivityId = "admin-finalize",
            DecisionTaken = "OpenNegotiation",
            CompletedBy = currentUser.UserId?.ToString() ?? string.Empty
        }, correlationId: quotation.Id.ToString());

        return new OpenNegotiationResult(
            quotation.Id,
            latestNegotiation.Id,
            latestNegotiation.NegotiationRound,
            quotation.Status);
    }
}
