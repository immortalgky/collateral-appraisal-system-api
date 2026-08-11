using Appraisal.Domain.Projects;

namespace Appraisal.Application.Services;

/// <summary>
/// Create-or-update implementation for the Project aggregate. Shared by SaveProject
/// (final) and SaveProjectDraft handlers — the difference between them is only the
/// validator that runs in the MediatR pipeline before this is reached.
/// </summary>
public class ProjectSaveService(
    IAppraisalUnitOfWork unitOfWork,
    IAppraisalRepository appraisalRepository,
    IProjectRepository projectRepository
) : IProjectSaveService
{
    public async Task<Guid> SaveAsync(SaveProjectData data, CancellationToken cancellationToken)
    {
        // Confirm the appraisal exists (throws AppraisalNotFoundException if not found)
        _ = await appraisalRepository.GetByIdAsync(data.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(data.AppraisalId);

        // Build value objects
        GpsCoordinate? coordinates = null;
        if (data.Latitude.HasValue && data.Longitude.HasValue)
            coordinates = GpsCoordinate.Create(data.Latitude.Value, data.Longitude.Value);

        Address? address = null;
        if (data.SubDistrict is not null || data.District is not null || data.Province is not null)
            address = Address.Create(data.SubDistrict, data.District, data.Province);

        var project = await projectRepository.GetByAppraisalIdAsync(data.AppraisalId, cancellationToken);

        if (project is null)
        {
            project = Project.Create(
                appraisalId: data.AppraisalId,
                projectType: ProjectType.FromString(data.ProjectType),
                projectName: data.ProjectName,
                projectDescription: data.ProjectDescription,
                developer: data.Developer,
                projectSaleLaunchDate: data.ProjectSaleLaunchDate,
                landAreaRai: data.LandAreaRai,
                landAreaNgan: data.LandAreaNgan,
                landAreaSquareWa: data.LandAreaSquareWa,
                unitForSaleCount: data.UnitForSaleCount,
                numberOfPhase: data.NumberOfPhase,
                landOffice: data.LandOffice,
                coordinates: coordinates,
                address: address,
                postcode: data.Postcode,
                houseNumber: data.HouseNumber,
                road: data.Road,
                soi: data.Soi,
                utilities: data.Utilities,
                utilitiesOther: data.UtilitiesOther,
                facilities: data.Facilities,
                facilitiesOther: data.FacilitiesOther,
                remark: data.Remark,
                builtOnTitleDeedNumber: data.BuiltOnTitleDeedNumber,
                licenseExpirationDate: data.LicenseExpirationDate);

            projectRepository.Add(project);
        }
        else
        {
            // ProjectType is immutable after creation
            project.Update(
                projectName: data.ProjectName,
                projectDescription: data.ProjectDescription,
                developer: data.Developer,
                projectSaleLaunchDate: data.ProjectSaleLaunchDate,
                landAreaRai: data.LandAreaRai,
                landAreaNgan: data.LandAreaNgan,
                landAreaSquareWa: data.LandAreaSquareWa,
                unitForSaleCount: data.UnitForSaleCount,
                numberOfPhase: data.NumberOfPhase,
                landOffice: data.LandOffice,
                coordinates: coordinates,
                address: address,
                postcode: data.Postcode,
                houseNumber: data.HouseNumber,
                road: data.Road,
                soi: data.Soi,
                utilities: data.Utilities,
                utilitiesOther: data.UtilitiesOther,
                facilities: data.Facilities,
                facilitiesOther: data.FacilitiesOther,
                remark: data.Remark,
                builtOnTitleDeedNumber: data.BuiltOnTitleDeedNumber,
                licenseExpirationDate: data.LicenseExpirationDate);
        }

        // Persist. The outer TransactionalBehavior (both commands are ITransactionalCommand)
        // also saves+commits, so this is technically redundant — but it's kept to match the
        // original handler exactly and to flush within the single open transaction (no nesting).
        // The returned Id is assigned in-memory by Project.Create (Guid.CreateVersion7), not here.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return project.Id;
    }
}
