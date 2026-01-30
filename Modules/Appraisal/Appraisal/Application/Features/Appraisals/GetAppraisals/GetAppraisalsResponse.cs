using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

public record GetAppraisalsResponse(PaginatedResult<AppraisalDto> Result);