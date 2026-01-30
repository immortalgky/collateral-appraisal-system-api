using Shared.CQRS;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

/// <summary>
/// Query to get all Appraisals with pagination
/// </summary>
public record GetAppraisalsQuery(PaginationRequest PaginationRequest) : IQuery<GetAppraisalsResult>;