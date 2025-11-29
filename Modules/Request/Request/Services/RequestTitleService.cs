// using MassTransit;
// using Microsoft.CodeAnalysis.CSharp;
// using Request.RequestTitles.Features.CreateLinkRequestTitleDocument;
// using Request.RequestTitles.Features.DraftRequestTitle;
// using Request.RequestTitles.Features.GetLinkRequestTitleDocumentsByTitleId;
// using Request.RequestTitles.Features.GetRequestTitlesByRequestId;
// using Request.RequestTitles.Features.RemoveLinkRequestTitleDocument;
// using Request.RequestTitles.Features.UpdateDraftRequestTitle;
// using Request.RequestTitles.Features.UpdateLinkRequestTitleDocument;
// using Request.RequestTitles.Features.UpdateRequestTitle;
// using Shared.Messaging.Events;
// using RequestTitleDto = Request.Contracts.Requests.Dtos.RequestTitleDto;

// namespace Request.Services;

// public class RequestTitleService : IRequestTitleService
// {
//     private readonly ISender _sender;
//     private readonly IBus _bus;

//     public RequestTitleService(ISender sender, IBus bus)
//     {
//         _sender = sender;
//         _bus = bus;
//     }

//     public Task CreateRequestTitleAsync(Guid sessionId, Guid requestId, RequestTitleDto requestTitleDto, CancellationToken cancellation)
//     {
//         throw new NotImplementedException();
//     }

//     public async Task<List<RequestTitleDto>> CreateRequestTitlesAsync(Guid sessionId, Guid requestId, List<RequestTitleDto> requestTitleDtos,
//         CancellationToken cancellationToken)
//     {
//         var documentLinks = new List<DocumentLink>();

//         var requestTitles = new List<RequestTitleDto>();

//         if (requestTitleDtos.Count <= 0)
//             throw new NotFoundException("Request titles not found");

//         foreach (var requestTitleDto in requestTitleDtos)
//         {
//             // Create Request Titles
//             var createRequestTitleCommand = BuildCreateCommand(requestId, requestTitleDto);
//             var requestTitle = await _sender.Send(createRequestTitleCommand, cancellationToken);

//             requestTitles.Add(requestTitleDto with { Id = requestTitle.Id, RequestId = requestTitle.RequestId });

//             // Create Request Title Documents
//             foreach (var requestTitleDocDto in requestTitleDto.RequestTitleDocumentDtos)
//             {
//                 var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
//                     requestTitle.Id,
//                     requestTitleDocDto
//                 );

//                 var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);

//                 if (requestTitleDocDto.DocumentId != Guid.Empty && requestTitleDocDto.DocumentId.HasValue)
//                 {
//                     documentLinks.Add(new DocumentLink
//                     {
//                         EntityType = "Title",
//                         EntityId = requestTitle.Id,
//                         DocumentId = requestTitleDocDto.DocumentId!.Value,
//                         IsUnlinked = false
//                     });
//                 }
//             }

//         }

//         await _bus.Publish(new DocumentLinkedIntegrationEvent
//         {
//             SessionId = sessionId,
//             DocumentLinks = documentLinks
//         }, cancellationToken);

//         return requestTitles;
//     }

//     public async Task<List<RequestTitleDto>> DraftRequestTitlesAsync(Guid sessionId, Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken)
//     {
//         var documentLinks = new List<DocumentLink>();

//         var requestTitles = new List<RequestTitleDto>();

//         if (requestTitleDtos.Count <= 0)
//             throw new NotFoundException("Request titles not found");

//         foreach (var requestTitleDto in requestTitleDtos)
//         {
//             // Create Request Titles
//             var draftRequestTitleCommand = BuildDraftCommand(requestId, requestTitleDto); 
//             var requestTitle = await _sender.Send(draftRequestTitleCommand, cancellationToken);
//             var requestTitleId = requestTitle.Id;

//             requestTitles.Add(requestTitleDto with { Id = requestTitle.Id, RequestId = requestTitle.RequestId });

//             // Create Request Title Documents
//             foreach (var requestTitleDocDto in requestTitleDto.RequestTitleDocumentDtos)
//             {
//                 var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
//                     requestTitleId,
//                     requestTitleDocDto
//                 );

//                 var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);
                
//                 if (requestTitleDocDto.DocumentId != Guid.Empty && requestTitleDocDto.DocumentId.HasValue)
//                 {
//                     documentLinks.Add(new DocumentLink
//                     {
//                         EntityType = "Title",
//                         EntityId = requestTitleId,
//                         DocumentId = requestTitleDocDto.DocumentId!.Value,
//                         IsUnlinked = false
//                     });
//                 }
//             }
//         }

