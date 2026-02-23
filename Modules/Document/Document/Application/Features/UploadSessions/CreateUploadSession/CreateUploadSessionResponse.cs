namespace Document.Domain.UploadSessions.Features.CreateUploadSession;

public record CreateUploadSessionResponse(Guid SessionId, DateTime ExpiresAt);
