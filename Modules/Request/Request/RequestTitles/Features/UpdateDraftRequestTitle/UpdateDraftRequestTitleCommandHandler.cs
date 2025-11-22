namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public class UpdateDraftRequestCommandTitleHandler(IRequestTitleRepository requestTitleRepository)
    : ICommandHandler<UpdateDraftRequestTitleCommand, UpdateDraftRequestTitleResult>
{
    public async Task<UpdateDraftRequestTitleResult> Handle(UpdateDraftRequestTitleCommand command, CancellationToken cancellationToken)
    {
        var requestTitle = await requestTitleRepository.GetByIdAsync(command.TitleId, cancellationToken);
        
        if (requestTitle.RequestId != command.RequestId)
            throw new Exception("New RequestId does not existed");
        
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
        
        requestTitle.UpdateDraft(requestTitleData);

        await requestTitleRepository.SaveChangesAsync(cancellationToken);
        
        return new UpdateDraftRequestTitleResult(requestTitle.Id);
    }
}
