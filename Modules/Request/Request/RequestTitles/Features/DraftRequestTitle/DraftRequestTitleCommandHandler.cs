namespace Request.RequestTitles.Features.DraftRequestTitle;

public class DraftRequestTitleCommandHandler(IRequestTitleRepository requestTitleRepository) : ICommandHandler<DraftRequestTitleCommand, DraftRequestTitleResult>
{
    public async Task<DraftRequestTitleResult> Handle(DraftRequestTitleCommand command, CancellationToken cancellationToken)
    {
        var requestTitle = RequestTitle.Create(
            command.RequestId,
            command.CollateralType,
            command.CollateralStatus,
            DtoExtensions.ToDomain(command.TitleDeedInfoDto),
            DtoExtensions.ToDomain(command.SurveyInfoDto),
            DtoExtensions.ToDomain(command.LandAreaDto),
            command.OwnerName,
            command.RegistrationNumber,
            DtoExtensions.ToDomain(command.VehicleDto),
            DtoExtensions.ToDomain(command.MachineryDto),
            DtoExtensions.ToDomain(command.BuildingInfoDto),
            DtoExtensions.ToDomain(command.CondoInfoDto),
            DtoExtensions.ToDomain(command.TitleAddress),
            DtoExtensions.ToDomain(command.DopaAddress),
            command.Notes
        );

        await requestTitleRepository.AddAsync(requestTitle, cancellationToken);
        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        return new DraftRequestTitleResult(requestTitle.Id);
    }
}
