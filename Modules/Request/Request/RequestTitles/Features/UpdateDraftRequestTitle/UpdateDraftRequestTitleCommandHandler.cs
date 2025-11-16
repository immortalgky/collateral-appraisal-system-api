namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public class UpdateDraftRequestCommandTitleHandler(IRequestTitleRepository requestTitleRepository)
    : ICommandHandler<UpdateDraftRequestTitleCommand, UpdateDraftRequestTitleResult>
{
    public async Task<UpdateDraftRequestTitleResult> Handle(UpdateDraftRequestTitleCommand command, CancellationToken cancellationToken)
    {
        var requestTitle = await requestTitleRepository.GetByIdAsync(command.Id, cancellationToken);

        if (requestTitle is null || requestTitle.RequestId != command.RequestId)
        {
            throw new RequestTitleNotFoundException(command.Id);
        }

        requestTitle.UpdateDraftDetails(
            command.CollateralType,
            command.CollateralStatus,
            DtoExtensions.ToDomain(command.TitleDeedInfoDto),
            DtoExtensions.ToDomain(command.SurveyInfoDto),
            DtoExtensions.ToDomain(command.LandAreaDto),
            command.OwnerName,
            command.RegistrationNumber,
            DtoExtensions.ToDomain(command.VehicleDto),
            DtoExtensions.ToDomain(command.MachineDto),
            DtoExtensions.ToDomain(command.BuildingInfoDto),
            DtoExtensions.ToDomain(command.CondoInfoDto),
            DtoExtensions.ToDomain(command.TitleAddress),
            DtoExtensions.ToDomain(command.DopaAddress),
            command.Notes
        );

        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        return new UpdateDraftRequestTitleResult(true);
    }
}
