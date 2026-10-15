using Document.Domain.UploadSessions.Model;
using MediatR;
using Shared.CQRS;
using Shared.Data;
using Shared.Exceptions;
using Shared.Time;

namespace Integration.Application.Features.UploadSessions.CancelUploadSession;

// Cancel = expire now. Repeating it is a no-op. A session turns Completed asynchronously (when
// SessionCompletedIntegrationEvent is consumed after POST /api/v1/requests), so the 409 covers "already marked
// Completed", and a cancel sent right after a create-request can still get 204.
public class CancelUploadSessionCommandHandler(
    IRepository<UploadSession, Guid> uploadSessionRepository,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<CancelUploadSessionCommand>
{
    public async Task<Unit> Handle(CancelUploadSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await uploadSessionRepository.GetByIdAsync(command.UploadSessionId, cancellationToken)
                      ?? throw new NotFoundException($"Upload session {command.UploadSessionId} not found");

        if (session.Status == "Completed")
            throw new ConflictException("Upload session has already been completed and cannot be cancelled");

        session.Expire(dateTimeProvider.ApplicationNow);

        return Unit.Value;
    }
}
