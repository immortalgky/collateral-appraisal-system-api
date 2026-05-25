namespace Appraisal.Application.Features.Project.GetProject;

/// <summary>
/// Result of getting a project. Includes both shared and type-specific nullable fields.
/// ProjectType lets the client switch rendering between Condo and LandAndBuilding UIs.
/// </summary>
public record GetProjectResult(
    Guid Id,
    Guid AppraisalId,
    string ProjectType,
    // Project Info
    string? ProjectName,
    string? ProjectDescription,
    string? Developer,
    string? ProjectSaleLaunchDate,
    // Land Area
    decimal? LandAreaRai,
    decimal? LandAreaNgan,
    decimal? LandAreaSquareWa,
    // Project Details
    int? UnitForSaleCount,
    int? NumberOfPhase,
    string? LandOffice,
    // Location (flattened)
    decimal? Latitude,
    decimal? Longitude,
    string? SubDistrict,
    string? District,
    string? Province,
    string? Postcode,
    string? HouseNumber,
    string? Road,
    string? Soi,
    // Utilities & Facilities
    List<string>? Utilities,
    string? UtilitiesOther,
    List<string>? Facilities,
    string? FacilitiesOther,
    // Other
    string? Remark,
    // Type-specific (nullable)
    string? BuiltOnTitleDeedNumber,       // Condo only
    DateTime? LicenseExpirationDate        // LandAndBuilding only
);
