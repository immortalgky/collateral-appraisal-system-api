using Request.Extensions;

namespace Request.RequestTitles.Features.UpdateRequestTitle;

internal class UpdateRequestTitleCommandHandler(IRequestTitleRepository requestTitleRepository)
    : ICommandHandler<UpdateRequestTitleCommand, UpdateRequestTitleResult>
{
    public async Task<UpdateRequestTitleResult> Handle(UpdateRequestTitleCommand command, CancellationToken cancellationToken)
    {
        var requestTitle = await requestTitleRepository.GetByIdAsync(command.Id, cancellationToken);

        if (requestTitle is null || requestTitle.RequestId != command.RequestId)
        {
            throw new RequestTitleNotFoundException(command.Id);
        }
        
        requestTitle.UpdateDetails(
            new RequestTitleData(
                requestTitle.RequestId,
                command.CollateralType,
                command.CollateralStatus,
                DtoExtensions.ToDomain(command.TitleDeedInfoDto),
                DtoExtensions.ToDomain(command.SurveyInfoDto),
                DtoExtensions.ToDomain(command.LandAreaDto),
                command.OwnerName,
                command.RegistrationNumber,
                DtoExtensions.ToDomain(command.VehicleDto),
                DtoExtensions.ToDomain(command.MachineryDto),
                DtoExtensions.ToDomain(command.BuildingInfoDto),
                DtoExtensions.ToDomain(command.CondoInfoDto),
                DtoExtensions.ToDomain(command.TitleAddress),
                DtoExtensions.ToDomain(command.DopaAddress),
                command.Notes
            )
        );

        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        return new UpdateRequestTitleResult(true);
    }
}