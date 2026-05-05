namespace Appraisal.Application.Features.Project.GetProject;

/// <summary>Handler for getting the project for an appraisal.</summary>
public class GetProjectQueryHandler(
    IProjectRepository projectRepository
) : IQueryHandler<GetProjectQuery, GetProjectResult?>
{
    public async Task<GetProjectResult?> Handle(
        GetProjectQuery query,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByAppraisalIdAsync(query.AppraisalId, cancellationToken);

        if (project is null)
            return null;

        return new GetProjectResult(
            Id: project.Id,
            AppraisalId: project.AppraisalId,
            ProjectType: project.ProjectType,
            ProjectName: project.ProjectName,
            ProjectDescription: project.ProjectDescription,
            Developer: project.Developer,
            ProjectSaleLaunchDate: project.ProjectSaleLaunchDate,
            LandAreaRai: project.LandAreaRai,
            LandAreaNgan: project.LandAreaNgan,
            LandAreaSquareWa: project.LandAreaSquareWa,
            UnitForSaleCount: project.UnitForSaleCount,
            NumberOfPhase: project.NumberOfPhase,
            LandOffice: project.LandOffice,
            Latitude: project.Coordinates?.Latitude,
            Longitude: project.Coordinates?.Longitude,
            SubDistrict: project.Address?.SubDistrict,
            District: project.Address?.District,
            Province: project.Address?.Province,
            Postcode: project.Postcode,
            HouseNumber: project.HouseNumber,
            Road: project.Road,
            Soi: project.Soi,
            Utilities: project.Utilities,
            UtilitiesOther: project.UtilitiesOther,
            Facilities: project.Facilities,
            FacilitiesOther: project.FacilitiesOther,
            Remark: project.Remark,
            BuiltOnTitleDeedNumber: project.BuiltOnTitleDeedNumber,
            LicenseExpirationDate: project.LicenseExpirationDate);
    }
}
