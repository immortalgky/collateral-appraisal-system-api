namespace Appraisal.Extensions;

public static class DtoExtensions
{
    public static LandAppraisalDetail ToAggregate(this LandAppraisalDetailDto dto) =>
        LandAppraisalDetail.Create(
            dto.ApprId,
            dto.PropertyName ?? string.Empty,
            dto.CheckOwner ?? string.Empty,
            dto.Owner ?? string.Empty,
            dto.ObligationDetail.ToEntity(),
            dto.LandLocationDetail.ToEntity(),
            dto.LandFillDetail.ToEntity(),
            dto.LandAccessibilityDetail.ToEntity(),
            dto.AnticipationOfProp,
            dto.LandLimitation.ToEntity(),
            dto.Eviction,
            dto.Allocation,
            dto.ConsecutiveArea.ToEntity(),
            dto.LandMiscellaneousDetail.ToEntity()
        );

    public static BuildingAppraisalDetail ToAggregate(this BuildingAppraisalDetailDto dto) =>
        BuildingAppraisalDetail.Create(
            dto.ApprId,
            dto.BuildingInformation.ToEntity(),
            dto.BuildingTypeDetail.ToEntity(),
            dto.DecorationDetail.ToEntity(),
            dto.Encroachment.ToEntity(),
            dto.BuildingConstructionInformation.ToEntity(),
            dto.BuildingMaterial,
            dto.BuildingStyle,
            dto.ResidentialStatus.ToEntity(),
            dto.BuildingStructureDetail.ToEntity(),
            dto.UtilizationDetail.ToEntity(),
            dto.Remark,
            dto.BuildingAppraisalSurfaces.Select(serface => serface.ToEntity()).ToList(),
            dto.BuildingAppraisalDepreciationDetails.Select(depreciation => depreciation.ToEntity()).ToList()
        );

    public static BuildingAppraisalDepreciationDetail ToEntity(this BuildingAppraisalDepreciationDetailDto dto) =>
        BuildingAppraisalDepreciationDetail.Create(
            dto.AreaDesc,
            dto.Area,
            dto.PricePerSqM,
            dto.PriceBeforeDegradation,
            dto.Year,
            dto.DegradationYearPct,
            dto.TotalDegradationPct,
            dto.PriceDegradation,
            dto.TotalPrice,
            dto.AppraisalMethod,
            dto.BuildingAppraisalDepreciationPeriods.Select(s => s.ToEntity()).ToList()
        );

    public static BuildingAppraisalDepreciationPeriod ToEntity(this BuildingAppraisalDepreciationPeriodDto dto) =>
        BuildingAppraisalDepreciationPeriod.Create(
            dto.AtYear,
            dto.DepreciationPerYear
        );

    public static BuildingAppraisalSurface ToEntity(this BuildingAppraisalSurfaceDto dto) =>
        BuildingAppraisalSurface.Create(
            dto.FromFloorNo,
            dto.ToFloorNo,
            dto.FloorType,
            dto.FloorStructure,
            dto.FloorStructureOther,
            dto.FloorSurface,
            dto.FloorSurfaceOther
        );
    public static MachineAppraisalDetail ToAggregate(this MachineAppraisalDetailDto dto) =>
        MachineAppraisalDetail.Create(
            dto.ApprId,
            dto.MachineAppraisalDetail.ToEntity()
        );

    public static VehicleAppraisalDetail ToAggregate(this VehicleAppraisalDetailDto dto) =>
        VehicleAppraisalDetail.Create(
            dto.ApprId,
            dto.AppraisalDetail.ToEntity()
        );

    public static VesselAppraisalDetail ToAggregate(this VesselAppraisalDetailDto dto) =>
        VesselAppraisalDetail.Create(
            dto.ApprId,
            dto.AppraisalDetail.ToEntity()
        );

    public static CondoAppraisalDetail ToAggregate(this CondoAppraisalDetailDto dto)
    {
        return CondoAppraisalDetail.Create(
            dto.ApprId,
            dto.ObligationDetail.ToEntity(),
            dto.DocValidate,
            dto.CondominiumLocation.ToEntity(),
            dto.CondoAttribute.ToEntity(),
            dto.Expropriation.ToEntity(),
            dto.CondominiumFacility.ToEntity(),
            dto.CondoPrice.ToEntity(),
            dto.ForestBoundary.ToEntity(),
            dto.Remark,
            dto.CondoAppraisalAreaDetails.Select(condo => condo.ToEntity()).ToList()
        );
    }

