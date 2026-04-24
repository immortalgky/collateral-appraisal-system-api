using Appraisal.Application.Features.Quotations.Shared;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.ShortlistQuotation;

public class ShortlistQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser)
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

        quotationRepository.Update(quotation);

        return new ShortlistQuotationResult(quotation.Id, quotation.TotalShortlisted);
    }
}
