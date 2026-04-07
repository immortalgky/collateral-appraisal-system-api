namespace Appraisal.Application.Features.BlockCondo.SaveCondoProject;

/// <summary>
/// Handler for saving a condo project
/// </summary>
public class SaveCondoProjectCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IAppraisalRepository appraisalRepository
) : ICommandHandler<SaveCondoProjectCommand, SaveCondoProjectResult>
{
    public async Task<SaveCondoProjectResult> Handle(
        SaveCondoProjectCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate with block condo data
        var appraisal = await appraisalRepository.GetByIdWithCondoDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Build value objects
        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);

        AdministrativeAddress? address = null;
        if (command.SubDistrict is not null || command.District is not null ||
            command.Province is not null)
            address = AdministrativeAddress.Create(
                command.SubDistrict,
                command.District,
                command.Province,
                command.LandOffice);

        // 3. Execute domain operation (creates if null, updates either way)
        var project = appraisal.SetCondoProject(
            command.ProjectName,
            command.ProjectDescription,
            command.Developer,
            command.ProjectSaleLaunchDate,
            command.LandAreaRai,
            command.LandAreaNgan,
            command.LandAreaWa,
            command.UnitForSaleCount,
            command.NumberOfPhase,
            command.LandOffice,
            command.ProjectType,
            command.BuiltOnTitleDeedNumber,
            coordinates,
            address,
            command.Postcode,
            command.LocationNumber,
            command.Road,
            command.Soi,
            command.Utilities,
            command.UtilitiesOther,
            command.Facilities,
            command.FacilitiesOther,
            command.Remark);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveCondoProjectResult(project.Id);
    }
}
