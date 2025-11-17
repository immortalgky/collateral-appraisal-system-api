namespace Request.RequestDocuments.Features.AddRequestDocument;

using Request.Contracts.RequestDocuments.Dto;

public record AddRequestDocumentCommand(
    List<RequestDocumentDto> Documents
) : ICommand<AddRequestDocumentResult>;
