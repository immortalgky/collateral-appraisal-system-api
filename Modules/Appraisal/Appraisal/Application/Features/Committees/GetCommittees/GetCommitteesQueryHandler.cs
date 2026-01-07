using Appraisal.Domain.Committees;
using Shared.CQRS;
using Shared.Pagination;

namespace Appraisal.Application.Features.Committees.GetCommittees;

/// <summary>
/// Handler for getting all Committees with pagination
/// </summary>
public class GetCommitteesQueryHandler(
    ICommitteeRepository committeeRepository
) : IQueryHandler<GetCommitteesQuery, GetCommitteesResult>
{
    public async Task<GetCommitteesResult> Handle(
        GetCommitteesQuery query,
        CancellationToken cancellationToken)
    {
        var committees = await committeeRepository.GetAllAsync(cancellationToken);

        var committeeList = committees.ToList();

        // Apply pagination
        var totalCount = committeeList.Count;
        var pageNumber = query.PaginationRequest.PageNumber;
        var pageSize = query.PaginationRequest.PageSize;

        var items = committeeList
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CommitteeDto
            {
                Id = c.Id,
                CommitteeName = c.CommitteeName,
                CommitteeCode = c.CommitteeCode,
                Description = c.Description,
                IsActive = c.IsActive,
                QuorumType = c.QuorumType,
                QuorumValue = c.QuorumValue,
                MajorityType = c.MajorityType,
                MemberCount = c.Members.Count(m => m.IsActive),
                ConditionCount = c.Conditions.Count,
                CreatedOn = c.CreatedOn
            })
            .ToList();

        var paginatedResult = new PaginatedResult<CommitteeDto>(
            items,
            totalCount,
            pageNumber,
            pageSize);

        return new GetCommitteesResult(paginatedResult);
    }
}