namespace Request.RequestTitles.Features.CreateRequestTitle;

public class CreateRequestTitleCommandHandler(IRequestTitleRepository requestTitleRepository, IRequestRepository requestRepository)
    : ICommandHandler<CreateRequestTitleCommand, CreateRequestTitleResult>
{
    public async Task<CreateRequestTitleResult> Handle(CreateRequestTitleCommand command,
        CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.RequestId, cancellationToken);

        if (request is null)
            throw new RequestNotFoundException(command.RequestId);

        var requestTitle = RequestTitle.Create(
            new RequestTitleData(
                command.RequestId,
                command.CollateralType, 
                command.CollateralStatus, 
                DtoExtensions.ToDomain(command.TitleDeedInfoDto), 
                DtoExtensions.ToDomain(command.SurveyInfoDto), 
                DtoExtensions.ToDomain(command.LandAreaDto), 
                command.OwnerName, 
                command.RegistrationNo, 
                DtoExtensions.ToDomain(command.VehicleDto), 
                DtoExtensions.ToDomain(command.MachineDto), 
                DtoExtensions.ToDomain(command.BuildingInfoDto), 
                DtoExtensions.ToDomain(command.CondoInfoDto), 
                DtoExtensions.ToDomain(command.TitleAddress), 
                DtoExtensions.ToDomain(command.DopaAddress), 
                command.Notes
                )
            );

        await requestTitleRepository.AddAsync(requestTitle, cancellationToken);

        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        return new CreateRequestTitleResult(requestTitle.Id);
    }
}