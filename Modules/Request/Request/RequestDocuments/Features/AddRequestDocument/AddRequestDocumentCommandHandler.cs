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

        var requestId = command.Documents.First().RequestId;
        await requestDocumentRepository.ClearAsync(requestId, cancellationToken);

        command.Documents.ForEach(async dto =>
        {
            var requestDocument = RequestDocument.Create(
                dto.RequestId,
                dto.DocumentId,
                dto.DocumentClassification.ToDomain(),
                dto.DocumentDescription,
                dto.UploadInfo.ToDomain());
            await requestDocumentRepository.AddAsync(requestDocument, cancellationToken);
            listDocument.Add(requestDocument.DocumentId);
        });

        await requestDocumentRepository.SaveChangesAsync(cancellationToken);

        return new AddRequestDocumentResult(listDocument);
    }
}
