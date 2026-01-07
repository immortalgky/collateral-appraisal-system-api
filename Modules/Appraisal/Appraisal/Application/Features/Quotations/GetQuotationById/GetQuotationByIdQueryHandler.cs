namespace Appraisal.Application.Features.Quotations.GetQuotationById;

public class GetQuotationByIdQueryHandler(IQuotationRepository quotationRepository)
    : IQueryHandler<GetQuotationByIdQuery, GetQuotationByIdResult>
{
    public async Task<GetQuotationByIdResult> Handle(GetQuotationByIdQuery query, CancellationToken cancellationToken)
    {
        var quotation = await quotationRepository.GetByIdAsync(query.Id, cancellationToken)
                        ?? throw new QuotationNotFoundException(query.Id);

        return new GetQuotationByIdResult(
            quotation.Id,
            quotation.QuotationNumber,
            quotation.RequestDate,
            quotation.DueDate,
            quotation.Status,
            quotation.RequestedBy,
            quotation.RequestedByName,
            quotation.RequestDescription,
            quotation.SpecialRequirements,
            quotation.TotalAppraisals,
            quotation.TotalCompaniesInvited,
            quotation.TotalQuotationsReceived,
            quotation.SelectedCompanyId,
            quotation.SelectedQuotationId,
            quotation.SelectedAt,
            quotation.SelectionReason);
    }
}

public class QuotationNotFoundException(Guid id)
    : NotFoundException($"Quotation with ID '{id}' was not found.");