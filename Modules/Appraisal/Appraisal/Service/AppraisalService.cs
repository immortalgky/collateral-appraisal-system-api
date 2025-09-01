using Appraisal.Data.Repository;
using Appraisal.Extensions;
using Mapster;
using Appraisal.Exceptions;
using Appraisal.RequestAppraisals.Features.GetAppraisalDetail;
using Shared.Pagination;

namespace Appraisal.Service;

public class AppraisalService(IAppraisalRepository appraisalRepository) : IAppraisalService
{
    public RequestAppraisal CreateRequestAppraisalDetail(RequestAppraisalDto appraisal, long reqId, long collatId)
    {
        var result = RequestAppraisal.Create(reqId, collatId);

        var typeActions = new Dictionary<string, Action<RequestAppraisalDto, RequestAppraisal>>
        {
            ["Land"] = AttachLand,
            ["Building"] = AttachBuilding,
            ["Condo"] = AttachCondo,
            ["LandAndBuilding"] = AttachLandAndBuilding,
            ["Machine"] = AttachMachine,
            ["MachineAdditional"] = AttachMachineAdditional,
            ["Vehicle"] = AttachVehicle,
            ["Vessel"] = AttachVessel
        };

        if (typeActions.TryGetValue(appraisal.Type, out var action))
            action(appraisal, result);
        else
            throw new RequestAppraisalDetailTypeNotFoundException(appraisal.Type);

        return result;
    }

    public TypeAdapterConfig CreateRequestAppraisalDetailConfig()
    {
        var config = new TypeAdapterConfig();
        config.ForType<RequestAppraisal, RequestAppraisalDto>()
              .Map(dest => dest.ApprId, src => src.Id);

        return config;
    }

    public async Task AddRequestAppraisalDetailAsync(RequestAppraisal appraisal, CancellationToken cancellationToken = default!)
    {
        await appraisalRepository.AddAsync(appraisal, cancellationToken);

        await appraisalRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRequestAppraisalDetailAsync(RequestAppraisal appraisal, CancellationToken cancellationToken = default!)
    {
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        await appraisalRepository.SaveChangesAsync(cancellationToken);
    }

    public RequestAppraisal UpdateRequestAppraisalDetail(RequestAppraisalDto appraisalDto)
    {
        var appraisalDetail = CreateRequestAppraisalDetail(appraisalDto, 0, 0);

        var newAppraisalDetail = appraisalDetail.Update(appraisalDetail);

        return newAppraisalDetail;
    }

    public async Task<PaginatedResult<RequestAppraisal>> GetRequestAppraisalDetailAsync(PaginationRequest pagination, CancellationToken cancellationToken = default!)
    {
        var appraisalDetails = await appraisalRepository.GetPaginatedAsync(pagination, cancellationToken);

        return appraisalDetails;
    }

    public async Task<RequestAppraisal> GetRequestAppraisalDetailByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new RequestAppraisalDetailNotFoundException(id);

        return appraisal;
    }


    public async Task DeleteRequestAppraisalDetailAsync(long id, CancellationToken cancellationToken = default!)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new RequestAppraisalDetailNotFoundException(id);

        await appraisalRepository.DeleteAsync(id, cancellationToken);
    }
    public async Task<List<RequestAppraisal>> GetRequestAppraisalDetailByCollateralIdAsync(long collatId, CancellationToken cancellationToken = default)
    {
        var appraisals = await appraisalRepository.GetByCollateralIdAsync(collatId, cancellationToken);

        if (appraisals is null || appraisals.Count == 0)
            throw new RequestAppraisalDetailNotFoundException(collatId);

        return appraisals;
    }

    private static void AttachLand(RequestAppraisalDto appraisal, RequestAppraisal result)
    {
        if (appraisal.LandAppraisalDetail is null) throw new RequestAppraisalDetailIsNulLException("Land");
        var land = appraisal.LandAppraisalDetail;
        result.WithLand(land.ToAggregate());
    }

    private static void AttachBuilding(RequestAppraisalDto appraisal, RequestAppraisal result)
    {
        if (appraisal.BuildingAppraisalDetail is null) throw new RequestAppraisalDetailIsNulLException("Land");
        var building = appraisal.BuildingAppraisalDetail;
        result.WithBuilding(building.ToAggregate());
    }

    private static void AttachCondo(RequestAppraisalDto appraisal, RequestAppraisal result)
    {
        if (appraisal.CondoAppraisalDetail is null) throw new RequestAppraisalDetailIsNulLException("Condo");
        var condo = appraisal.CondoAppraisalDetail;
        result.WithCondo(condo.ToAggregate());
    }

    private static void AttachLandAndBuilding(RequestAppraisalDto appraisal, RequestAppraisal result)
    {
        if (appraisal.LandAppraisalDetail is null || appraisal.BuildingAppraisalDetail is null)
            throw new RequestAppraisalDetailIsNulLException("LandAndHouse");

        var land = appraisal.LandAppraisalDetail;
        var building = appraisal.BuildingAppraisalDetail;
        result.WithLand(land.ToAggregate());
        result.WithBuilding(building.ToAggregate());
    }

    private static void AttachMachine(RequestAppraisalDto appraisal, RequestAppraisal result)
    {
        if (appraisal.MachineAppraisalDetail is null) throw new RequestAppraisalDetailIsNulLException("Machine");
        var machine = appraisal.MachineAppraisalDetail;
        result.WithMachine(machine.ToAggregate());
    }

    private static void AttachMachineAdditional(RequestAppraisalDto appraisal, RequestAppraisal result)
    {
        if (appraisal.MachineAppraisalAdditionalInfo is null) throw new RequestAppraisalDetailIsNulLException("MachineInfo");
        var machineInfo = appraisal.MachineAppraisalAdditionalInfo;
        result.WithMachineInfo(machineInfo.ToAggregate());
    }

    private static void AttachVehicle(RequestAppraisalDto appraisal, RequestAppraisal result)
    {
        if (appraisal.VehicleAppraisalDetail is null) throw new RequestAppraisalDetailIsNulLException("Vehicle");
        var vehicle = appraisal.VehicleAppraisalDetail;
        result.WithVehicle(vehicle.ToAggregate());
    }

    private static void AttachVessel(RequestAppraisalDto appraisal, RequestAppraisal result)
    {
        if (appraisal.VesselAppraisalDetail is null) throw new RequestAppraisalDetailIsNulLException("Vessel");
        var vessel = appraisal.VesselAppraisalDetail;
        result.WithVessel(vessel.ToAggregate());
    }
}