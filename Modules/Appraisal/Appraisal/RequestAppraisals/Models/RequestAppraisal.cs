namespace Appraisal.RequestAppraisals.Models;

public class RequestAppraisal : Aggregate<long>
{
    public long RequestId { get; private set; } = default!;
    public long CollateralId { get; private set; } = default!;

    public LandAppraisalDetail? LandAppraisalDetail { get; private set; } = default!;
    public BuildingAppraisalDetail? BuildingAppraisalDetail { get; private set; } = default!;
    public CondoAppraisalDetail? CondoAppraisalDetail { get; private set; } = default!;

    public MachineAppraisalDetail? MachineAppraisalDetail { get; private set; } = default!;
    public MachineAppraisalAdditionalInfo? MachineAppraisalAdditionalInfo { get; private set; } = default!;
    public VehicleAppraisalDetail? VehicleAppraisalDetail { get; private set; } = default!;
    public VesselAppraisalDetail? VesselAppraisalDetail { get; private set; } = default!;

    private RequestAppraisal(){ }

    private RequestAppraisal(
        long requestId,
        long collateralId
    )
    {
        RequestId = requestId;
        CollateralId = collateralId;
    }

    public static RequestAppraisal Create(
        long requestId,
        long collateralId
    )
    {
        return new RequestAppraisal(
            requestId,
            collateralId
        );
    }

    public RequestAppraisal WithLand(LandAppraisalDetail land)
    {
        ArgumentNullException.ThrowIfNull(land);
        LandAppraisalDetail = land;
        return this;
    }

    public RequestAppraisal WithBuilding(BuildingAppraisalDetail building)
    {
        ArgumentNullException.ThrowIfNull(building);
        BuildingAppraisalDetail = building;
        return this;
    }

    public RequestAppraisal WithCondo(CondoAppraisalDetail condo)
    {
        CondoAppraisalDetail = condo;
        return this;
    }

    public RequestAppraisal WithMachine(MachineAppraisalDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        MachineAppraisalDetail = detail;
        return this;
    }

    public RequestAppraisal WithMachineInfo(MachineAppraisalAdditionalInfo additional)
    {
        MachineAppraisalAdditionalInfo = additional;
        return this;
    }

    public RequestAppraisal WithVehicle(VehicleAppraisalDetail vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        VehicleAppraisalDetail = vehicle;
        return this;
    }

    public RequestAppraisal WithVessel(VesselAppraisalDetail vessel)
    {
        ArgumentNullException.ThrowIfNull(vessel);
        VesselAppraisalDetail = vessel;
        return this;
    }
    public RequestAppraisal Update(RequestAppraisal appraisal)
    {
        ArgumentNullException.ThrowIfNull(appraisal);

        LandAppraisalDetail = UpdateDetail(LandAppraisalDetail, appraisal.LandAppraisalDetail);
        BuildingAppraisalDetail = UpdateDetail(BuildingAppraisalDetail, appraisal.BuildingAppraisalDetail);
        CondoAppraisalDetail = UpdateDetail(CondoAppraisalDetail, appraisal.CondoAppraisalDetail);
        MachineAppraisalDetail = UpdateDetail(MachineAppraisalDetail, appraisal.MachineAppraisalDetail);
        MachineAppraisalAdditionalInfo = UpdateDetail(MachineAppraisalAdditionalInfo, appraisal.MachineAppraisalAdditionalInfo);
        VehicleAppraisalDetail = UpdateDetail(VehicleAppraisalDetail, appraisal.VehicleAppraisalDetail);
        VesselAppraisalDetail = UpdateDetail(VesselAppraisalDetail, appraisal.VesselAppraisalDetail);

        return this;
    }

    private static T? UpdateDetail<T>(T? current, T? updated) where T : class
    {
        if (current is not null && updated is not null && !updated.Equals(current))
        {
            var updateMethod = current.GetType().GetMethod("Update");
            if (updateMethod != null)
            {
                updateMethod.Invoke(current, [updated]);
                return current;
            }
        }
        else if (current is null && updated is not null)
        {
            return updated;
        }
        return current;
    }
}