using Appraisal.Extensions;
using Appraisal.AppraisalProperties.ValueObjects;
using Appraisal.Appraisal.Shared.ValueObjects;

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
    public RequestAppraisal Update(RequestAppraisalDto appraisal)
    {
        ArgumentNullException.ThrowIfNull(appraisal);

        if (appraisal.LandAppraisalDetail is not null)
        {
            if (LandAppraisalDetail is null)
            {
                var d = appraisal.LandAppraisalDetail;
                LandAppraisalDetail = LandAppraisalDetail.Create(
                    d.ApprId,
                    d.PropertyName ?? string.Empty,
                    d.CheckOwner ?? string.Empty,
                    d.Owner ?? string.Empty,
                    d.ObligationDetail.ToEntity(),
                    d.LandLocationDetail.ToEntity(),
                    d.LandFillDetail.ToEntity(),
                    d.LandAccessibilityDetail.ToEntity(),
                    d.AnticipationOfProp,
                    d.LandLimitation.ToEntity(),
                    d.Eviction,
                    d.Allocation,
                    d.ConsecutiveArea.ToEntity(),
                    d.LandMiscellaneousDetail.ToEntity());
            }
            else
            {
                LandAppraisalDetail.Update(appraisal.LandAppraisalDetail);
            }
        }

        if (appraisal.BuildingAppraisalDetail is not null)
        {
            if (BuildingAppraisalDetail is null)
            {
                var d = appraisal.BuildingAppraisalDetail;
                BuildingAppraisalDetail = BuildingAppraisalDetail.Create(
                    d.ApprId,
                    d.BuildingInformation.ToEntity(),
                    d.BuildingTypeDetail.ToEntity(),
                    d.DecorationDetail.ToEntity(),
                    d.Encroachment.ToEntity(),
                    d.BuildingConstructionInformation.ToEntity(),
                    d.BuildingMaterial,
                    d.BuildingStyle,
                    d.ResidentialStatus.ToEntity(),
                    d.BuildingStructureDetail.ToEntity(),
                    d.UtilizationDetail.ToEntity(),
                    d.Remark);
                // surfaces & depreciation details appended via dedicated Update(dto)
                BuildingAppraisalDetail.Update(d);
            }
            else
            {
                BuildingAppraisalDetail.Update(appraisal.BuildingAppraisalDetail);
            }
        }

        if (appraisal.CondoAppraisalDetail is not null)
        {
            if (CondoAppraisalDetail is null)
            {
                var d = appraisal.CondoAppraisalDetail;
                CondoAppraisalDetail = CondoAppraisalDetail.Create(
                    d.ApprId,
                    d.ObligationDetail.ToEntity(),
                    d.DocValidate,
                    d.CondominiumLocation.ToEntity(),
                    d.CondoAttribute.ToEntity(),
                    d.Expropriation.ToEntity(),
                    d.CondominiumFacility.ToEntity(),
                    d.CondoPrice.ToEntity(),
                    d.ForestBoundary.ToEntity(),
                    d.Remark);
                CondoAppraisalDetail.Update(d);
            }
            else
            {
                CondoAppraisalDetail.Update(appraisal.CondoAppraisalDetail);
            }
        }

        if (appraisal.MachineAppraisalDetail is not null)
        {
            if (MachineAppraisalDetail is null)
            {
                var d = appraisal.MachineAppraisalDetail;
                MachineAppraisalDetail = MachineAppraisalDetail.Create(
                    d.ApprId,
                    AppraisalDetail.Create(
                        d.MachineAppraisalDetail.CanUse,
                        d.MachineAppraisalDetail.Location,
                        d.MachineAppraisalDetail.ConditionUse,
                        d.MachineAppraisalDetail.UsePurpose,
                        d.MachineAppraisalDetail.Part,
                        d.MachineAppraisalDetail.Remark,
                        d.MachineAppraisalDetail.Other,
                        d.MachineAppraisalDetail.AppraiserOpinion));
            }
            else
            {
                MachineAppraisalDetail.Update(appraisal.MachineAppraisalDetail);
            }
        }

        if (appraisal.MachineAppraisalAdditionalInfo is not null)
        {
            if (MachineAppraisalAdditionalInfo is null)
            {
                var d = appraisal.MachineAppraisalAdditionalInfo;
                var purpose = PurposeAndLocationMachine.Create(
                    d.Assignment ?? string.Empty,
                    d.ApprCollatPurpose ?? string.Empty,
                    d.ApprDate ?? string.Empty,
                    d.ApprCollatType ?? string.Empty);
                var machineDetail = MachineDetail.Create(
                    GeneralMachinery.Crate(d.Industrial, d.SurveyNo, d.ApprNo),
                    AtSurveyDate.Create(
                        d.Installed ?? 0,
                        d.ApprScrap ?? string.Empty,
                        d.NoOfAppraise ?? 0,
                        d.NotInstalled ?? 0,
                        d.Maintenance ?? string.Empty,
                        d.Exterior ?? string.Empty,
                        d.Performance ?? string.Empty,
                        d.MarketDemand ?? false,
                        d.MarketDemandRemark ?? string.Empty),
                    RightsAndConditionsOfLegalRestrictions.Crate(
                        d.Proprietor ?? string.Empty,
                        d.Owner ?? string.Empty,
                        d.MachineLocation ?? string.Empty,
                        d.Obligation ?? string.Empty,
                        d.Other ?? string.Empty));
                MachineAppraisalAdditionalInfo = MachineAppraisalAdditionalInfo.Create(d.ApprId, purpose, machineDetail);
            }
            else
            {
                MachineAppraisalAdditionalInfo.Update(appraisal.MachineAppraisalAdditionalInfo);
            }
        }

        if (appraisal.VehicleAppraisalDetail is not null)
        {
            if (VehicleAppraisalDetail is null)
            {
                var d = appraisal.VehicleAppraisalDetail;
                VehicleAppraisalDetail = VehicleAppraisalDetail.Create(
                    d.ApprId,
                    AppraisalDetail.Create(
                        d.AppraisalDetail.CanUse,
                        d.AppraisalDetail.Location,
                        d.AppraisalDetail.ConditionUse,
                        d.AppraisalDetail.UsePurpose,
                        d.AppraisalDetail.Part,
                        d.AppraisalDetail.Remark,
                        d.AppraisalDetail.Other,
                        d.AppraisalDetail.AppraiserOpinion));
            }
            else
            {
                VehicleAppraisalDetail.Update(appraisal.VehicleAppraisalDetail);
            }
        }

        if (appraisal.VesselAppraisalDetail is not null)
        {
            if (VesselAppraisalDetail is null)
            {
                var d = appraisal.VesselAppraisalDetail;
                VesselAppraisalDetail = VesselAppraisalDetail.Create(
                    d.ApprId,
                    AppraisalDetail.Create(
                        d.AppraisalDetail.CanUse,
                        d.AppraisalDetail.Location,
                        d.AppraisalDetail.ConditionUse,
                        d.AppraisalDetail.UsePurpose,
                        d.AppraisalDetail.Part,
                        d.AppraisalDetail.Remark,
                        d.AppraisalDetail.Other,
                        d.AppraisalDetail.AppraiserOpinion));
            }
            else
            {
                VesselAppraisalDetail.Update(appraisal.VesselAppraisalDetail);
            }
        }

        return this;
    }
}