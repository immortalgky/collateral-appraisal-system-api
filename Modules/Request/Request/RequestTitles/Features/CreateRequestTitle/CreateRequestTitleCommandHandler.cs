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

        var requestTitleData = new RequestTitleData
        {
            RequestId = command.RequestId,
            CollateralType = command.CollateralType,
            CollateralStatus = command.CollateralStatus,
            TitleDeedInfo = DtoExtensions.ToDomain(command.TitleDeedInfoDto),
            SurveyInfo = DtoExtensions.ToDomain(command.SurveyInfoDto),
            LandArea = DtoExtensions.ToDomain(command.LandAreaDto),
            OwnerName = command.OwnerName,
            RegistrationNo = command.RegistrationNo,
            VehicleInfo = DtoExtensions.ToDomain(command.VehicleDto),
            MachineInfo = DtoExtensions.ToDomain(command.MachineDto),
            BuildingInfo = DtoExtensions.ToDomain(command.BuildingInfoDto),
            CondoInfo = DtoExtensions.ToDomain(command.CondoInfoDto),
            TitleAddress = DtoExtensions.ToDomain(command.TitleAddress),
            DopaAddress = DtoExtensions.ToDomain(command.DopaAddress),
            Notes = command.Notes
        };

        var requestTitle = RequestTitleFactory.Create(requestTitleData);

        await requestTitleRepository.AddAsync(requestTitle, cancellationToken);

        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        return new CreateRequestTitleResult(requestTitle.Id);
    }
}