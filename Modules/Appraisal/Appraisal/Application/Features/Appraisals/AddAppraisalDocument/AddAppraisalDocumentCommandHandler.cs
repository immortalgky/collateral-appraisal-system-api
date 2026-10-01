using Appraisal.Domain.Appraisals;
using Shared.CQRS;
using Shared.Data.Outbox;
using Shared.Exceptions;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Appraisals.AddAppraisalDocument;

public class AddAppraisalDocumentCommandHandler(
    IAppraisalDocumentRepository repository,
    ISqlConnectionFactory connectionFactory,
    IIntegrationEventOutbox outbox
) : ICommandHandler<AddAppraisalDocumentCommand, AddAppraisalDocumentResult>
{
    public async Task<AddAppraisalDocumentResult> Handle(
        AddAppraisalDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var typeCode = command.DocumentTypeCode.Trim().ToUpperInvariant();
        var connection = connectionFactory.GetOpenConnection();

        if (!await AppraisalDocumentQueries.IsValuationDocumentTypeAsync(connection, typeCode))
            throw new BadRequestException($"'{typeCode}' is not a valid valuation document type.");

        var sortOrder = command.SortOrder
                        ?? await AppraisalDocumentQueries.NextSortOrderAsync(connection, command.AppraisalId, typeCode);

        var document = AppraisalDocument.Create(
            command.AppraisalId,
            typeCode,
            command.DocumentId,
            command.FileName,
            command.MimeType,
            command.FileSizeBytes,
            command.Notes,
            sortOrder,
            command.UploadedByName);

        await repository.AddAsync(document, cancellationToken);

        outbox.Publish(
            new DocumentLinkedIntegrationEventV2(command.AppraisalId, command.DocumentId),
            correlationId: command.AppraisalId.ToString());

        return new AddAppraisalDocumentResult(document.Id);
    }
}
