namespace Appraisal.Application.Features.BlockCondo.GetCondoProject;

/// <summary>
/// Handler for getting the condo project
/// </summary>
public class GetCondoProjectQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetCondoProjectQuery, GetCondoProjectResult>
{
    public async Task<GetCondoProjectResult> Handle(
        GetCondoProjectQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate with block condo data
        var appraisal = await appraisalRepository.GetByIdWithCondoDataAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        // 2. Get the condo project
        var project = appraisal.CondoProject;
        if (project is null)
            return null!;

        // 3. Map to result with flattened coordinates/address
        return new GetCondoProjectResult(
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
            BuiltOnTitleDeedNumber: project.BuiltOnTitleDeedNumber,
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
