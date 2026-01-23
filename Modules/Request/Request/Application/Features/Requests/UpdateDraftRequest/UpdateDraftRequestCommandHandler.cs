namespace Request.Application.Features.Requests.UpdateDraftRequest;

internal class UpdateDraftRequestCommandHandler(
    IRequestRepository requestRepository,
    IRequestSyncService syncService
) : ICommandHandler<UpdateDraftRequestCommand, UpdateDraftRequestResult>
{
    public async Task<UpdateDraftRequestResult> Handle(UpdateDraftRequestCommand command,
        CancellationToken cancellationToken)
    {
        var request = await UpdateDraftRequestAsync(command, cancellationToken);

        if (command.Documents is not null)
            await syncService.SyncDocumentsAsync(request, command.Documents, cancellationToken);

        if (command.Titles is not null)
            await syncService.SyncTitlesAsync(command.Id, command.Titles, cancellationToken);

        return new UpdateDraftRequestResult(true);
    }

    private async Task<Domain.Requests.Request> UpdateDraftRequestAsync(
        UpdateDraftRequestCommand command,
        CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdWithDocumentsAsync(command.Id, cancellationToken);
        if (request is null) throw new RequestNotFoundException(command.Id);

        request.Save(new RequestData(
            command.Purpose,
            command.Channel,
            new UserInfo(command.Requestor.UserId, command.Requestor.Username),
            new UserInfo(command.Creator.UserId, command.Creator.Username),
            request.CreatedAt,
            command.Priority,
            command.IsPma
        ));

        request.SetDetail(RequestDetail.Create(new RequestDetailData(
            command.Detail?.HasAppraisalBook ?? false,
            LoanDetail.Create(new LoanDetailData(
                command.Detail?.LoanDetail?.BankingSegment,
                command.Detail?.LoanDetail?.LoanApplicationNumber,
                command.Detail?.LoanDetail?.FacilityLimit,
                command.Detail?.LoanDetail?.AdditionalFacilityLimit,
                command.Detail?.LoanDetail?.PreviousFacilityLimit,
                command.Detail?.LoanDetail?.TotalSellingPrice
            )),
            command.Detail?.PrevAppraisalId,
            Address.Create(new AddressData(
                command.Detail?.Address?.HouseNumber,
                command.Detail?.Address?.ProjectName,
                command.Detail?.Address?.Moo,
                command.Detail?.Address?.Soi,
                command.Detail?.Address?.Road,
                command.Detail?.Address?.SubDistrict,
                command.Detail?.Address?.District,
                command.Detail?.Address?.Province,
                command.Detail?.Address?.Postcode
            )),
            Contact.Create(
                command.Detail?.Contact?.ContactPersonName,
                command.Detail?.Contact?.ContactPersonPhone,
                command.Detail?.Contact?.DealerCode),
            Appointment.Create(
                command.Detail?.Appointment?.AppointmentDateTime,
                command.Detail?.Appointment?.AppointmentLocation),
            Fee.Create(
                command.Detail?.Fee?.FeePaymentType,
                command.Detail?.Fee?.FeeNotes,
                command.Detail?.Fee?.AbsorbedAmount)
        )));

        var customers = command.Customers?
            .Select(c => RequestCustomer.Create(c.Name, c.ContactNumber))
            .ToList();
        request.SetCustomers(customers);

        var properties = command.Properties?
            .Select(p => RequestProperty.Create(p.PropertyType, p.BuildingType, p.SellingPrice))
            .ToList();
        request.SetProperties(properties);

        return request;
    }
}