namespace Collateral.CollateralMasters.Models;

public class CondoDetail
{
    public Guid CollateralMasterId { get; private set; }

    // Dedup key (7 columns): CondoRegistrationNumber + BuildingNumber + FloorNumber + RoomNumber
    //                      + Province + District + SubDistrict.
    // Province/District/SubDistrict are the geographic disambiguator (replacing LandOfficeCode),
    // since a CondoRegistrationNumber is only unique within an administrative area.
    // LandOfficeCode is retained as a descriptive column (nullable) but is NOT part of the dedup key.
    // TitleNumber/TitleType were dropped — they carried no condo identity (TitleType was always the
    // "DEED" constant; TitleNumber was the underlying land's BuiltOnTitleNumber).
    public string? LandOfficeCode { get; private set; }
    public string CondoRegistrationNumber { get; private set; } = null!;
    public string BuildingNumber { get; private set; } = null!;
    public string FloorNumber { get; private set; } = null!;
    public string RoomNumber { get; private set; } = null!;
    public string Province { get; private set; } = null!;
    public string District { get; private set; } = null!;
    public string SubDistrict { get; private set; } = null!;

    // Identity-extra
    public string? CondoName { get; private set; }

    // Last-known
    public decimal? UsableArea { get; private set; }
    public string? LocationType { get; private set; }
    public int? BuildingAge { get; private set; }
    public int? ConstructionYear { get; private set; }
    public string? ModelName { get; private set; }

    // GPS coordinates (Phase 1 — geo filter prerequisite)
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    // Three-value model (Phase C, wired in PR-8)
    // UnitPrice: per-unit price — cost approach only. Source: PricingFinalValue.FinalValueAdjusted.
    public decimal? UnitPrice { get; private set; }
    // BuildingValue: IsMaster only — from PricingFinalValue.BuildingValue, cost approach only.
    public decimal? BuildingValue { get; private set; }
    // AppraisalValue: IsMaster only — from PricingFinalValue.AppraisalPrice (fallbacks: FinalValueAdjusted, FinalValueRounded).
    public decimal? AppraisalValue { get; private set; }

    // Appraisal summary (owned)
    public AppraisalSummary AppraisalSummary { get; private set; } = null!;

    // Synced from CollateralMaster for filtered unique index support
    public bool IsDeleted { get; private set; }

    private CondoDetail() { }

    internal CondoDetail(
        Guid collateralMasterId,
        string? landOfficeCode,
        string condoRegistrationNumber,
        string buildingNumber,
        string floorNumber,
        string roomNumber,
        string province,
        string district,
        string subDistrict,
        string? condoName,
        bool isDeleted)
    {
        CollateralMasterId = collateralMasterId;
        LandOfficeCode = landOfficeCode;
        CondoRegistrationNumber = condoRegistrationNumber;
        BuildingNumber = buildingNumber;
        FloorNumber = floorNumber;
        RoomNumber = roomNumber;
        Province = province;
        District = district;
        SubDistrict = subDistrict;
        CondoName = condoName;
        AppraisalSummary = new AppraisalSummary(null, null, null);
        IsDeleted = isDeleted;
    }

    // Province/District/SubDistrict are dedup-key identity (set at construction only),
    // so they are intentionally NOT updated here — matching how LandDetail treats its geo key.
    public void UpdateLastKnown(
        string? condoName,
        decimal? usableArea,
        string? locationType,
        int? buildingAge,
        int? constructionYear,
        string? modelName,
        decimal? latitude,
        decimal? longitude)
    {
        CondoName = condoName;
        UsableArea = usableArea;
        LocationType = locationType;
        BuildingAge = buildingAge;
        ConstructionYear = constructionYear;
        ModelName = modelName;
        Latitude = latitude;
        Longitude = longitude;
    }

    public void UpdateAppraisalSummary(
        Guid appraisalId,
        string appraisalNumber,
        DateTime appraisedDate)
    {
        AppraisalSummary.Update(appraisalId, appraisalNumber, appraisedDate);
    }

