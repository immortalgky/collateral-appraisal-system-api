namespace Integration.Application.Features.UploadSessions.CreateUploadSession;

public sealed record CreateUploadSessionResult(
    Guid UploadSessionId,
    string Status,
    string ExpiresAt,
    UploadLimitation Limits
);

public sealed record UploadLimitation(
    int MaxFiles,
    int MaxFileBytes,
    int MaxTotalBytes
);