//         await _bus.Publish(new DocumentLinkedIntegrationEvent
//         {
//             SessionId = sessionId,
//             DocumentLinks = documentLinks
//         }, cancellationToken);

//         return requestTitles;
//     }

//     public Task UpdateRequestTitleAsync(Guid sessionId, Guid requestId, RequestTitleDto requestTitleDto, CancellationToken cancellationToken)
//     {
//         throw new NotImplementedException();
//     }

//     /*
//      * 1. Create new request title
//      * 1.1 Create new link => publish event
//      * 2. Update existing request title 
//      * 2.1 Create new link => publish event
//      * 2.2 Remove link => publish event
//      * 3. Remove existing request title 
//      * 3.1 Remove link => publish event
//      */

//     /*
//         == request title ==
//         1. Updating list
//         2. Removing list
//         3. Creating list
//         == title doc ==
//         1. Updating list
//         2. Removing list
//         3. Creating list

//         loop titleDto in dtos:
//             if (1)
//                 action:
//             if (2)
//                 action:
//             if (3)
//                 action:
//             loop titleDocDto in titleDto:
//                 if (1)
//                     action:
//                 if (2)
//                     action:
//                 if (3)
//                     action:
//     */
//     public async Task UpdateRequestTitlesAsync(Guid sessionId, Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken)
//     {
//         var documentLinks = new List<DocumentLink>();
//         var requestTitles = new List<RequestTitleDto>();

//         var requestTitlesResult = await _sender.Send(new GetRequestTitlesByRequestIdQuery(requestId), cancellationToken);

//         if (requestTitlesResult is null)
//             throw new RequestNotFoundException(requestId);

//         var existingRequestTitles = requestTitlesResult.RequestTitles;
//         var existingRequestTitleIds = requestTitlesResult.RequestTitles.Select(rt => rt.Id!.Value).ToHashSet();

//         var updatingRequestTitles = requestTitleDtos.Where(rtd => rtd.Id.HasValue && rtd.Id != Guid.Empty).ToList();
//         var updatingRequestTitleIds = updatingRequestTitles.Select(rtd => rtd.Id!.Value).ToHashSet();

//         var creatingRequestTitles = requestTitleDtos.Where(rtd => !rtd.Id.HasValue || rtd.Id == Guid.Empty).ToList();

//         var removingRequestTitleIds = existingRequestTitleIds.Except(requestTitleDtos.Where(rtd => rtd.Id.HasValue && rtd.Id.Value != Guid.Empty).Select(rtd => rtd.Id!.Value).ToHashSet()).ToHashSet();
//         var removingRequestTitles = existingRequestTitles.Where(rtd => removingRequestTitleIds.Contains(rtd.Id!.Value)).ToList();

//         foreach (var requestTitle in removingRequestTitles)
//         {
//             var existingReqTitleDocResult = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(requestTitle.Id!.Value), cancellationToken);
//             var existingReqTitleDocs = existingReqTitleDocResult.RequestTitleDocuments;

//             if (existingReqTitleDocs.Count > 0)
//             {
//                 documentLinks.AddRange(existingReqTitleDocs.Where(rtd => rtd.DocumentId.HasValue && rtd.DocumentId != Guid.Empty).Select(rtd => new DocumentLink
//                 {
//                     EntityType = "Title",
//                     EntityId = requestTitle.Id!.Value,
//                     DocumentId = rtd.DocumentId!.Value,
//                     IsUnlinked = true
//                 }).ToList());
//             }

//             var result = await _sender.Send(new RemoveRequestTitleCommand(requestId, requestTitle.Id!.Value), cancellationToken);
//         }

//         foreach (var requestTitle in creatingRequestTitles)
//         {
//             var createRequestTitleCommand = BuildCreateCommand(requestId, requestTitle);
//             var requestTitleResult = await _sender.Send(createRequestTitleCommand, cancellationToken);

//             foreach (var reqTitleDoc in requestTitle.RequestTitleDocumentDtos)
//             {
//                 var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(requestTitleResult.Id, reqTitleDoc);
//                 var createLinkRequestTitleDocResult = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);

