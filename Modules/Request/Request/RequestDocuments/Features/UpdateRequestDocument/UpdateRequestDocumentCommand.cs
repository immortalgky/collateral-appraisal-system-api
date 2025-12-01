using Request.Contracts.RequestDocuments.Dto;

namespace Request.RequestDocuments.Features.UpdateRequestDocument;

public record UpdateRequestDocumentCommand(
    Guid RequestId,
    List<RequestDocumentDto> Documents
) : ICommand<UpdateRequestDocumentResult>;