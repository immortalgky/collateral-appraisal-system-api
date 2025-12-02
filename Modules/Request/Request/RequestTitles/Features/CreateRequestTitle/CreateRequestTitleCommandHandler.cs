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
        var requestTitle = RequestTitleFactory.Create(requestTitleData.CollateralType).Create(requestTitleData);

        requestTitle.AddDomainEvent(new RequestTitleCreatedEvent(requestTitle.RequestId, requestTitle));
        
        await requestTitleRepository.AddAsync(requestTitle, cancellationToken);
        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        var result = new CreateRequestTitleResult
        {
            Id = requestTitle.Id,
            RequestId = requestTitle.RequestId,
            CollateralType = requestTitle.CollateralType,
            CollateralStatus = requestTitle.CollateralStatus,
            TitleDeedInfoDto = requestTitle.TitleDeedInfo.Adapt<TitleDeedInfoDto>(),
            SurveyInfoDto = requestTitle.SurveyInfo.Adapt<SurveyInfoDto>(),
            LandAreaDto = requestTitle.LandArea.Adapt<LandAreaDto>(),
            OwnerName = requestTitle.OwnerName,
            RegistrationNo = requestTitle.RegistrationNo,
            VehicleInfoDto = requestTitle.VehicleInfo.Adapt<VehicleDto>(),
            MachineInfoDto = requestTitle.MachineInfo.Adapt<MachineDto>(),
            BuildingInfoDto = requestTitle.BuildingInfo.Adapt<BuildingInfoDto>(),
            CondoInfoDto = requestTitle.CondoInfo.Adapt<CondoInfoDto>(),
            TitleAddressDto = requestTitle.TitleAddress.Adapt<AddressDto>(),
            DopaAddressDto = requestTitle.DopaAddress.Adapt<AddressDto>(),
            Notes = command.Notes
        };

        return result;
    }
}