using Appraisal.Application.Features.Appraisals.GetAppraisals;
using Shared.CQRS;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetEligibleAppraisalsForQuotation;

/// <summary>
/// Query to get appraisals that are eligible to be added to a new standalone quotation.
/// Eligible means: no active assignment AND not already attached to a non-terminal quotation.
/// </summary>
public record GetEligibleAppraisalsForQuotationQuery(
    PaginationRequest PaginationRequest,
    GetAppraisalsFilterRequest? Filter = null
) : IQuery<PaginatedResult<AppraisalDto>>;
