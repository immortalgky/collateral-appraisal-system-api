using Request.Extensions;

namespace Request.RequestDocuments.Features.UpdateRequestDocument;

public class UpdateRequestDocumentCommandHandler(IRequestDocumentRepository requestDocumentRepository)
    : ICommandHandler<UpdateRequestDocumentCommand, UpdateRequestDocumentResult>
{
    public async Task<UpdateRequestDocumentResult> Handle(UpdateRequestDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var existingDocuments = await requestDocumentRepository
            .GetByRequestIdAsync(command.RequestId, cancellationToken);

        if (existingDocuments == null || !existingDocuments.Any())
            throw new NotFoundException($"No documents found for RequestId {command.RequestId}");

        foreach (var dto in command.Documents)
        {
            var existingDoc = existingDocuments.FirstOrDefault(d => d.Id == dto.Id.Value);
            if (existingDoc == null)
                continue;

            existingDoc.UpdateRequestDocument(
                dto.DocumentId,
                dto.FileName,
                dto.Prefix,
                dto.Set,
                dto.FilePath,
                dto.DocumentFollowUp,
                dto.DocumentClassification.ToDomain(),
                dto.DocumentDescription,
                dto.UploadInfo.ToDomain()
            );
        }

        await requestDocumentRepository.SaveChangesAsync(cancellationToken);

        return new UpdateRequestDocumentResult(command.Documents);
    }
}
