namespace Appraisal.Domain.SupportingDataMaintenance;

public class SupportingDataDetail : Entity<Guid>
{
    public Guid SupportingDataId { get; private set; }   // FK to parent

    public string? PropertyName { get; private set; }
    public string? Developer { get; private set; }
    public string? ModelName { get; private set; }
    public string CollateralType { get; private set; } = default!;
    public string BuildingType { get; private set; } = default!;
    public decimal? LandArea { get; private set; }
    public decimal? UsableArea { get; private set; }
    public string? ProjectName { get; private set; }
    public string? RoomFloor { get; private set; }

    public SupportingAddress Address { get; private set; } = default!;
    public GeoLocation? Location { get; private set; }

    public string? PlotLocationType { get; private set; }
    public string? PlotLocationTypeOther { get; private set; }
    public decimal? PricePerUnit { get; private set; }
    public decimal? OfferingPrice { get; private set; }
    public decimal? SellingPrice { get; private set; }
    public string? PhoneNo { get; private set; }
    public DateTime InformationDate { get; private set; }
    public string? Website { get; private set; }
    public string? SourceUrl { get; private set; }
    public string? Remark { get; private set; }

    private SupportingDataDetail() { /* EF */ }

    private SupportingDataDetail(Guid supportingDataId, SupportingDataDetailData data)
    {
        Id = Guid.CreateVersion7();
        SupportingDataId = supportingDataId;
        Apply(data);
    }

    internal static SupportingDataDetail Create(Guid supportingDataId, SupportingDataDetailData data)
        => new(supportingDataId, data);

    internal void Update(SupportingDataDetailData data) => Apply(data);

    public void Validate()
    {
        var rc = new RuleCheck();
        rc.AddErrorIf(string.IsNullOrWhiteSpace(CollateralType), "CollateralType is required.");
        rc.AddErrorIf(string.IsNullOrWhiteSpace(BuildingType), "BuildingType is required.");
        rc.ThrowIfInvalid();
    }

    private void Apply(SupportingDataDetailData d)
    {
        PropertyName = d.PropertyName;
        Developer = d.Developer;
        ModelName = d.ModelName;
        CollateralType = d.CollateralType;
        BuildingType = d.BuildingType;
        LandArea = d.LandArea;
        UsableArea = d.UsableArea;
        ProjectName = d.ProjectName;
        RoomFloor = d.RoomFloor;
        Address = SupportingAddress.Create(d.HouseNo, d.SubDistrict, d.District, d.Province);
        Location = (d.Latitude.HasValue && d.Longitude.HasValue) ? GeoLocation.Create(d.Latitude.Value, d.Longitude.Value) : null;
        PlotLocationType = d.PlotLocationType;
        PlotLocationTypeOther = d.PlotLocationTypeOther;
        PricePerUnit = d.PricePerUnit;
        OfferingPrice = d.OfferingPrice;
        SellingPrice = d.SellingPrice;
        PhoneNo = d.PhoneNo;
        InformationDate = d.InformationDate;
        Website = d.Website;
        SourceUrl = d.SourceUrl;
        Remark = d.Remark;
    }
}

public record SupportingDataDetailData(
    string? PropertyName,
    string? Developer,
    string? ModelName,
    string CollateralType,
    string BuildingType,
    decimal? LandArea,
    decimal? UsableArea,
    string? ProjectName,
    string? RoomFloor,
    string? HouseNo,
    string? SubDistrict,
    string? District,
    string? Province,
    decimal? Latitude,
    decimal? Longitude,
    string? PlotLocationType,
    string? PlotLocationTypeOther,
    decimal? PricePerUnit,
    decimal? OfferingPrice,
    decimal? SellingPrice,
    string? PhoneNo,
    DateTime InformationDate,
    string? Website,
    string? SourceUrl,
    string? Remark
);