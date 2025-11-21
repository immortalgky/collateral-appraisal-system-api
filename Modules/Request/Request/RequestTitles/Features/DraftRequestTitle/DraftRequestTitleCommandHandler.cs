namespace Request.RequestTitles.Features.DraftRequestTitle;

public class DraftRequestTitleCommandHandler(IRequestTitleRepository requestTitleRepository, IRequestRepository requestRepository) : ICommandHandler<DraftRequestTitleCommand, DraftRequestTitleResult>
{
    public async Task<DraftRequestTitleResult> Handle(DraftRequestTitleCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.RequestId);

        if (request is null)
            throw new RequestNotFoundException(command.RequestId);

        var requestTitle = RequestTitle.CreateDraft(new RequestTitleData
        (
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
        ));

        await requestTitleRepository.AddAsync(requestTitle, cancellationToken);
        
        await requestTitleRepository.SaveChangesAsync(cancellationToken);
        
        return new DraftRequestTitleResult(requestTitle.Id);
    }
}
