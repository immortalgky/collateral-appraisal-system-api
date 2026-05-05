using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Contracts.Services;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.SaveDraftQuotation;

public class SaveDraftQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IQuotationTaskOwnershipService taskOwnership)
    : ICommandHandler<SaveDraftQuotationCommand, SaveDraftQuotationResult>
{
    public async Task<SaveDraftQuotationResult> Handle(
        SaveDraftQuotationCommand command,
        CancellationToken cancellationToken)
    {
        var quotationRequest = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                               ?? throw new NotFoundException(
                                   $"Quotation request '{command.QuotationRequestId}' not found");

        // Find the invitation for this company
        var invitation = quotationRequest.Invitations
                             .FirstOrDefault(i => i.CompanyId == command.CompanyId)
                         ?? throw new BadRequestException(
                             $"Company '{command.CompanyId}' is not invited to this quotation");

        // Enforce ext-company access (Maker or Checker)
        QuotationAccessPolicy.EnsureCanSubmitQuotation(invitation, currentUser);

        if (quotationRequest.Status != "Sent")
            throw new BadRequestException($"Cannot save draft: RFQ is in status '{quotationRequest.Status}'");

        // Find any existing CompanyQuotation for this company
        var existingQuotation = quotationRequest.Quotations
            .FirstOrDefault(q => q.CompanyId == command.CompanyId);

        CompanyQuotation companyQuotation;

        if (existingQuotation is null)
        {
            // Create path: no quotation yet — only the Maker may create a fresh Draft.
            var isMakerTaskOwner = await taskOwnership.IsCallerActiveTaskOwnerAsync(
                command.QuotationRequestId, command.CompanyId, expectedStageName: "maker", cancellationToken);
            if (!isMakerTaskOwner)
                throw new UnauthorizedAccessException(
                    "You do not hold the active Maker task for this quotation. Only the current task owner may save a draft.");

            companyQuotation = CompanyQuotation.CreateDraft(
                command.QuotationRequestId,
                invitation.Id,
                command.CompanyId,
                command.QuotationNumber,
                command.EstimatedDays);

            ApplyScalarFields(companyQuotation, command);
            AddItems(companyQuotation, command.Items);

            // Register on aggregate — does NOT affect TotalQuotationsReceived (Draft is excluded)
            quotationRequest.AddQuotation(companyQuotation);
        }
        else
        {
            // Update path — Maker edits while Draft, Checker edits while PendingCheckerReview.
            // Two-person rule: the caller must hold the active task at the corresponding stage.
            var expectedStage = existingQuotation.Status switch
            {
                "Draft" => "maker",
                "PendingCheckerReview" => "checker",
                _ => throw new BadRequestException(
                    $"Cannot save draft: existing quotation is in status '{existingQuotation.Status}'")
            };

            var isTaskOwner = await taskOwnership.IsCallerActiveTaskOwnerAsync(
                command.QuotationRequestId, command.CompanyId, expectedStage, cancellationToken);
            if (!isTaskOwner)
                throw new UnauthorizedAccessException(
                    $"You do not hold the active {expectedStage} task for this quotation. " +
                    "Only the current task owner may save changes.");

            companyQuotation = existingQuotation;
            ApplyScalarFields(companyQuotation, command);

            // Replace-all items strategy
            companyQuotation.ClearItems();
            AddItems(companyQuotation, command.Items);
        }

        return new SaveDraftQuotationResult(companyQuotation.Id, companyQuotation.Status);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void ApplyScalarFields(CompanyQuotation quotation, SaveDraftQuotationCommand command)
    {
        if (command.ValidUntil.HasValue)
            quotation.SetValidUntil(command.ValidUntil.Value);

        quotation.SetProposedDates(command.ProposedStartDate, command.ProposedCompletionDate);
        quotation.SetRemarks(command.Remarks);
        quotation.SetTermsAndConditions(command.TermsAndConditions);
        quotation.SetContactInfo(command.ContactName, command.ContactEmail, command.ContactPhone);
    }

    private static void AddItems(CompanyQuotation quotation, List<SaveDraftQuotationItem> items)
    {
        foreach (var itemRequest in items)
        {
            // Derive NetAmount so that the legacy QuotedPrice column stays in sync.
            // Formula: (FeeAmount - Discount - NegotiatedDiscount) * (1 + VatPercent / 100)
            var feeAfterDiscount =
                itemRequest.FeeAmount - itemRequest.Discount - (itemRequest.NegotiatedDiscount ?? 0m);
            var vatAmount = Math.Round(feeAfterDiscount * itemRequest.VatPercent / 100m, 2,
                MidpointRounding.AwayFromZero);
            var netAmount = feeAfterDiscount + vatAmount;

            var item = quotation.AddItem(
                itemRequest.QuotationRequestItemId,
                itemRequest.AppraisalId,
                itemRequest.ItemNumber,
                netAmount,
                itemRequest.EstimatedDays);

            item.SetFeeBreakdown(itemRequest.FeeAmount, itemRequest.Discount, itemRequest.VatPercent);

            if (itemRequest.NegotiatedDiscount.HasValue)
                item.SetNegotiatedDiscount(itemRequest.NegotiatedDiscount.Value);

            item.SetItemNotes(itemRequest.ItemNotes);
        }
    }
}