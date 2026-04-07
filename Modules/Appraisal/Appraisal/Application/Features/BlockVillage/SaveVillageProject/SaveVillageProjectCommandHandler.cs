namespace Appraisal.Application.Features.BlockVillage.SaveVillageProject;

public class SaveVillageProjectCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IAppraisalRepository appraisalRepository
) : ICommandHandler<SaveVillageProjectCommand, SaveVillageProjectResult>
{
    public async Task<SaveVillageProjectResult> Handle(
        SaveVillageProjectCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithVillageDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);

        AdministrativeAddress? address = null;
        if (command.SubDistrict is not null || command.District is not null || command.Province is not null)
            address = AdministrativeAddress.Create(command.SubDistrict, command.District, command.Province, command.LandOffice);

        var project = appraisal.SetVillageProject(
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
            command.LicenseExpirationDate,
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

        return new SaveVillageProjectResult(project.Id);
    }
}
