using MassTransit;
using Request.RequestTitles.Features.CreateLinkRequestTitleDocument;
using Request.RequestTitles.Features.GetLinkRequestTitleDocumentsByTitleId;
using Request.RequestTitles.Features.GetRequestTitlesByRequestId;
using Request.RequestTitles.Features.RemoveLinkRequestTitleDocument;
using Request.RequestTitles.Features.SyncRequestTitleDocuments;
using Request.RequestTitles.Features.UpdateLinkRequestTitleDocument;
using Request.RequestTitles.Features.UpdateRequestTitle;
using Shared.Messaging.Events;

namespace Request.RequestTitles.Features.SyncRequestTitle;

public class SyncRequestTitleCommandHandler(ISender sender, IBus bus) : ICommandHandler<SyncRequestTitleCommand, SyncRequestTitleResult>
{
    public async Task<SyncRequestTitleResult> Handle(SyncRequestTitleCommand command, CancellationToken cancellationToken)
    {
        var results = new List<RequestTitleDto>();

        var requestTitlesResult = await sender.Send(new GetRequestTitlesByRequestIdQuery(command.RequestId), cancellationToken);

        if (requestTitlesResult is null)
            throw new RequestNotFoundException(command.RequestId);

        var existingRequestTitles = requestTitlesResult.RequestTitles?.ToList() ?? new List<RequestTitleDto>();
        var existingById = existingRequestTitles
            .Where(x => x.Id.HasValue && x.Id.Value != Guid.Empty)
            .ToDictionary(x => x.Id!.Value);

        var existingRequestTitleIds = existingById.Keys.ToHashSet();

        var requestTitleDtos = command.requestTitleDtos?.ToList() ?? new List<RequestTitleDto>();

        var incomingIds = requestTitleDtos
            .Where(x => x.Id.HasValue && x.Id.Value != Guid.Empty)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        var updatingRequestTitles = requestTitleDtos
            .Where(x => x.Id.HasValue && x.Id.Value != Guid.Empty)
            .ToList();

        var creatingRequestTitles = requestTitleDtos
            .Where(x => !x.Id.HasValue || x.Id.Value == Guid.Empty)
            .ToList();

        var removingRequestTitleIds = existingRequestTitleIds.Except(incomingIds).ToHashSet();
        var removingRequestTitles = removingRequestTitleIds.Select(id => existingById[id]).ToList();

        foreach (var requestTitle in removingRequestTitles)
        {
            var removedRequestTitleDocumentsResult = await sender.Send(new SyncRequestTitleDocumentsCommand
            {
                SessionId = command.SessionId,
                RequestId = command.RequestId,
                TitleId = requestTitle.Id!.Value
            }, cancellationToken);

            var removeRequestTitleResult = await sender.Send(new RemoveRequestTitleCommand(command.RequestId, requestTitle.Id!.Value), cancellationToken);
        }

        foreach (var requestTitle in creatingRequestTitles)
        {
            var createRequestTitleCommand = BuildCreateCommand(command.RequestId, requestTitle);
            var requestTitleResult = await sender.Send(createRequestTitleCommand, cancellationToken);

            results.Add(requestTitle with { Id = requestTitleResult.Id , RequestId = command.RequestId }); 
            
            var createdRequestTitleDocumentsResult = await sender.Send(new SyncRequestTitleDocumentsCommand
            {
                SessionId = command.SessionId,
                RequestId = command.RequestId,
                TitleId = requestTitleResult.Id,
                RequestTitleDocumentDtos = requestTitle.RequestTitleDocumentDtos
            }, cancellationToken);
        }

        foreach (var requestTitle in updatingRequestTitles)
        {
            /*
                in case that update type of title information, EF core not allow to change it directly. So, we have to remove existing and create the new one.
            */
            var existing = existingById.GetValueOrDefault(requestTitle.Id!.Value);
            if (existing is null)
            {
            }
            else if (requestTitle.CollateralType == existing.CollateralType)
            {

                var updatedRequestTitleResult = await sender.Send(BuildUpdateCommand(command.RequestId, requestTitle), cancellationToken);

                results.Add(requestTitle with { RequestId = command.RequestId });

                var updatedRequestTitleDocumentsResult = await sender.Send(new SyncRequestTitleDocumentsCommand
                {
                    SessionId = command.SessionId,
                    RequestId = command.RequestId,
                    TitleId = requestTitle.Id!.Value,
                    RequestTitleDocumentDtos = requestTitle.RequestTitleDocumentDtos
                }, cancellationToken);
            }
            else
            {
                var removedRequestTitleDocumentsResult = await sender.Send(new SyncRequestTitleDocumentsCommand
                {
                    SessionId = command.SessionId,
                    RequestId = command.RequestId,
                    TitleId = requestTitle.Id!.Value
                }, cancellationToken);
                
                var removeResult = await sender.Send(new RemoveRequestTitleCommand(command.RequestId, requestTitle.Id!.Value));
                var newRequestTitle = await sender.Send(BuildCreateCommand(command.RequestId, requestTitle));

                results.Add(requestTitle with { Id = newRequestTitle.Id , RequestId = command.RequestId});

                if (requestTitle.RequestTitleDocumentDtos.Count > 0)
                {
                    var createdRequestTitleDocumentsResult = await sender.Send(new SyncRequestTitleDocumentsCommand
                    {
                        SessionId = command.SessionId,
                        RequestId = command.RequestId,
                        TitleId = newRequestTitle.Id,
                        RequestTitleDocumentDtos = requestTitle.RequestTitleDocumentDtos.Select(rtd => rtd with { Id = null }).ToList()
                    }, cancellationToken);
                }
            }
        }
        
        var result = new SyncRequestTitleResult(results);
        return result;
    }

