namespace Collateral.CollateralMasters.Models;

public class LandDetail
{
    public Guid CollateralMasterId { get; private set; }

    // Dedup key (7 columns)
    public string LandOfficeCode { get; private set; } = null!;
    public string Province { get; private set; } = null!;
    public string Amphur { get; private set; } = null!;
    public string Tambon { get; private set; } = null!;
    public string TitleDeedType { get; private set; } = null!;
    public string TitleDeedNo { get; private set; } = null!;
    public string? SurveyOrParcelNo { get; private set; }

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

    // Construction tracking trio
    public bool IsUnderConstructionAtLastAppraisal { get; private set; }
    public decimal? OverallConstructionProgressPercent { get; private set; }
    public Guid? LastConstructionInspectionId { get; private set; }

    // Appraisal summary (owned) — plus land-specific LastTotalAppraisedValue
    public AppraisalSummary AppraisalSummary { get; private set; } = null!;
    public decimal? LastTotalAppraisedValue { get; private set; }

    // Synced from CollateralMaster for filtered unique index support
    public bool IsDeleted { get; private set; }

    private LandDetail() { }

    internal LandDetail(
        Guid collateralMasterId,
        string landOfficeCode,
        string province,
        string amphur,
        string tambon,
        string titleDeedType,
        string titleDeedNo,
        string? surveyOrParcelNo,
        string? street,
        string? village,
        string? postalCode,
        decimal? latitude,
        decimal? longitude,
        bool isDeleted)
    {
        CollateralMasterId = collateralMasterId;
        LandOfficeCode = landOfficeCode;
        Province = province;
        Amphur = amphur;
        Tambon = tambon;
        TitleDeedType = titleDeedType;
        TitleDeedNo = titleDeedNo;
        SurveyOrParcelNo = surveyOrParcelNo;
        Address = new Address(street, village, postalCode);
        Coordinates = new Coordinates(latitude, longitude);
        AppraisalSummary = new AppraisalSummary(null, null, null, null);
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
        string? postalCode,
        decimal? latitude,
        decimal? longitude)
    {
        LandShapeType = landShapeType;
        LandZoneType = landZoneType;
        UrbanPlanningType = urbanPlanningType;
        AccessRoadWidth = accessRoadWidth;
        RoadFrontage = roadFrontage;
        LandArea = landArea;
        Address.Update(street, village, postalCode);
        Coordinates.Update(latitude, longitude);
    }

    public void UpdateAppraisalSummary(
        Guid appraisalId,
        string appraisalNumber,
        DateTime appraisedDate,
        decimal appraisedValue,
        decimal totalAppraisedValue,
        bool isUnderConstruction,
        decimal? overallConstructionProgressPercent,
        Guid? lastConstructionInspectionId)
    {
        AppraisalSummary.Update(appraisalId, appraisalNumber, appraisedDate, appraisedValue);
        LastTotalAppraisedValue = totalAppraisedValue;
        IsUnderConstructionAtLastAppraisal = isUnderConstruction;
        OverallConstructionProgressPercent = overallConstructionProgressPercent;
        LastConstructionInspectionId = lastConstructionInspectionId;
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
        if (edit.Amphur is not null && edit.Amphur != Amphur)
        {
            diff["Land.Amphur"] = new { from = Amphur, to = edit.Amphur };
            Amphur = edit.Amphur;
        }
        if (edit.Tambon is not null && edit.Tambon != Tambon)
        {
            diff["Land.Tambon"] = new { from = Tambon, to = edit.Tambon };
            Tambon = edit.Tambon;
        }
        if (edit.TitleDeedType is not null && edit.TitleDeedType != TitleDeedType)
        {
            diff["Land.TitleDeedType"] = new { from = TitleDeedType, to = edit.TitleDeedType };
            TitleDeedType = edit.TitleDeedType;
        }
        if (edit.TitleDeedNo is not null && edit.TitleDeedNo != TitleDeedNo)
        {
            diff["Land.TitleDeedNo"] = new { from = TitleDeedNo, to = edit.TitleDeedNo };
            TitleDeedNo = edit.TitleDeedNo;
        }
        if (edit.SurveyOrParcelNo is not null && edit.SurveyOrParcelNo != SurveyOrParcelNo)
        {
            diff["Land.SurveyOrParcelNo"] = new { from = SurveyOrParcelNo, to = edit.SurveyOrParcelNo };
            SurveyOrParcelNo = edit.SurveyOrParcelNo;
        }
        if (edit.Street is not null && edit.Street != Address.Street)
        {
            diff["Land.Street"] = new { from = Address.Street, to = edit.Street };
            Address.Update(edit.Street, Address.Village, Address.PostalCode);
        }
        if (edit.Village is not null && edit.Village != Address.Village)
        {
            diff["Land.Village"] = new { from = Address.Village, to = edit.Village };
            Address.Update(Address.Street, edit.Village, Address.PostalCode);
        }
        if (edit.PostalCode is not null && edit.PostalCode != Address.PostalCode)
        {
            diff["Land.PostalCode"] = new { from = Address.PostalCode, to = edit.PostalCode };
            Address.Update(Address.Street, Address.Village, edit.PostalCode);
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
