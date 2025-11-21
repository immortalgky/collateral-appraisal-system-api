namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public class UpdateDraftRequestCommandTitleHandler(IRequestTitleRepository requestTitleRepository)
    : ICommandHandler<UpdateDraftRequestTitleCommand, UpdateDraftRequestTitleResult>
{
    public async Task<UpdateDraftRequestTitleResult> Handle(UpdateDraftRequestTitleCommand command, CancellationToken cancellationToken)
    {
        var requestTitle = await requestTitleRepository.GetByIdAsync(command.TitleId, cancellationToken);
        
        if (requestTitle.RequestId != command.RequestId)
            throw new Exception("New RequestId does not existed");
        
        requestTitle.UpdateDetails(new RequestTitleData(
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
        );;

        await requestTitleRepository.SaveChangesAsync(cancellationToken);
        
        return new UpdateDraftRequestTitleResult(requestTitle.Id);
    }
}
