using MassTransit;
using Request.RequestTitles.Features.CreateRequestTitle;
using Request.RequestTitles.Features.GetRequestTitlesByRequestId;
using Request.RequestTitles.Features.RemoveRequestTitle;
using Request.RequestTitles.Features.SyncRequestTitleDocuments;
using Request.RequestTitles.Features.UpdateRequestTitle;

namespace Request.RequestTitles.Features.SyncRequestTitles;

public class SyncRequestTitlesCommandHandler(ISender sender) : ICommandHandler<SyncRequestTitlesCommand, SyncRequestTitlesResult>
{
    public async Task<SyncRequestTitlesResult> Handle(SyncRequestTitlesCommand command, CancellationToken cancellationToken)
    {
        // Make sure that linQ operations do not fail due to null reference
        var requestTitleDtos = command.RequestTitleDtos?.ToList() ?? new List<RequestTitleDto>();

        var requestTitlesResult = await sender.Send(new GetRequestTitlesByRequestIdQuery(command.RequestId), cancellationToken);

        var existingRequestTitles = requestTitlesResult.RequestTitles?.ToList() ?? new List<RequestTitleDto>();
        var existingRequestTitleWithId = existingRequestTitles
            .Where(x => x.Id.HasValue && x.Id.Value != Guid.Empty)
            .ToDictionary(x => x.Id!.Value);
        var existingRequestTitleIds = existingRequestTitleWithId.Keys.ToHashSet();

        var requestTitleIds = requestTitleDtos
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
            .Except(requestTitleIds)
            .ToHashSet();
        var removingRequestTitles = removingRequestTitleIds
            .Select(id => existingRequestTitleWithId[id])
            .ToList();

        foreach (var requestTitle in removingRequestTitles)
        {
            // Call 'SyncRequestTitleDocumentsCommand' with empty requestTitleDocumentDtos to remove all documents linked to this title and publish events
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
            var existing = existingRequestTitleWithId.GetValueOrDefault(requestTitle.Id!.Value);
            if (existing is null)
            {
                throw new RequestTitleNotFoundException(requestTitle.Id!.Value);
            }
            else if (requestTitle.CollateralType == existing.CollateralType)
            {
                var updatedRequestTitleResult = await sender.Send(BuildUpdateCommand(command.RequestId, requestTitle), cancellationToken);

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
                // In case that change collateral type of title information, we have to remove existing and create the new one. due to EF core not allow to change it directly.

                // Call 'SyncRequestTitleDocumentsCommand' with empty requestTitleDocumentDtos to remove all documents linked to this title and publish events
                var removedRequestTitleDocumentsResult = await sender.Send(new SyncRequestTitleDocumentsCommand
                {
                    SessionId = command.SessionId,
                    RequestId = command.RequestId,
                    TitleId = requestTitle.Id!.Value
                }, cancellationToken);
                // Remove existing Request Title, RequestTitleDocuments will be removed by cascade
                var removeResult = await sender.Send(new RemoveRequestTitleCommand(command.RequestId, requestTitle.Id!.Value));
                
                // Create new Request Title
                var newRequestTitle = await sender.Send(BuildCreateCommand(command.RequestId, requestTitle));

                // Create new RequestTitleDocuments, with new TitleId
                var createdRequestTitleDocumentsResult = await sender.Send(new SyncRequestTitleDocumentsCommand
                {
                    SessionId = command.SessionId,
                    RequestId = command.RequestId,
                    TitleId = newRequestTitle.Id,
                    RequestTitleDocumentDtos = requestTitle.RequestTitleDocumentDtos.Select(rtd => rtd with { Id = null }).ToList()
                }, cancellationToken);
                
            }
        }
        
        var result = new SyncRequestTitlesResult(true);
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