//                 if (reqTitleDoc.DocumentId.HasValue && reqTitleDoc.DocumentId != Guid.Empty)
//                 {
//                     documentLinks.Add(new DocumentLink
//                     {
//                         EntityType = "Title",
//                         EntityId = requestTitleResult.Id,
//                         DocumentId = reqTitleDoc.DocumentId!.Value,
//                         IsUnlinked = false
//                     });
//                 }
//             }
//         }

//         foreach (var requestTitle in updatingRequestTitles)
//         {
//             /*
//                 in case that update type of title information, EF core not allow to change it directly. So, we have to remove existing and create the new one.
//             */
//             var updateRequestTitleCommand = BuildUpdateCommand(requestId, requestTitle);
//             var updateRequestTitleResult = await _sender.Send(updateRequestTitleCommand, cancellationToken);

//             var existingReqTitleDocsResult = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(requestTitle.Id!.Value), cancellationToken);

//             var existingReqTitleDocs = existingReqTitleDocsResult.RequestTitleDocuments;
//             var existingReqTitleDocIds = existingReqTitleDocs.Select(rt => rt.Id!.Value).ToHashSet();

//             var removingReqTitleDocIds = existingReqTitleDocIds.Except(requestTitle.RequestTitleDocumentDtos.Where(d => d.Id.HasValue && d.Id.Value != Guid.Empty).Select(d => d.Id!.Value).ToHashSet()).ToHashSet();
//             var removingReqTitleDocs = existingReqTitleDocs.Where(rtd => removingReqTitleDocIds.Contains(rtd.Id!.Value)).ToList();

//             var creatingReqTitleDocs = requestTitle.RequestTitleDocumentDtos.Where(rtd => !rtd.Id.HasValue || rtd.Id!.Value == Guid.Empty).ToList();

//             var updatingReqTitleDocs = requestTitle.RequestTitleDocumentDtos.Where(rtd => rtd.Id.HasValue && rtd.Id != Guid.Empty).ToList();
//             var updatingReqTitleDocIds = updatingReqTitleDocs.Select(rtd => rtd.Id!.Value);

//             foreach (var reqTitleDoc in removingReqTitleDocs)
//             {
//                 if (reqTitleDoc.DocumentId.HasValue && reqTitleDoc.DocumentId != Guid.Empty)
//                 {
//                     documentLinks.Add(new DocumentLink
//                     {
//                         EntityType = "Title",
//                         EntityId = reqTitleDoc.TitleId!.Value,
//                         DocumentId = reqTitleDoc.DocumentId!.Value,
//                         IsUnlinked = true
//                     });
//                 }

//                 var result = await _sender.Send(new RemoveLinkRequestTitleDocumentByIdCommand(reqTitleDoc.Id!.Value, reqTitleDoc.TitleId!.Value), cancellationToken);
//             }

//             foreach (var reqTitleDoc in creatingReqTitleDocs)
//             {
//                 var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(requestTitle.Id!.Value, reqTitleDoc);
//                 var createLinkRequestTitleDocResult = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);

//                 if (reqTitleDoc.DocumentId.HasValue && reqTitleDoc.DocumentId != Guid.Empty)
//                 {
//                     documentLinks.Add(new DocumentLink
//                     {
//                         EntityType = "Title",
//                         EntityId = requestTitle.Id!.Value,
//                         DocumentId = reqTitleDoc.DocumentId!.Value,
//                         IsUnlinked = false
//                     });
//                 }
//             }

//             foreach (var reqTitleDoc in updatingReqTitleDocs)
//             {
//                 var updateLinkRequestTitleDocumentCommand = new UpdateLinkRequestTitleDocumentCommand(
//                     requestTitle.Id!.Value,
//                     reqTitleDoc
//                 );
//                 var updateLinkRequestTitleDocumentResult = await _sender.Send(updateLinkRequestTitleDocumentCommand, cancellationToken);

//                 if (!reqTitleDoc.DocumentId.HasValue || reqTitleDoc.DocumentId == Guid.Empty) // update only uploded document
//                 {
//                     documentLinks.Add(new DocumentLink
//                     {
//                         EntityType = "Title",
//                         EntityId = requestTitle.Id!.Value,
//                         DocumentId = reqTitleDoc.DocumentId!.Value,
//                         IsUnlinked = false
//                     });
//                 }
//             }
//         }

//         await _bus.Publish(new DocumentLinkedIntegrationEvent
//         {
//             SessionId = sessionId,
//             DocumentLinks = documentLinks
//         }, cancellationToken);
//     }

//     public async Task UpdateDraftRequestTitlesAsync(Guid sessionId, Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken)
//     {
//         var documentLinks = new List<DocumentLink>();
        