    private CreateRequestTitleCommand BuildCreateCommand(Guid requestId, RequestTitleDto dto)
    {
        return new CreateRequestTitleCommand(
            requestId,
            dto.CollateralType,
            dto.CollateralStatus,
            new TitleDeedInfoDto(dto.TitleNo, dto.DeedType, dto.TitleDetail),
            new SurveyInfoDto(dto.Rawang, dto.LandNo, dto.SurveyNo),
            new LandAreaDto(dto.AreaRai, dto.AreaNgan, dto.AreaSquareWa),
            dto.OwnerName,
            dto.RegistrationNo,
            new VehicleDto(dto.VehicleType, dto.VehicleAppointmentLocation,
                dto.ChassisNumber),
            new MachineDto(dto.MachineStatus, dto.MachineType,
                dto.InstallationStatus, dto.InvoiceNumber,
                dto.NumberOfMachine),
            new BuildingInfoDto(dto.BuildingType, dto.UsableArea,
                dto.NumberOfBuilding),
            new CondoInfoDto(dto.CondoName, dto.BuildingNo, dto.RoomNo,
                dto.FloorNo),
            dto.TitleAddress,
            dto.DopaAddress,
            dto.Notes
        );
    }

    private UpdateRequestTitleCommand BuildUpdateCommand(Guid requestId, RequestTitleDto dto)
    {
        return new UpdateRequestTitleCommand(
            requestId,
            dto.Id!.Value,
            dto.CollateralType,
            dto.CollateralStatus,
            new TitleDeedInfoDto(dto.TitleNo, dto.DeedType, dto.TitleDetail),
            new SurveyInfoDto(dto.Rawang, dto.LandNo, dto.SurveyNo),
            new LandAreaDto(dto.AreaRai, dto.AreaNgan, dto.AreaSquareWa),
            dto.OwnerName,
            dto.RegistrationNo,
            new VehicleDto(dto.VehicleType, dto.VehicleAppointmentLocation,
                dto.ChassisNumber),
            new MachineDto(dto.MachineStatus, dto.MachineType,
                dto.InstallationStatus, dto.InvoiceNumber,
                dto.NumberOfMachine),
            new BuildingInfoDto(dto.BuildingType, dto.UsableArea,
                dto.NumberOfBuilding),
            new CondoInfoDto(dto.CondoName, dto.BuildingNo, dto.RoomNo,
                dto.FloorNo),
            dto.TitleAddress,
            dto.DopaAddress,
            dto.Notes
        );
    }
}
