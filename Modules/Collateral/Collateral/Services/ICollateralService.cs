using Shared.Pagination;

namespace Collateral.Services;

public interface ICollateralService
{
    Task CreateDefaultCollateral(
        List<RequestTitleDto> requestTitles,
        CancellationToken cancellationToken = default
    );
    Task<CollateralMaster> CreateCollateral(
        CollateralType collatType,
        CollateralMasterDto collateral,
        CancellationToken cancellationToken = default
    );
    Task DeleteCollateral(long collatId, CancellationToken cancellationToken = default);
    Task<CollateralMaster> GetCollateralById(
        long collatId,
        CancellationToken cancellationToken = default
    );
    Task<PaginatedResult<CollateralMaster>> GetCollateralPaginatedAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default
    );
    Task UpdateCollateral(
        long collatId,
        CollateralMasterDto dto,
        CancellationToken cancellationToken = default
    );
}
