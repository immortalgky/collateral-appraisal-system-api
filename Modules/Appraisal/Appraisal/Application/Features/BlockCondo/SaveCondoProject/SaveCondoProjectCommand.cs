namespace Appraisal.Application.Features.BlockCondo.SaveCondoProject;

/// <summary>
/// Command to save (create or update) a condo project for an appraisal
/// </summary>
public record SaveCondoProjectCommand(
    Guid AppraisalId,
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
    string? ProjectType = null,
    string? BuiltOnTitleDeedNumber = null,
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
    string? Remark = null
) : ICommand<SaveCondoProjectResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