    /// <summary>
    /// Updates the three-value model fields.
    /// <paramref name="buildingCost"/> and <paramref name="appraisalValue"/> are IsMaster only (pass null for aliases).
    /// </summary>
    public void UpdateValues(decimal? unitPrice, decimal? buildingCost, decimal? appraisalValue)
    {
        UnitPrice = unitPrice;
        BuildingValue = buildingCost;
        AppraisalValue = appraisalValue;
    }

    internal void SetIsDeleted(bool isDeleted) => IsDeleted = isDeleted;

    internal void ApplyAdminEdit(CondoAdminEdit edit, System.Collections.Generic.Dictionary<string, object?> diff)
    {
        if (edit.LandOfficeCode is not null && edit.LandOfficeCode != LandOfficeCode)
        {
            diff["Condo.LandOfficeCode"] = new { from = LandOfficeCode, to = edit.LandOfficeCode };
            LandOfficeCode = edit.LandOfficeCode;
        }
        if (edit.CondoRegistrationNumber is not null && edit.CondoRegistrationNumber != CondoRegistrationNumber)
        {
            diff["Condo.CondoRegistrationNumber"] = new { from = CondoRegistrationNumber, to = edit.CondoRegistrationNumber };
            CondoRegistrationNumber = edit.CondoRegistrationNumber;
        }
        if (edit.BuildingNumber is not null && edit.BuildingNumber != BuildingNumber)
        {
            diff["Condo.BuildingNumber"] = new { from = BuildingNumber, to = edit.BuildingNumber };
            BuildingNumber = edit.BuildingNumber;
        }
        if (edit.FloorNumber is not null && edit.FloorNumber != FloorNumber)
        {
            diff["Condo.FloorNumber"] = new { from = FloorNumber, to = edit.FloorNumber };
            FloorNumber = edit.FloorNumber;
        }
        if (edit.RoomNumber is not null && edit.RoomNumber != RoomNumber)
        {
            diff["Condo.RoomNumber"] = new { from = RoomNumber, to = edit.RoomNumber };
            RoomNumber = edit.RoomNumber;
        }
        if (edit.CondoName is not null && edit.CondoName != CondoName)
        {
            diff["Condo.CondoName"] = new { from = CondoName, to = edit.CondoName };
            CondoName = edit.CondoName;
        }
        if (edit.Province is not null && edit.Province != Province)
        {
            diff["Condo.Province"] = new { from = Province, to = edit.Province };
            Province = edit.Province;
        }
        if (edit.District is not null && edit.District != District)
        {
            diff["Condo.District"] = new { from = District, to = edit.District };
            District = edit.District;
        }
        if (edit.SubDistrict is not null && edit.SubDistrict != SubDistrict)
        {
            diff["Condo.SubDistrict"] = new { from = SubDistrict, to = edit.SubDistrict };
            SubDistrict = edit.SubDistrict;
        }
        if (edit.UsableArea is not null && edit.UsableArea != UsableArea)
        {
            diff["Condo.UsableArea"] = new { from = UsableArea, to = edit.UsableArea };
            UsableArea = edit.UsableArea;
        }
        if (edit.LocationType is not null && edit.LocationType != LocationType)
        {
            diff["Condo.LocationType"] = new { from = LocationType, to = edit.LocationType };
            LocationType = edit.LocationType;
        }
        if (edit.BuildingAge is not null && edit.BuildingAge != BuildingAge)
        {
            diff["Condo.BuildingAge"] = new { from = BuildingAge, to = edit.BuildingAge };
            BuildingAge = edit.BuildingAge;
        }
        if (edit.ConstructionYear is not null && edit.ConstructionYear != ConstructionYear)
        {
            diff["Condo.ConstructionYear"] = new { from = ConstructionYear, to = edit.ConstructionYear };
            ConstructionYear = edit.ConstructionYear;
        }
        if (edit.ModelName is not null && edit.ModelName != ModelName)
        {
            diff["Condo.ModelName"] = new { from = ModelName, to = edit.ModelName };
            ModelName = edit.ModelName;
        }
    }
}
