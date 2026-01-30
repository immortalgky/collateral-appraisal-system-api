using MassTransit;
using Microsoft.Extensions.Logging;
using Request.Contracts.Requests.Dtos;
using Request.Infrastructure.Repositories;
using Shared.Messaging.Events;

namespace Request.Application.EventHandlers.Request;

public class RequestSubmittedEventHandler(
    ILogger<RequestSubmittedEventHandler> logger,
    IRequestTitleRepository requestTitleRepository,
    IBus bus) : INotificationHandler<RequestSubmittedEvent>
{
    public async Task Handle(RequestSubmittedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event handled: {DomainEvent} for RequestId: {RequestId}",
            notification.GetType().Name, notification.Request.Id);

        // Query all request titles for this request
        var requestTitles = await requestTitleRepository.GetByRequestIdAsync(
            notification.Request.Id,
            cancellationToken);

        // Map to DTOs
        var requestTitleDtos = requestTitles.Select(MapToDto).ToList();

        // Publish integration event
        var integrationEvent = new RequestSubmittedIntegrationEvent
        {
            RequestId = notification.Request.Id,
            RequestTitles = requestTitleDtos
        };

        await bus.Publish(integrationEvent, cancellationToken);

        logger.LogInformation(
            "Published RequestSubmittedIntegrationEvent for RequestId: {RequestId} with {TitleCount} titles",
            notification.Request.Id,
            requestTitleDtos.Count);
    }

    private static RequestTitleDto MapToDto(Domain.RequestTitles.RequestTitle title)
    {
        var dto = new RequestTitleDto
        {
            Id = title.Id,
            RequestId = title.RequestId,
            CollateralType = title.CollateralType ?? string.Empty,
            CollateralStatus = title.CollateralStatus ?? false,
            OwnerName = title.OwnerName,
            TitleAddress = new AddressDto(
                title.TitleAddress.HouseNumber,
                title.TitleAddress.ProjectName,
                title.TitleAddress.Moo,
                title.TitleAddress.Soi,
                title.TitleAddress.Road,
                title.TitleAddress.SubDistrict,
                title.TitleAddress.District,
                title.TitleAddress.Province,
                title.TitleAddress.Postcode
            ),
            DopaAddress = new AddressDto(
                title.DopaAddress.HouseNumber,
                title.DopaAddress.ProjectName,
                title.DopaAddress.Moo,
                title.DopaAddress.Soi,
                title.DopaAddress.Road,
                title.DopaAddress.SubDistrict,
                title.DopaAddress.District,
                title.DopaAddress.Province,
                title.DopaAddress.Postcode
            ),
            Notes = title.Notes,
            Documents = title.Documents.Select(d => new RequestTitleDocumentDto
            {
                Id = d.Id,
                TitleId = d.TitleId,
                DocumentId = d.DocumentId,
                DocumentType = d.DocumentType,
                Filename = d.Filename,
                Prefix = d.Prefix,
                Set = d.Set,
                DocumentDescription = d.DocumentDescription,
                FilePath = d.FilePath,
                CreatedWorkstation = d.CreatedWorkstation,
                IsRequired = d.IsRequired,
                UploadedBy = d.UploadedBy,
                UploadedByName = d.UploadedByName,
                UploadedAt = d.UploadedAt
            }).ToList()
        };

        // Map type-specific fields based on title type
        if (title is Domain.RequestTitles.TitleTypes.TitleLand landTitle)
        {
            dto = dto with
            {
                TitleNo = landTitle.TitleDeedInfo.TitleNo,
                DeedType = landTitle.TitleDeedInfo.DeedType,
                Rawang = landTitle.LandLocationInfo.Rawang,
                LandNo = landTitle.LandLocationInfo.LandNo,
                SurveyNo = landTitle.LandLocationInfo.SurveyNo,
                AreaRai = landTitle.LandArea.AreaRai,
                AreaNgan = landTitle.LandArea.AreaNgan,
                AreaSquareWa = landTitle.LandArea.AreaSquareWa
            };
        }
        // Additional type mappings can be added here for other collateral types

        return dto;
    }
}
