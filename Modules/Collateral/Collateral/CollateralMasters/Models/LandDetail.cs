namespace Collateral.CollateralMasters.Models;

public class LandDetail
{
    public Guid CollateralMasterId { get; private set; }

    // Dedup key: Province + District + SubDistrict + TitleType + TitleNumber
    //          + SurveyNumber + LandParcelNumber + Rawang.
    // LandOfficeCode is retained as a descriptive column but is NOT part of the dedup key.
    public string LandOfficeCode { get; private set; } = null!;
    public string Province { get; private set; } = null!;
    public string District { get; private set; } = null!;
    public string SubDistrict { get; private set; } = null!;
    public string TitleType { get; private set; } = null!;
    public string TitleNumber { get; private set; } = null!;
    public string? SurveyNumber { get; private set; }
    public string? LandParcelNumber { get; private set; }
    public string? Rawang { get; private set; }

    // Address (owned)
    public Address Address { get; private set; } = null!;

    // Coordinates (owned)
    public Coordinates Coordinates { get; private set; } = null!;

    // Last-known land context
    public string? LandShapeType { get; private set; }
    public string? LandZoneType { get; private set; }
    public string? UrbanPlanningType { get; private set; }
    public decimal? AccessRoadWidth { get; private set; }
    public decimal? RoadFrontage { get; private set; }
    public decimal? LandArea { get; private set; }

    // Construction tracking
    public bool IsUnderConstructionAtLastAppraisal { get; private set; }
    public decimal? OverallConstructionProgressPercent { get; private set; }

    // Three-value model (Phase C, wired in PR-8)
    // UnitPrice: populated on every land master (IsMaster + aliases) — cost approach only, null otherwise.
    // Source: PricingFinalValue.FinalValueAdjusted (the adjusted unit price per sq.wa).
    public decimal? UnitPrice { get; private set; }
    // BuildingValue: IsMaster only — from PricingFinalValue.BuildingValue, cost approach only.
    public decimal? BuildingValue { get; private set; }
    // AppraisalValue: IsMaster only — from PricingFinalValue.AppraisalPrice (fallbacks: FinalValueAdjusted, FinalValueRounded).
    public decimal? AppraisalValue { get; private set; }

    // Appraisal summary (owned)
    public AppraisalSummary AppraisalSummary { get; private set; } = null!;

    // Synced from CollateralMaster for filtered unique index support
    public bool IsDeleted { get; private set; }

    private LandDetail() { }

    internal LandDetail(
        Guid collateralMasterId,
        string landOfficeCode,
        string province,
        string district,
        string subDistrict,
        string titleType,
        string titleNumber,
        string? surveyNumber,
        string? landParcelNumber,
        string? rawang,
        string? street,
        string? village,
        decimal? latitude,
        decimal? longitude,
        bool isDeleted)
    {
        CollateralMasterId = collateralMasterId;
        LandOfficeCode = landOfficeCode;
        Province = province;
        District = district;
        SubDistrict = subDistrict;
        TitleType = titleType;
        TitleNumber = titleNumber;
        SurveyNumber = surveyNumber;
        LandParcelNumber = landParcelNumber;
        Rawang = rawang;
        Address = new Address(street, village);
        Coordinates = new Coordinates(latitude, longitude);
        AppraisalSummary = new AppraisalSummary(null, null, null);
        IsUnderConstructionAtLastAppraisal = false;
        IsDeleted = isDeleted;
    }

    public void UpdateLastKnown(
        string? landShapeType,
        string? landZoneType,
        string? urbanPlanningType,
        decimal? accessRoadWidth,
        decimal? roadFrontage,
        decimal? landArea,
        string? street,
        string? village,
        decimal? latitude,
        decimal? longitude)
    {
        LandShapeType = landShapeType;
        LandZoneType = landZoneType;
        UrbanPlanningType = urbanPlanningType;
        AccessRoadWidth = accessRoadWidth;
        RoadFrontage = roadFrontage;
        LandArea = landArea;
        Address.Update(street, village);
        Coordinates.Update(latitude, longitude);
    }

    public void UpdateAppraisalSummary(
        Guid appraisalId,
        string appraisalNumber,
        DateTime appraisedDate,
        bool isUnderConstruction,
        decimal? overallConstructionProgressPercent)
    {
        AppraisalSummary.Update(appraisalId, appraisalNumber, appraisedDate);
        IsUnderConstructionAtLastAppraisal = isUnderConstruction;
        OverallConstructionProgressPercent = overallConstructionProgressPercent;
    }

    /// <summary>
    /// Updates the three-value model fields.
    /// <paramref name="unitPrice"/> is set on every land master (IsMaster + aliases).
    /// <paramref name="buildingCost"/> (stored as BuildingValue) and <paramref name="appraisalValue"/> are IsMaster only (pass null for aliases).
    /// </summary>
    public void UpdateValues(decimal? unitPrice, decimal? buildingCost, decimal? appraisalValue)
    {
        UnitPrice = unitPrice;
        BuildingValue = buildingCost;
        AppraisalValue = appraisalValue;
    }

