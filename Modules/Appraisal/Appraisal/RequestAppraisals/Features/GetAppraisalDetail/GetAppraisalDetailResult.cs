using Shared.Pagination;

namespace Appraisal.RequestAppraisals.Features.GetAppraisalDetail;

public record GetAppraisalDetailResult(PaginatedResult<RequestAppraisalDto> Appraisal);