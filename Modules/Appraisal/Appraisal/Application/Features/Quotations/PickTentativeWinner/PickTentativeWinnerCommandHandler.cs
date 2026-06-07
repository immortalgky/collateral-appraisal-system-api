using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Contracts.Services;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.Features.Quotations.PickTentativeWinner;

public class PickTentativeWinnerCommandHandler(
    IQuotationRepository quotationRepository,
    IIntegrationEventOutbox outbox,
    IQuotationTaskOwnershipService taskOwnership,
    IQuotationActivityLogger activityLogger,
    IDateTimeProvider dateTimeProvider)
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
        // recommendation/role semantics below assume an admin actor). Other source statuses require
        // the caller to hold the active rm-pick-winner task — gating by the static RmUserId let any
        // original requestor pick even after the task was reassigned.
        if (startedFromAdminReview)
        {
            QuotationAccessPolicy.EnsureAdmin(command.Actor);
        }
        else if (command.Actor.Role is not ("Admin" or "IntAdmin"))
        {
            // Admin override preserved from the previous IsCallerActiveTaskOwnerAsync bypass:
            // admins may pick on PendingRmSelection without holding the rm-pick-winner task.
            var isOwner = await taskOwnership.IsUserActiveRmPickTaskOwnerAsync(
                quotation.Id, command.Actor.Username, cancellationToken);
            if (!isOwner)
                throw new UnauthorizedAccessException(
                    "Only the RM assigned to the active rm-pick-winner task can pick the winner");
        }

        var role = startedFromAdminReview
            ? "Admin"
            : command.Actor.Role == "RequestMaker" ? "RM" : "Admin";

        quotation.PickTentativeWinner(
            command.CompanyQuotationId,
            command.Actor.UserId ?? Guid.Empty,
            role,
            dateTimeProvider.ApplicationNow);

        // RM negotiation recommendation only makes sense when an RM picked. Skipping on admin-direct-pick
        // also avoids silently clearing a prior recommendation if the quotation has bounced back through
        // UnderAdminReview after an earlier RM selection.
        if (!startedFromAdminReview)
            quotation.SetRmNegotiationRecommendation(command.RequestNegotiation, command.NegotiationNote);

        var pickedQuotation = quotation.Quotations.First(q => q.Id == command.CompanyQuotationId);
        activityLogger.Log(
            quotation.Id,
            command.CompanyQuotationId,
            pickedQuotation.CompanyId,
            QuotationActivityNames.TentativeWinnerPicked,
            remark: startedFromAdminReview ? null : command.NegotiationNote,
            actionByRole: role);

        quotationRepository.Update(quotation);

        // Resume the workflow step the engine is parked on. Admin direct-pick happens on
        // "admin-review-submissions" (decision SelectAsWinner → admin-finalize); the normal RM
        // flow happens on "rm-pick-winner" (decision Pick → admin-finalize).
        outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            ActivityId = startedFromAdminReview ? "admin-review-submissions" : "rm-pick-winner",
            DecisionTaken = startedFromAdminReview ? "SelectAsWinner" : "Pick",
            CompletedBy = command.Actor.Username,
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