    public static MachineAppraisalAdditionalInfo ToAggregate(this MachineAppraisalAdditionalInfoDto dto)
    {
        var purpose = PurposeAndLocationMachine.Create(
            dto.Assignment ?? string.Empty,
            dto.ApprCollatPurpose ?? string.Empty,
            dto.ApprDate ?? string.Empty,
            dto.ApprCollatType ?? string.Empty
        );

        var machineDetail = MachineDetail.Create(
            GeneralMachinery.Create(dto.Industrial, dto.SurveyNo, dto.ApprNo),
            AtSurveyDate.Create(
                dto.Installed ?? 0,
                dto.ApprScrap ?? string.Empty,
                dto.NoOfAppraise ?? 0,
                dto.NotInstalled ?? 0,
                dto.Maintenance ?? string.Empty,
                dto.Exterior ?? string.Empty,
                dto.Performance ?? string.Empty,
                dto.MarketDemand ?? false,
                dto.MarketDemandRemark ?? string.Empty
            ),
            RightsAndConditionsOfLegalRestrictions.Create(
                dto.Proprietor ?? string.Empty,
                dto.Owner ?? string.Empty,
                dto.MachineLocation ?? string.Empty,
                dto.Obligation ?? string.Empty,
                dto.Other ?? string.Empty
            )
        );

        return MachineAppraisalAdditionalInfo.Create(
            dto.ApprId,
            purpose,
            machineDetail
        );
    }

    public static PurposeAndLocationMachine ToEntity(this PurposeAndLocationMachineDto dto) =>
        PurposeAndLocationMachine.Create(
            dto.Assignment ?? string.Empty,
            dto.ApprCollatPurpose ?? string.Empty,
            dto.ApprDate ?? string.Empty,
            dto.ApprCollatType ?? string.Empty
        );

    public static GeneralMachinery ToEntity(this GeneralMachineryDto dto) =>
        GeneralMachinery.Create(
            dto.Industrial,
            dto.SurveyNo,
            dto.ApprNo
        );

    public static AtSurveyDate ToEntity(this AtSurveyDateDto dto) =>
        AtSurveyDate.Create(
            dto.Installed ?? 0,
            dto.ApprScrap ?? string.Empty,
            dto.NoOfAppraise ?? 0,
            dto.NotInstalled ?? 0,
            dto.Maintenance ?? string.Empty,
            dto.Exterior ?? string.Empty,
            dto.Performance ?? string.Empty,
            dto.MarketDemand ?? false,
            dto.MarketDemandRemark ?? string.Empty
        );

    public static RightsAndConditionsOfLegalRestrictions ToEntity(this RightsAndConditionsOfLegalRestrictionsDto dto) =>
        RightsAndConditionsOfLegalRestrictions.Create(
            dto.Proprietor ?? string.Empty,
            dto.Owner ?? string.Empty,
            dto.MachineLocation ?? string.Empty,
            dto.Obligation ?? string.Empty,
            dto.Other ?? string.Empty
        );

    public static MachineDetail ToEntity(this MachineDetailDto dto) =>
        MachineDetail.Create(
            dto.GeneralMachinery.ToEntity(),
            dto.AtSurveyDate.ToEntity(),
            dto.RightsAndConditionsOfLegalRestrictions.ToEntity()
        );

    public static CondoAppraisalAreaDetail ToEntity(this CondoAppraisalAreaDetailDto dto) =>
        CondoAppraisalAreaDetail.Create(
            dto.AreaDesc,
            dto.AreaSize
        );

    public static CondominiumLocation ToEntity(this CondominiumLocationDto dto) =>
        CondominiumLocation.Create(
            dto.CondoLocation,
            dto.Street,
            dto.Soi,
            dto.Distance,
            dto.RoadWidth,
            dto.RightOfWay,
            dto.RoadSurface,
            dto.PublicUtility,
            dto.PublicUtilityOther
        );

    public static CondominiumFacility ToEntity(this CondominiumFacilityDto dto) =>
        CondominiumFacility.Create(
            dto.CondoFacility,
            dto.CondoFacilityOther
        );

    public static CondoPrice ToEntity(this CondoPriceDto dto) =>
        CondoPrice.Create(
            dto.BuildingInsurancePrice,
            dto.SellingPrice,
            dto.ForceSellingPrice
        );