    internal void SetIsDeleted(bool isDeleted) => IsDeleted = isDeleted;

    /// <summary>
    /// Applies admin-editable fields and records changed fields into the diff dictionary.
    /// Dedup-key (identity) fields and system-managed fields (construction tracking, last appraisal)
    /// are NOT editable here.
    /// </summary>
    internal void ApplyAdminEdit(LandAdminEdit edit, System.Collections.Generic.Dictionary<string, object?> diff)
    {
        if (edit.LandOfficeCode is not null && edit.LandOfficeCode != LandOfficeCode)
        {
            diff["Land.LandOfficeCode"] = new { from = LandOfficeCode, to = edit.LandOfficeCode };
            LandOfficeCode = edit.LandOfficeCode;
        }
        if (edit.Province is not null && edit.Province != Province)
        {
            diff["Land.Province"] = new { from = Province, to = edit.Province };
            Province = edit.Province;
        }
        if (edit.District is not null && edit.District != District)
        {
            diff["Land.District"] = new { from = District, to = edit.District };
            District = edit.District;
        }
        if (edit.SubDistrict is not null && edit.SubDistrict != SubDistrict)
        {
            diff["Land.SubDistrict"] = new { from = SubDistrict, to = edit.SubDistrict };
            SubDistrict = edit.SubDistrict;
        }
        if (edit.TitleType is not null && edit.TitleType != TitleType)
        {
            diff["Land.TitleType"] = new { from = TitleType, to = edit.TitleType };
            TitleType = edit.TitleType;
        }
        if (edit.TitleNumber is not null && edit.TitleNumber != TitleNumber)
        {
            diff["Land.TitleNumber"] = new { from = TitleNumber, to = edit.TitleNumber };
            TitleNumber = edit.TitleNumber;
        }
        if (edit.SurveyNumber is not null && edit.SurveyNumber != SurveyNumber)
        {
            diff["Land.SurveyNumber"] = new { from = SurveyNumber, to = edit.SurveyNumber };
            SurveyNumber = edit.SurveyNumber;
        }
        if (edit.LandParcelNumber is not null && edit.LandParcelNumber != LandParcelNumber)
        {
            diff["Land.LandParcelNumber"] = new { from = LandParcelNumber, to = edit.LandParcelNumber };
            LandParcelNumber = edit.LandParcelNumber;
        }
        if (edit.Rawang is not null && edit.Rawang != Rawang)
        {
            diff["Land.Rawang"] = new { from = Rawang, to = edit.Rawang };
            Rawang = edit.Rawang;
        }
        if (edit.Street is not null && edit.Street != Address.Street)
        {
            diff["Land.Street"] = new { from = Address.Street, to = edit.Street };
            Address.Update(edit.Street, Address.Village);
        }
        if (edit.Village is not null && edit.Village != Address.Village)
        {
            diff["Land.Village"] = new { from = Address.Village, to = edit.Village };
            Address.Update(Address.Street, edit.Village);
        }
        if (edit.Latitude is not null && edit.Latitude != Coordinates.Latitude)
        {
            diff["Land.Latitude"] = new { from = Coordinates.Latitude, to = edit.Latitude };
            Coordinates.Update(edit.Latitude, Coordinates.Longitude);
        }
        if (edit.Longitude is not null && edit.Longitude != Coordinates.Longitude)
        {
            diff["Land.Longitude"] = new { from = Coordinates.Longitude, to = edit.Longitude };
            Coordinates.Update(Coordinates.Latitude, edit.Longitude);
        }
        if (edit.LandShapeType is not null && edit.LandShapeType != LandShapeType)
        {
            diff["Land.LandShapeType"] = new { from = LandShapeType, to = edit.LandShapeType };
            LandShapeType = edit.LandShapeType;
        }
        if (edit.LandZoneType is not null && edit.LandZoneType != LandZoneType)
        {
            diff["Land.LandZoneType"] = new { from = LandZoneType, to = edit.LandZoneType };
            LandZoneType = edit.LandZoneType;
        }
        if (edit.UrbanPlanningType is not null && edit.UrbanPlanningType != UrbanPlanningType)
        {
            diff["Land.UrbanPlanningType"] = new { from = UrbanPlanningType, to = edit.UrbanPlanningType };
            UrbanPlanningType = edit.UrbanPlanningType;
        }
        if (edit.AccessRoadWidth is not null && edit.AccessRoadWidth != AccessRoadWidth)
        {
            diff["Land.AccessRoadWidth"] = new { from = AccessRoadWidth, to = edit.AccessRoadWidth };
            AccessRoadWidth = edit.AccessRoadWidth;
        }
        if (edit.RoadFrontage is not null && edit.RoadFrontage != RoadFrontage)
        {
            diff["Land.RoadFrontage"] = new { from = RoadFrontage, to = edit.RoadFrontage };
            RoadFrontage = edit.RoadFrontage;
        }
        if (edit.LandArea is not null && edit.LandArea != LandArea)
        {
            diff["Land.LandArea"] = new { from = LandArea, to = edit.LandArea };
            LandArea = edit.LandArea;
        }
    }
}
