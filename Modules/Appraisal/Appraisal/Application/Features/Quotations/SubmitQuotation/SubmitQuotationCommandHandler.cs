using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Contracts.Services;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.Features.Quotations.SubmitQuotation;

public class SubmitQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox,
    IDateTimeProvider dateTimeProvider,
    IQuotationActivityLogger activityLogger,
    IQuotationTaskOwnershipService taskOwnership)
    : ICommandHandler<SubmitQuotationCommand, SubmitQuotationResult>
{
    public async Task<SubmitQuotationResult> Handle(
        SubmitQuotationCommand command,
        CancellationToken cancellationToken)
    {
        var quotationRequest = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                               ?? throw new NotFoundException($"Quotation request '{command.QuotationRequestId}' not found");

        // Find the invitation for this company
        var invitation = quotationRequest.Invitations
            .FirstOrDefault(i => i.CompanyId == command.CompanyId)
            ?? throw new BadRequestException($"Company '{command.CompanyId}' is not invited to this quotation");

        // Enforce ext-company access (Maker or Checker)
        QuotationAccessPolicy.EnsureCanSubmitQuotation(invitation, currentUser);

        if (quotationRequest.Status != "Sent")
            throw new BadRequestException($"Cannot submit quotation: RFQ is in status '{quotationRequest.Status}'");

        // Hard cap: reject if any item's EstimatedDays exceeds the admin-set MaxAppraisalDays
        var itemsByAppraisalId = quotationRequest.Items
            .GroupBy(i => i.AppraisalId)
            .ToDictionary(g => g.Key, g => g.First());

        var violations = command.Items
            .Where(i =>
            {
                itemsByAppraisalId.TryGetValue(i.AppraisalId, out var rfqItem);
                return rfqItem?.MaxAppraisalDays is int max && i.EstimatedDays > max;
            })
            .Select(i =>
            {
                itemsByAppraisalId.TryGetValue(i.AppraisalId, out var rfqItem);
                return $"{rfqItem!.AppraisalNumber}: {i.EstimatedDays} > {rfqItem.MaxAppraisalDays}";
            })
            .ToList();

        if (violations.Count > 0)
            throw new BadRequestException(
                $"Estimated Mandays exceeds the maximum duration for appraisal(s): {string.Join(", ", violations)}.");

        // Determine which path we are on
        var existingQuotation = quotationRequest.Quotations
            .FirstOrDefault(q => q.CompanyId == command.CompanyId);

        CompanyQuotation companyQuotation;
        string submitRole;

        if (existingQuotation is null)
        {
            // ─── Legacy path: no prior draft — create and submit directly ────────
            companyQuotation = CompanyQuotation.Create(
                quotationRequestId: command.QuotationRequestId,
                invitationId: invitation.Id,
                companyId: command.CompanyId,
                quotationNumber: command.QuotationNumber,
                estimatedDays: command.EstimatedDays,
                submittedAt: dateTimeProvider.ApplicationNow);

            ApplyScalarFields(companyQuotation, command);
            AddItems(companyQuotation, command.Items);

            quotationRequest.AddQuotation(companyQuotation);
            submitRole = "ExtAdmin";
        }
        else if (existingQuotation.Status == "PendingCheckerReview")
        {
            // ─── Checker-final path: Checker finalises a pending draft ───────────
            // Two-person rule: caller must hold the active "checker" task (task ownership)
            // rather than only relying on the ExtAppraisalChecker role claim.
            var isCheckerTaskOwner = await taskOwnership.IsCallerActiveTaskOwnerAsync(
                command.QuotationRequestId, command.CompanyId, expectedStageName: "checker", cancellationToken);
            if (!isCheckerTaskOwner)
                throw new UnauthorizedAccessException(
                    "You do not hold the active Checker task for this quotation. Only the current task owner may make a final submission.");

            companyQuotation = existingQuotation;
            ApplyScalarFields(companyQuotation, command);
            companyQuotation.ClearItems();
            AddItems(companyQuotation, command.Items);
            companyQuotation.MarkSubmitted(dateTimeProvider.ApplicationNow);

            quotationRequest.RecalculateQuotationsReceived();
            submitRole = "ExtAppraisalChecker";
        }
        else if (existingQuotation.Status == "Draft")
        {
            // ─── Two-person rule: Draft must go through submit-to-checker first ──
            // A Checker calling /submit on a Draft bypasses the Maker hand-off.
            // Reject unconditionally so the Maker must explicitly call /submit-to-checker.
            throw new BadRequestException("Draft must be submitted to checker first (Maker action).");
        }
        else
        {
            throw new BadRequestException(
                $"Cannot submit quotation: existing quotation is in status '{existingQuotation.Status}'");
        }

        // Mark invitation as submitted (idempotent)
        invitation.MarkSubmitted();

        activityLogger.Log(
            quotationRequest.Id,
            companyQuotation.Id,
            command.CompanyId,
            QuotationActivityNames.QuotationSubmitted,
            actionByRole: submitRole);

        var autoClosed = quotationRequest.TryAutoCloseAfterAllResponses();

        quotationRepository.Update(quotationRequest);

        if (autoClosed)
        {
            outbox.Publish(new QuotationSubmissionsClosedIntegrationEvent
            {
                QuotationRequestId = quotationRequest.Id,
                RequestId = quotationRequest.RequestId ?? Guid.Empty,
                AdminUserIds = []
            }, correlationId: quotationRequest.Id.ToString());
        }

        // Resume fan-out step in quotation child workflow for this company's submission
        outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
        {
            QuotationRequestId = quotationRequest.Id,
            ActivityId = "ext-collect-submissions",
            DecisionTaken = "Submit",
            CompletedBy = currentUser.Username ?? currentUser.UserId?.ToString() ?? string.Empty,
            CompanyId = command.CompanyId
        }, correlationId: quotationRequest.Id.ToString());

        return new SubmitQuotationResult(
            companyQuotation.Id,
            companyQuotation.QuotationNumber,
            companyQuotation.TotalQuotedPrice,
            companyQuotation.Status);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void ApplyScalarFields(CompanyQuotation quotation, SubmitQuotationCommand command)
    {
        if (command.ValidUntil.HasValue)
            quotation.SetValidUntil(command.ValidUntil.Value);

        quotation.SetProposedDates(command.ProposedStartDate, command.ProposedCompletionDate);
        quotation.SetRemarks(command.Remarks);
        quotation.SetTermsAndConditions(command.TermsAndConditions);
        quotation.SetContactInfo(command.ContactName, command.ContactEmail, command.ContactPhone);
    }

    private static void AddItems(CompanyQuotation quotation, List<SubmitQuotationItemRequest> items)
    {
        foreach (var itemRequest in items)
        {
            // If the caller supplied a full fee breakdown (FeeAmount + Discount + VatPercent),
            // derive the canonical NetAmount and persist the breakdown so it survives the
            // Checker-final submit unchanged. Legacy callers that only send QuotedPrice use
            // that value directly and skip SetFeeBreakdown.
            decimal quotedPrice;
            bool hasFeeBreakdown = itemRequest.FeeAmount.HasValue
                                   && itemRequest.Discount.HasValue
                                   && itemRequest.VatPercent.HasValue;

            if (hasFeeBreakdown)
            {
                var feeAfterDiscount = itemRequest.FeeAmount!.Value
                                       - itemRequest.Discount!.Value
                                       - (itemRequest.NegotiatedDiscount ?? 0m);
                var vatAmount = Math.Round(
                    feeAfterDiscount * itemRequest.VatPercent!.Value / 100m, 2,
                    MidpointRounding.AwayFromZero);
                quotedPrice = feeAfterDiscount + vatAmount;
            }
            else
            {
                quotedPrice = itemRequest.QuotedPrice;
            }

            var item = quotation.AddItem(
                itemRequest.QuotationRequestItemId,
                itemRequest.AppraisalId,
                itemRequest.ItemNumber,
                quotedPrice,
                itemRequest.EstimatedDays);

            if (hasFeeBreakdown)
            {
                item.SetFeeBreakdown(itemRequest.FeeAmount!.Value, itemRequest.Discount!.Value, itemRequest.VatPercent!.Value);

                if (itemRequest.NegotiatedDiscount.HasValue)
                    item.SetNegotiatedDiscount(itemRequest.NegotiatedDiscount.Value);
            }

            item.SetItemNotes(itemRequest.ItemNotes);
        }
    }
}