    public static CondoAttribute ToEntity(this CondoAttributeDto dto) =>
        CondoAttribute.Create(
            dto.Decoration,
            dto.BuildingYear,
            dto.CondoHeight,
            dto.BuildingForm,
            dto.ConstMaterial,
            dto.CondoRoomLayout.ToEntity(),
            dto.CondoFloor.ToEntity(),
            dto.CondoRoof.ToEntity(),
            dto.TotalAreaInSqM
        );

    public static CondoRoomLayout ToEntity(this CondoRoomLayoutDto dto) =>
        CondoRoomLayout.Create(
            dto.RoomLayout,
            dto.RoomLayoutOther
        );

    public static CondoFloor ToEntity(this CondoFloorDto dto) =>
        CondoFloor.Create(
            dto.GroundFloorMaterial,
            dto.GroundFloorMaterialOther,
            dto.UpperFloorMaterial,
            dto.UpperFloorMaterialOther,
            dto.BathroomFloorMaterial,
            dto.BathroomFloorMaterialOther
        );

    public static CondoRoof ToEntity(this CondoRoofDto dto) =>
        CondoRoof.Create(
            dto.Roof,
            dto.RoofOther
        );
    
    
    public static BuildingStructureDetail ToEntity(this BuildingStructureDetailDto dto) =>
        BuildingStructureDetail.Create(
            dto.BuildingConstructionStyle.ToEntity(),
            dto.BuildingGeneralStructure.ToEntity(),
            dto.BuildingRoofFrame.ToEntity(),
            dto.BuildingRoof.ToEntity(),
            dto.BuildingCeiling.ToEntity(),
            dto.BuildingWall.ToEntity(),
            dto.BuildingFence.ToEntity(),
            dto.ConstType.ToEntity()
        );

    public static AppraisalDetail ToEntity(this AppraisalDetailDto dto) =>
        AppraisalDetail.Create(
            dto.CanUse,
            dto.Location,
            dto.ConditionUse,
            dto.UsePurpose,
            dto.Part,
            dto.Remark,
            dto.Other,
            dto.AppraiserOpinion
        );

    public static UtilizationDetail ToEntity(this UtilizationDetailDto dto) =>
        UtilizationDetail.Create(
            dto.Utilization,
            dto.UseForOtherPurpose
        );
    public static BuildingConstructionStyle ToEntity(this BuildingConstructionStyleDto dto) =>
        BuildingConstructionStyle.Create(
            dto.ConstStyle,
            dto.ConstStyleRemark
        );

    public static BuildingGeneralStructure ToEntity(this BuildingGeneralStructureDto dto) =>
        BuildingGeneralStructure.Create(
            dto.GeneralStructure,
            dto.GeneralStructureOther
        );

    public static BuildingRoofFrame ToEntity(this BuildingRoofFrameDto dto) =>
        BuildingRoofFrame.Create(
            dto.RoofFrame,
            dto.RoofFrameOther
        );

    public static BuildingRoof ToEntity(this BuildingRoofDto dto) =>
        BuildingRoof.Create(
            dto.Roof,
            dto.RoofOther
        );

    public static BuildingCeiling ToEntity(this BuildingCeilingDto dto) =>
        BuildingCeiling.Create(
            dto.Ceiling,
            dto.CeilingOther
        );

    public static BuildingWall ToEntity(this BuildingWallDto dto) =>
        BuildingWall.Create(
            dto.InteriorWall,
            dto.InteriorWallOther,
            dto.ExteriorWall,
            dto.ExteriorWallOther
        );

    public static BuildingFence ToEntity(this BuildingFenceDto dto) =>
        BuildingFence.Create(
            dto.Fence,
            dto.FenceOther
        );

    public static BuildingConstructionType ToEntity(this BuildingConstructionTypeDto dto) =>
        BuildingConstructionType.Create(
            dto.ConstType,
            dto.ConstTypeOther
        );
    public static ResidentialStatus ToEntity(this ResidentialStatusDto dto) =>
        ResidentialStatus.Create(
            dto.IsResidential,
            dto.BuildingYear,
            dto.DueTo
        );

    public static BuildingConstructionInformation ToEntity(this BuildingConstructionInformationDto dto) =>
        BuildingConstructionInformation.Create(
            dto.OriginalBuildingPct,
            dto.UnderConstPct
        );

