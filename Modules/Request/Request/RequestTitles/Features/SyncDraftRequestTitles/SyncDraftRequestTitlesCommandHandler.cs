using MassTransit;
using Request.RequestTitles.Features.DraftRequestTitle;
using Request.RequestTitles.Features.GetRequestTitlesByRequestId;
using Request.RequestTitles.Features.SyncDraftRequestTitleDocuments;
using Request.RequestTitles.Features.UpdateDraftRequestTitle;
using Shared.Messaging.Events;

namespace Request.RequestTitles.Features.SyncDraftRequestTitles;

public class SyncDraftRequestTitlesCommandHandler(ISender sender, IBus bus) : ICommandHandler<SyncDraftRequestTitlesCommand, SyncDraftRequestTitlesResult>
{
    public async Task<SyncDraftRequestTitlesResult> Handle(SyncDraftRequestTitlesCommand command, CancellationToken cancellationToken)
    {
        // Make sure that linQ operations do not fail due to null reference
        var requestTitleDtos = command.requestTitleDtos?.ToList() ?? new List<RequestTitleDto>();
        // Collecting results which be sent to caller
        var results = new List<RequestTitleDto>();

        var requestTitlesResult = await sender.Send(new GetRequestTitlesByRequestIdQuery(command.RequestId), cancellationToken);

        if (requestTitlesResult is null)
            throw new RequestNotFoundException(command.RequestId);

        var existingRequestTitles = requestTitlesResult.RequestTitles?.ToList() ?? new List<RequestTitleDto>();
        var existingById = existingRequestTitles
            .Where(x => x.Id.HasValue && x.Id.Value != Guid.Empty)
            .ToDictionary(x => x.Id!.Value);

        var existingRequestTitleIds = existingById.Keys.ToHashSet();

        var incomingIds = requestTitleDtos
            .Where(x => x.Id.HasValue && x.Id.Value != Guid.Empty)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        // Updating Request Titles that have an Id
        var updatingRequestTitles = requestTitleDtos
            .Where(x => x.Id.HasValue && x.Id.Value != Guid.Empty)
            .ToList();

        // Creating Request Titles that do not have an Id
        var creatingRequestTitles = requestTitleDtos
            .Where(x => !x.Id.HasValue || x.Id.Value == Guid.Empty)
            .ToList();

        // Removing Request Titles that are not in the incoming list
        var removingRequestTitleIds = existingRequestTitleIds
            .Except(incomingIds)
            .ToHashSet();
        var removingRequestTitles = removingRequestTitleIds
            .Select(id => existingById[id])
            .ToList();

        foreach (var requestTitle in removingRequestTitles)
        {
            // Call 'SyncRequestTitleDocumentsCommand' with empty requestTitleDocumentDtos to remove all documents linked to this title and publish events
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

            
            var createdRequestTitleDocumentsResult = await sender.Send(new SyncDraftRequestTitleDocumentsCommand
            {
                SessionId = command.SessionId,
                RequestId = command.RequestId,
                TitleId = requestTitleResult.Id,
                RequestTitleDocumentDtos = requestTitle.RequestTitleDocumentDtos
            }, cancellationToken);

            results.Add(requestTitle with { Id = requestTitleResult.Id, RequestId = command.RequestId, RequestTitleDocumentDtos = createdRequestTitleDocumentsResult.RequestTitleDocumentDtos });
        }

        foreach (var requestTitle in updatingRequestTitles)
        {
            var existing = existingRequestTitles.FirstOrDefault(ert => ert.Id == requestTitle.Id);
            if (existing is null)
            {
                throw new RequestTitleNotFoundException(requestTitle.Id!.Value);
            }
            else if (requestTitle.CollateralType == existing.CollateralType)
            {

                var updatedRequestTitleResult = await sender.Send(BuildDraftUpdateCommand(command.RequestId, requestTitle), cancellationToken);


                var updatedRequestTitleDocumentsResult = await sender.Send(new SyncDraftRequestTitleDocumentsCommand
                {
                    SessionId = command.SessionId,
                    RequestId = command.RequestId,
                    TitleId = requestTitle.Id!.Value,
                    RequestTitleDocumentDtos = requestTitle.RequestTitleDocumentDtos
                }, cancellationToken);
                
                results.Add(requestTitle with { RequestId = command.RequestId, RequestTitleDocumentDtos = updatedRequestTitleDocumentsResult.RequestTitleDocumentDtos });
            }
            else
            {
                // In case that change collateral type of title information, we have to remove existing and create the new one. due to EF core not allow to change it directly.

                // Call 'SyncRequestTitleDocumentsCommand' with empty requestTitleDocumentDtos to remove all documents linked to this title and publish events
                var removedRequestTitleDocumentsResult = await sender.Send(new SyncDraftRequestTitleDocumentsCommand
                {
                    SessionId = command.SessionId,
                    RequestId = command.RequestId,
                    TitleId = requestTitle.Id!.Value
                }, cancellationToken);

                // Remove existing Request Title, RequestTitleDocuments will be removed by cascade
                var removeResult = await sender.Send(new RemoveRequestTitleCommand(command.RequestId, requestTitle.Id!.Value));
                // Create new Request Title
                var newRequestTitle = await sender.Send(BuildDraftCommand(command.RequestId, requestTitle));

                results.Add(requestTitle with { Id = newRequestTitle.Id , RequestId = command.RequestId});

                var createdRequestTitleDocumentsResult = await sender.Send(new SyncDraftRequestTitleDocumentsCommand
                {
                    SessionId = command.SessionId,
                    RequestId = command.RequestId,
                    TitleId = newRequestTitle.Id,
                    RequestTitleDocumentDtos = requestTitle.RequestTitleDocumentDtos.Select(rtd => rtd with { Id = null }).ToList()
                }, cancellationToken);

                results.Add(requestTitle with { Id = newRequestTitle.Id, RequestId = command.RequestId, RequestTitleDocumentDtos = createdRequestTitleDocumentsResult.RequestTitleDocumentDtos });
            }
        }
        
        var result = new SyncDraftRequestTitlesResult(results);
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