//         var requestTitlesResult = await _sender.Send(new GetRequestTitlesByRequestIdQuery(requestId), cancellationToken);
        
//         if (requestTitlesResult is null)
//             throw new RequestNotFoundException(requestId);
        
//         var existingRequestTitleIds = requestTitlesResult.RequestTitles.Select(rt => rt.Id!.Value).ToHashSet();
        
//         var dtosWithId = requestTitleDtos
//             .Where(rtd => rtd.Id.HasValue)
//             .ToList();
        
//         var dtoIds = dtosWithId
//             .Select(rtd => rtd.Id!.Value)
//             .ToHashSet();
        
//         var updatingReqTitleDtos = dtosWithId
//             .Where(rtd => existingRequestTitleIds.Contains(rtd.Id!.Value))
//             .ToList();
        
//         foreach (var requestTitleDto in updatingReqTitleDtos)
//         {
//             // snapshot existing link
//             var existingReqTitleDocsResult = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(requestTitleDto.Id!.Value), cancellationToken);
//             var existingReqTitleDocsDtos = existingReqTitleDocsResult.RequestTitleDocuments;
//             var existingDocsById = existingReqTitleDocsDtos
//                 .Where(d => d.Id.HasValue)
//                 .ToDictionary(d => d.Id!.Value);
            
//             // update RequestTitle
//             var draftRequestTitleCommand = BuildDraftCommand(requestId, requestTitleDto);
//             var requestTitleResult = await _sender.Send(draftRequestTitleCommand, cancellationToken);
//             var requestTitleId = requestTitleResult.Id;

//             // request title documents which don't have any Id => create new request title document
//             var dtoDocNew = requestTitleDto.RequestTitleDocumentDtos
//                 .Where(d => !d.Id.HasValue)
//                 .ToList();
            
//             // request title documents which have Id => check with existing to be removed or updated
//             var dtoDocWithId = requestTitleDto.RequestTitleDocumentDtos
//                 .Where(d => d.Id.HasValue)
//                 .ToList();

//             var dtoDocIds = dtoDocWithId
//                 .Select(d => d.Id!.Value)
//                 .ToHashSet();
            
//             // Create new Request Title Documents
//             // we will create every mandatory documents even it doesn't have document id but we won't publish in case case that it doesn't have document Id
//             foreach (var dtoDoc in dtoDocNew)
//             {
//                 var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
//                     requestTitleId,
//                     dtoDoc
//                 );
                
//                 var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);
                
//                 if (dtoDoc.DocumentId != Guid.Empty && dtoDoc.DocumentId.HasValue)
//                 {
//                     documentLinks.Add(new DocumentLink
//                     {
//                         EntityType = "Title",
//                         EntityId = requestTitleId,
//                         DocumentId = dtoDoc.DocumentId!.Value,
//                         IsUnlinked = false
//                     });
//                 }
//             }
            
//             // Check which request title documents existed or not; not existed => remove;
//             var removingReqTitleDocIds = existingDocsById.Keys
//                 .Except(dtoDocIds)
//                 .ToList();

//             var removingReqTitleDocDtos = existingReqTitleDocsDtos.Where(dto => removingReqTitleDocIds.Contains(dto.Id!.Value)).ToList();
            
//             // Remove existing Request Title Documents
//             foreach (var titleDocDto in removingReqTitleDocDtos)
//             {
//                 var removeResult = await _sender.Send(new RemoveLinkRequestTitleDocumentByIdCommand(titleDocDto.Id!.Value, requestTitleId), cancellationToken);

//                 if (titleDocDto.DocumentId != Guid.Empty && titleDocDto.DocumentId.HasValue)
//                 {
//                     documentLinks.Add(new DocumentLink
//                     {
//                         EntityType = "Title",
//                         EntityId = requestTitleId,
//                         DocumentId = titleDocDto.DocumentId!.Value,
//                         IsUnlinked = true
//                     });
//                 }
//             }
            
//             // Check which request title documents existed or not; existed => update;
//             var updatingReqTitleDocIds = existingDocsById.Keys
//                 .Intersect(dtoDocIds)
//                 .ToList();

//             var updatingReqTitleDocDtos = existingReqTitleDocsDtos.Where(dto => updatingReqTitleDocIds.Contains(dto.Id!.Value)).ToList();

