using Shared.Pagination;

namespace Appraisal.Application.Features.Committees.GetCommittees;

public record GetCommitteesResponse(PaginatedResult<CommitteeDto> Result);