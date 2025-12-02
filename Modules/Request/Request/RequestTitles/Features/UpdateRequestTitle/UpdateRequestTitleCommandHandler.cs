using MassTransit.Initializers;
using Request.Extensions;

namespace Request.RequestTitles.Features.UpdateRequestTitle;

internal class UpdateRequestTitleCommandHandler(IRequestTitleRepository requestTitleRepository)
    : ICommandHandler<UpdateRequestTitleCommand, UpdateRequestTitleResult>
{
    public async Task<UpdateRequestTitleResult> Handle(UpdateRequestTitleCommand command, CancellationToken cancellationToken)
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

        requestTitle.Update(requestTitleData);

        requestTitle.AddDomainEvent(new RequestTitleUpdatedEvent(requestTitle.RequestId, requestTitle));

        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        return new UpdateRequestTitleResult(requestTitle.Id);
    }
}