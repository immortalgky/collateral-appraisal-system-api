using Appraisal.Application.Features.Quotations.Shared;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.ShortlistQuotation;

public class ShortlistQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IQuotationActivityLogger activityLogger)
    : ICommandHandler<ShortlistQuotationCommand, ShortlistQuotationResult>
{
    public async Task<ShortlistQuotationResult> Handle(
        ShortlistQuotationCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        quotation.MarkShortlisted(command.CompanyQuotationId, currentUser.UserId!.Value);

        var shortlistedQuotation = quotation.Quotations.First(q => q.Id == command.CompanyQuotationId);
        var adminRole = currentUser.IsInRole("Admin") ? "Admin" : "IntAdmin";
        activityLogger.Log(
            quotation.Id,
            command.CompanyQuotationId,
            shortlistedQuotation.CompanyId,
            QuotationActivityNames.QuotationShortlisted,
            actionByRole: adminRole);

        quotationRepository.Update(quotation);

        return new ShortlistQuotationResult(quotation.Id, quotation.TotalShortlisted);
    }
}
