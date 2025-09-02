using Collateral.Collateral.Shared.Features.GetCollaterals;
using Shared.Pagination;

namespace Collateral.Services;

public interface ICollateralService
{
    Task CreateDefaultCollateral(
        List<RequestTitleDto> requestTitles,
        CancellationToken cancellationToken = default
    );
    Task<CollateralMaster> CreateCollateral(
        CollateralMasterDto collateral,
        CancellationToken cancellationToken = default
    );
    Task DeleteCollateral(long collatId, CancellationToken cancellationToken = default);
    Task<CollateralMaster> GetCollateralById(
        long collatId,
        CancellationToken cancellationToken = default
    );
    Task<PaginatedResult<CollateralMaster>> GetCollateralPaginatedAsync(
        GetCollateralRequest request,
        CancellationToken cancellationToken = default
    );
    Task UpdateCollateral(
        long collatId,
        CollateralMasterDto dto,
        CancellationToken cancellationToken = default
    );

    Task AddCollateralRequestId(
        long collatId,
        long reqId,
        CancellationToken cancellationToken = default
    );
}
