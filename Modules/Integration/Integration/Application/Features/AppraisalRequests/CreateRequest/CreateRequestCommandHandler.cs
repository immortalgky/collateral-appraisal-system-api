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
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<CreateRequestCommand, Guid>
{
    public async Task<Guid> Handle(
        CreateRequestCommand command,
        CancellationToken cancellationToken)
    {
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
        var request = await createRequestService.CreateRequestAsync(createRequestData, cancellationToken);

        request.Submit(dateTimeProvider.Now);

        return request.Id;
    }
}
