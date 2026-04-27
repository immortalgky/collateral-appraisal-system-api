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

        // Capture the source status before transition so we know which workflow step to resume.
        // From UnderAdminReview the admin is selecting a winner directly (skipping RM); the workflow is
        // parked on "admin-review-submissions". From PendingRmSelection it's parked on "rm-pick-winner".
        var startedFromAdminReview = quotation.Status == "UnderAdminReview";

        // Admin-direct-pick from UnderAdminReview is admin-only (RMs can't see this status, and the
        // recommendation/role semantics below assume an admin actor). Other source statuses allow RM or Admin.
        if (startedFromAdminReview)
            QuotationAccessPolicy.EnsureAdmin(currentUser);
        else
            QuotationAccessPolicy.EnsureRmOrAdmin(quotation, currentUser);

        var role = startedFromAdminReview
            ? "Admin"
            : currentUser.IsInRole("RequestMaker") ? "RM" : "Admin";

        quotation.PickTentativeWinner(
            command.CompanyQuotationId,
            currentUser.UserId!.Value,
            role);

        // RM negotiation recommendation only makes sense when an RM picked. Skipping on admin-direct-pick
        // also avoids silently clearing a prior recommendation if the quotation has bounced back through
        // UnderAdminReview after an earlier RM selection.
        if (!startedFromAdminReview)
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

        // Resume the workflow step the engine is parked on. Admin direct-pick happens on
        // "admin-review-submissions" (decision SelectAsWinner → admin-finalize); the normal RM
        // flow happens on "rm-pick-winner" (decision Pick → admin-finalize).
        outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            ActivityId = startedFromAdminReview ? "admin-review-submissions" : "rm-pick-winner",
            DecisionTaken = startedFromAdminReview ? "SelectAsWinner" : "Pick",
            CompletedBy = currentUser.Username ?? currentUser.UserId?.ToString() ?? string.Empty,
            TentativeWinnerCompanyQuotationId = command.CompanyQuotationId,
            TentativeWinnerCompanyId = pickedQuotation.CompanyId,
            // Suppress RM-only fields when admin picked directly so admin-finalize doesn't render
            // a non-existent "RM requested negotiation" hint.
            RmRequestsNegotiation = startedFromAdminReview ? false : command.RequestNegotiation,
            RmNegotiationNote = startedFromAdminReview ? null : command.NegotiationNote
        }, correlationId: quotation.Id.ToString());

        return new PickTentativeWinnerResult(quotation.Id, command.CompanyQuotationId, quotation.Status);
    }
}
