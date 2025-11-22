namespace Request.RequestTitles.Features.DraftRequestTitle;

public class DraftRequestTitleCommandHandler(IRequestTitleRepository requestTitleRepository, IRequestRepository requestRepository) : ICommandHandler<DraftRequestTitleCommand, DraftRequestTitleResult>
{
    public async Task<DraftRequestTitleResult> Handle(DraftRequestTitleCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.RequestId);

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
        };;
        
        var requestTitle = RequestTitleFactory.Create(requestTitleData.CollateralType).Draft(requestTitleData);

        await requestTitleRepository.AddAsync(requestTitle, cancellationToken);
        
        await requestTitleRepository.SaveChangesAsync(cancellationToken);
        
        return new DraftRequestTitleResult(requestTitle.Id);
    }
}
