namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public class UpdateDraftRequestCommandTitleHandler(IRequestTitleRepository requestTitleRepository)
    : ICommandHandler<UpdateDraftRequestTitleCommand, UpdateDraftRequestTitleResult>
{
    public async Task<UpdateDraftRequestTitleResult> Handle(UpdateDraftRequestTitleCommand command, CancellationToken cancellationToken)
    {
        // var ids = command.RequestTitleCommandDtos.Select(rt => rt.TitleId).ToList();
        // var requestTitle = await requestTitleRepository.GetByIdAsync(command.Id, cancellationToken);
        //
        // if (requestTitle is null || requestTitle.RequestId != command.RequestId)
        // {
        //     throw new RequestTitleNotFoundException(command.Id);
        // }
        //
        // command.RequestTitleCommandDtos.Select(rt =>
        //     RequestTitle.UpdateDetail(
        //         new RequestTitleData(
        //             command.RequestId,
        //             rt.CollateralType,
        //             rt.CollateralStatus,
        //             DtoExtensions.ToDomain(rt.TitleDeedInfoDto),
        //             DtoExtensions.ToDomain(rt.SurveyInfoDto),
        //             DtoExtensions.ToDomain(rt.LandAreaDto),
        //             rt.OwnerName,
        //             rt.RegistrationNo,
        //             DtoExtensions.ToDomain(rt.VehicleDto),
        //             DtoExtensions.ToDomain(rt.MachineDto),
        //             DtoExtensions.ToDomain(rt.BuildingInfoDto),
        //             DtoExtensions.ToDomain(rt.CondoInfoDto),
        //             DtoExtensions.ToDomain(rt.TitleAddress),
        //             DtoExtensions.ToDomain(rt.DopaAddress),
        //             rt.Notes
        //         ),
        //         rt.RequestTitleDocumentDtos.Select(rtd => rtd.Adapt<RequestTitleDocumentData>()).ToList()
        //     )
        // ).ToList();

        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        return new UpdateDraftRequestTitleResult(true);
    }
}
