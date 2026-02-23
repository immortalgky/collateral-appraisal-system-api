namespace Integration.Application.Features.UploadSessions.CreateUploadSession;

public record CreateUploadSessionRequest(
    string ClientReference,
    string ExternalCaseKey
);

public record CreateUploadSessionResponse(
    Guid UploadSessionId,
    string Status,
    string ExpiresAt,
    UploadLimitation Limits
);