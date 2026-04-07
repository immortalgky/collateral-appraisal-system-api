namespace Appraisal.Application.Features.BlockCondo.GetCondoProject;

/// <summary>
/// Response for getting a condo project
/// </summary>
public record GetCondoProjectResponse(
    Guid Id,
    Guid AppraisalId,
    // Project Info
    string? ProjectName,
    string? ProjectDescription,
    string? Developer,
    DateTime? ProjectSaleLaunchDate,
    // Land Area
    decimal? LandAreaRai,
    decimal? LandAreaNgan,
    decimal? LandAreaWa,
    // Project Details
    int? UnitForSaleCount,
    int? NumberOfPhase,
    string? LandOffice,
    string? ProjectType,
    string? BuiltOnTitleDeedNumber,
    // Location (flattened)
    decimal? Latitude,
    decimal? Longitude,
    string? SubDistrict,
    string? District,
    string? Province,
    string? Postcode,
    string? LocationNumber,
    string? Road,
    string? Soi,
    // Utilities & Facilities
    List<string>? Utilities,
    string? UtilitiesOther,
    List<string>? Facilities,
    string? FacilitiesOther,
    // Other
    string? Remark
);