//             foreach (var titleDocDto in updatingReqTitleDocDtos)
//             {
//                 var updateResult = await _sender.Send(new UpdateLinkRequestTitleDocumentCommand(titleDocDto.Id!.Value,
//                     new RequestTitleDocumentDto{
//                         Id = titleDocDto.Id,
//                         TitleId = titleDocDto.TitleId,
//                         DocumentId = titleDocDto.DocumentId,
//                         DocumentType = titleDocDto.DocumentType,
//                         Filename = titleDocDto.Filename,
//                         Prefix = titleDocDto.Prefix,
//                         Set = titleDocDto.Set,
//                         DocumentDescription = titleDocDto.DocumentDescription,
//                         FilePath = titleDocDto.FilePath,
//                         CreatedWorkstation = titleDocDto.CreatedWorkstation,
//                         IsRequired = titleDocDto.IsRequired,
//                         UploadedBy = titleDocDto.UploadedBy,
//                         UploadedByName = titleDocDto.UploadedByName
//                     })
//                 );

//                 if (titleDocDto.DocumentId != Guid.Empty && titleDocDto.DocumentId.HasValue)
//                 {
//                     documentLinks.Add(new DocumentLink
//                     {
//                         EntityType = "Title",
//                         EntityId = requestTitleId,
//                         DocumentId = titleDocDto.DocumentId!.Value,
//                         IsUnlinked = false
//                     });
//                 }
//             }
//         }
        
//         // Case: new title
//         var newTitlesDtos = requestTitleDtos.Where(rtd => !rtd.Id.HasValue).ToList();

//         foreach (var titleDto in newTitlesDtos)
//         {
//             // create RequestTitle
//             var createRequestTitleCommand = BuildDraftCommand(requestId, titleDto);

//             var requestTitleResult = await _sender.Send(createRequestTitleCommand, cancellationToken);

//             var requestTitleId = requestTitleResult.Id;

//             // Create RequestTitleDocument
//             foreach (var titleDocDto in titleDto.RequestTitleDocumentDtos)
//             {
//                 var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
//                     requestTitleId,
//                     titleDocDto
//                 );

//                 var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);

//                 if (titleDocDto.DocumentId != Guid.Empty && titleDocDto.DocumentId.HasValue)
//                 {
//                     documentLinks.Add(new DocumentLink
//                     {
//                         EntityType = "Title",
//                         EntityId = requestTitleId,
//                         DocumentId = titleDocDto.DocumentId!.Value,
//                         IsUnlinked = false
//                     });
//                 }
//             }
//         }

//         // Case: remove title
//         var removingRequestTitleIds = existingRequestTitleIds
//             .Except(dtoIds)
//             .ToList();
        
//         foreach (var requestTitleId in removingRequestTitleIds)
//         {
//             var existingReqTitleDocResult = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(requestTitleId), cancellationToken);
//             var existingReqTitleDocDtos = existingReqTitleDocResult.RequestTitleDocuments;

//             documentLinks.AddRange(existingReqTitleDocDtos.Where(rtd => rtd.DocumentId != Guid.Empty && rtd.DocumentId.HasValue).Select(rtd => new DocumentLink
//             {
//                 EntityType = "Title",
//                 EntityId = requestTitleId,
//                 DocumentId = rtd.DocumentId!.Value,
//                 IsUnlinked = true
//             }).ToList());

//             var result = await _sender.Send(new RemoveRequestTitleCommand(requestId, requestTitleId));
            
//             if (!result.Success)
//                 throw new Exception($"RequestTitle Id: {requestTitleId} cannot be removed");
//         }

//         await _bus.Publish(new DocumentLinkedIntegrationEvent
//         {
//             SessionId = sessionId,
//             DocumentLinks = documentLinks,
//         }, cancellationToken);
//     }

    
//     private CreateRequestTitleCommand BuildCreateCommand(Guid requestId, RequestTitleDto dto)
//     {
//         return new CreateRequestTitleCommand(
//             requestId,
//             dto.CollateralType,
//             dto.CollateralStatus,
//             new TitleDeedInfoDto(dto.TitleNo, dto.DeedType, dto.TitleDetail),
//             new SurveyInfoDto(dto.Rawang, dto.LandNo, dto.SurveyNo),
//             new LandAreaDto(dto.AreaRai, dto.AreaNgan, dto.AreaSquareWa),
//             dto.OwnerName,
//             dto.RegistrationNo,
//             new VehicleDto(dto.VehicleType, dto.VehicleAppointmentLocation,
//                 dto.ChassisNumber),
//             new MachineDto(dto.MachineStatus, dto.MachineType,
//                 dto.InstallationStatus, dto.InvoiceNumber,
//                 dto.NumberOfMachine),
//             new BuildingInfoDto(dto.BuildingType, dto.UsableArea,
//                 dto.NumberOfBuilding),
//             new CondoInfoDto(dto.CondoName, dto.BuildingNo, dto.RoomNo,
//                 dto.FloorNo),
//             dto.TitleAddress,
//             dto.DopaAddress,
//             dto.Notes
//         );
//     }

