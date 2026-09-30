using System.Text.Json;
using Appraisal.Application.Features.Appraisals.AddAppraisalDocument;
using Appraisal.Infrastructure;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.Features.Appraisals.CorrectAppraisalDocuments;

/// <summary>
/// Attaches, deletes or replaces valuation documents on a Completed appraisal and records one audit row.
///
/// Authorization is the endpoint's "appraisal.data-correction" policy. This handler repeats what
/// AddAppraisalDocument / RemoveAppraisalDocument do rather than sending those commands: both are
/// transactional, and a nested transactional command would find the outer transaction already open and
/// skip its own commit handling.
/// </summary>
public class CorrectAppraisalDocumentsCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalDocumentRepository documentRepository,
    AppraisalDbContext dbContext,
    ISqlConnectionFactory connectionFactory,
    IIntegrationEventOutbox outbox,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<CorrectAppraisalDocumentsCommand, CorrectAppraisalDocumentsResult>
{
    public async Task<CorrectAppraisalDocumentsResult> Handle(
        CorrectAppraisalDocumentsCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // Same rule as CorrectPropertyData: Completed only, so this cannot be used to edit in-flight work.
        if (appraisal.Status != AppraisalStatus.Completed)
        {
            throw new ConflictException(
                $"Appraisal is {appraisal.Status.Code}. Data correction applies to Completed " +
                "appraisals only.",
                "APPRAISAL_NOT_COMPLETED");
        }

        var by = currentUser.UserCode ?? currentUser.Username ?? "unknown";
        var changes = new Dictionary<string, object?>();

        AppraisalDocument? removed = null;
        if (command.RemoveId is { } removeId)
        {
            // Load by BOTH id AND appraisalId, like RemoveAppraisalDocument, so a document of another
            // appraisal cannot be deleted through this route.
            removed = await documentRepository.GetByIdAndAppraisalIdAsync(
                          removeId, command.AppraisalId, cancellationToken)
                      ?? throw new NotFoundException(nameof(AppraisalDocument), removeId);

            await documentRepository.DeleteAsync(removed, cancellationToken);
            outbox.Publish(new DocumentUnlinkedIntegrationEvent(command.AppraisalId, removed.DocumentId));
            changes[removed.DocumentTypeCode] = new { from = (string?)removed.FileName, to = (string?)null };
        }

        Guid? addedId = null;
        if (command.Add is { } add)
        {
            // Unlink + link of the same file is a no-op that would still write a "replaced" history row.
            if (removed?.DocumentId == add.DocumentId)
                throw new BadRequestException("The replacement is the same file as the one being removed.");

            var typeCode = add.DocumentTypeCode.Trim().ToUpperInvariant();
            var connection = connectionFactory.GetOpenConnection();

            if (!await AppraisalDocumentQueries.IsValuationDocumentTypeAsync(connection, typeCode))
                throw new BadRequestException($"'{typeCode}' is not a valid valuation document type.");

            // The audit row is the point of this endpoint, so name/mime/size come from the stored upload,
            // not from the request — otherwise the history could name a file the list never shows.
            var file = await AppraisalDocumentQueries.GetUploadedFileAsync(connection, add.DocumentId)
                       ?? throw new NotFoundException("Document", add.DocumentId);

            // Uploads also accept Office files; the valuation checklist takes images and PDFs only.
            if (!file.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(file.MimeType, "application/pdf", StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException($"'{file.MimeType}' is not a supported valuation document type.");

            // A same-type replace takes the removed file's slot. The delete is not flushed yet, so MAX()
            // would still count it and push the replacement to the end of the list.
            var sameTypeReplace = removed is not null && removed.DocumentTypeCode == typeCode;
            var sortOrder = sameTypeReplace
                ? removed!.SortOrder
                : await AppraisalDocumentQueries.NextSortOrderAsync(connection, command.AppraisalId, typeCode);

            var document = AppraisalDocument.Create(
                command.AppraisalId,
                typeCode,
                add.DocumentId,
                file.FileName,
                file.MimeType,
                file.FileSizeBytes,
                // The slot's notes go with it on a same-type replace; notes cannot be edited on a closed appraisal.
                sameTypeReplace ? removed!.Notes : null,
                sortOrder,
                by);

            await documentRepository.AddAsync(document, cancellationToken);
            outbox.Publish(
                new DocumentLinkedIntegrationEventV2(command.AppraisalId, add.DocumentId),
                correlationId: command.AppraisalId.ToString());

            // Replacing within one type is a single from->to entry; across two types it is two keys.
            changes[typeCode] = new
            {
                from = sameTypeReplace ? removed!.FileName : null,
                to = (string?)file.FileName,
            };
            addedId = document.Id;
        }

        dbContext.AppraisalPropertyCorrectionLogs.Add(AppraisalPropertyCorrectionLog.ForDocuments(
            command.AppraisalId,
            JsonSerializer.Serialize(changes),
            command.Reason.Trim(),
            by,
            dateTimeProvider.ApplicationNow));

        // Nothing is saved here: TransactionalBehavior flushes the document change and the audit row
        // together, so the two cannot diverge.
        return new CorrectAppraisalDocumentsResult(addedId);
    }
}
