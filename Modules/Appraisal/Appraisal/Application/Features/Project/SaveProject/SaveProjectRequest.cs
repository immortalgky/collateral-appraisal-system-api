namespace Appraisal.Application.Features.Project.SaveProject;

/// <summary>
/// Request to save (create or update) a project for an appraisal.
/// ProjectType must be provided and determines which type-specific fields are used.
/// </summary>
public record SaveProjectRequest(
    // Project Type (required to distinguish Condo vs LandAndBuilding)
    ProjectType ProjectType,
    // Project Info
    string? ProjectName = null,
    string? ProjectDescription = null,
    string? Developer = null,
    DateTime? ProjectSaleLaunchDate = null,
    // Land Area
    decimal? LandAreaRai = null,
    decimal? LandAreaNgan = null,
    decimal? LandAreaWa = null,
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
    string? LocationNumber = null,
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
    string? BuiltOnTitleDeedNumber = null,        // Condo only
    DateTime? LicenseExpirationDate = null         // LandAndBuilding only
);
