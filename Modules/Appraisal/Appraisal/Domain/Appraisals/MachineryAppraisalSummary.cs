namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Appraisal-level machinery summary (Section 3.1 general + Section 3.3 rights/legal).
/// Standalone entity with FK to Appraisal (1:1 via unique index).
/// </summary>
public class MachineryAppraisalSummary : Entity<Guid>
{
    // Foreign Key - 1:1 with Appraisal
    public Guid AppraisalId { get; private set; }

    // Section 3.1 — General Machinery
    public string? InIndustrial { get; private set; }
    public int? SurveyedNumber { get; private set; }
    public int? AppraisalNumber { get; private set; }
    public int? InstalledAndUseCount { get; private set; }
    public int? AppraisalScrapCount { get; private set; }
    public int? AppraisedByDocumentCount { get; private set; }
    public int? NotInstalledCount { get; private set; }
    public string? Maintenance { get; private set; }
    public string? Exterior { get; private set; }
    public string? Performance { get; private set; }
    public bool? MarketDemandAvailable { get; private set; }
    public string? MarketDemand { get; private set; }

    // Section 3.3 — Rights & Legal
    public string? Proprietor { get; private set; }
    public string? Owner { get; private set; }
    public string? MachineAddress { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public string? Obligation { get; private set; }
    public string? Other { get; private set; }

    private MachineryAppraisalSummary()
    {
        // For EF Core
    }

    public static MachineryAppraisalSummary Create(Guid appraisalId)
    {
        return new MachineryAppraisalSummary
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId
        };
    }

    public void Update(
        // Section 3.1 — General Machinery
        string? inIndustrial = null,
        int? surveyedNumber = null,
        int? appraisalNumber = null,
        int? installedAndUseCount = null,
        int? appraisalScrapCount = null,
        int? appraisedByDocumentCount = null,
        int? notInstalledCount = null,
        string? maintenance = null,
        string? exterior = null,
        string? performance = null,
        bool? marketDemandAvailable = null,
        string? marketDemand = null,
        // Section 3.3 — Rights & Legal
        string? proprietor = null,
        string? owner = null,
        string? machineAddress = null,
        decimal? latitude = null,
        decimal? longitude = null,
        string? obligation = null,
        string? other = null)
    {
        // Section 3.1
        if (inIndustrial is not null) InIndustrial = inIndustrial;
        if (surveyedNumber.HasValue) SurveyedNumber = surveyedNumber.Value;
        if (appraisalNumber.HasValue) AppraisalNumber = appraisalNumber.Value;
        if (installedAndUseCount.HasValue) InstalledAndUseCount = installedAndUseCount.Value;
        if (appraisalScrapCount.HasValue) AppraisalScrapCount = appraisalScrapCount.Value;
        if (appraisedByDocumentCount.HasValue) AppraisedByDocumentCount = appraisedByDocumentCount.Value;
        if (notInstalledCount.HasValue) NotInstalledCount = notInstalledCount.Value;
        if (maintenance is not null) Maintenance = maintenance;
        if (exterior is not null) Exterior = exterior;
        if (performance is not null) Performance = performance;
        if (marketDemandAvailable.HasValue) MarketDemandAvailable = marketDemandAvailable.Value;
        if (marketDemand is not null) MarketDemand = marketDemand;

        // Section 3.3
        if (proprietor is not null) Proprietor = proprietor;
        if (owner is not null) Owner = owner;
        if (machineAddress is not null) MachineAddress = machineAddress;
        if (latitude.HasValue) Latitude = latitude.Value;
        if (longitude.HasValue) Longitude = longitude.Value;
        if (obligation is not null) Obligation = obligation;
        if (other is not null) Other = other;
    }
}
