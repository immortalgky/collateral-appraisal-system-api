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

        var newProjectType = Domain.Projects.ProjectType.FromString(command.NewProjectType);

        // Guard: same type is a no-op and should be rejected
        if (existing.ProjectType == newProjectType)
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
        var landAreaSquareWa       = existing.LandAreaSquareWa;
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
        var houseNumber      = existing.HouseNumber;
        var road             = existing.Road;
        var soi              = existing.Soi;
        // List<string> collections — copied to new lists so the new aggregate owns its own instances
        var utilities        = existing.Utilities is not null ? new List<string>(existing.Utilities) : null;
        var utilitiesOther   = existing.UtilitiesOther;
        var facilities       = existing.Facilities is not null ? new List<string>(existing.Facilities) : null;
        var facilitiesOther  = existing.FacilitiesOther;
        var remark           = existing.Remark;
        // Construction progress IS carried across, unlike the type-specific fields below.
        //
        // Clearing it was tried and is worse. The pair describes the development, not its
        // inventory, and a cleared pair does not read downstream as "unknown" — NULL on a
        // completed appraisal is reported to LOS as 100% under the accepted rule. So the choice is
        // between carrying a figure an appraiser actually observed and minting one nobody ever
        // did. A possibly-stale 45 beats a fabricated 100. The realistic sequence is not exotic:
        // record 45%, change type mid-appraisal, rebuild the inventory, save without re-ticking
        // the box, complete — and LOS is told a half-built development is finished.
        //
        // The cost of carrying, stated plainly: the inventory that 45% described is gone, so the
        // figure can outlive what it measured, and Update never writes the flag back to NULL — the
        // only ways out are unticking (which stores false and reports 100) or a round trip through
        // Land. Both directions have a bad case; this one is bad with a number a human once
        // observed, the other is bad with one nobody ever did.
        //
        // It does not survive a round trip THROUGH Land: Create drops the pair for a type that
        // carries no structures, so LB -> L -> LB comes back empty. That is proportionate -- the
        // same switch destroys every tower, model and unit both ways, so the appraiser is rebuilding
        // the project from nothing and re-ticking a checkbox is the smallest part of it.
        //
        // Still worth doing separately: this handler has no appraisal-status guard, so a direct
        // call against a COMPLETED appraisal also destroys its whole tower/model/unit inventory.
        // The frontend already blocks it (ProjectTypePill disables on read-only), so a guard would
        // break no caller.
        //
        // RejectClosedAppraisalWriteFilter is the module's mechanism for this and was tried here.
        // It is not enough on its own, for two reasons found in review: fifteen block write routes
        // lack it (SaveProjectUnitPrices among them, which writes the very figures the LOS feed
        // reports as appraised and forced-sale value), so guarding three of them just moves the
        // hole; and the filter's 409 sends the caller to the data-correction screen, which is keyed
        // on an AppraisalProperty — a block has none, so a completed block would become uneditable
        // through every API with nowhere to go. The guard belongs with a project-level correction
        // path, as one piece of work.
        var isUnderConstruction = existing.IsUnderConstruction;
        var constructionPct     = existing.ConstructionProgressPercent;
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

            Address? address = null;
            if (subDistrict is not null || district is not null || province is not null)
                address = Address.Create(subDistrict, district, province);

            var newProject = DomainProject.Create(
                appraisalId:          appraisalId,
                projectType:          newProjectType,
                projectName:          projectName,
                projectDescription:   projectDesc,
                developer:            developer,
                projectSaleLaunchDate: saleLaunchDate,
                landAreaRai:          landAreaRai,
                landAreaNgan:         landAreaNgan,
                landAreaSquareWa:           landAreaSquareWa,
                unitForSaleCount:     unitForSaleCount,
                numberOfPhase:        numberOfPhase,
                landOffice:           landOffice,
                coordinates:          coordinates,
                address:              address,
                postcode:             postcode,
                houseNumber:          houseNumber,
                road:                 road,
                soi:                  soi,
                utilities:            utilities,
                utilitiesOther:       utilitiesOther,
                facilities:           facilities,
                facilitiesOther:      facilitiesOther,
                remark:               remark,
                isUnderConstruction:  isUnderConstruction,
                constructionProgressPercent: constructionPct,
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
            LandAreaSquareWa:            project.LandAreaSquareWa,
            UnitForSaleCount:      project.UnitForSaleCount,
            NumberOfPhase:         project.NumberOfPhase,
            LandOffice:            project.LandOffice,
            Latitude:              project.Coordinates?.Latitude,
            Longitude:             project.Coordinates?.Longitude,
            SubDistrict:           project.Address?.SubDistrict,
            District:              project.Address?.District,
            Province:              project.Address?.Province,
            Postcode:              project.Postcode,
            HouseNumber:           project.HouseNumber,
            Road:                  project.Road,
            Soi:                   project.Soi,
            Utilities:             project.Utilities,
            UtilitiesOther:        project.UtilitiesOther,
            Facilities:            project.Facilities,
            FacilitiesOther:       project.FacilitiesOther,
            Remark:                project.Remark,
            IsUnderConstruction: project.IsUnderConstruction,
            ConstructionProgressPercent: project.ConstructionProgressPercent,
            BuiltOnTitleDeedNumber: project.BuiltOnTitleDeedNumber,
            LicenseExpirationDate:  project.LicenseExpirationDate);
}
