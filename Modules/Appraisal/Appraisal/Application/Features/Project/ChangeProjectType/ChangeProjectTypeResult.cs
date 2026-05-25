using Appraisal.Domain.Projects;

namespace Appraisal.Application.Features.Project.ChangeProjectType;

/// <summary>
/// Result of a successful project type change. Contains the full project state so the
/// frontend can hydrate without a follow-up GET.
/// </summary>
public record ChangeProjectTypeResult(
    Guid Id,
    Guid AppraisalId,
    string ProjectType,
    string? ProjectName,
    string? ProjectDescription,
    string? Developer,
    string? ProjectSaleLaunchDate,
    decimal? LandAreaRai,
    decimal? LandAreaNgan,
    decimal? LandAreaSquareWa,
    int? UnitForSaleCount,
    int? NumberOfPhase,
    string? LandOffice,
    decimal? Latitude,
    decimal? Longitude,
    string? SubDistrict,
    string? District,
    string? Province,
    string? Postcode,
    string? HouseNumber,
    string? Road,
    string? Soi,
    List<string>? Utilities,
    string? UtilitiesOther,
    List<string>? Facilities,
    string? FacilitiesOther,
    string? Remark,
    string? BuiltOnTitleDeedNumber,
    DateTime? LicenseExpirationDate
);
