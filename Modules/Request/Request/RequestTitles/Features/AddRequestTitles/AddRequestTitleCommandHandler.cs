namespace Request.RequestTitles.Features.AddRequestTitles;

public class AddRequestTitlesCommandHandler(IRequestTitleRepository requestTitleRepository, IRequestRepository requestRepository)
    : ICommandHandler<AddRequestTitlesCommand, AddRequestTitlesResult>
{
    public async Task<AddRequestTitlesResult> Handle(AddRequestTitlesCommand command,
        CancellationToken cancellationToken)
    {

        var request = await requestRepository.GetByIdAsync(command.RequestId);

        if (request is null)
            throw new RequestNotFoundException(command.RequestId);

        // throw exception which identify index of item 
        var requestTitles = command.AddRequestTitleCommandDtos.Select(rt =>
            RequestTitle.Create(
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
                rt.RequestTitleDocumentDtos.Select(rtd => rtd.Adapt<RequestTitleData>())
            )
        ).ToList();

        await requestTitleRepository.AddRangeAsync(requestTitles, cancellationToken);
        await requestRepository.SaveChangesAsync(cancellationToken);

        return new AddRequestTitlesResult(requestTitles.OrderBy(rt => rt.CreatedOn).Select(rt => rt.Id).ToList());
    }
}