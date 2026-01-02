using Shared.Pagination;

namespace Collateral.Collateral.Shared.Features.GetCollaterals;

public record GetCollateralResponse(PaginatedResult<CollateralMasterDto> Result);
