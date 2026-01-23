namespace Request.Application.Features.Requests.CreateDraftRequest;

internal class CreateDraftRequestCommandHandler(
    IDateTimeProvider dateTimeProvider,
    IRequestRepository requestRepository,
    IRequestTitleRepository requestTitleRepository,
    IRequestCommentRepository requestCommentRepository
) : ICommandHandler<CreateDraftRequestCommand, CreateDraftRequestResult>
{
    public async Task<CreateDraftRequestResult> Handle(
        CreateDraftRequestCommand command,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.Now;

        var request = await CreateRequestAsync(command, now, cancellationToken);
        await CreateTitlesAsync(command, request.Id, cancellationToken);
        await CreateCommentsAsync(command, request.Id, now, cancellationToken);

        return new CreateDraftRequestResult(request.Id);
    }

    private async Task<Domain.Requests.Request> CreateRequestAsync(
        CreateDraftRequestCommand command,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var request = Domain.Requests.Request.Create(new RequestData(
            command.Purpose,
            command.Channel,
            new UserInfo(command.Requestor.UserId, command.Requestor.Username),
            new UserInfo(command.Creator.UserId, command.Creator.Username),
            now,
            command.Priority,
            command.IsPma
        ));

        if (command.Detail is not null)
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
                    command.Detail.Appointment?.AppointmentDate,
                    command.Detail.Appointment?.AppointmentLocation),
                Fee.Create(
                    command.Detail.Fee?.FeePaymentType,
                    command.Detail.Fee?.FeeNotes,
                    command.Detail.Fee?.AbsorbedAmount)
            )));

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

        request.UpdateStatus(RequestStatus.New);

        await requestRepository.AddAsync(request, cancellationToken);

        return request;
    }

    private async Task CreateTitlesAsync(
        CreateDraftRequestCommand command,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        if (command.Titles is not { Count: > 0 })
            return;

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
                    Filename = doc.Filename,
                    Prefix = doc.Prefix,
                    Set = doc.Set,
                    Notes = doc.DocumentDescription,
                    FilePath = doc.FilePath,
                    UploadedBy = doc.UploadedBy,
                    UploadedByName = doc.UploadedByName,
                    UploadedAt = doc.UploadedAt
                });

            titles.Add(title);
        }

        await requestTitleRepository.AddRangeAsync(titles, cancellationToken);
    }

    private async Task CreateCommentsAsync(
        CreateDraftRequestCommand command,
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