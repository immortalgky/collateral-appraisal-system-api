namespace Appraisal.Application.Features.Project.SaveProjectDraft;

/// <summary>
/// Command to save (create or update) a project as a DRAFT. Identical fields to
/// SaveProjectCommand; the only difference is the lenient validator. Shares the same
/// create-or-update logic via IProjectSaveService.
/// </summary>
public record SaveProjectDraftCommand(
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
) : ICommand<SaveProjectDraftResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
