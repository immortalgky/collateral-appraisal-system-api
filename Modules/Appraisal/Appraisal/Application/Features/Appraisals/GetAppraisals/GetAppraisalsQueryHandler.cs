using Appraisal.Domain.Appraisals;
using Shared.CQRS;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

/// <summary>
/// Handler for getting all Appraisals with pagination
/// </summary>
public class GetAppraisalsQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetAppraisalsQuery, GetAppraisalsResult>
{
    public async Task<GetAppraisalsResult> Handle(
        GetAppraisalsQuery query,
        CancellationToken cancellationToken)
    {
        var appraisals = await appraisalRepository.GetAllAsync(cancellationToken);

        var appraisalList = appraisals.ToList();

        // Apply pagination
        var totalCount = appraisalList.Count;
        var pageNumber = query.PaginationRequest.PageNumber;
        var pageSize = query.PaginationRequest.PageSize;

        var items = appraisalList
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AppraisalDto
            {
                Id = a.Id,
                AppraisalNumber = a.AppraisalNumber,
                RequestId = a.RequestId,
                Status = a.Status.ToString(),
                AppraisalType = a.AppraisalType,
                Priority = a.Priority,
                SLADays = a.SLADays,
                SLADueDate = a.SLADueDate,
                SLAStatus = a.SLAStatus,
                PropertyCount = a.Properties.Count,
                CreatedOn = a.CreatedOn
            })
            .ToList();

        var paginatedResult = new PaginatedResult<AppraisalDto>(
            items,
            totalCount,
            pageNumber,
            pageSize);

        return new GetAppraisalsResult(paginatedResult);
    }
}