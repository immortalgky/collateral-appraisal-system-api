namespace Appraisal.Domain.Appraisals;

public class VillageProject : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }

    // Project Info
    public string? ProjectName { get; private set; }
    public string? ProjectDescription { get; private set; }
    public string? Developer { get; private set; }
    public DateTime? ProjectSaleLaunchDate { get; private set; }

    // Land Area
    public decimal? LandAreaRai { get; private set; }
    public decimal? LandAreaNgan { get; private set; }
    public decimal? LandAreaWa { get; private set; }

    // Project Details
    public int? UnitForSaleCount { get; private set; }
    public int? NumberOfPhase { get; private set; }
    public string? LandOffice { get; private set; }
    public string? ProjectType { get; private set; }
    public DateTime? LicenseExpirationDate { get; private set; }

    // Location
    public GpsCoordinate? Coordinates { get; private set; }
    public AdministrativeAddress? Address { get; private set; }
    public string? Postcode { get; private set; }
    public string? LocationNumber { get; private set; }
    public string? Road { get; private set; }
    public string? Soi { get; private set; }

    // Utilities & Facilities
    public List<string>? Utilities { get; private set; }
    public string? UtilitiesOther { get; private set; }
    public List<string>? Facilities { get; private set; }
    public string? FacilitiesOther { get; private set; }

    // Other
    public string? Remark { get; private set; }

    private VillageProject()
    {
    }

    public static VillageProject Create(Guid appraisalId)
    {
        return new VillageProject
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void Update(
        // Project Info
        string? projectName = null,
        string? projectDescription = null,
        string? developer = null,
        DateTime? projectSaleLaunchDate = null,
        // Land Area
        decimal? landAreaRai = null,
        decimal? landAreaNgan = null,
        decimal? landAreaWa = null,
        // Project Details
        int? unitForSaleCount = null,
        int? numberOfPhase = null,
        string? landOffice = null,
        string? projectType = null,
        DateTime? licenseExpirationDate = null,
        // Location
        GpsCoordinate? coordinates = null,
        AdministrativeAddress? address = null,
        string? postcode = null,
        string? locationNumber = null,
        string? road = null,
        string? soi = null,
        // Utilities & Facilities
        List<string>? utilities = null,
        string? utilitiesOther = null,
        List<string>? facilities = null,
        string? facilitiesOther = null,
        // Other
        string? remark = null)
    {
        // Validation
        if (landAreaRai is < 0)
            throw new ArgumentException("Land area (Rai) cannot be negative", nameof(landAreaRai));
        if (landAreaNgan is < 0)
            throw new ArgumentException("Land area (Ngan) cannot be negative", nameof(landAreaNgan));
        if (landAreaWa is < 0)
            throw new ArgumentException("Land area (Wa) cannot be negative", nameof(landAreaWa));
        if (unitForSaleCount is < 0)
            throw new ArgumentException("Unit for sale count cannot be negative", nameof(unitForSaleCount));
        if (numberOfPhase is < 0)
            throw new ArgumentException("Number of phases cannot be negative", nameof(numberOfPhase));

        // Project Info
        ProjectName = projectName;
        ProjectDescription = projectDescription;
        Developer = developer;
        ProjectSaleLaunchDate = projectSaleLaunchDate;

        // Land Area
        LandAreaRai = landAreaRai;
        LandAreaNgan = landAreaNgan;
        LandAreaWa = landAreaWa;

        // Project Details
        UnitForSaleCount = unitForSaleCount;
        NumberOfPhase = numberOfPhase;
        LandOffice = landOffice;
        ProjectType = projectType;
        LicenseExpirationDate = licenseExpirationDate;

        // Location
        Coordinates = coordinates;
        Address = address;
        Postcode = postcode;
        LocationNumber = locationNumber;
        Road = road;
        Soi = soi;

        // Utilities & Facilities
        Utilities = utilities;
        UtilitiesOther = utilitiesOther;
        Facilities = facilities;
        FacilitiesOther = facilitiesOther;

        // Other
        Remark = remark;
    }
}
