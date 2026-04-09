namespace Request.Application.EventHandlers.Request;

public class RequestSubmittedEventHandler(
    ILogger<RequestSubmittedEventHandler> logger,
    IRequestTitleRepository requestTitleRepository,
    IIntegrationEventOutbox outbox) : INotificationHandler<RequestSubmittedEvent>
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

        // Map Appointment data if available
        AppointmentDto? appointmentDto = null;
        if (notification.Request.Detail?.Appointment != null)
            appointmentDto = new AppointmentDto(
                notification.Request.Detail.Appointment.AppointmentDateTime,
                notification.Request.Detail.Appointment.AppointmentLocation);

        // Map Fee data if available
        FeeDto? feeDto = null;
        if (notification.Request.Detail?.Fee != null)
            feeDto = new FeeDto(
                notification.Request.Detail.Fee.FeePaymentType,
                notification.Request.Detail.Fee.FeeNotes,
                notification.Request.Detail.Fee.AbsorbedAmount,
                notification.Request.Detail.LoanDetail?.TotalSellingPrice);

        // Map Contact data if available
        ContactDto? contactDto = null;
        if (notification.Request.Detail?.Contact != null)
            contactDto = new ContactDto(
                notification.Request.Detail.Contact.ContactPersonName,
                notification.Request.Detail.Contact.ContactPersonPhone,
                notification.Request.Detail.Contact.DealerCode);

        // Publish integration event
        var integrationEvent = new RequestSubmittedIntegrationEvent
        {
            RequestId = notification.Request.Id,
            RequestTitles = requestTitleDtos,
            Appointment = appointmentDto,
            Fee = feeDto,
            Contact = contactDto,
            CreatedBy = notification.Request.Requestor.UserId,
            Priority = notification.Request.Priority,
            IsPma = notification.Request.IsPma,
            Purpose = notification.Request.Purpose,
            Channel = notification.Request.Channel,
            BankingSegment = notification.Request.Detail?.LoanDetail?.BankingSegment,
            FacilityLimit = notification.Request.Detail?.LoanDetail?.FacilityLimit,
            HasAppraisalBook = notification.Request.Detail?.HasAppraisalBook ?? false,
            RequestedBy = notification.Request.Requestor.Username,
            RequestedAt = notification.Request.RequestedAt
        };

        outbox.Publish(integrationEvent, correlationId: notification.Request.Id.ToString());

        logger.LogInformation(
            "Published RequestSubmittedIntegrationEvent for RequestId: {RequestId} with {TitleCount} titles",
            notification.Request.Id,
            requestTitleDtos.Count);
    }

    private static RequestTitleDto MapToDto(RequestTitle title)
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
                FileName = d.FileName,
                Prefix = d.Prefix,
                Set = d.Set,
                Notes = d.Notes,
                FilePath = d.FilePath,
                IsRequired = d.IsRequired,
                UploadedBy = d.UploadedBy,
                UploadedByName = d.UploadedByName,
                UploadedAt = d.UploadedAt
            }).ToList()
        };

        // Map type-specific fields based on the title type
        if (title is TitleLand landTitle)
            dto = dto with
            {
                TitleNumber = landTitle.TitleDeedInfo.TitleNumber,
                TitleType = landTitle.TitleDeedInfo.TitleType,
                Rawang = landTitle.LandLocationInfo.Rawang,
                LandParcelNumber = landTitle.LandLocationInfo.LandParcelNumber,
                SurveyNumber = landTitle.LandLocationInfo.SurveyNumber,
                BookNumber = landTitle.LandLocationInfo.BookNumber,
                PageNumber = landTitle.LandLocationInfo.PageNumber,
                MapSheetNumber = landTitle.LandLocationInfo.MapSheetNumber,
                AerialMapName = landTitle.LandLocationInfo.AerialMapName,
                AerialMapNumber = landTitle.LandLocationInfo.AerialMapNumber,
                AreaRai = landTitle.LandArea.AreaRai,
                AreaNgan = landTitle.LandArea.AreaNgan,
                AreaSquareWa = landTitle.LandArea.AreaSquareWa
            };
        else if (title is TitleLandBuilding lbTitle)
            dto = dto with
            {
                TitleNumber = lbTitle.TitleDeedInfo.TitleNumber,
                TitleType = lbTitle.TitleDeedInfo.TitleType,
                Rawang = lbTitle.LandLocationInfo.Rawang,
                LandParcelNumber = lbTitle.LandLocationInfo.LandParcelNumber,
                SurveyNumber = lbTitle.LandLocationInfo.SurveyNumber,
                BookNumber = lbTitle.LandLocationInfo.BookNumber,
                PageNumber = lbTitle.LandLocationInfo.PageNumber,
                MapSheetNumber = lbTitle.LandLocationInfo.MapSheetNumber,
                AerialMapName = lbTitle.LandLocationInfo.AerialMapName,
                AerialMapNumber = lbTitle.LandLocationInfo.AerialMapNumber,
                AreaRai = lbTitle.LandArea.AreaRai,
                AreaNgan = lbTitle.LandArea.AreaNgan,
                AreaSquareWa = lbTitle.LandArea.AreaSquareWa,
                BuildingType = lbTitle.BuildingInfo.BuildingType,
                UsableArea = lbTitle.BuildingInfo.UsableArea,
                NumberOfBuilding = lbTitle.BuildingInfo.NumberOfBuilding
            };
        else if (title is TitleCondo condoTitle)
            dto = dto with
            {
                TitleNumber = condoTitle.TitleDeedInfo.TitleNumber,
                TitleType = condoTitle.TitleDeedInfo.TitleType,
                CondoName = condoTitle.CondoInfo.CondoName,
                BuildingNumber = condoTitle.CondoInfo.BuildingNumber,
                RoomNumber = condoTitle.CondoInfo.RoomNumber,
                FloorNumber = condoTitle.CondoInfo.FloorNumber,
                UsableArea = condoTitle.CondoInfo.UsableArea
            };

        return dto;
    }
}