//     private DraftRequestTitleCommand BuildDraftCommand(Guid requestId, RequestTitleDto dto)
//     {
//         return new DraftRequestTitleCommand(
//             requestId,
//             dto.CollateralType,
//             dto.CollateralStatus,
//             new TitleDeedInfoDto(dto.TitleNo, dto.DeedType, dto.TitleDetail),
//             new SurveyInfoDto(dto.Rawang, dto.LandNo, dto.SurveyNo),
//             new LandAreaDto(dto.AreaRai, dto.AreaNgan, dto.AreaSquareWa),
//             dto.OwnerName,
//             dto.RegistrationNo,
//             new VehicleDto(dto.VehicleType, dto.VehicleAppointmentLocation,
//                 dto.ChassisNumber),
//             new MachineDto(dto.MachineStatus, dto.MachineType,
//                 dto.InstallationStatus, dto.InvoiceNumber,
//                 dto.NumberOfMachine),
//             new BuildingInfoDto(dto.BuildingType, dto.UsableArea,
//                 dto.NumberOfBuilding),
//             new CondoInfoDto(dto.CondoName, dto.BuildingNo, dto.RoomNo,
//                 dto.FloorNo),
//             dto.TitleAddress,
//             dto.DopaAddress,
//             dto.Notes
//         );
//     }

//     private UpdateRequestTitleCommand BuildUpdateCommand(Guid requestId, RequestTitleDto dto)
//     {
//         return new UpdateRequestTitleCommand(
//             requestId,
//             dto.Id!.Value,
//             dto.CollateralType,
//             dto.CollateralStatus,
//             new TitleDeedInfoDto(dto.TitleNo, dto.DeedType, dto.TitleDetail),
//             new SurveyInfoDto(dto.Rawang, dto.LandNo, dto.SurveyNo),
//             new LandAreaDto(dto.AreaRai, dto.AreaNgan, dto.AreaSquareWa),
//             dto.OwnerName,
//             dto.RegistrationNo,
//             new VehicleDto(dto.VehicleType, dto.VehicleAppointmentLocation,
//                 dto.ChassisNumber),
//             new MachineDto(dto.MachineStatus, dto.MachineType,
//                 dto.InstallationStatus, dto.InvoiceNumber,
//                 dto.NumberOfMachine),
//             new BuildingInfoDto(dto.BuildingType, dto.UsableArea,
//                 dto.NumberOfBuilding),
//             new CondoInfoDto(dto.CondoName, dto.BuildingNo, dto.RoomNo,
//                 dto.FloorNo),
//             dto.TitleAddress,
//             dto.DopaAddress,
//             dto.Notes
//         );
//     }

//     private UpdateDraftRequestTitleCommand BuildDraftUpdateCommand(Guid requestId, RequestTitleDto dto)
//     {
//         return new UpdateDraftRequestTitleCommand(
//             requestId,
//             dto.Id!.Value,
//             dto.CollateralType,
//             dto.CollateralStatus,
//             new TitleDeedInfoDto(dto.TitleNo, dto.DeedType, dto.TitleDetail),
//             new SurveyInfoDto(dto.Rawang, dto.LandNo, dto.SurveyNo),
//             new LandAreaDto(dto.AreaRai, dto.AreaNgan, dto.AreaSquareWa),
//             dto.OwnerName,
//             dto.RegistrationNo,
//             new VehicleDto(dto.VehicleType, dto.VehicleAppointmentLocation,
//                 dto.ChassisNumber),
//             new MachineDto(dto.MachineStatus, dto.MachineType,
//                 dto.InstallationStatus, dto.InvoiceNumber,
//                 dto.NumberOfMachine),
//             new BuildingInfoDto(dto.BuildingType, dto.UsableArea,
//                 dto.NumberOfBuilding),
//             new CondoInfoDto(dto.CondoName, dto.BuildingNo, dto.RoomNo,
//                 dto.FloorNo),
//             dto.TitleAddress,
//             dto.DopaAddress,
//             dto.Notes
//         );
//     }
// }