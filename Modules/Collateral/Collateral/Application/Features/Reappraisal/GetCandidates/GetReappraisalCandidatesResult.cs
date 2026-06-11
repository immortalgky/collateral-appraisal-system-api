using Shared.Pagination;

namespace Collateral.Application.Features.Reappraisal.GetCandidates;

public record GetReappraisalCandidatesResult(PaginatedResult<ReappraisalCandidateListItem> Items);
