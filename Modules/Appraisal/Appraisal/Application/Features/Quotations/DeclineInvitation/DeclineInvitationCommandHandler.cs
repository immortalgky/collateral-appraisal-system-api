using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Contracts.Services;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.Features.Quotations.DeclineInvitation;

public class DeclineInvitationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox,
    IDateTimeProvider dateTimeProvider,
    IQuotationActivityLogger activityLogger,
    IQuotationTaskOwnershipService taskOwnership)
    : ICommandHandler<DeclineInvitationCommand, DeclineInvitationResult>
{
    public async Task<DeclineInvitationResult> Handle(
        DeclineInvitationCommand command,
        CancellationToken cancellationToken)
    {
        // Ensure caller is an ExtAdmin from the correct company
        QuotationAccessPolicy.EnsureExtCompanyUser(currentUser, command.CompanyId);

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation request '{command.QuotationRequestId}' not found.");

        // M3: allow Decline while quotation is Sent (not yet responded) or UnderAdminReview
        // (admin is still reviewing — a company withdrawing after submission is legitimate).
        // Reject when quotation is PendingRmSelection or later (bids have been shortlisted/sent to RM).
        var allowedStatuses = new[] { "Sent", "UnderAdminReview" };
        if (!allowedStatuses.Contains(quotation.Status))
            throw new BadRequestException(
                $"Cannot decline: quotation is in status '{quotation.Status}'. " +
                "Declination is only allowed while Status is Sent or UnderAdminReview.");

        // Two-person rule: the caller must hold whichever stage task is currently active for their
        // company (either "maker" or "checker"). Pass null to accept any active stage.
        var isActiveTaskOwner = await taskOwnership.IsCallerActiveTaskOwnerAsync(
            command.QuotationRequestId, command.CompanyId, expectedStageName: null, cancellationToken);
        if (!isActiveTaskOwner)
            throw new UnauthorizedAccessException(
                "You do not hold the active task for this quotation. Only the current task owner may decline the invitation.");

        var invitation = quotation.Invitations
            .FirstOrDefault(i => i.CompanyId == command.CompanyId)
            ?? throw new BadRequestException($"Company '{command.CompanyId}' is not invited to this quotation.");

        var declinedBy = currentUser.Username ?? currentUser.UserId?.ToString() ?? command.CompanyId.ToString();

        // Check if a CompanyQuotation already exists for this company (e.g., they submitted then changed mind)
        var existing = quotation.Quotations.FirstOrDefault(q => q.CompanyId == command.CompanyId);
        CompanyQuotation declinedQuotation;

        if (existing is not null)
        {
            // Decline the existing submitted quotation
            existing.Decline(command.Reason, declinedBy, dateTimeProvider.ApplicationNow);
            declinedQuotation = existing;
        }
        else
        {
            // Create a new Declined record (never submitted)
            var newDeclined = CompanyQuotation.CreateDeclined(
                quotationRequestId: command.QuotationRequestId,
                invitationId: invitation.Id,
                companyId: command.CompanyId,
                quotationNumber: $"DECLINED-{command.CompanyId:N}",
                reason: command.Reason,
                declinedBy: declinedBy,
                declinedAt: dateTimeProvider.ApplicationNow);

            quotation.AddQuotation(newDeclined);
            declinedQuotation = newDeclined;
        }

        // Mark the invitation as Declined
        if (invitation.Status == "Pending")
            invitation.Decline();

        activityLogger.Log(
            quotation.Id,
            declinedQuotation.Id,
            command.CompanyId,
            QuotationActivityNames.InvitationDeclined,
            command.Reason,
            actionByRole: "ExtAdmin");

        var autoClosed = quotation.TryAutoCloseAfterAllResponses();

        quotationRepository.Update(quotation);

        if (autoClosed)
        {
            outbox.Publish(new QuotationSubmissionsClosedIntegrationEvent
            {
                QuotationRequestId = quotation.Id,
                RequestId = quotation.RequestId ?? Guid.Empty,
                AdminUserIds = []
            }, correlationId: quotation.Id.ToString());
        }

        outbox.Publish(new QuotationInvitationDeclinedIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            CompanyId = command.CompanyId,
            Reason = command.Reason
        }, correlationId: quotation.Id.ToString());

        // v4: resume fan-out step in quotation child workflow for this company's decline
        outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            ActivityId = "ext-collect-submissions",
            DecisionTaken = "Decline",
            CompletedBy = declinedBy,
            CompanyId = command.CompanyId
        }, correlationId: quotation.Id.ToString());

        return new DeclineInvitationResult(
            quotation.Id,
            command.CompanyId,
            "Declined",
            false);
    }
}
