using System;

namespace Request.RequestDocuments.Features.RemoveRequestDocument;

public class RemoveRequestDocumentCommandHandler(IRequestDocumentRepository requestDocumentRepository)
    : ICommandHandler<RemoveRequestDocumentCommand, RemoveRequestDocumentResult>
{
    public async Task<RemoveRequestDocumentResult> Handle(RemoveRequestDocumentCommand command,
        CancellationToken cancellationToken)
    {
        await requestDocumentRepository.RemoveAsync(command.Id, cancellationToken);
        return new RemoveRequestDocumentResult(true);
    }
}
