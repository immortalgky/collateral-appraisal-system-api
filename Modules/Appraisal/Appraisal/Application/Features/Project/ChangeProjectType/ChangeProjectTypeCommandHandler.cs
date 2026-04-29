using Appraisal.Application.Configurations;
using DomainProject = Appraisal.Domain.Projects.Project;

namespace Appraisal.Application.Features.Project.ChangeProjectType;

/// <summary>
/// Atomically replaces the Project aggregate with a new one of a different ProjectType,
/// preserving all shared fields and dropping type-specific fields and child collections.
///
/// Two-phase transaction:
///   Phase 1 — Remove existing Project and SaveChanges (clears the unique AppraisalId index).
///   Phase 2 — Create and Add the new Project and SaveChanges.
/// Both phases run inside a single database transaction so the appraisal is never left
/// without a project if the insert fails.
/// </summary>
public class ChangeProjectTypeCommandHandler(
    IProjectRepository projectRepository,
    IAppraisalUnitOfWork unitOfWork,
    AppraisalDbContext dbContext
) : ICommandHandler<ChangeProjectTypeCommand, ChangeProjectTypeResult>
{
    public async Task<ChangeProjectTypeResult> Handle(
        ChangeProjectTypeCommand command,
        CancellationToken cancellationToken)
    {
        // Load existing project
        var existing = await projectRepository.GetByAppraisalIdAsync(command.AppraisalId, cancellationToken)
            ?? throw new NotFoundException(
                $"Project not found for appraisal {command.AppraisalId}. Create a project before changing its type.");

        // Guard: same type is a no-op and should be rejected
        if (existing.ProjectType == command.NewProjectType)
            throw new BadRequestException(
                $"Project type is already '{existing.ProjectType}'. No change was made.");

        // Snapshot all shared primitive fields before removing the entity.
        // Value objects / owned entity instances (GpsCoordinate, AdministrativeAddress) are NOT
        // captured by reference — after Remove()+SaveChanges() EF marks owned instances as
        // Detached, and re-attaching the same CLR instance to a new aggregate confuses the
        // change tracker. Instead we snapshot the primitives and reconstruct fresh instances below.
        var appraisalId      = existing.AppraisalId;
        var projectName      = existing.ProjectName;
        var projectDesc      = existing.ProjectDescription;
        var developer        = existing.Developer;
        var saleLaunchDate   = existing.ProjectSaleLaunchDate;
        var landAreaRai      = existing.LandAreaRai;
        var landAreaNgan     = existing.LandAreaNgan;
        var landAreaWa       = existing.LandAreaWa;
        var unitForSaleCount = existing.UnitForSaleCount;
        var numberOfPhase    = existing.NumberOfPhase;
        var landOffice       = existing.LandOffice;
        // GpsCoordinate primitives — reconstructed fresh below
        var latitude         = existing.Coordinates?.Latitude;
        var longitude        = existing.Coordinates?.Longitude;
        // AdministrativeAddress primitives — reconstructed fresh below
        var subDistrict      = existing.Address?.SubDistrict;
        var district         = existing.Address?.District;
        var province         = existing.Address?.Province;
        var postcode         = existing.Postcode;
        var locationNumber   = existing.LocationNumber;
        var road             = existing.Road;
        var soi              = existing.Soi;
        // List<string> collections — copied to new lists so the new aggregate owns its own instances
        var utilities        = existing.Utilities is not null ? new List<string>(existing.Utilities) : null;
        var utilitiesOther   = existing.UtilitiesOther;
        var facilities       = existing.Facilities is not null ? new List<string>(existing.Facilities) : null;
        var facilitiesOther  = existing.FacilitiesOther;
        var remark           = existing.Remark;
        // Type-specific fields are intentionally NOT snapshotted; they are reset for the new type.

        // Begin explicit transaction — the handler owns both SaveChanges calls
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Phase 1: delete the old project (cascade removes all children in the DB)
            dbContext.Projects.Remove(existing);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            // Phase 2: reconstruct owned value objects from primitives (never reuse the detached
            // instances from the removed aggregate), then create and persist the new project.
            GpsCoordinate? coordinates = null;
            if (latitude.HasValue && longitude.HasValue)
                coordinates = GpsCoordinate.Create(latitude, longitude);

            AdministrativeAddress? address = null;
            if (subDistrict is not null || district is not null || province is not null)
                address = AdministrativeAddress.Create(subDistrict, district, province, landOffice);

            var newProject = DomainProject.Create(
                appraisalId:          appraisalId,
                projectType:          command.NewProjectType,
                projectName:          projectName,
                projectDescription:   projectDesc,
                developer:            developer,
                projectSaleLaunchDate: saleLaunchDate,
                landAreaRai:          landAreaRai,
                landAreaNgan:         landAreaNgan,
                landAreaWa:           landAreaWa,
                unitForSaleCount:     unitForSaleCount,
                numberOfPhase:        numberOfPhase,
                landOffice:           landOffice,
                coordinates:          coordinates,
                address:              address,
                postcode:             postcode,
                locationNumber:       locationNumber,
                road:                 road,
                soi:                  soi,
                utilities:            utilities,
                utilitiesOther:       utilitiesOther,
                facilities:           facilities,
                facilitiesOther:      facilitiesOther,
                remark:               remark,
                builtOnTitleDeedNumber: null,   // type-specific — reset
                licenseExpirationDate:  null);  // type-specific — reset

            projectRepository.Add(newProject);

            // CommitTransactionAsync calls SaveChangesAsync internally then commits
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            return MapToResult(newProject);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private static ChangeProjectTypeResult MapToResult(DomainProject project) =>
        new(
            Id:                    project.Id,
            AppraisalId:           project.AppraisalId,
            ProjectType:           project.ProjectType,
            ProjectName:           project.ProjectName,
            ProjectDescription:    project.ProjectDescription,
            Developer:             project.Developer,
            ProjectSaleLaunchDate: project.ProjectSaleLaunchDate,
            LandAreaRai:           project.LandAreaRai,
            LandAreaNgan:          project.LandAreaNgan,
            LandAreaWa:            project.LandAreaWa,
            UnitForSaleCount:      project.UnitForSaleCount,
            NumberOfPhase:         project.NumberOfPhase,
            LandOffice:            project.LandOffice,
            Latitude:              project.Coordinates?.Latitude,
            Longitude:             project.Coordinates?.Longitude,
            SubDistrict:           project.Address?.SubDistrict,
            District:              project.Address?.District,
            Province:              project.Address?.Province,
            Postcode:              project.Postcode,
            LocationNumber:        project.LocationNumber,
            Road:                  project.Road,
            Soi:                   project.Soi,
            Utilities:             project.Utilities,
            UtilitiesOther:        project.UtilitiesOther,
            Facilities:            project.Facilities,
            FacilitiesOther:       project.FacilitiesOther,
            Remark:                project.Remark,
            BuiltOnTitleDeedNumber: project.BuiltOnTitleDeedNumber,
            LicenseExpirationDate:  project.LicenseExpirationDate);
}
