using Document.Domain.UploadSessions.Model;
using MediatR;
using Shared.CQRS;
using Shared.Data;

namespace Integration.Application.Features.UploadSessions.CreateUploadSession;

public class CreateUploadSessionCommandHandler(
    IRepository<UploadSession, Guid> uploadSessionRepository
) : ICommandHandler<CreateUploadSessionCommand, CreateUploadSessionResult>
{
    public async Task<CreateUploadSessionResult> Handle(
        CreateUploadSessionCommand command,
        CancellationToken cancellationToken)
    {
        var expiresAt = DateTime.UtcNow.AddHours(24);

        var session = UploadSession.Create(
            expiresAt: expiresAt,
            userAgent: null,
            ipAddress: null
        );

        if (!string.IsNullOrWhiteSpace(command.ExternalReference))
        {
            session.SetExternalReference(command.ExternalReference);
        }

        await uploadSessionRepository.AddAsync(session, cancellationToken);
        await uploadSessionRepository.SaveChangesAsync(cancellationToken);

        return new CreateUploadSessionResult(session.Id, expiresAt);
    }
}
