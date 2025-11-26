using MassTransit;
using Request.RequestTitles.Features.CreateLinkRequestTitleDocument;
using Request.RequestTitles.Features.DraftRequestTitle;
using Request.RequestTitles.Features.GetLinkRequestTitleDocumentsByTitleId;
using Request.RequestTitles.Features.GetRequestTitlesByRequestId;
using Request.RequestTitles.Features.RemoveLinkRequestTitleDocument;
using Request.RequestTitles.Features.UpdateDraftRequestTitle;
using Request.RequestTitles.Features.UpdateLinkRequestTitleDocument;
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

        if (requestTitleDtos.Count <= 0)
            throw new NotFoundException("Request titles not found");

        foreach (var requestTitleDto in requestTitleDtos)
        {
            // Create Request Titles
            var createRequestTitleCommand = BuildCreateCommand(requestId, requestTitleDto);
            var requestTitleResult = await _sender.Send(createRequestTitleCommand, cancellationToken);
            var requestTitleId = requestTitleResult.TitleId;

            // Create Request Title Documents
            foreach (var requestTitleDocDto in requestTitleDto.RequestTitleDocumentDtos)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    requestTitleId,
                    requestTitleDocDto
                );

                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);

                if (requestTitleDocDto.DocumentId != Guid.Empty && requestTitleDocDto.DocumentId.HasValue)
                {
                    documentLinks.Add(new DocumentLink
                    {
                        EntityType = "Title",
                        EntityId = requestTitleId,
                        DocumentId = requestTitleDocDto.DocumentId!.Value,
                        IsUnlinked = false
                    });
                }
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

        if (requestTitleDtos.Count <= 0)
            throw new NotFoundException("Request titles not found");

        foreach (var requestTitleDto in requestTitleDtos)
        {
            // Create Request Titles
            var draftRequestTitleCommand = BuildDraftCommand(requestId, requestTitleDto); 
            var requestTitleResult = await _sender.Send(draftRequestTitleCommand, cancellationToken);
            var requestTitleId = requestTitleResult.TitleId;

            // Create Request Title Documents
            foreach (var requestTitleDocDto in requestTitleDto.RequestTitleDocumentDtos)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    requestTitleId,
                    requestTitleDocDto
                );

                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);
                
                if (requestTitleDocDto.DocumentId != Guid.Empty && requestTitleDocDto.DocumentId.HasValue)
                {
                    documentLinks.Add(new DocumentLink
                    {
                        EntityType = "Title",
                        EntityId = requestTitleId,
                        DocumentId = requestTitleDocDto.DocumentId!.Value,
                        IsUnlinked = false
                    });
                }
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

    /*
     * 1. Create new request title +
     * 1.1 Create new link => publish event
     * 2. Update existing request title 
     * 2.1 Create new link => publish event
     * 2.2 Remove link => publish event
     * 3. Remove existing request title 
     * 3.1 Remove link => publish event
     */
    public async Task UpdateRequestTitlesAsync(Guid sessionId, Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken)
    {
        var documentLinks = new List<DocumentLink>();
        
        var requestTitlesResult = await _sender.Send(new GetRequestTitlesByRequestIdQuery(requestId), cancellationToken);
        
        if (requestTitlesResult is null)
            throw new RequestNotFoundException(requestId);
        
        var existingRequestTitleIds = requestTitlesResult.RequestTitles.Select(rt => rt.Id!.Value).ToHashSet();
        
        var dtosWithId = requestTitleDtos
            .Where(rtd => rtd.Id.HasValue)
            .ToList();
        
        
        var dtoIds = dtosWithId
            .Select(rtd => rtd.Id!.Value)
            .ToHashSet();
        
        // existingRequestTitleIds.Except(dtoIds) validate when wrongly send Id which not contain in our system.
        
        var updatingReqTitleDtos = dtosWithId
            .Where(rtd => existingRequestTitleIds.Contains(rtd.Id!.Value))
            .ToList();
        
        foreach (var requestTitleDto in updatingReqTitleDtos)
        {
            // snapshot existing link
            var existingReqTitleDocsResult = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(requestTitleDto.Id!.Value), cancellationToken);
            var existingReqTitleDocsDtos = existingReqTitleDocsResult.RequestTitleDocuments;
            var existingDocsById = existingReqTitleDocsDtos
                .Where(d => d.Id.HasValue)
                .ToDictionary(d => d.Id!.Value);
            
            // update RequestTitle
            var updateRequestTitleCommand = BuildUpdateCommand(requestId, requestTitleDto);
            var requestTitleResult = await _sender.Send(updateRequestTitleCommand, cancellationToken);
            var requestTitleId = requestTitleResult.RequestTitleId;

            // request title documents which don't have any Id => create new request title document
            var dtoDocNew = requestTitleDto.RequestTitleDocumentDtos
                .Where(d => !d.Id.HasValue)
                .ToList();
            
            // request title documents which have Id => check with existing to be removed or updated
            var dtoDocWithId = requestTitleDto.RequestTitleDocumentDtos
                .Where(d => d.Id.HasValue)
                .ToList();

            var dtoDocIds = dtoDocWithId
                .Select(d => d.Id!.Value)
                .ToHashSet();
            
            // Create new Request Title Documents
            // we will create every mandatory documents even it doesn't have document id but we won't publish in case case that it doesn't have document Id
            foreach (var dtoDoc in dtoDocNew)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    requestTitleId,
                    dtoDoc
                );
                
                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);
                
                if (dtoDoc.DocumentId != Guid.Empty && dtoDoc.DocumentId.HasValue)
                {
                    documentLinks.Add(new DocumentLink
                    {
                        EntityType = "Title",
                        EntityId = requestTitleId,
                        DocumentId = dtoDoc.DocumentId!.Value,
                        IsUnlinked = false
                    });
                }
            }
            
            // Check which request title documents existed or not; not existed => remove;
            var removingReqTitleDocIds = existingDocsById.Keys
                .Except(dtoDocIds)
                .ToList();

            var removingReqTitleDocDtos = existingReqTitleDocsDtos.Where(dto => removingReqTitleDocIds.Contains(dto.Id!.Value)).ToList();
            
            // Remove existing Request Title Documents
            foreach (var titleDocDto in removingReqTitleDocDtos)
            {
                var removeResult = await _sender.Send(new RemoveLinkRequestTitleDocumentByIdCommand(titleDocDto.Id!.Value, requestTitleId), cancellationToken);

                if (titleDocDto.DocumentId != Guid.Empty && titleDocDto.DocumentId.HasValue)
                {
                    documentLinks.Add(new DocumentLink
                    {
                        EntityType = "Title",
                        EntityId = requestTitleId,
                        DocumentId = titleDocDto.DocumentId!.Value,
                        IsUnlinked = true
                    });
                }
            }
            
            // Check which request title documents existed or not; existed => update;
            var updatingReqTitleDocIds = existingDocsById.Keys
                .Intersect(dtoDocIds)
                .ToList();

            var updatingReqTitleDocDtos = dtoDocWithId.Where(dto => updatingReqTitleDocIds.Contains(dto.Id!.Value)).ToList();

            foreach (var titleDocDto in updatingReqTitleDocDtos)
            {
                var updateResult = await _sender.Send(new UpdateLinkRequestTitleDocumentCommand(titleDocDto.Id!.Value,
                    new RequestTitleDocumentDto
                    {
                        Id = titleDocDto.Id,
                        TitleId = titleDocDto.TitleId,
                        DocumentId = titleDocDto.DocumentId,
                        DocumentType = titleDocDto.DocumentType,
                        Filename = titleDocDto.Filename,
                        Prefix = titleDocDto.Prefix,
                        Set = titleDocDto.Set,
                        DocumentDescription = titleDocDto.DocumentDescription,
                        FilePath = titleDocDto.FilePath,
                        CreatedWorkstation = titleDocDto.CreatedWorkstation,
                        IsRequired = titleDocDto.IsRequired,
                        UploadedBy = titleDocDto.UploadedBy,
                        UploadedByName = titleDocDto.UploadedByName
                    })
                );

                if (updateResult.Success)
                {
                    documentLinks.Add(new DocumentLink
                    {
                        EntityType = "Title",
                        EntityId = requestTitleId,
                        DocumentId = titleDocDto.DocumentId!.Value,
                        IsUnlinked = false
                    });
                }
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

                if (titleDocDto.DocumentId != Guid.Empty && titleDocDto.DocumentId.HasValue)
                {
                    documentLinks.Add(new DocumentLink
                    {
                        EntityType = "Title",
                        EntityId = requestTitleId,
                        DocumentId = titleDocDto.DocumentId!.Value,
                        IsUnlinked = false
                    });
                }
            }
        }

        // Case: remove title
        var removingRequestTitleIds = existingRequestTitleIds
            .Except(dtoIds)
            .ToList();
        
        foreach (var requestTitleId in removingRequestTitleIds)
        {
            var existingReqTitleDocResult = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(requestTitleId), cancellationToken);
            var existingReqTitleDocDtos = existingReqTitleDocResult.RequestTitleDocuments;
            
            var removingLinkReqTitleDocDtos = existingReqTitleDocDtos.Where(rtd => rtd.DocumentId.HasValue).ToList();
            if (removingLinkReqTitleDocDtos.Count > 0)
            {
                documentLinks.AddRange(removingLinkReqTitleDocDtos.Select(rtd => new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = requestTitleId,
                    DocumentId = rtd.DocumentId!.Value,
                    IsUnlinked = true
                }).ToList());
            }

            var result = await _sender.Send(new RemoveRequestTitleCommand(requestId, requestTitleId));
            
            if (!result.Success)
                throw new Exception($"RequestTitle Id: {requestTitleId} cannot be removed");
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
        
        var existingRequestTitleIds = requestTitlesResult.RequestTitles.Select(rt => rt.Id!.Value).ToHashSet();
        
        var dtosWithId = requestTitleDtos
            .Where(rtd => rtd.Id.HasValue)
            .ToList();
        
        var dtoIds = dtosWithId
            .Select(rtd => rtd.Id!.Value)
            .ToHashSet();
        
        var updatingReqTitleDtos = dtosWithId
            .Where(rtd => existingRequestTitleIds.Contains(rtd.Id!.Value))
            .ToList();
        
        foreach (var requestTitleDto in updatingReqTitleDtos)
        {
            // snapshot existing link
            var existingReqTitleDocsResult = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(requestTitleDto.Id!.Value), cancellationToken);
            var existingReqTitleDocsDtos = existingReqTitleDocsResult.RequestTitleDocuments;
            var existingDocsById = existingReqTitleDocsDtos
                .Where(d => d.Id.HasValue)
                .ToDictionary(d => d.Id!.Value);
            
            // update RequestTitle
            var draftRequestTitleCommand = BuildDraftCommand(requestId, requestTitleDto);
            var requestTitleResult = await _sender.Send(draftRequestTitleCommand, cancellationToken);
            var requestTitleId = requestTitleResult.TitleId;

            // request title documents which don't have any Id => create new request title document
            var dtoDocNew = requestTitleDto.RequestTitleDocumentDtos
                .Where(d => !d.Id.HasValue)
                .ToList();
            
            // request title documents which have Id => check with existing to be removed or updated
            var dtoDocWithId = requestTitleDto.RequestTitleDocumentDtos
                .Where(d => d.Id.HasValue)
                .ToList();

            var dtoDocIds = dtoDocWithId
                .Select(d => d.Id!.Value)
                .ToHashSet();
            
            // Create new Request Title Documents
            // we will create every mandatory documents even it doesn't have document id but we won't publish in case case that it doesn't have document Id
            foreach (var dtoDoc in dtoDocNew)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    requestTitleId,
                    dtoDoc
                );
                
                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);
                
                if (dtoDoc.DocumentId != Guid.Empty && dtoDoc.DocumentId.HasValue)
                {
                    documentLinks.Add(new DocumentLink
                    {
                        EntityType = "Title",
                        EntityId = requestTitleId,
                        DocumentId = dtoDoc.DocumentId!.Value,
                        IsUnlinked = false
                    });
                }
            }
            
            // Check which request title documents existed or not; not existed => remove;
            var removingReqTitleDocIds = existingDocsById.Keys
                .Except(dtoDocIds)
                .ToList();

            var removingReqTitleDocDtos = existingReqTitleDocsDtos.Where(dto => removingReqTitleDocIds.Contains(dto.Id!.Value)).ToList();
            
            // Remove existing Request Title Documents
            foreach (var titleDocDto in removingReqTitleDocDtos)
            {
                var removeResult = await _sender.Send(new RemoveLinkRequestTitleDocumentByIdCommand(titleDocDto.Id!.Value, requestTitleId), cancellationToken);

                if (titleDocDto.DocumentId != Guid.Empty && titleDocDto.DocumentId.HasValue)
                {
                    documentLinks.Add(new DocumentLink
                    {
                        EntityType = "Title",
                        EntityId = requestTitleId,
                        DocumentId = titleDocDto.DocumentId!.Value,
                        IsUnlinked = true
                    });
                }
            }
            
            // Check which request title documents existed or not; existed => update;
            var updatingReqTitleDocIds = existingDocsById.Keys
                .Intersect(dtoDocIds)
                .ToList();

            var updatingReqTitleDocDtos = existingReqTitleDocsDtos.Where(dto => updatingReqTitleDocIds.Contains(dto.Id!.Value)).ToList();

            foreach (var titleDocDto in updatingReqTitleDocDtos)
            {
                var updateResult = await _sender.Send(new UpdateLinkRequestTitleDocumentCommand(titleDocDto.Id!.Value,
                    new RequestTitleDocumentDto{
                        Id = titleDocDto.Id,
                        TitleId = titleDocDto.TitleId,
                        DocumentId = titleDocDto.DocumentId,
                        DocumentType = titleDocDto.DocumentType,
                        Filename = titleDocDto.Filename,
                        Prefix = titleDocDto.Prefix,
                        Set = titleDocDto.Set,
                        DocumentDescription = titleDocDto.DocumentDescription,
                        FilePath = titleDocDto.FilePath,
                        CreatedWorkstation = titleDocDto.CreatedWorkstation,
                        IsRequired = titleDocDto.IsRequired,
                        UploadedBy = titleDocDto.UploadedBy,
                        UploadedByName = titleDocDto.UploadedByName
                    })
                );

                if (titleDocDto.DocumentId != Guid.Empty && titleDocDto.DocumentId.HasValue)
                {
                    documentLinks.Add(new DocumentLink
                    {
                        EntityType = "Title",
                        EntityId = requestTitleId,
                        DocumentId = titleDocDto.DocumentId!.Value,
                        IsUnlinked = false
                    });
                }
            }
        }
        
        // Case: new title
        var newTitlesDtos = requestTitleDtos.Where(rtd => !rtd.Id.HasValue).ToList();

        foreach (var titleDto in newTitlesDtos)
        {
            // create RequestTitle
            var createRequestTitleCommand = BuildDraftCommand(requestId, titleDto);

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

                if (titleDocDto.DocumentId != Guid.Empty && titleDocDto.DocumentId.HasValue)
                {
                    documentLinks.Add(new DocumentLink
                    {
                        EntityType = "Title",
                        EntityId = requestTitleId,
                        DocumentId = titleDocDto.DocumentId!.Value,
                        IsUnlinked = false
                    });
                }
            }
        }

        // Case: remove title
        var removingRequestTitleIds = existingRequestTitleIds
            .Except(dtoIds)
            .ToList();
        
        foreach (var requestTitleId in removingRequestTitleIds)
        {
            var existingReqTitleDocResult = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(requestTitleId), cancellationToken);
            var existingReqTitleDocDtos = existingReqTitleDocResult.RequestTitleDocuments;

            documentLinks.AddRange(existingReqTitleDocDtos.Where(rtd => rtd.DocumentId != Guid.Empty && rtd.DocumentId.HasValue).Select(rtd => new DocumentLink
            {
                EntityType = "Title",
                EntityId = requestTitleId,
                DocumentId = rtd.DocumentId!.Value,
                IsUnlinked = true
            }).ToList());

            var result = await _sender.Send(new RemoveRequestTitleCommand(requestId, requestTitleId));
            
            if (!result.Success)
                throw new Exception($"RequestTitle Id: {requestTitleId} cannot be removed");
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

}