using Shared.CQRS;
using Shared.Pagination;

namespace Appraisal.Application.Features.Committees.GetCommittees;

/// <summary>
/// Query to get all Committees with pagination
/// </summary>
public record GetCommitteesQuery(PaginationRequest PaginationRequest) : IQuery<GetCommitteesResult>;