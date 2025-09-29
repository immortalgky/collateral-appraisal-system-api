using Shared.Pagination;

namespace Collateral.Collateral.Shared.Features.GetCollaterals;

public record GetCollateralResult(PaginatedResult<CollateralMasterDto> Result);
