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
                isUnderConstruction: data.IsUnderConstruction,
                constructionProgressPercent: data.ConstructionProgressPercent,
                builtOnTitleDeedNumber: data.BuiltOnTitleDeedNumber,
                licenseExpirationDate: data.LicenseExpirationDate);

            projectRepository.Add(project);
        }
        else
        {
            // ProjectType is immutable after creation, and a payload that disagrees is IGNORED,
            // not rejected. Rejecting it looks right and was tried: it bricks Land. The frontend
            // offers "L" in the change-type dialog but has no route for it -- targetRoute() sends
            // every non-condo type to block-village, which router.tsx renders as
            // <BlockProjectPage projectType="LB" /> -- and ProjectInfoTab stamps that route prop
            // onto every payload. So a Land project posts "LB" against a stored "L" on every save,
            // and a strict comparison would 400 it forever with no way out of the UI. Restore the
            // comparison only together with an "L" route on the frontend.
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
                isUnderConstruction: data.IsUnderConstruction,
                constructionProgressPercent: data.ConstructionProgressPercent,
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
