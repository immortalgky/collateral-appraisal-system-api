using Shared.Pagination;

namespace Request.Application.Features.Reappraisal.GetCandidates;

public record GetReappraisalCandidatesResult(PaginatedResult<ReappraisalCandidateListItem> Items);
