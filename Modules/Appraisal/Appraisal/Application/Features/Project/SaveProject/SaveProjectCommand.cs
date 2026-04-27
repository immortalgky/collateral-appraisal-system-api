namespace Appraisal.Application.Features.Project.SaveProject;

/// <summary>
/// Command to save (create or update) a project for an appraisal.
/// If no project exists for the appraisalId, a new one is created.
/// If one already exists, it is updated (ProjectType cannot be changed after creation).
/// </summary>
public record SaveProjectCommand(
    Guid AppraisalId,
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
) : ICommand<SaveProjectResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
