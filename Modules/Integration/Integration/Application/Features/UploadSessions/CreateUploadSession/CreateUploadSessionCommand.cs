using Document;
using Shared.CQRS;

namespace Integration.Application.Features.UploadSessions.CreateUploadSession;

public record CreateUploadSessionCommand(
    string? ClientId,
    string? UserAgent,
    string? IpAddress
) : ICommand<CreateUploadSessionResult>, ITransactionalCommand<IDocumentUnitOfWork>;