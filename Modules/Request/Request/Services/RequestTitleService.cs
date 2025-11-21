using MassTransit;
using Request.RequestTitles.Features.CreateLinkRequestTitleDocument;
using Request.RequestTitles.Features.DraftRequestTitle;
using Request.RequestTitles.Features.GetLinkRequestTitleDocumentsByTitleId;
using Request.RequestTitles.Features.GetRequestTitlesByRequestId;
using Request.RequestTitles.Features.RemoveLinkRequestTitleDocument;
using Request.RequestTitles.Features.UpdateDraftRequestTitle;
using Request.RequestTitles.Features.UpdateRequestTitle;
using Shared.Messaging.Events;
using RequestTitleDto = Request.Contracts.Requests.Dtos.RequestTitleDto;

namespace Request.Services;

public class RequestTitleService : IRequestTitleService
{
    private readonly ISender _sender;
    private readonly IBus _bus;

    public RequestTitleService(ISender sender, IBus bus)
    {
        _sender = sender;
        _bus = bus;
    }

    public Task CreateRequestTitleAsync(Guid sessionId, Guid requestId, RequestTitleDto requestTitleDto, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    public async Task CreateRequestTitlesAsync(Guid sessionId, Guid requestId, List<RequestTitleDto> requestTitleDtos,
        CancellationToken cancellationToken)
    {
        // check RequestId is existed or not

        var documentLinks = new List<DocumentLink>();

        if (requestTitleDtos.Count == 0)
            throw new NotFoundException("Request titles not found");

        foreach (var requestTitleDto in requestTitleDtos)
        {
            // create RequestTitle
            var createRequestTitleCommand = BuildCreateCommand(requestId, requestTitleDto);

            var requestTitleResult = await _sender.Send(createRequestTitleCommand, cancellationToken);

            var requestTitleId = requestTitleResult.TitleId;

            // Create RequestTitleDocument
            foreach (var requestTitleDocDto in requestTitleDto.RequestTitleDocumentDtos)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    requestTitleId,
                    requestTitleDocDto
                );

                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);

                documentLinks.Add(new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = requestTitleId,
                    DocumentId = requestTitleDocDto.DocumentId,
                    IsUnlinked = false
                });
            }
        }

        await _bus.Publish(new DocumentLinkedIntegrationEvent
        {
            SessionId = sessionId,
            DocumentLinks = documentLinks
        }, cancellationToken);
    }

    public async Task DraftRequestTitlesAsync(Guid sessionId, Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken)
    {
        // check RequestId is existed or not

        var documentLinks = new List<DocumentLink>();

        if (requestTitleDtos.Count == 0)
            throw new NotFoundException("Request titles not found");

        foreach (var draftReqTitleDto in requestTitleDtos)
        {
            // create RequestTitle
            var draftReqTitleCommand = BuildDraftCreateCommand(requestId, draftReqTitleDto); 

            var draftReqTitleResult = await _sender.Send(draftReqTitleCommand, cancellationToken);

            var draftReqTitleId = draftReqTitleResult.TitleId;

            // Create RequestTitleDocument
            foreach (var requestTitleDocDto in draftReqTitleDto.RequestTitleDocumentDtos)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    draftReqTitleId,
                    requestTitleDocDto
                );

                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);

                documentLinks.Add(new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = draftReqTitleId,
                    DocumentId = requestTitleDocDto.DocumentId,
                    IsUnlinked = false
                });
            }
        }

        await _bus.Publish(new DocumentLinkedIntegrationEvent
        {
            SessionId = sessionId,
            DocumentLinks = documentLinks
        }, cancellationToken);
    }

    public Task UpdateRequestTitleAsync(Guid sessionId, Guid requestId, RequestTitleDto requestTitleDto, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task UpdateRequestTitlesAsync(Guid sessionId, Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken)
    {
        var documentLinks = new List<DocumentLink>();
        
        var requestTitlesResult = await _sender.Send(new GetRequestTitlesByRequestIdQuery(requestId), cancellationToken);
        
        if (requestTitlesResult is null)
            throw new RequestNotFoundException(requestId);
        
        var existingRequestTitleIds = requestTitlesResult.RequestTitles.Select(rt => rt.Id).ToHashSet();
        
        // DTOs with Id => candidates for update/keep
        var dtosWithId = requestTitleDtos
            .Where(rtd => rtd.Id.HasValue)
            .ToList();
        
        var dtoIds = dtosWithId
            .Select(rtd => rtd.Id!.Value)
            .ToHashSet();
        
        // For update: intersection of client Ids and existing DB Ids
        var updatingReqTitleDtos = dtosWithId
            .Where(rtd => existingRequestTitleIds.Contains(rtd.Id!.Value))
            .ToList();
        
        foreach (var requestTitleDto in updatingReqTitleDtos)
        {
            // update RequestTitle
            var updateRequestTitleCommand = BuildUpdateCommand(requestId, requestTitleDto);
            var requestTitleResult = await _sender.Send(updateRequestTitleCommand, cancellationToken);
            
            var requestTitleId = requestTitleResult.RequestTitleId;

            // snapshot exiting link
            var existingReqTitleDocsResult = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(requestTitleId), cancellationToken);

            var existingReqTitleDocsDtos = existingReqTitleDocsResult.RequestTitleDocuments;
            
            var existingDocsById = existingReqTitleDocsDtos
                .Where(d => d.Id.HasValue)
                .ToDictionary(d => d.Id!.Value);
            
            var dtoDocNew = requestTitleDto.RequestTitleDocumentDtos
                .Where(d => !d.Id.HasValue)
                .ToList();
            
            var dtoDocWithId = requestTitleDto.RequestTitleDocumentDtos
                .Where(d => d.Id.HasValue)
                .ToList();

            var dtoDocIds = dtoDocWithId
                .Select(d => d.Id!.Value)
                .ToHashSet();
            
            foreach (var linkDto in dtoDocNew)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    requestTitleId,
                    linkDto
                );
                
                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);
                
                documentLinks.Add(new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = requestTitleId,
                    DocumentId = linkDto.DocumentId,
                    IsUnlinked = false
                });
            }
            
            var removedLinkIds = existingDocsById.Keys
                .Except(dtoDocIds)
                .ToList();

            var removingReqTitleDocDtos = existingReqTitleDocsDtos.Where(dto => removedLinkIds.Contains(dto.Id!.Value)).ToList();
            
            foreach (var titleDocDto in removingReqTitleDocDtos)
            {
                var removeResult = await _sender.Send(new RemoveLinkRequestTitleDocumentByIdCommand(titleDocDto.Id!.Value, requestTitleId), cancellationToken);
                
                documentLinks.Add(new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = requestTitleId,
                    DocumentId = removeResult.TitleDocId,
                    IsUnlinked = true
                });
            }
        }
        
        // Case: new title
        var newTitlesDtos = requestTitleDtos.Where(rtd => !rtd.Id.HasValue).ToList();

        foreach (var titleDto in newTitlesDtos)
        {
            // create RequestTitle
            var createRequestTitleCommand = BuildCreateCommand(requestId, titleDto);

            var requestTitleResult = await _sender.Send(createRequestTitleCommand, cancellationToken);

            var requestTitleId = requestTitleResult.TitleId;

            // Create RequestTitleDocument
            foreach (var titleDocDto in titleDto.RequestTitleDocumentDtos)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    requestTitleId,
                    titleDocDto
                );

                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);

                documentLinks.Add(new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = requestTitleId,
                    DocumentId = titleDocDto.DocumentId,
                    IsUnlinked = false
                });
            }
        }

        // Case: remove title
        var titlesToRemove = existingRequestTitleIds
            .Except(dtoIds)
            .ToList();
        
        foreach (var id in titlesToRemove)
        {
            var existingReqTitleDocResult = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(id), cancellationToken);
            
            var existingReqTitleDocDtos = existingReqTitleDocResult.RequestTitleDocuments;
            
            var removingReqTitleDocDtos = existingReqTitleDocDtos.Where(d => d.Id.HasValue).ToList();

            foreach (var titleDocDto in removingReqTitleDocDtos)
            {
                var  removeResult = await _sender.Send(new RemoveLinkRequestTitleDocumentByIdCommand(titleDocDto.Id!.Value, id));
                
                documentLinks.Add(new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = id,
                    DocumentId = removeResult.TitleDocId,
                    IsUnlinked = true
                });
            }

            var result = await _sender.Send(new RemoveRequestTitleCommand(requestId, id));
            
            if (!result.Success)
                throw new Exception($"RequestTitle Id: {id} cannot be removed");
        }

        await _bus.Publish(new DocumentLinkedIntegrationEvent
        {
            SessionId = sessionId,
            DocumentLinks = documentLinks,
        }, cancellationToken);
    }

    public async Task UpdateDraftRequestTitlesAsync(Guid sessionId, Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken)
    {
        var documentLinks = new List<DocumentLink>();
        
        var requestTitlesResult = await _sender.Send(new GetRequestTitlesByRequestIdQuery(requestId), cancellationToken);
        
        if (requestTitlesResult is null)
            throw new RequestNotFoundException(requestId);
        
        var existingRequestTitleIds = requestTitlesResult.RequestTitles.Select(rt => rt.Id).ToHashSet();
        
        // DTOs with Id => candidates for update/keep
        var dtosWithId = requestTitleDtos
            .Where(rtd => rtd.Id.HasValue)
            .ToList();
        
        var dtoIds = dtosWithId
            .Select(rtd => rtd.Id!.Value)
            .ToHashSet();
        
        // For update: intersection of client Ids and existing DB Ids
        var updatingReqTitleDtos = dtosWithId
            .Where(rtd => existingRequestTitleIds.Contains(rtd.Id!.Value))
            .ToList();
        
        foreach (var requestTitleDto in updatingReqTitleDtos)
        {
            // update RequestTitle
            var updateDraftRequestTitleCommand = BuildDraftUpdateCommand(requestId, requestTitleDto);
            var requestTitleResult = await _sender.Send(updateDraftRequestTitleCommand);
            
            var requestTitleId = requestTitleResult.RequestTitleId;
            
            // snapshot exiting link
            var existingReqTitleDocsResult = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(requestTitleId), cancellationToken);
            
            var existingReqTitleDocsDtos = existingReqTitleDocsResult.RequestTitleDocuments;
            
            var existingDocsById = existingReqTitleDocsDtos
                .Where(d => d.Id.HasValue)
                .ToDictionary(d => d.Id!.Value);
            
            var dtoDocNew = requestTitleDto.RequestTitleDocumentDtos
                .Where(d => !d.Id.HasValue)
                .ToList();
            
            var dtoDocWithId = requestTitleDto.RequestTitleDocumentDtos
                .Where(d => d.Id.HasValue)
                .ToList();

            var dtoDocIds = dtoDocWithId
                .Select(d => d.Id!.Value)
                .ToHashSet();
            
            foreach (var linkDto in dtoDocNew)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    requestTitleId,
                    linkDto
                );
                
                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);
                
                documentLinks.Add(new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = requestTitleId,
                    DocumentId = linkDto.DocumentId,
                    IsUnlinked = false
                });
            }
            
            var removedLinkIds = existingDocsById.Keys
                .Except(dtoDocIds)
                .ToList();

            var removingReqTitleDocDtos = existingReqTitleDocsDtos.Where(dto => removedLinkIds.Contains(dto.Id!.Value)).ToList();
            
            foreach (var titleDocDto in removingReqTitleDocDtos)
            {
                var removeResult = await _sender.Send(new RemoveLinkRequestTitleDocumentByIdCommand(titleDocDto.Id!.Value, requestTitleId), cancellationToken);
                
                documentLinks.Add(new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = requestTitleId,
                    DocumentId = removeResult.TitleDocId,
                    IsUnlinked = true
                });
            }
        }
        
        // Case: new title
        var newTitlesDtos = requestTitleDtos.Where(rtd => !rtd.Id.HasValue).ToList();

        foreach (var titleDto in newTitlesDtos)
        {
            // create RequestTitle
            var draftRequestTitleCommand = BuildDraftCreateCommand(requestId, titleDto);
            var requestTitleResult = await _sender.Send(draftRequestTitleCommand, cancellationToken);

            var requestTitleId = requestTitleResult.TitleId;

            // Create RequestTitleDocument
            foreach (var titleDocDto in titleDto.RequestTitleDocumentDtos)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    requestTitleId,
                    titleDocDto
                );

                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);

                documentLinks.Add(new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = requestTitleId,
                    DocumentId = titleDocDto.DocumentId,
                    IsUnlinked = false
                });
            }
        }
            
        
        // Case: remove title
        var titlesToRemove = existingRequestTitleIds
            .Except(dtoIds)
            .ToList();
        
        foreach (var id in titlesToRemove)
        {
            var existingReqTitleDocResult = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(id), cancellationToken);
            
            var existingReqTitleDocDtos = existingReqTitleDocResult.RequestTitleDocuments;
            
            var removingReqTitleDocDtos = existingReqTitleDocDtos.Where(d => !d.Id.HasValue).ToList();

            foreach (var titleDocDto in removingReqTitleDocDtos)
            {
                var  removeResult = await _sender.Send(new RemoveLinkRequestTitleDocumentByIdCommand(titleDocDto.Id!.Value, id));
                
                documentLinks.Add(new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = id,
                    DocumentId = removeResult.TitleDocId,
                    IsUnlinked = true
                });
            }
            
            var result = await _sender.Send(new RemoveRequestTitleCommand(requestId, id));
            
            if (!result.Success)
                throw new Exception($"RequestTitle Id: {id} cannot be removed");
        }

        await _bus.Publish(new DocumentLinkedIntegrationEvent
        {
            SessionId = sessionId,
            DocumentLinks = documentLinks,
        }, cancellationToken);
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
            dto.RegistrationNumber,
            new VehicleDto(dto.VehicleType, dto.VehicleAppointmentLocation,
                dto.ChassisNumber),
            new MachineDto(dto.MachineryStatus, dto.MachineryType,
                dto.InstallationStatus, dto.InvoiceNumber,
                dto.NumberOfMachinery),
            new BuildingInfoDto(dto.BuildingType, dto.UsableArea,
                dto.NumberOfBuilding),
            new CondoInfoDto(dto.CondoName, dto.BuildingNo, dto.RoomNo,
                dto.FloorNo),
            dto.TitleAddress,
            dto.DopaAddress,
            dto.Notes
        );
    }

    private DraftRequestTitleCommand BuildDraftCreateCommand(Guid requestId, RequestTitleDto dto)
    {
        return new DraftRequestTitleCommand(
            requestId,
            dto.CollateralType,
            dto.CollateralStatus,
            new TitleDeedInfoDto(dto.TitleNo, dto.DeedType, dto.TitleDetail),
            new SurveyInfoDto(dto.Rawang, dto.LandNo, dto.SurveyNo),
            new LandAreaDto(dto.AreaRai, dto.AreaNgan, dto.AreaSquareWa),
            dto.OwnerName,
            dto.RegistrationNumber,
            new VehicleDto(dto.VehicleType, dto.VehicleAppointmentLocation,
                dto.ChassisNumber),
            new MachineDto(dto.MachineryStatus, dto.MachineryType,
                dto.InstallationStatus, dto.InvoiceNumber,
                dto.NumberOfMachinery),
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
            dto.RegistrationNumber,
            new VehicleDto(dto.VehicleType, dto.VehicleAppointmentLocation,
                dto.ChassisNumber),
            new MachineDto(dto.MachineryStatus, dto.MachineryType,
                dto.InstallationStatus, dto.InvoiceNumber,
                dto.NumberOfMachinery),
            new BuildingInfoDto(dto.BuildingType, dto.UsableArea,
                dto.NumberOfBuilding),
            new CondoInfoDto(dto.CondoName, dto.BuildingNo, dto.RoomNo,
                dto.FloorNo),
            dto.TitleAddress,
            dto.DopaAddress,
            dto.Notes
        );
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
            dto.RegistrationNumber,
            new VehicleDto(dto.VehicleType, dto.VehicleAppointmentLocation,
                dto.ChassisNumber),
            new MachineDto(dto.MachineryStatus, dto.MachineryType,
                dto.InstallationStatus, dto.InvoiceNumber,
                dto.NumberOfMachinery),
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