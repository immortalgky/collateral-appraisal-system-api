namespace Request.RequestTitles.Features.DraftRequestTitle;

public class DraftRequestTitleCommandHandler(IRequestTitleRepository requestTitleRepository, IRequestRepository requestRepository) : ICommandHandler<DraftRequestTitleCommand, DraftRequestTitleResult>
{
    public async Task<DraftRequestTitleResult> Handle(DraftRequestTitleCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.RequestId);

        if (request is null)
            throw new RequestNotFoundException(command.RequestId);
        
        var requestTitles = command.AddRequestTitleCommandDtos.Select(rt =>
            RequestTitle.CreateDraft(
                new RequestTitleData(
                    command.RequestId,
                    rt.CollateralType,
                    rt.CollateralStatus,
                    DtoExtensions.ToDomain(rt.TitleDeedInfoDto),
                    DtoExtensions.ToDomain(rt.SurveyInfoDto),
                    DtoExtensions.ToDomain(rt.LandAreaDto),
                    rt.OwnerName,
                    rt.RegistrationNumber,
                    DtoExtensions.ToDomain(rt.VehicleDto),
                    DtoExtensions.ToDomain(rt.MachineryDto),
                    DtoExtensions.ToDomain(rt.BuildingInfoDto),
                    DtoExtensions.ToDomain(rt.CondoInfoDto),
                    DtoExtensions.ToDomain(rt.TitleAddress),
                    DtoExtensions.ToDomain(rt.DopaAddress),
                    rt.Notes
                ),
                rt.RequestTitleDocumentDtos.Select(rtd => rtd.Adapt<RequestTitleDocumentData>()).ToList()
            )
        ).ToList();

        await requestTitleRepository.AddRangeAsync(requestTitles, cancellationToken);
        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        return new DraftRequestTitleResult(requestTitles.OrderBy(rt => rt.CreatedOn).Select(rt => rt.Id).ToList());
    }
}
