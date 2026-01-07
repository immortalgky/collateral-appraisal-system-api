namespace Appraisal.Application.Features.Quotations.GetQuotations;

public class GetQuotationsQueryHandler(IQuotationRepository quotationRepository)
    : IQueryHandler<GetQuotationsQuery, GetQuotationsResult>
{
    public async Task<GetQuotationsResult> Handle(GetQuotationsQuery query, CancellationToken cancellationToken)
    {
        var quotations = await quotationRepository.GetAllAsync(cancellationToken);
        var quotationList = quotations.ToList();

        var items = quotationList
            .Skip((query.PaginationRequest.PageNumber - 1) * query.PaginationRequest.PageSize)
            .Take(query.PaginationRequest.PageSize)
            .Select(q => new QuotationDto(
                q.Id,
                q.QuotationNumber,
                q.RequestDate,
                q.DueDate,
                q.Status,
                q.RequestedByName,
                q.TotalAppraisals,
                q.TotalCompaniesInvited,
                q.TotalQuotationsReceived))
            .ToList();

        var paginatedResult = new PaginatedResult<QuotationDto>(
            items,
            quotationList.Count,
            query.PaginationRequest.PageNumber,
            query.PaginationRequest.PageSize);

        return new GetQuotationsResult(paginatedResult);
    }
}

public record QuotationDto(
    Guid Id,
    string QuotationNumber,
    DateTime RequestDate,
    DateTime DueDate,
    string Status,
    string RequestedByName,
    int TotalAppraisals,
    int TotalCompaniesInvited,
    int TotalQuotationsReceived);