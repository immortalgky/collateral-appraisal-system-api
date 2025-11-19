using Request.RequestTitles.Features.CreateLinkRequestTitleDocument;
using Request.RequestTitles.Features.DraftRequestTitle;
using Request.RequestTitles.Features.GetLinkRequestTitleDocumentsByTitleId;
using Request.RequestTitles.Features.GetRequestTitlesByRequestId;
using Request.RequestTitles.Features.RemoveLinkRequestTitleDocument;
using Request.RequestTitles.Features.UpdateDraftRequestTitle;
using Request.RequestTitles.Features.UpdateRequestTitle;
using RequestTitleDto = Request.Contracts.Requests.Dtos.RequestTitleDto;

namespace Request.Services;

public class RequestTitleService : IRequestTitleService
{
    private readonly ISender _sender;

    public RequestTitleService(ISender sender)
    {
        _sender = sender;
    }

    public Task CreateRequestTitleAsync(RequestTitleDto requestTitleDto, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    public async Task CreateRequestTitlesAsync(Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken)
    {
        foreach (var requestTitleDto in requestTitleDtos)
        {
            // create RequestTitle
            var createRequestTitleCommand = new CreateRequestTitleCommand(
                requestId,
                requestTitleDto.CollateralType,
                requestTitleDto.CollateralStatus,
                new TitleDeedInfoDto(requestTitleDto.TitleNo, requestTitleDto.DeedType, requestTitleDto.TitleDetail),
                new SurveyInfoDto(requestTitleDto.Rawang, requestTitleDto.LandNo, requestTitleDto.SurveyNo),
                new LandAreaDto(requestTitleDto.AreaRai, requestTitleDto.AreaNgan, requestTitleDto.AreaSquareWa),
                requestTitleDto.OwnerName,
                requestTitleDto.RegistrationNumber,
                new VehicleDto(requestTitleDto.VehicleType, requestTitleDto.VehicleAppointmentLocation, requestTitleDto.ChassisNumber),
                new MachineDto(requestTitleDto.MachineryStatus, requestTitleDto.MachineryType, requestTitleDto.InstallationStatus, requestTitleDto.InvoiceNumber,
                    requestTitleDto.NumberOfMachinery),
                new BuildingInfoDto(requestTitleDto.BuildingType, requestTitleDto.UsableArea, requestTitleDto.NumberOfBuilding),
                new CondoInfoDto(requestTitleDto.CondoName, requestTitleDto.BuildingNo, requestTitleDto.RoomNo, requestTitleDto.FloorNo),
                requestTitleDto.TitleAddress,
                requestTitleDto.DopaAddress,
                requestTitleDto.Notes
            );
            var requestTitleResult = await _sender.Send(createRequestTitleCommand, cancellationToken);
            
            var titleId = requestTitleResult.TitleId;
            
            // Create RequestTitleDocument
            foreach (var requestTitleDocDto in requestTitleDto.RequestTitleDocuments)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    titleId,
                    requestTitleDocDto
                );
            
                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);
                
                if (!result.Success)
                    throw new Exception(
                        $"Cannot create link to DocumentId : {requestTitleDocDto.DocumentId}");
            }
        }
    }

    public async Task DraftRequestTitlesAsync(Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken)
    {
        foreach (var requestTitleDto in requestTitleDtos)
        {
            // create RequestTitle
            var draftRequestTitleCommand = new DraftRequestTitleCommand(
                requestId,
                requestTitleDto.CollateralType,
                requestTitleDto.CollateralStatus,
                new TitleDeedInfoDto(requestTitleDto.TitleNo, requestTitleDto.DeedType, requestTitleDto.TitleDetail),
                new SurveyInfoDto(requestTitleDto.Rawang, requestTitleDto.LandNo, requestTitleDto.SurveyNo),
                new LandAreaDto(requestTitleDto.AreaRai, requestTitleDto.AreaNgan, requestTitleDto.AreaSquareWa),
                requestTitleDto.OwnerName,
                requestTitleDto.RegistrationNumber,
                new VehicleDto(requestTitleDto.VehicleType, requestTitleDto.VehicleAppointmentLocation, requestTitleDto.ChassisNumber),
                new MachineDto(requestTitleDto.MachineryStatus, requestTitleDto.MachineryType, requestTitleDto.InstallationStatus, requestTitleDto.InvoiceNumber,
                    requestTitleDto.NumberOfMachinery),
                new BuildingInfoDto(requestTitleDto.BuildingType, requestTitleDto.UsableArea, requestTitleDto.NumberOfBuilding),
                new CondoInfoDto(requestTitleDto.CondoName, requestTitleDto.BuildingNo, requestTitleDto.RoomNo, requestTitleDto.FloorNo),
                requestTitleDto.TitleAddress,
                requestTitleDto.DopaAddress,
                requestTitleDto.Notes
            );
            var requestTitleResult = await _sender.Send(draftRequestTitleCommand, cancellationToken);
            
            var titleId = requestTitleResult.TitleId;
            
            // Create RequestTitleDocument
            foreach (var requestTitleDocDto in requestTitleDto.RequestTitleDocuments)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    titleId,
                    requestTitleDocDto
                );
            
                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);
                if (!result.Success)
                    throw new Exception(
                        $"Cannot create link where DocumentId is {requestTitleDocDto.DocumentId}");
            }
        }
    }

    public Task UpdateRequestTitleAsync(RequestTitleDto requestTitleDto, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    public async Task UpdateRequestTitlesAsync(Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken)
    {
        var requestTitlesResult = await _sender.Send(new GetRequestTitlesByRequestIdQuery(requestId), cancellationToken);
        
        if (requestTitlesResult is null)
            throw new Exception("RequestTitles not found");
        
        var existingRequestTitleIds = requestTitlesResult.RequestTitles.Select(rt => rt.Id).ToList();
        
        var newRequestTitleIds = requestTitleDtos.Where(rtd => rtd.Id is not null).Select(rtd => rtd.Id.Value).ToList();
        
        // Case: update title + linkDocs
        var updateList = newRequestTitleIds.Intersect(existingRequestTitleIds).ToList();
        var updateRequestTitle = requestTitleDtos.Where(rtd => updateList.Contains(rtd.Id.Value)).ToList();
        
        foreach (var requestTitleDto in updateRequestTitle)
        {
            // update RequestTitle
            var requestTitleResult = await _sender.Send(new UpdateRequestTitleCommand(
                requestId,
                requestTitleDto.Id.Value,
                requestTitleDto.CollateralType,
                requestTitleDto.CollateralStatus,
                new TitleDeedInfoDto(requestTitleDto.TitleNo, requestTitleDto.DeedType, requestTitleDto.TitleDetail),
                new SurveyInfoDto(requestTitleDto.Rawang, requestTitleDto.LandNo, requestTitleDto.SurveyNo),
                new LandAreaDto(requestTitleDto.AreaRai, requestTitleDto.AreaNgan, requestTitleDto.AreaSquareWa),
                requestTitleDto.OwnerName,
                requestTitleDto.RegistrationNumber,
                new VehicleDto(requestTitleDto.VehicleType, requestTitleDto.VehicleAppointmentLocation, requestTitleDto.ChassisNumber),
                new MachineDto(requestTitleDto.MachineryStatus, requestTitleDto.MachineryType, requestTitleDto.InstallationStatus, requestTitleDto.InvoiceNumber,
                    requestTitleDto.NumberOfMachinery),
                new BuildingInfoDto(requestTitleDto.BuildingType, requestTitleDto.UsableArea, requestTitleDto.NumberOfBuilding),
                new CondoInfoDto(requestTitleDto.CondoName, requestTitleDto.BuildingNo, requestTitleDto.RoomNo, requestTitleDto.FloorNo),
                requestTitleDto.TitleAddress,
                requestTitleDto.DopaAddress,
                requestTitleDto.Notes
            ));
            var requestTitleId = requestTitleResult.RequestTitleId;
            
            // Create RequestTitleDocument
            // 1. find new link
            var createdLink = requestTitleDto.RequestTitleDocuments.Where(rtd => rtd.Id is null).ToList();
            // 2. loop create
            foreach (var link in createdLink)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    requestTitleId,
                    link
                );
                
                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);
                // 2.1 check is success?
                if (!result.Success)
                    throw new Exception(
                        $"Cannot create link where DocumentId is {link.DocumentId}");
            }
            
            // update Link RequestTitleDocument
            // 1. get existing RequestTitleDocs from titleId
            var existingRequestTitleDocs = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(requestTitleId), cancellationToken);
            
            // 2. find LinkDocId which not contain in existing to remove
            var removedLinkIds = existingRequestTitleDocs.RequestTitleDocuments.Select(rtd => rtd.Id.Value).ToList().Except(requestTitleDto.RequestTitleDocuments.Where(rtd => rtd.Id != null).Select(rtd => rtd.Id.Value).ToList()).ToList();
            
            // 3. loop remove
            foreach (var linkId in removedLinkIds)
            {
                var result = await _sender.Send(new RemoveLinkRequestTitleDocumentCommand(linkId, requestTitleId));
                // 3.1 check is success?
                if (!result.Success)
                    throw new Exception($"RequestTitleDocument Id: {linkId} cannot be removed");
            }
        }
        
        // Case: new title
        var newList = requestTitleDtos.Where(rtd => rtd.Id is null).ToList();
        await CreateRequestTitlesAsync(requestId, newList, cancellationToken);
        
        // Case: remove title
        var removeList = existingRequestTitleIds.Except(newRequestTitleIds).ToList();
        foreach (var Id in removeList)
        {
            var result = await _sender.Send(new RemoveRequestTitleCommand(requestId, Id));
            
            if (!result.Success)
                throw new Exception($"Cannot remove RequestTitle where Id is {Id}");
        }
    }

    public async Task UpdateDraftRequestTitlesAsync(Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken)
    {
        var requestTitlesResult = await _sender.Send(new GetRequestTitlesByRequestIdQuery(requestId), cancellationToken);
        
        if (requestTitlesResult is null)
            throw new Exception("RequestTitles not found");
        
        var existingRequestTitleIds = requestTitlesResult.RequestTitles.Select(rt => rt.Id).ToList();
        var newRequestTitleIds = requestTitleDtos.Where(rtd => rtd.Id is not null).Select(rtd => rtd.Id.Value).ToList();
        
        // Case: update title + linkDocs
        var updateList = newRequestTitleIds.Intersect(existingRequestTitleIds).ToList();
        var updateRequestTitle = requestTitleDtos.Where(rtd => updateList.Contains(rtd.Id.Value)).ToList();
        
        foreach (var requestTitleDto in updateRequestTitle)
        {
            // update RequestTitle
            var requestTitleResult = await _sender.Send(new UpdateDraftRequestTitleCommand(
                requestId,
                requestTitleDto.Id.Value,
                requestTitleDto.CollateralType,
                requestTitleDto.CollateralStatus,
                new TitleDeedInfoDto(requestTitleDto.TitleNo, requestTitleDto.DeedType, requestTitleDto.TitleDetail),
                new SurveyInfoDto(requestTitleDto.Rawang, requestTitleDto.LandNo, requestTitleDto.SurveyNo),
                new LandAreaDto(requestTitleDto.AreaRai, requestTitleDto.AreaNgan, requestTitleDto.AreaSquareWa),
                requestTitleDto.OwnerName,
                requestTitleDto.RegistrationNumber,
                new VehicleDto(requestTitleDto.VehicleType, requestTitleDto.VehicleAppointmentLocation, requestTitleDto.ChassisNumber),
                new MachineDto(requestTitleDto.MachineryStatus, requestTitleDto.MachineryType, requestTitleDto.InstallationStatus, requestTitleDto.InvoiceNumber,
                    requestTitleDto.NumberOfMachinery),
                new BuildingInfoDto(requestTitleDto.BuildingType, requestTitleDto.UsableArea, requestTitleDto.NumberOfBuilding),
                new CondoInfoDto(requestTitleDto.CondoName, requestTitleDto.BuildingNo, requestTitleDto.RoomNo, requestTitleDto.FloorNo),
                requestTitleDto.TitleAddress,
                requestTitleDto.DopaAddress,
                requestTitleDto.Notes,
                requestTitleDtos
            ));
            var requestTitleId = requestTitleResult.RequestTitleId;
            
            // Create RequestTitleDocument
            // 1. find new link
            var createdLink = requestTitleDto.RequestTitleDocuments.Where(rtd => rtd.Id is null).ToList();
            // 2. loop create
            foreach (var link in createdLink)
            {
                var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand(
                    requestTitleId,
                    link
                );
                
                var result = await _sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);
                // 2.1 check is success?
                if (!result.Success)
                    throw new Exception(
                        $"Cannot create link where DocumentId is {link.DocumentId}");
            }
            
            // update Link RequestTitleDocument
            // 1. get existing RequestTitleDocs from titleId
            var existingRequestTitleDocs = await _sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(requestTitleId), cancellationToken);
            
            // 2. find LinkDocId which not contain in existing to remove
            var removedLinkIds = existingRequestTitleDocs.RequestTitleDocuments.Select(rtd => rtd.Id.Value).ToList().Except(requestTitleDto.RequestTitleDocuments.Where(rtd => rtd.Id != null).Select(rtd => rtd.Id.Value).ToList()).ToList();
            
            // 3. loop remove
            foreach (var linkId in removedLinkIds)
            {
                var result = await _sender.Send(new RemoveLinkRequestTitleDocumentCommand(linkId, requestTitleId));
                // 3.1 check is success?
                if (!result.Success)
                    throw new Exception($"RequestTitleDocument Id: {linkId} cannot be removed");
            }
        }
        
        // Case: new title
        var newList = requestTitleDtos.Where(rtd => rtd.Id is null).ToList();
        await CreateRequestTitlesAsync(requestId, newList, cancellationToken);
        
        // Case: remove title
        var removeList = existingRequestTitleIds.Except(newRequestTitleIds).ToList();
        foreach (var Id in removeList)
        {
            var result = await _sender.Send(new RemoveRequestTitleCommand(requestId, Id));
            
            if (!result.Success)
                throw new Exception($"Cannot remove RequestTitle where Id is {Id}");
        }
    }
}