using System.Linq.Expressions;
using Collateral.Collateral.Shared.Features.GetCollaterals;
using Shared.Pagination;

namespace Collateral.Services;

public class CollateralService(ICollateralRepository collateralRepository) : ICollateralService
{
    public async Task CreateDefaultCollateral(
        List<RequestTitleDto> requestTitles,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var requestTitleDto in requestTitles)
        {
            var collateralMaster = CollateralMaster.Create(
                CollateralType.Land,
                null,
                requestTitleDto.RequestId
            );
            await collateralRepository.AddAsync(collateralMaster, cancellationToken);
            var collateralLand = CollateralLand.FromRequestTitleDto(
                collateralMaster.Id,
                requestTitleDto
            );
            collateralMaster.SetCollateralLand(collateralLand);
        }

        await collateralRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<CollateralMaster> CreateCollateral(
        CollateralMasterDto collateral,
        CancellationToken cancellationToken = default
    )
    {
        _ = Enum.TryParse(collateral.CollatType, out CollateralType collatType);
        if (collateral.ReqIds.Count < 1)
        {
            throw new DomainException(
                "One request ID should be provided when creating collateral."
            );
        }
        var collateralMaster = CollateralMaster.Create(collatType, null, collateral.ReqIds[0]);
        await collateralRepository.AddAsync(collateralMaster, cancellationToken);

        switch (collatType)
        {
            case CollateralType.Land:
                CreateCollateralLand(collateralMaster, collateral);
                break;
            case CollateralType.Building:
                CreateCollateralBuilding(collateralMaster, collateral);
                break;
            case CollateralType.LandAndBuilding:
                CreateCollateralLand(collateralMaster, collateral);
                CreateCollateralBuilding(collateralMaster, collateral);
                break;
            case CollateralType.Condo:
                var collateralCondo = collateral.CollateralCondo!.ToDomain(collateralMaster.Id);
                collateralMaster.SetCollateralCondo(collateralCondo);
                break;
            case CollateralType.Machine:
                var collateralMachine = collateral.CollateralMachine!.ToDomain(collateralMaster.Id);
                collateralMaster.SetCollateralMachine(collateralMachine);
                break;
            case CollateralType.Vehicle:
                var collateralVehicle = collateral.CollateralVehicle!.ToDomain(collateralMaster.Id);
                collateralMaster.SetCollateralVehicle(collateralVehicle);
                break;
            case CollateralType.Vessel:
                var collateralVessel = collateral.CollateralVessel!.ToDomain(collateralMaster.Id);
                collateralMaster.SetCollateralVessel(collateralVessel);
                break;
        }
        await collateralRepository.SaveChangesAsync(cancellationToken);

        return collateralMaster;
    }

    private static void CreateCollateralLand(
        CollateralMaster collateralMaster,
        CollateralMasterDto dto
    )
    {
        var collateralLand = dto.CollateralLand!.ToDomain(collateralMaster.Id);

        var landTitles = dto.LandTitles!.Select(dto => dto.ToDomain(collateralMaster.Id)).ToList();
        collateralMaster.SetCollateralLand(collateralLand);
        collateralMaster.SetLandTitles(landTitles);
    }

    private static void CreateCollateralBuilding(
        CollateralMaster collateralMaster,
        CollateralMasterDto dto
    )
    {
        var collateralBuilding = dto.CollateralBuilding!.ToDomain(collateralMaster.Id);
        collateralMaster.SetCollateralBuilding(collateralBuilding);
    }

    public async Task DeleteCollateral(long collatId, CancellationToken cancellationToken = default)
    {
        var collateral = await GetCollateralById(collatId, cancellationToken);
        await collateralRepository.DeleteAsync(collateral, cancellationToken);
        await collateralRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<CollateralMaster> GetCollateralById(
        long collatId,
        CancellationToken cancellationToken = default
    )
    {
        return await collateralRepository.GetByIdAsync(collatId, cancellationToken)
            ?? throw new NotFoundException("Cannot find collateral with this ID.");
    }

    public async Task<PaginatedResult<CollateralMaster>> GetCollateralPaginatedAsync(
        GetCollateralRequest request,
        CancellationToken cancellationToken = default
    )
    {
        Expression<Func<CollateralMaster, bool>> predicate = p => true;
        if (request.ReqId is not null)
        {
            predicate = p => p.RequestCollaterals.Any(r => r.ReqId == request.ReqId);
        }
        return await collateralRepository.GetPaginatedAsync(request, predicate, cancellationToken);
    }

    public async Task UpdateCollateral(
        long collatId,
        CollateralMasterDto dto,
        CancellationToken cancellationToken = default
    )
    {
        var collateral =
            await collateralRepository.GetByIdTrackedAsync(collatId, cancellationToken)
            ?? throw new NotFoundException("Cannot get collateral with this id");
        var inputCollateral = dto.ToDomain();

        collateral.Update(inputCollateral);

        await collateralRepository.SaveChangesAsync(cancellationToken);
    }
}
