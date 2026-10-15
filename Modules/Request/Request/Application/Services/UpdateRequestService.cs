namespace Request.Application.Services;

public class UpdateRequestService(
    IRequestRepository requestRepository,
    ISender mediator
) : IUpdateRequestService
{
    public async Task<Domain.Requests.Request> GetByIdWithDocumentsAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdWithDocumentsAsync(requestId, cancellationToken);
        if (request is null) throw new RequestNotFoundException(requestId);
        return request;
    }

    public async Task<Domain.Requests.Request> ResubmitRequestAsync(ResubmitRequestData command, CancellationToken cancellationToken)
    {
        // The DTO models Detail/Customers/Properties as nullable so the data record can be shared
        // with paths that don't carry them (e.g. document-followup resubmit). On THIS path they are
        // required — fail fast rather than NRE inside the dereferences below.
        ArgumentNullException.ThrowIfNull(command.Detail);
        ArgumentNullException.ThrowIfNull(command.Customers);
        ArgumentNullException.ThrowIfNull(command.Properties);

        var request = await requestRepository.GetByIdWithDocumentsAsync(command.RequestId, cancellationToken);
        if (request is null) throw new RequestNotFoundException(command.RequestId);

        // Once submitted the appraisal has been built from the purpose and prior, and nothing re-bases it, so
        // LOS may resend them but not change them (the same rule as the screen's update). Rejected before
        // anything is written.
        request.EnsurePurposeUnchanged(command.Purpose);

        request.Save(new RequestData(
            command.Purpose,
            command.Channel,
            new UserInfo(command.Requestor.UserId, command.Requestor.Username),
            new UserInfo(command.Creator.UserId, command.Creator.Username),
            request.CreatedAt,
            command.Priority,
            command.IsPma
        ));

        var prevAppraisalId = PriorAppraisalFields.NormalizeId(command.Detail.PrevAppraisalId);

        // A submitted request keeps its stored prior as is: the id or number LOS sent is only checked against it.
        // Re-resolving would cost a lookup per resubmit, and one that comes back empty (the prior was deleted
        // since) must not wipe what the request was submitted with. The submit-time guard is skipped for the same
        // reason: purpose and prior are unchanged, so the combination it checked at submit still stands.
        var locked = request.HasBeenSubmitted();
        PriorAppraisalFields? prior = null;
        if (locked)
        {
            request.EnsurePriorUnchanged(prevAppraisalId, command.Detail.PrevAppraisalNumber);
            // LOS may identify a CAS prior by number only; the stored id stays.
            prevAppraisalId = request.Detail?.PrevAppraisalId;
        }
        else
            // Resubmit replaces the request data, so the prior appraisal is whatever LOS sent now: an id is
            // re-resolved from CAS, a 99A number becomes the legacy book, nothing means no prior (the guard
            // below decides whether the purpose can do without one).
            prior = await PriorAppraisalFields.ResolveAsync(
                mediator, request, prevAppraisalId, LegacyPriorBook.ForResubmit(command.Detail, request.ReappraisalBookNumber),
                cancellationToken);

        request.SetDetail(RequestDetail.Create(new RequestDetailData(
            command.Detail.HasAppraisalBook,
            LoanDetail.Create(new LoanDetailData(
                command.Detail.LoanDetail?.BankingSegment,
                command.Detail.LoanDetail?.LoanApplicationNumber,
                command.Detail.LoanDetail?.FacilityLimit,
                command.Detail.LoanDetail?.AdditionalFacilityLimit,
                command.Detail.LoanDetail?.PreviousFacilityLimit,
                command.Detail.LoanDetail?.TotalSellingPrice
            )),
            prevAppraisalId,
            Address.Create(new AddressData(
                command.Detail.Address?.HouseNumber,
                command.Detail.Address?.ProjectName,
                command.Detail.Address?.Moo,
                command.Detail.Address?.Soi,
                command.Detail.Address?.Road,
                command.Detail.Address?.SubDistrict,
                command.Detail.Address?.District,
                command.Detail.Address?.Province,
                command.Detail.Address?.Postcode
            )),
            Contact.Create(
                command.Detail.Contact?.ContactPersonName,
                command.Detail.Contact?.ContactPersonPhone,
                command.Detail.Contact?.DealerCode),
            Appointment.Create(
                command.Detail.Appointment?.AppointmentDateTime,
                command.Detail.Appointment?.AppointmentLocation),
            Fee.Create(
                command.Detail.Fee?.FeePaymentType,
                command.Detail.Fee?.FeeNotes,
                command.Detail.Fee?.AbsorbedAmount),
            locked ? request.Detail?.PrevAppraisalNumber : prior!.Number,
            locked ? request.Detail?.PrevAppraisalValue : prior!.Value,
            locked ? request.Detail?.PrevAppraisalDate : prior!.Date
        )));

        if (!locked)
            await PriorAppraisalSubmissionGuard.EnsureValidAsync(
                request.Purpose, request.Detail?.PrevAppraisalId,
                request.Detail?.PrevAppraisalNumber ?? command.Detail.PrevAppraisalNumber, mediator,
                cancellationToken, prior!.Reference, request.ReappraisalBookNumber);

        var customers = command.Customers
            .Select(c => RequestCustomer.Create(c.Name, c.ContactNumber))
            .ToList();
        request.SetCustomers(customers);

        var properties = command.Properties
            .Select(p => RequestProperty.Create(p.PropertyType, p.BuildingType, p.BuildingTypeOther, p.SellingPrice))
            .ToList();
        request.SetProperties(properties);

        return request;
    }
}
