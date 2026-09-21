using Dapper;
using Microsoft.Extensions.Options;
using Shared.Configurations;

namespace Appraisal.Application.Features.Appraisals.AddAppendixDocument;

public class AddAppendixDocumentCommandHandler(
    IAppraisalAppendixRepository repository,
    IAppraisalGalleryRepository galleryRepository,
    ISqlConnectionFactory connectionFactory,
    IOptions<FileStorageConfiguration> fileStorageOptions
) : ICommandHandler<AddAppendixDocumentCommand, AddAppendixDocumentResult>
{
    public async Task<AddAppendixDocumentResult> Handle(
        AddAppendixDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var appendix = await repository.GetByIdWithDocumentsAsync(command.AppendixId, cancellationToken)
                       ?? throw new NotFoundException(nameof(AppraisalAppendix), command.AppendixId);

        AppendixDocument document;

        if (command.DocumentId is { } documentId)
        {
            // PDF path: bypass the gallery entirely. Validate against the authoritative
            // document.Documents row — never trust a client-supplied mime/size.
            var connection = connectionFactory.GetOpenConnection();
            var file = await connection.QuerySingleOrDefaultAsync<DocumentFileInfo>(
                """
                SELECT [MimeType], [FileSizeBytes]
                FROM [document].[Documents]
                WHERE [Id] = @DocumentId AND [IsActive] = 1 AND [IsDeleted] = 0
                """,
                new { DocumentId = documentId });

            if (file is null)
                throw new NotFoundException("Document", documentId);

            var isPdf = string.Equals(file.MimeType, "application/pdf", StringComparison.OrdinalIgnoreCase);
            var isImage = file.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            if (!isPdf && !isImage)
                throw new BadRequestException($"'{file.MimeType}' is not a supported appendix document type.");

            // The appendix limit, not the upload limit. The report book holds every page it
            // carries in memory at once, so what may be stored and what may be bound into a book
            // are different sizes — and a file accepted here that the assembler later refuses
            // would drop out of the finished book with nothing said to anyone, because the report
            // job can only report a failure, not a warning on a job that succeeded.
            var maxAppendixBytes = fileStorageOptions.Value.MaxAppendixFileSizeBytes;
            if (file.FileSizeBytes > maxAppendixBytes)
                // Rounded up, not truncated: a 100.4 MB file over a 100 MB cap has to read as
                // "101 MB", or the message refuses a file for being exactly the allowed size.
                throw new BadRequestException(
                    $"An appendix file must not exceed {maxAppendixBytes / (1024 * 1024)} MB " +
                    $"(this file is {Math.Ceiling(file.FileSizeBytes / 1024d / 1024d)} MB).");

            document = appendix.AddPdfDocument(documentId, command.DisplaySequence);
        }
        else
        {
            var galleryPhotoId = command.GalleryPhotoId!.Value;
            document = appendix.AddDocument(galleryPhotoId, command.DisplaySequence);

            // Mark gallery photo as in use
            var photo = await galleryRepository.GetByIdAsync(galleryPhotoId, cancellationToken);
            photo?.MarkAsInUse();
        }

        await repository.UpdateAsync(appendix, cancellationToken);

        return new AddAppendixDocumentResult(document.Id, appendix.Id);
    }

    private sealed class DocumentFileInfo
    {
        public string MimeType { get; init; } = string.Empty;
        public long FileSizeBytes { get; init; }
    }
}
