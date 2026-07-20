using Appraisal.Domain.Appraisals;
using Dapper;
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

        var isValidType = await connection.ExecuteScalarAsync<bool>(
            """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM [parameter].[DocumentTypes]
                WHERE [Code] = @Code AND [Category] IN ('VAL_DOC', 'VAL_REPORT') AND [IsActive] = 1
            ) THEN 1 ELSE 0 END
            """,
            new { Code = typeCode });

        if (!isValidType)
            throw new BadRequestException($"'{typeCode}' is not a valid valuation document type.");

        var sortOrder = command.SortOrder;
        if (sortOrder is null)
        {
            var maxSortOrder = await connection.ExecuteScalarAsync<int?>(
                """
                SELECT MAX([SortOrder]) FROM [appraisal].[AppraisalDocuments]
                WHERE [AppraisalId] = @AppraisalId AND [DocumentTypeCode] = @Code
                """,
                new { command.AppraisalId, Code = typeCode });
            sortOrder = (maxSortOrder ?? -1) + 1;
        }

        var document = AppraisalDocument.Create(
            command.AppraisalId,
            typeCode,
            command.DocumentId,
            command.FileName,
            command.MimeType,
            command.FileSizeBytes,
            command.Notes,
            sortOrder.Value,
            command.UploadedByName);

        await repository.AddAsync(document, cancellationToken);

        outbox.Publish(
            new DocumentLinkedIntegrationEventV2(command.AppraisalId, command.DocumentId),
            correlationId: command.AppraisalId.ToString());

        return new AddAppraisalDocumentResult(document.Id);
    }
}
