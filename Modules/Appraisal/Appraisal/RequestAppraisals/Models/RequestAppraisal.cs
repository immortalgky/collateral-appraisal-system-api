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

    private RequestAppraisal() { }

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
        ArgumentNullException.ThrowIfNull(condo);
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
        ArgumentNullException.ThrowIfNull(additional);
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
    public RequestAppraisal Update(RequestAppraisalDto appraisal)
    {
        ArgumentNullException.ThrowIfNull(appraisal);

        if (appraisal.LandAppraisalDetail is not null && LandAppraisalDetail is not null)
            LandAppraisalDetail.Update(appraisal.LandAppraisalDetail);
        
        if (appraisal.BuildingAppraisalDetail is not null && BuildingAppraisalDetail is not null)
            BuildingAppraisalDetail.Update(appraisal.BuildingAppraisalDetail);
        
        if (appraisal.CondoAppraisalDetail is not null && CondoAppraisalDetail is not null)
            CondoAppraisalDetail.Update(appraisal.CondoAppraisalDetail);

        if (appraisal.MachineAppraisalDetail is not null && MachineAppraisalDetail is not null)
            MachineAppraisalDetail.Update(appraisal.MachineAppraisalDetail);

        if (appraisal.MachineAppraisalAdditionalInfo is not null && MachineAppraisalAdditionalInfo is not null)
            MachineAppraisalAdditionalInfo.Update(appraisal.MachineAppraisalAdditionalInfo);

        if (appraisal.VehicleAppraisalDetail is not null && VehicleAppraisalDetail is not null)
            VehicleAppraisalDetail.Update(appraisal.VehicleAppraisalDetail);

        if (appraisal.VesselAppraisalDetail is not null && VesselAppraisalDetail is not null)
            VesselAppraisalDetail.Update(appraisal.VesselAppraisalDetail);

        return this;
    }
}