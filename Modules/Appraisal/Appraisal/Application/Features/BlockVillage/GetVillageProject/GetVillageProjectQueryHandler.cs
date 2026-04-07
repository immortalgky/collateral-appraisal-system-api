namespace Appraisal.Application.Features.BlockVillage.GetVillageProject;

public class GetVillageProjectQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetVillageProjectQuery, GetVillageProjectResult>
{
    public async Task<GetVillageProjectResult> Handle(
        GetVillageProjectQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithVillageDataAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        var project = appraisal.VillageProject;
        if (project is null)
            return null!;

        return new GetVillageProjectResult(
            Id: project.Id,
            AppraisalId: project.AppraisalId,
            ProjectName: project.ProjectName,
            ProjectDescription: project.ProjectDescription,
            Developer: project.Developer,
            ProjectSaleLaunchDate: project.ProjectSaleLaunchDate,
            LandAreaRai: project.LandAreaRai,
            LandAreaNgan: project.LandAreaNgan,
            LandAreaWa: project.LandAreaWa,
            UnitForSaleCount: project.UnitForSaleCount,
            NumberOfPhase: project.NumberOfPhase,
            LandOffice: project.LandOffice,
            ProjectType: project.ProjectType,
            LicenseExpirationDate: project.LicenseExpirationDate,
            Latitude: project.Coordinates?.Latitude,
            Longitude: project.Coordinates?.Longitude,
            SubDistrict: project.Address?.SubDistrict,
            District: project.Address?.District,
            Province: project.Address?.Province,
            Postcode: project.Postcode,
            LocationNumber: project.LocationNumber,
            Road: project.Road,
            Soi: project.Soi,
            Utilities: project.Utilities,
            UtilitiesOther: project.UtilitiesOther,
            Facilities: project.Facilities,
            FacilitiesOther: project.FacilitiesOther,
            Remark: project.Remark);
    }
}
