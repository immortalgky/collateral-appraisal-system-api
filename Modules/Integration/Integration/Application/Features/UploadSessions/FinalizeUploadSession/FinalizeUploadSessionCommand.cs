using Shared.CQRS;

namespace Integration.Application.Features.UploadSessions.FinalizeUploadSession;

public record FinalizeUploadSessionCommand(
    Guid SessionId,
    string? IdempotencyKey
) : ICommand<FinalizeUploadSessionResult>;

public record FinalizeUploadSessionResult(
    Guid SessionId,
    string Status,
    List<Guid> DocumentIds
);
