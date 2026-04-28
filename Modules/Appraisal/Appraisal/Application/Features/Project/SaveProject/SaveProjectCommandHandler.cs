namespace Appraisal.Application.Features.Project.SaveProject;

/// <summary>
/// Handler for saving (create or update) a Project aggregate.
/// Looks up an existing project by AppraisalId; creates a new one if none exists.
/// The handler confirms the appraisal exists before proceeding.
/// </summary>
public class SaveProjectCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IAppraisalRepository appraisalRepository,
    IProjectRepository projectRepository
) : ICommandHandler<SaveProjectCommand, SaveProjectResult>
{
    public async Task<SaveProjectResult> Handle(
        SaveProjectCommand command,
        CancellationToken cancellationToken)
    {
        // Confirm the appraisal exists (throws AppraisalNotFoundException if not found)
        _ = await appraisalRepository.GetByIdAsync(command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // Build value objects
        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);

        AdministrativeAddress? address = null;
        if (command.SubDistrict is not null || command.District is not null || command.Province is not null)
            address = AdministrativeAddress.Create(
                command.SubDistrict, command.District, command.Province, command.LandOffice);

        // Try to load an existing project
        var project = await projectRepository.GetByAppraisalIdAsync(command.AppraisalId, cancellationToken);

        if (project is null)
        {
            // Create
            project = Domain.Projects.Project.Create(
                appraisalId: command.AppraisalId,
                projectType: command.ProjectType,
                projectName: command.ProjectName,
                projectDescription: command.ProjectDescription,
                developer: command.Developer,
                projectSaleLaunchDate: command.ProjectSaleLaunchDate,
                landAreaRai: command.LandAreaRai,
                landAreaNgan: command.LandAreaNgan,
                landAreaWa: command.LandAreaWa,
                unitForSaleCount: command.UnitForSaleCount,
                numberOfPhase: command.NumberOfPhase,
                landOffice: command.LandOffice,
                coordinates: coordinates,
                address: address,
                postcode: command.Postcode,
                locationNumber: command.LocationNumber,
                road: command.Road,
                soi: command.Soi,
                utilities: command.Utilities,
                utilitiesOther: command.UtilitiesOther,
                facilities: command.Facilities,
                facilitiesOther: command.FacilitiesOther,
                remark: command.Remark,
                builtOnTitleDeedNumber: command.BuiltOnTitleDeedNumber,
                licenseExpirationDate: command.LicenseExpirationDate);

            projectRepository.Add(project);
        }
        else
        {
            // Update (ProjectType is immutable after creation)
            project.Update(
                projectName: command.ProjectName,
                projectDescription: command.ProjectDescription,
                developer: command.Developer,
                projectSaleLaunchDate: command.ProjectSaleLaunchDate,
                landAreaRai: command.LandAreaRai,
                landAreaNgan: command.LandAreaNgan,
                landAreaWa: command.LandAreaWa,
                unitForSaleCount: command.UnitForSaleCount,
                numberOfPhase: command.NumberOfPhase,
                landOffice: command.LandOffice,
                coordinates: coordinates,
                address: address,
                postcode: command.Postcode,
                locationNumber: command.LocationNumber,
                road: command.Road,
                soi: command.Soi,
                utilities: command.Utilities,
                utilitiesOther: command.UtilitiesOther,
                facilities: command.Facilities,
                facilitiesOther: command.FacilitiesOther,
                remark: command.Remark,
                builtOnTitleDeedNumber: command.BuiltOnTitleDeedNumber,
                licenseExpirationDate: command.LicenseExpirationDate);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveProjectResult(project.Id);
    }
}
