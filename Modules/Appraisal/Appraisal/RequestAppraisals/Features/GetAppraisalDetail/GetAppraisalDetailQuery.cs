using Shared.Pagination;

namespace Appraisal.RequestAppraisals.Features.GetAppraisalDetail;

public record GetAppraisalDetailQuery(PaginationRequest PaginationRequest) : IQuery<GetAppraisalDetailResult>;