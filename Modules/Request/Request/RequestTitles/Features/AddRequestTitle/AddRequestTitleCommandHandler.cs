namespace Request.RequestTitles.Features.AddRequestTitle;

public class AddRequestTitleCommandHandler(IRequestTitleRepository requestTitleRepository)
    : ICommandHandler<AddRequestTitleCommand, AddRequestTitleResult>
{
    public async Task<AddRequestTitleResult> Handle(AddRequestTitleCommand command,
        CancellationToken cancellationToken)
    {

        var request = await requestTitleRepository.GetByIdAsync(command.RequestId);

        if (request is null)
            throw new RequestNotFoundException(command.RequestId);

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

        return new AddRequestTitleResult(requestTitle.Id);
    }
}