using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Contracts.Services;
using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.SelectQuotationFromIntegration;

public class SelectQuotationFromIntegrationCommandHandler(
    IQuotationRepository quotationRepository,
    IIntegrationEventOutbox outbox,
    IQuotationActivityLogger activityLogger,
    IQuotationTaskOwnershipService taskOwnership)
    : ICommandHandler<SelectQuotationFromIntegrationCommand, SelectQuotationFromIntegrationResult>
{
    public async Task<SelectQuotationFromIntegrationResult> Handle(
        SelectQuotationFromIntegrationCommand command,
        CancellationToken cancellationToken)
    {
        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        // Validate RM staff code when the quotation has an assigned RM.
        // If RmUsername is null/empty on the quotation, no RM validation is possible — allow through.
        if (!string.IsNullOrEmpty(quotation.RmUsername) &&
            !string.Equals(quotation.RmUsername, command.RmUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                "RM staff code does not match the assigned RM for this quotation");
        }

        var isRmOwner = await taskOwnership.IsUserActiveRmPickTaskOwnerAsync(
            quotation.Id, command.RmUsername, cancellationToken);
        if (!isRmOwner)
            throw new UnauthorizedAccessException(
                "The supplied RM is not the active task owner for rm-pick-winner on this quotation");

        if (quotation.Status != "PendingRmSelection")
            throw new InvalidOperationException(
                $"Quotation must be in PendingRmSelection status to select a winner, but is '{quotation.Status}'");

        quotation.PickTentativeWinner(command.CompanyQuotationId, Guid.Empty, "RM");

        quotation.SetRmNegotiationRecommendation(command.RequestNegotiation, command.NegotiationNote);

        var pickedQuotation = quotation.Quotations.First(q => q.Id == command.CompanyQuotationId);
        activityLogger.Log(
            quotation.Id,
            command.CompanyQuotationId,
            pickedQuotation.CompanyId,
            QuotationActivityNames.TentativeWinnerPicked,
            remark: command.NegotiationNote,
            actionByRole: "RM");

        quotationRepository.Update(quotation);

        outbox.Publish(new TentativeWinnerPickedIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            RequestId = quotation.RequestId ?? Guid.Empty,
            CompanyId = pickedQuotation.CompanyId,
            CompanyQuotationId = command.CompanyQuotationId,
            PickedBy = command.RmUsername,
            Role = "RM"
        }, correlationId: quotation.Id.ToString());

        outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            ActivityId = "rm-pick-winner",
            DecisionTaken = "Pick",
            CompletedBy = command.RmUsername,
            TentativeWinnerCompanyQuotationId = command.CompanyQuotationId,
            TentativeWinnerCompanyId = pickedQuotation.CompanyId,
            RmRequestsNegotiation = command.RequestNegotiation,
            RmNegotiationNote = command.NegotiationNote
        }, correlationId: quotation.Id.ToString());

        return new SelectQuotationFromIntegrationResult(quotation.Id, command.CompanyQuotationId, quotation.Status);
    }
}
