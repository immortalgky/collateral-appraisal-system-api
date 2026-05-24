namespace Appraisal.Application.Features.Project.SaveProjectDraft;

/// <summary>
/// Request to save a project as a DRAFT. Same shape as the final SaveProjectRequest,
/// but required/business-field validation is relaxed (see SaveProjectDraftCommandValidator).
/// </summary>
public record SaveProjectDraftRequest(
    // Project Type code: "U"=Condo, "LB"=LandAndBuilding, "L"=Land (still required — aggregate discriminator)
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
    string? BuiltOnTitleDeedNumber = null,        // Condo only
    DateTime? LicenseExpirationDate = null         // LandAndBuilding only
);
