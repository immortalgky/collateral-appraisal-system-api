using Appraisal.Application.Features.Quotations.Shared;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.UnshortlistQuotation;

public class UnshortlistQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser)
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

        quotationRepository.Update(quotation);

        return new UnshortlistQuotationResult(quotation.Id, quotation.TotalShortlisted);
    }
}
