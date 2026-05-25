namespace Appraisal.Application.Services;

/// <summary>
/// Mode-agnostic input for creating or updating a Project aggregate.
/// Both the final-save and draft-save commands map to this via Mapster; the only
/// difference between the two flows is the FluentValidation validator that runs first.
/// </summary>
public record SaveProjectData(
    Guid AppraisalId,
    string ProjectType,
    // Project Info
    string? ProjectName = null,
    string? ProjectDescription = null,
    string? Developer = null,
    string? ProjectSaleLaunchDate = null,
    // Land Area
    decimal? LandAreaRai = null,
    decimal? LandAreaNgan = null,
    decimal? LandAreaSquareWa = null,
    // Project Details
    int? UnitForSaleCount = null,
    int? NumberOfPhase = null,
    string? LandOffice = null,
    // Location
    decimal? Latitude = null,
    decimal? Longitude = null,
    string? SubDistrict = null,
    string? District = null,
    string? Province = null,
    string? Postcode = null,
    string? HouseNumber = null,
    string? Road = null,
    string? Soi = null,
    // Utilities & Facilities
    List<string>? Utilities = null,
    string? UtilitiesOther = null,
    List<string>? Facilities = null,
    string? FacilitiesOther = null,
    // Other
    string? Remark = null,
    // Type-specific
    string? BuiltOnTitleDeedNumber = null,
    DateTime? LicenseExpirationDate = null
);
