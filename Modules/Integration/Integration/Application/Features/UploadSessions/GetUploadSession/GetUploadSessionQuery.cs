using Shared.CQRS;

namespace Integration.Application.Features.UploadSessions.GetUploadSession;

public record GetUploadSessionQuery(Guid SessionId) : IQuery<GetUploadSessionResult>;

public record GetUploadSessionResult(
    Guid SessionId,
    string Status,
    int TotalDocuments,
    long TotalSizeBytes,
    DateTime? CompletedAt,
    DateTime ExpiresAt,
    string? ExternalReference
);
