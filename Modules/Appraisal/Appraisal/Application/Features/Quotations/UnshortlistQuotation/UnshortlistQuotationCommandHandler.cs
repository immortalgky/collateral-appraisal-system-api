using Appraisal.Application.Features.Quotations.Shared;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.UnshortlistQuotation;

public class UnshortlistQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IQuotationActivityLogger activityLogger)
    : ICommandHandler<UnshortlistQuotationCommand, UnshortlistQuotationResult>
{
    public async Task<UnshortlistQuotationResult> Handle(
        UnshortlistQuotationCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        quotation.UnmarkShortlisted(command.CompanyQuotationId, currentUser.UserId!.Value);

        var unshortlistedQuotation = quotation.Quotations.First(q => q.Id == command.CompanyQuotationId);
        var adminRole = currentUser.IsInRole("Admin") ? "Admin" : "IntAdmin";
        activityLogger.Log(
            quotation.Id,
            command.CompanyQuotationId,
            unshortlistedQuotation.CompanyId,
            QuotationActivityNames.QuotationUnshortlisted,
            actionByRole: adminRole);

        quotationRepository.Update(quotation);

        return new UnshortlistQuotationResult(quotation.Id, quotation.TotalShortlisted);
    }
}
