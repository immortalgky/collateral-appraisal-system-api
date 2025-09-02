using Shared.Pagination;

namespace Collateral.Collateral.Shared.Features.GetCollaterals;

public class GetCollateralQueryHandler(ICollateralService collateralService)
    : IQueryHandler<GetCollateralQuery, GetCollateralResult>
{
    public async Task<GetCollateralResult> Handle(
        GetCollateralQuery query,
        CancellationToken cancellationToken
    )
    {
        var pagination = await collateralService.GetCollateralPaginatedAsync(
            query.GetCollateralRequest,
            cancellationToken
        );
        var paginationDto = new PaginatedResult<CollateralMasterDto>(
            pagination.Items.Select(item => item.ToDto()),
            pagination.Count,
            pagination.PageNumber,
            pagination.PageSize
        );
        return new GetCollateralResult(paginationDto);
    }
}
