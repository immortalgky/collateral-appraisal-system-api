using Shared.Pagination;

namespace Collateral.Collateral.Shared.Features.GetCollaterals;

public record GetCollateralRequest(long? ReqId, int PageNumber = 0, int PageSize = 10)
    : PaginationRequest(PageNumber, PageSize);
