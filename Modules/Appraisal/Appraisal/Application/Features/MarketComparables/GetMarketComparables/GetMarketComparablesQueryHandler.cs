using Appraisal.Domain.MarketComparables;
using Shared.CQRS;
using Shared.Pagination;

namespace Appraisal.Application.Features.MarketComparables.GetMarketComparables;

/// <summary>
/// Handler for getting all Market Comparables with pagination
/// </summary>
public class GetMarketComparablesQueryHandler(
    IMarketComparableRepository marketComparableRepository
) : IQueryHandler<GetMarketComparablesQuery, GetMarketComparablesResult>
{
    public async Task<GetMarketComparablesResult> Handle(
        GetMarketComparablesQuery query,
        CancellationToken cancellationToken)
    {
        var comparables = await marketComparableRepository.GetAllAsync(cancellationToken);

        var comparableList = comparables.ToList();

        // Apply pagination
        var totalCount = comparableList.Count;
        var pageNumber = query.PaginationRequest.PageNumber;
        var pageSize = query.PaginationRequest.PageSize;

        var items = comparableList
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new MarketComparableDto
            {
                Id = c.Id,
                ComparableNumber = c.ComparableNumber,
                PropertyType = c.PropertyType,
                SurveyName = c.SurveyName,
                InfoDateTime = c.InfoDateTime,
                SourceInfo = c.SourceInfo,
                Notes = c.Notes,
                CreatedOn = c.CreatedOn
            })
            .ToList();

        var paginatedResult = new PaginatedResult<MarketComparableDto>(
            items,
            totalCount,
            pageNumber,
            pageSize);

        return new GetMarketComparablesResult(paginatedResult);
    }
}