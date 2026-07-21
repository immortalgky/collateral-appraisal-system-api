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

            if (file.FileSizeBytes > fileStorageOptions.Value.MaxFileSizeBytes)
                throw new BadRequestException("The document exceeds the maximum allowed file size.");

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
