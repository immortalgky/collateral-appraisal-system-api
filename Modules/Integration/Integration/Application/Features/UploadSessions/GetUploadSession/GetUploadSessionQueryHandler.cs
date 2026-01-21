using Document.Domain.UploadSessions.Model;
using Shared.CQRS;
using Shared.Data;

namespace Integration.Application.Features.UploadSessions.GetUploadSession;

public class GetUploadSessionQueryHandler(
    IRepository<UploadSession, Guid> uploadSessionRepository
) : IQueryHandler<GetUploadSessionQuery, GetUploadSessionResult>
{
    public async Task<GetUploadSessionResult> Handle(
        GetUploadSessionQuery query,
        CancellationToken cancellationToken)
    {
        var session = await uploadSessionRepository.GetByIdAsync(query.SessionId, cancellationToken);

        if (session is null)
        {
            throw new KeyNotFoundException($"Upload session {query.SessionId} not found");
        }

        return new GetUploadSessionResult(
            session.Id,
            session.Status,
            session.TotalDocuments,
            session.TotalSizeBytes,
            session.CompletedAt,
            session.ExpiresAt,
            session.ExternalReference
        );
    }
}
