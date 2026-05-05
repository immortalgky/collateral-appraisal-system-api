namespace Collateral.CollateralMasters.Models;

public class CondoDetail
{
    public Guid CollateralMasterId { get; private set; }

    // Dedup key (7 columns per spec v1 section 5.5)
    public string LandOfficeCode { get; private set; } = null!;
    public string CondoRegistrationNumber { get; private set; } = null!;
    public string BuildingNumber { get; private set; } = null!;
    public string FloorNumber { get; private set; } = null!;
    public string UnitNumber { get; private set; } = null!;
    public string TitleNumber { get; private set; } = null!;
    public string TitleType { get; private set; } = null!;

    // Identity-extra
    public string? CondoName { get; private set; }
    public string? Province { get; private set; }

    // Last-known
    public decimal? UsableArea { get; private set; }
    public string? LocationType { get; private set; }
    public int? BuildingAge { get; private set; }
    public int? ConstructionYear { get; private set; }
    public string? ModelName { get; private set; }

    // Appraisal summary (owned)
    public AppraisalSummary AppraisalSummary { get; private set; } = null!;

    // Synced from CollateralMaster for filtered unique index support
    public bool IsDeleted { get; private set; }

    private CondoDetail() { }

    internal CondoDetail(
        Guid collateralMasterId,
        string landOfficeCode,
        string condoRegistrationNumber,
        string buildingNumber,
        string floorNumber,
        string unitNumber,
        string titleNumber,
        string titleType,
        string? condoName,
        string? province,
        bool isDeleted)
    {
        CollateralMasterId = collateralMasterId;
        LandOfficeCode = landOfficeCode;
        CondoRegistrationNumber = condoRegistrationNumber;
        BuildingNumber = buildingNumber;
        FloorNumber = floorNumber;
        UnitNumber = unitNumber;
        TitleNumber = titleNumber;
        TitleType = titleType;
        CondoName = condoName;
        Province = province;
        AppraisalSummary = new AppraisalSummary(null, null, null, null);
        IsDeleted = isDeleted;
    }

    public void UpdateLastKnown(
        string? condoName,
        string? province,
        decimal? usableArea,
        string? locationType,
        int? buildingAge,
        int? constructionYear,
        string? modelName)
    {
        CondoName = condoName;
        Province = province;
        UsableArea = usableArea;
        LocationType = locationType;
        BuildingAge = buildingAge;
        ConstructionYear = constructionYear;
        ModelName = modelName;
    }

    public void UpdateAppraisalSummary(
        Guid appraisalId,
        string appraisalNumber,
        DateTime appraisedDate,
        decimal appraisedValue)
    {
        AppraisalSummary.Update(appraisalId, appraisalNumber, appraisedDate, appraisedValue);
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
        if (edit.UnitNumber is not null && edit.UnitNumber != UnitNumber)
        {
            diff["Condo.UnitNumber"] = new { from = UnitNumber, to = edit.UnitNumber };
            UnitNumber = edit.UnitNumber;
        }
        if (edit.TitleNumber is not null && edit.TitleNumber != TitleNumber)
        {
            diff["Condo.TitleNumber"] = new { from = TitleNumber, to = edit.TitleNumber };
            TitleNumber = edit.TitleNumber;
        }
        if (edit.TitleType is not null && edit.TitleType != TitleType)
        {
            diff["Condo.TitleType"] = new { from = TitleType, to = edit.TitleType };
            TitleType = edit.TitleType;
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
