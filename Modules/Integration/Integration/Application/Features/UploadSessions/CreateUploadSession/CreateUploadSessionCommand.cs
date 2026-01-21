using Shared.CQRS;

namespace Integration.Application.Features.UploadSessions.CreateUploadSession;

public record CreateUploadSessionCommand(
    string? ExternalReference,
    int ExpectedDocumentCount
) : ICommand<CreateUploadSessionResult>;

public record CreateUploadSessionResult(
    Guid SessionId,
    DateTime ExpiresAt
);
