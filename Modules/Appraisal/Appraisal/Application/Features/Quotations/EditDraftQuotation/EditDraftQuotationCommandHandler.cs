using Appraisal.Application.Features.Quotations.Shared;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.EditDraftQuotation;

public class EditDraftQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser)
    : ICommandHandler<EditDraftQuotationCommand, EditDraftQuotationResult>
{
    public async Task<EditDraftQuotationResult> Handle(
        EditDraftQuotationCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var adminUsername = currentUser.Username
            ?? throw new UnauthorizedAccessException("Cannot resolve current user username from token");

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation request '{command.QuotationRequestId}' not found.");

        if (quotation.RequestedBy != adminUsername)
            throw new UnauthorizedAccessException("You can only modify your own Draft quotation.");

        if (quotation.Status != "Draft")
            throw new BadRequestException($"Cannot edit quotation in status '{quotation.Status}'. Only Draft quotations are editable.");

        quotation.UpdateDueDate(command.DueDate);

        var current = quotation.Invitations.Select(i => i.CompanyId).ToHashSet();
        var requested = command.CompanyIds.ToHashSet();

        foreach (var toRemove in current.Except(requested).ToList())
            quotation.RemoveInvitation(toRemove);

        foreach (var toAdd in requested.Except(current).ToList())
            quotation.InviteCompany(toAdd);

        // Per-appraisal MaxAppraisalDays. We only touch items the caller listed; entries
        // whose AppraisalId isn't on the quotation throw via the aggregate guard.
        foreach (var entry in command.Appraisals)
            quotation.SetItemMaxAppraisalDays(entry.AppraisalId, entry.MaxAppraisalDays);

        quotationRepository.Update(quotation);

        return new EditDraftQuotationResult(quotation.Id);
    }
}
