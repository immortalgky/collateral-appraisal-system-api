namespace Request.Application.Features.Requests.SubmitRequest;

internal class SubmitRequestCommandHandler(
    IRequestRepository requestRepository,
    IRequestTitleRepository requestTitleRepository,
    IRequestDocumentValidator validator,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<SubmitRequestCommand, SubmitRequestResult>
{
    public async Task<SubmitRequestResult> Handle(SubmitRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdWithDocumentsAsync(command.Id, cancellationToken);
        if (request is null) throw new RequestNotFoundException(command.Id);

        var titles = (await requestTitleRepository
            .GetByRequestIdWithDocumentsAsync(request.Id, cancellationToken)).ToList();

        var input = new DocumentValidationInput(
            request.Purpose,
            request.Documents
                .Where(d => d.DocumentId.HasValue)
                .Select(d => d.DocumentType)
                .ToList(),
            titles.Select(t => new TitleDocumentInput(
                t.CollateralType,
                t.Documents
                    .Where(d => d.DocumentId.HasValue && !string.IsNullOrWhiteSpace(d.DocumentType))
                    .Select(d => d.DocumentType!)
                    .ToList()
            )).ToList());

        await validator.ValidateAsync(input, cancellationToken);

        request.Submit(dateTimeProvider.Now);

        return new SubmitRequestResult(true);
    }
}
