using Appraisal.Application.Features.Quotations.Shared;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.SaveDraftQuotation;

public class SaveDraftQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser)
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
            // Create path: no quotation yet — create a new Draft
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
            // Update path — only allowed if still in Draft
            if (existingQuotation.Status != "Draft")
                throw new BadRequestException(
                    $"Cannot save draft: existing quotation is in status '{existingQuotation.Status}'");

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