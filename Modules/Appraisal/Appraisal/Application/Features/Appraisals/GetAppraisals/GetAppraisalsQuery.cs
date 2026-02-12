using Shared.CQRS;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

/// <summary>
/// Query to get all Appraisals with pagination and optional filters
/// </summary>
public record GetAppraisalsQuery(
    PaginationRequest PaginationRequest,
    GetAppraisalsFilterRequest? Filter = null
) : IQuery<GetAppraisalsResult>;