using MassTransit;
using Request.RequestTitles.Features.DraftRequestTitle;
using Request.RequestTitles.Features.GetRequestTitlesByRequestId;
using Request.RequestTitles.Features.SyncDraftRequestTitleDocuments;
using Request.RequestTitles.Features.UpdateDraftRequestTitle;
using Shared.Messaging.Events;

namespace Request.RequestTitles.Features.SyncDraftRequestTitles;

public class SyncDraftRequestTitleCommandHandler(ISender sender, IBus bus) : ICommandHandler<SyncDraftRequestTitleCommand, SyncDraftRequestTitleResult>
{
    public async Task<SyncDraftRequestTitleResult> Handle(SyncDraftRequestTitleCommand command, CancellationToken cancellationToken)
    {
        var documentLinks = new List<DocumentLink>();
        var results = new List<RequestTitleDto>();

        var requestTitlesResult = await sender.Send(new GetRequestTitlesByRequestIdQuery(command.RequestId), cancellationToken);

        if (requestTitlesResult is null)
            throw new RequestNotFoundException(command.RequestId);

        var existingRequestTitles = requestTitlesResult.RequestTitles;
        var existingRequestTitleIds = requestTitlesResult.RequestTitles.Select(rt => rt.Id!.Value).ToHashSet();

        var updatingRequestTitles = command.requestTitleDtos.Where(rtd => rtd.Id.HasValue && rtd.Id != Guid.Empty).ToList();
        var updatingRequestTitleIds = updatingRequestTitles.Select(rtd => rtd.Id!.Value).ToHashSet();
        
        var creatingRequestTitles = command.requestTitleDtos.Where(rtd => !rtd.Id.HasValue || rtd.Id == Guid.Empty).ToList();

        var removingRequestTitleIds = existingRequestTitleIds.Except(command.requestTitleDtos.Where(rtd => rtd.Id.HasValue && rtd.Id.Value != Guid.Empty).Select(rtd => rtd.Id!.Value).ToHashSet()).ToHashSet();
        var removingRequestTitles = existingRequestTitles.Where(rtd => removingRequestTitleIds.Contains(rtd.Id!.Value)).ToList();

        foreach (var requestTitle in removingRequestTitles)
        {
            var removedRequestTitleDocumentsResult = await sender.Send(new SyncDraftRequestTitleDocumentsCommand
            {
                SessionId = command.SessionId,
                RequestId = command.RequestId,
                TitleId = requestTitle.Id!.Value
            }, cancellationToken);

            var removeRequestTitleResult = await sender.Send(new RemoveRequestTitleCommand(command.RequestId, requestTitle.Id!.Value), cancellationToken);
        }

        foreach (var requestTitle in creatingRequestTitles)
        {
            var createRequestTitleCommand = BuildDraftCommand(command.RequestId, requestTitle);
            var requestTitleResult = await sender.Send(createRequestTitleCommand, cancellationToken);

            results.Add(requestTitle with { Id = requestTitleResult.Id , RequestId = command.RequestId }); 
            
            var createdRequestTitleDocumentsResult = await sender.Send(new SyncDraftRequestTitleDocumentsCommand
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
            var existing = existingRequestTitles.FirstOrDefault(ert => ert.Id == requestTitle.Id);
            if (existing is null)
            {
            }
            else if (requestTitle.CollateralType == existing.CollateralType)
            {

                var updatedRequestTitleResult = await sender.Send(BuildDraftUpdateCommand(command.RequestId, requestTitle), cancellationToken);

                results.Add(requestTitle with { RequestId = command.RequestId });

                var updatedRequestTitleDocumentsResult = await sender.Send(new SyncDraftRequestTitleDocumentsCommand
                {
                    SessionId = command.SessionId,
                    RequestId = command.RequestId,
                    TitleId = requestTitle.Id!.Value,
                    RequestTitleDocumentDtos = requestTitle.RequestTitleDocumentDtos
                }, cancellationToken);
            }
            else
            {
                var removedRequestTitleDocumentsResult = await sender.Send(new SyncDraftRequestTitleDocumentsCommand
                {
                    SessionId = command.SessionId,
                    RequestId = command.RequestId,
                    TitleId = requestTitle.Id!.Value
                }, cancellationToken);
                
                var removeResult = await sender.Send(new RemoveRequestTitleCommand(command.RequestId, requestTitle.Id!.Value));
                var newRequestTitle = await sender.Send(BuildDraftCommand(command.RequestId, requestTitle));

                results.Add(requestTitle with { Id = newRequestTitle.Id , RequestId = command.RequestId});

                if (requestTitle.RequestTitleDocumentDtos.Count > 0)
                {
                    var createdRequestTitleDocumentsResult = await sender.Send(new SyncDraftRequestTitleDocumentsCommand
                    {
                        SessionId = command.SessionId,
                        RequestId = command.RequestId,
                        TitleId = newRequestTitle.Id,
                        RequestTitleDocumentDtos = requestTitle.RequestTitleDocumentDtos.Select(rtd => rtd with { Id = null }).ToList()
                    }, cancellationToken);
                }
            }
        }
        
        var result = new SyncDraftRequestTitleResult(results);
        return result;
    }

    private UpdateDraftRequestTitleCommand BuildDraftUpdateCommand(Guid requestId, RequestTitleDto dto)
    {
        return new UpdateDraftRequestTitleCommand(
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

    private DraftRequestTitleCommand BuildDraftCommand(Guid requestId, RequestTitleDto dto)
    {
        return new DraftRequestTitleCommand(
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
}
