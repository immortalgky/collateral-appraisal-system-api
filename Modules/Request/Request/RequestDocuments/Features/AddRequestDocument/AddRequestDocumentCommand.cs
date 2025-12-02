namespace Request.RequestDocuments.Features.AddRequestDocument;

using Request.Contracts.RequestDocuments.Dto;

public record AddRequestDocumentCommand(
    Guid RequestId,
    List<RequestDocumentDto> Documents
) : ICommand<AddRequestDocumentResult>;
