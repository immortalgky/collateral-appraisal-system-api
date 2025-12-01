using Request.Extensions;
using Request.RequestDocuments.ValueObjects;
using Request.Contracts.RequestDocuments.Dto;


namespace Request.RequestDocuments.Features.AddRequestDocument;

public class AddRequestDocumentCommandHandler(IRequestDocumentRepository requestDocumentRepository)
    : ICommandHandler<AddRequestDocumentCommand, AddRequestDocumentResult>
{
    public async Task<AddRequestDocumentResult> Handle(AddRequestDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var listDocument = new List<Guid>();
        if (command.Documents.Count == 0)
        {
            return new AddRequestDocumentResult(listDocument);
        }

        foreach (var dto in command.Documents)
        {
            var requestDocument = RequestDocument.Create(
                command.RequestId,
                dto.DocumentId,
                dto.FileName,
                dto.Prefix,
                dto.Set,
                dto.FilePath,
                dto.DocumentFollowUp,
                dto.DocumentClassification.ToDomain(),
                dto.DocumentDescription,
                dto.UploadInfo.ToDomain());

            await requestDocumentRepository.AddAsync(requestDocument, cancellationToken);
            if (requestDocument.DocumentId.HasValue)
            {
                listDocument.Add(requestDocument.DocumentId.Value);
            }
        }

        await requestDocumentRepository.SaveChangesAsync(cancellationToken);

        return new AddRequestDocumentResult(listDocument);
    }
}
