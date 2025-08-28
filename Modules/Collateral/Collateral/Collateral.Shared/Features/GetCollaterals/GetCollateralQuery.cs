using Shared.Pagination;

namespace Collateral.Collateral.Shared.Features.GetCollaterals;

public record GetCollateralQuery(PaginationRequest PaginationRequest) : IQuery<GetCollateralResult>;
