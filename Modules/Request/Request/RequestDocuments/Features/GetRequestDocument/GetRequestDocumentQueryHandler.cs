using System;

namespace Request.RequestDocuments.Features.GetRequestDocument;

public class GetRequestDocumentQueryHandler(IRequestDocumentRepository requestDocumentRepository)
    : IQueryHandler<GetRequestDocumentQuery, GetRequestDocumentResult>
{
    public async Task<GetRequestDocumentResult> Handle(GetRequestDocumentQuery command,
        CancellationToken cancellationToken)
    {
        var documents = await requestDocumentRepository.GetByRequestIdAsync(command.requestId);
        return new GetRequestDocumentResult(documents);
    }
}