    public static DecorationDetail ToEntity(this DecorationDetailDto dto) =>
        DecorationDetail.Create(
            dto.Decoration,
            dto.DecorationOther
        );

    public static BuildingTypeDetail ToEntity(this BuildingTypeDetailDto dto) =>
        BuildingTypeDetail.Create(
            dto.BuildingType,
            dto.BuildingTypeOther,
            dto.TotalFloor
        );

    public static BuildingInformation ToEntity(this BuildingInformationDto dto) =>
        BuildingInformation.Create(
            dto.NoHouseNumber,
            dto.LandArea,
            dto.BuildingCondition ?? string.Empty,
            dto.BuildingStatus ?? string.Empty,
            dto.LicenseExpirationDate,
            dto.IsAppraise ?? string.Empty,
            dto.ObligationDetail.ToEntity()
        );

    public static ObligationDetail ToEntity(this ObligationDetailDto dto) =>
        ObligationDetail.Create(
            dto.IsObligation,
            dto.Obligation
        );

    public static LandLocationDetail ToEntity(this LandLocationDetailDto dto) =>
        LandLocationDetail.Create(
            dto.LandLocation,
            dto.LandCheck,
            dto.LandCheckOther,
            dto.Street,
            dto.Soi,
            dto.Distance,
            dto.Village,
            dto.AddressLocation,
            dto.LandShape,
            dto.UrbanPlanningType,
            dto.Location,
            dto.PlotLocation,
            dto.PlotLocationOther
        );

    public static LandFillDetail ToEntity(this LandFillDetailDto dto) =>
        LandFillDetail.Create(
            dto.LandFill,
            dto.LandFillPct,
            dto.SoilLevel
        );

    public static LandAccessibilityDetail ToEntity(this LandAccessibilityDetailDto dto) =>
        LandAccessibilityDetail.Create(
            dto.FrontageRoad.ToEntity(),
            dto.RoadSurface,
            dto.RoadSurfaceOther,
            dto.PublicUtility,
            dto.PublicUtilityOther,
            dto.LandUse,
            dto.LandUseOther,
            dto.LandEntranceExit,
            dto.LandEntranceExitOther,
            dto.Transportation,
            dto.TransportationOther
        );

    public static FrontageRoad ToEntity(this FrontageRoadDto dto) =>
        FrontageRoad.Create(
            dto.RoadWidth,
            dto.RightOfWay,
            dto.WideFrontageOfLand,
            dto.NoOfSideFacingRoad,
            dto.RoadPassInFrontOfLand,
            dto.LandAccessibility,
            dto.LandAccessibilityDesc
        );
    public static LandLimitation ToEntity(this LandLimitationDto dto) =>
        LandLimitation.Create(
            dto.Expropriation.ToEntity(),
            dto.Encroachment.ToEntity(),
            dto.Electricity,
            dto.ElectricityDistance,
            dto.IsLandlocked,
            dto.IsLandlockedRemark,
            dto.ForestBoundary.ToEntity(),
            dto.LimitationOther
        );

    public static Expropriation ToEntity(this ExpropriationDto dto) =>
        Expropriation.Create(
            dto.IsExpropriate,
            dto.IsExpropriateRemark,
            dto.InLineExpropriate,
            dto.InLineExpropriateRemark,
            dto.RoyalDecree
        );

    public static Encroachment ToEntity(this EncroachmentDto dto) =>
        Encroachment.Create(
            dto.IsEncroached,
            dto.IsEncroachedRemark,
            dto.EncroachArea
        );

    public static ForestBoundary ToEntity(this ForestBoundaryDto dto) =>
        ForestBoundary.Create(
            dto.IsForestBoundary,
            dto.IsForestBoundaryRemark
        );
    public static ConsecutiveArea ToEntity(this ConsecutiveAreaDto dto) =>
        ConsecutiveArea.Create(
            dto.NConsecutiveArea,
            dto.NEstimateLength,
            dto.SConsecutiveArea,
            dto.SEstimateLength,
            dto.EConsecutiveArea,
            dto.EEstimateLength,
            dto.WConsecutiveArea,
            dto.WEstimateLength
        );

    public static LandMiscellaneousDetail ToEntity(this LandMiscellaneousDetailDto dto) =>
        LandMiscellaneousDetail.Create(
            dto.PondArea,
            dto.DepthPit,
            dto.HasBuilding,
            dto.HasBuildingOther
        );
}