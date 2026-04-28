using System.Security.Authentication;
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

        var quotation =
            await quotationRepository.GetByIdWithNegotiationsAsync(command.QuotationRequestId, cancellationToken)
            ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        if (currentUser.UserId is null)
            throw new AuthenticationException("Current user does not have a valid user id");

        quotation.StartNegotiation(
            command.CompanyQuotationId,
            currentUser.UserId,
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
            CompletedBy = currentUser.Username ?? currentUser.UserId?.ToString() ?? string.Empty
        }, quotation.Id.ToString());

        return new OpenNegotiationResult(
            quotation.Id,
            latestNegotiation.Id,
            latestNegotiation.NegotiationRound,
            quotation.Status);
    }
}