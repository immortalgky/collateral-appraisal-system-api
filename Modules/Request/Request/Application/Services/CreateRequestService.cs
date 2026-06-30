using Appraisal.Contracts.Appraisals;
using Auth.Contracts.Users;

namespace Request.Application.Services;

public class CreateRequestService(
    IDateTimeProvider dateTimeProvider,
    IRequestRepository requestRepository,
    IRequestTitleRepository requestTitleRepository,
    IRequestCommentRepository requestCommentRepository,
    IRequestUnitOfWork unitOfWork,
    ISender mediator,
    IUserLookupService userLookupService
) : ICreateRequestService
{
    public async Task<(Request.Domain.Requests.Request, List<RequestTitle>)> CreateRequestAsync(CreateRequestData data,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.Now;

        var request = await CreateRequestAsync(data, now, cancellationToken);
        var titles = await CreateTitlesAsync(data, request.Id, cancellationToken);
        await CreateCommentsAsync(data, request.Id, now, cancellationToken);

        return (request, titles);
    }

    public async Task<(Request.Domain.Requests.Request, List<RequestTitle>)> CreateAndSubmitRequestAsync(
        CreateRequestData data,
        DateTime submittedAt,
        string? externalCaseKey,
        CancellationToken cancellationToken)
    {
        var (request, titles) = await CreateRequestAsync(data, cancellationToken);

        if (!string.IsNullOrWhiteSpace(externalCaseKey) && !string.IsNullOrWhiteSpace(data.Channel))
            request.SetExternalReference(externalCaseKey, data.Channel);

        request.Validate();
        foreach (var title in titles) title.Validate();

        // Appeal/Progressive require a Completed prior appraisal — reject before submitting.
        await PriorAppraisalSubmissionGuard.EnsureValidAsync(
            request.Purpose, request.Detail?.PrevAppraisalId, mediator, cancellationToken);

        // Persist before Submit so RequestSubmittedEventHandler's DB query for titles +
        // documents returns the committed state. Runs inside the caller's transaction;
        // a throw in Submit() still rolls back via TransactionalBehavior.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Sole caller is the Integration (API) module — mark the entry source so the workflow
        // skips the appraisal-initiation-check task that the UI path requires.
        request.Submit(submittedAt, entrySource: "API");

        return (request, titles);
    }

    private async Task<Domain.Requests.Request> CreateRequestAsync(
        CreateRequestData command,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // UI path: resolve employee code → identity (name). Org detail (email, cost center, etc.)
        // is NOT snapshotted — it is resolved on read from the stored employee code via auth.
        // Integration / reappraisal path: requestor is pre-resolved; use directly.
        UserInfo requestorUserInfo;

        if (!string.IsNullOrWhiteSpace(command.RequestorEmployeeId))
        {
            var info = await userLookupService.GetRequestorAsync(command.RequestorEmployeeId, cancellationToken);
            if (info is null)
                throw new NotFoundException("Requestor", command.RequestorEmployeeId);

            requestorUserInfo = new UserInfo(info.EmployeeId, info.Name);
        }
        else
        {
            requestorUserInfo = new UserInfo(command.Requestor!.UserId, command.Requestor.Username);
        }

        var request = Domain.Requests.Request.Create(new RequestData(
            command.Purpose,
            command.Channel,
            requestorUserInfo,
            new UserInfo(command.Creator.UserId, command.Creator.Username),
            now,
            command.Priority,
            command.IsPma
        ));

        if (command.Detail is not null)
        {
            AppraisalReferenceResult? appraisalRef = null;
            if (command.Detail.PrevAppraisalId.HasValue)
                appraisalRef = await mediator.Send(
                    new GetAppraisalReferenceQuery(command.Detail.PrevAppraisalId.Value), cancellationToken);

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
                command.Detail.PrevAppraisalId,
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
                appraisalRef?.AppraisalNumber,
                appraisalRef?.AppraisalValue,
                appraisalRef?.AppointmentDate
            )));
        }

        if (command.Customers is { Count: > 0 })
        {
            var customers = command.Customers
                .Select(c => RequestCustomer.Create(c.Name, c.ContactNumber))
                .ToList();

            request.SetCustomers(customers);
        }

        if (command.Properties is { Count: > 0 })
        {
            var properties = command.Properties
                .Select(p => RequestProperty.Create(p.PropertyType, p.BuildingType, p.SellingPrice))
                .ToList();

            request.SetProperties(properties);
        }

        if (command.Documents is { Count: > 0 })
            foreach (var doc in command.Documents)
                request.AddDocument(new RequestDocumentData(
                    doc.DocumentId,
                    doc.DocumentType,
                    doc.FileName,
                    doc.Prefix,
                    doc.Set,
                    doc.Notes,
                    doc.FilePath,
                    doc.Source,
                    doc.IsRequired,
                    doc.UploadedBy,
                    doc.UploadedByName,
                    doc.UploadedAt
                ));

        await requestRepository.AddAsync(request, cancellationToken);

        return request;
    }

    private async Task<List<RequestTitle>> CreateTitlesAsync(
        CreateRequestData command,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        if (command.Titles is not { Count: > 0 })
            return [];

        var titles = new List<RequestTitle>(command.Titles.Count);

        foreach (var titleDto in command.Titles)
        {
            var title = TitleFactory.Create(titleDto.CollateralType,
                titleDto.ToRequestTitleData() with { RequestId = requestId });

            foreach (var doc in titleDto.Documents)
                title.AddDocument(new TitleDocumentData
                {
                    DocumentId = doc.DocumentId,
                    DocumentType = doc.DocumentType,
                    FileName = doc.FileName,
                    Prefix = doc.Prefix,
                    Set = doc.Set,
                    Notes = doc.Notes,
                    FilePath = doc.FilePath,
                    UploadedBy = doc.UploadedBy,
                    UploadedByName = doc.UploadedByName,
                    UploadedAt = doc.UploadedAt
                });

            titles.Add(title);
        }

        await requestTitleRepository.AddRangeAsync(titles, cancellationToken);

        return titles;
    }

    private async Task CreateCommentsAsync(
        CreateRequestData command,
        Guid requestId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (command.Comments is not { Count: > 0 })
            return;

        var comments = new List<RequestComment>(command.Comments.Count);

        foreach (var c in command.Comments)
        {
            var comment = RequestComment.Create(new RequestCommentData(
                requestId,
                c.Comment,
                c.CommentedBy,
                c.CommentedByName,
                now
            ));

            comments.Add(comment);
        }

        await requestCommentRepository.AddRangeAsync(comments, cancellationToken);
    }
}