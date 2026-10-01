namespace Request.Application.Features.Requests.SubmitRequest;

internal class SubmitRequestCommandHandler(
    IRequestRepository requestRepository,
    IRequestTitleRepository requestTitleRepository,
    IRequestDocumentValidator validator,
    IDateTimeProvider dateTimeProvider,
    ISender mediator
) : ICommandHandler<SubmitRequestCommand, SubmitRequestResult>
{
    public async Task<SubmitRequestResult> Handle(SubmitRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdWithDocumentsAsync(command.Id, cancellationToken);
        if (request is null) throw new RequestNotFoundException(command.Id);

        // Appeal/Progressive require a Completed prior appraisal — reject before submitting.
        await PriorAppraisalSubmissionGuard.EnsureValidAsync(
            request.Purpose, request.Detail?.PrevAppraisalId, mediator, cancellationToken);

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

        // Manual submission from the frontend UI requires the appraisal-initiation-check task — except
        // an AS400 periodical reappraisal: Initiate creates it and staff only review it before sending,
        // so it goes straight to assignment exactly as when the consumer submitted it itself. Keyed on
        // GroupTag, which only the reappraisal consumer sets — never on Channel, which any UI request
        // can pick ("SIBS" is a selectable channel) and would skip the check.
        var entrySource = request.GroupTag is not null ? "SIBS" : "UI";
        request.Submit(dateTimeProvider.Now, entrySource: entrySource);

        return new SubmitRequestResult(true);
    }
}
