using Integration.Application.Services;
using Mapster;
using Request.Application.Services;
using Request.Contracts.RequestDocuments.Dto;
using Request.Contracts.Requests.Dtos;
using Shared.CQRS;
using Shared.Time;

namespace Integration.Application.Features.AppraisalRequests.CreateRequest;

public class CreateRequestCommandHandler(
    ICreateRequestService createRequestService,
    IRequestDocumentValidator validator,
    IAppraisalLookupService appraisalLookup,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<CreateRequestCommand, Guid>
{
    public async Task<Guid> Handle(
        CreateRequestCommand command,
        CancellationToken cancellationToken)
    {
        // PrevAppraisalNumber -> PrevAppraisalId for every purpose (400 naming the number when missing or
        // not Completed); a 99A legacy number stays a number and is recorded as a legacy prior book.
        // Whether the purpose needs / forbids / allows a prior appraisal is PriorAppraisalSubmissionGuard's call.
        command = command with
        {
            Detail = await PriorAppraisalNumberResolver.ResolveAsync(appraisalLookup, command.Purpose, command.Detail, cancellationToken)
        };

        var input = new DocumentValidationInput(
            command.Purpose,
            (command.Documents ?? new List<RequestDocumentDto>())
            .Where(d => d.DocumentId.HasValue)
            .Select(d => d.DocumentType)
            .ToList(),
            (command.Titles ?? new List<RequestTitleDto>())
            .Select(t => new TitleDocumentInput(
                t.CollateralType,
                (t.Documents ?? new List<RequestTitleDocumentDto>())
                .Where(d => d.DocumentId.HasValue && !string.IsNullOrWhiteSpace(d.DocumentType))
                .Select(d => d.DocumentType!)
                .ToList()))
            .ToList());

        await validator.ValidateAsync(input, cancellationToken);

        var createRequestData = command.Adapt<CreateRequestData>();
        var (request, _) = await createRequestService.CreateAndSubmitRequestAsync(
            createRequestData,
            dateTimeProvider.Now,
            command.ExternalCaseKey,
            PriorAppraisalNumberResolver.LegacyNumber(command.Detail),
            cancellationToken);

        return request.Id;
    }
}