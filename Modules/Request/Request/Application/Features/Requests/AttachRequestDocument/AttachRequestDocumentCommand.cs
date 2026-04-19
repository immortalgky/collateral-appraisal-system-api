namespace Request.Application.Features.Requests.AttachRequestDocument;

public record AttachRequestDocumentCommand(
    Guid RequestId,
    Guid DocumentId,
    string DocumentType,
    string? FileName,
    string? Source) : ICommand<AttachRequestDocumentResult>, ITransactionalCommand<IRequestUnitOfWork>;