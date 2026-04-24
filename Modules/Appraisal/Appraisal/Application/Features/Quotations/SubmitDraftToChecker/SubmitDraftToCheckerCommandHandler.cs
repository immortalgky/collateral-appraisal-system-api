using Appraisal.Application.Features.Quotations.Shared;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.SubmitDraftToChecker;

public class SubmitDraftToCheckerCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox,
    IQuotationActivityLogger activityLogger)
    : ICommandHandler<SubmitDraftToCheckerCommand, SubmitDraftToCheckerResult>
{
    public async Task<SubmitDraftToCheckerResult> Handle(
        SubmitDraftToCheckerCommand command,
        CancellationToken cancellationToken)
    {
        var quotationRequest = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                               ?? throw new NotFoundException($"Quotation request '{command.QuotationRequestId}' not found");

        // Find the invitation for this company
        var invitation = quotationRequest.Invitations
            .FirstOrDefault(i => i.CompanyId == command.CompanyId)
            ?? throw new BadRequestException($"Company '{command.CompanyId}' is not invited to this quotation");

        // Enforce ext-company access — Maker role (ExtAdmin)
        // EnsureCanSubmitQuotation accepts both roles, but submit-to-checker is a Maker-only action.
        if (!currentUser.IsInRole("ExtAdmin"))
            throw new UnauthorizedAccessException("Only the Maker (ExtAdmin) can submit a draft to checker");

        QuotationAccessPolicy.EnsureCanSubmitQuotation(invitation, currentUser);

        if (quotationRequest.Status != "Sent")
            throw new BadRequestException($"Cannot submit to checker: RFQ is in status '{quotationRequest.Status}'");

        var companyQuotation = quotationRequest.Quotations
            .FirstOrDefault(q => q.CompanyId == command.CompanyId);

        if (companyQuotation is null)
            throw new BadRequestException("No draft to submit: company has not saved a draft yet");

        if (companyQuotation.Status == "PendingCheckerReview")
            throw new BadRequestException("Draft already submitted to checker");

        if (companyQuotation.Status != "Draft")
            throw new BadRequestException(
                $"Cannot submit to checker: existing quotation is in status '{companyQuotation.Status}'");

        companyQuotation.MarkPendingCheckerReview();

        outbox.Publish(new QuotationDraftSubmittedToCheckerIntegrationEvent
        {
            QuotationRequestId = quotationRequest.Id,
            CompanyId = command.CompanyId,
            SubmittedBy = currentUser.Username ?? currentUser.UserId?.ToString() ?? string.Empty
        }, correlationId: quotationRequest.Id.ToString());

        activityLogger.Log(
            quotationRequest.Id,
            companyQuotation.Id,
            command.CompanyId,
            "Submitted to Checker",
            actionByRole: "ExtAdmin");

        quotationRepository.Update(quotationRequest);

        return new SubmitDraftToCheckerResult(companyQuotation.Id, companyQuotation.Status);
    }
}
