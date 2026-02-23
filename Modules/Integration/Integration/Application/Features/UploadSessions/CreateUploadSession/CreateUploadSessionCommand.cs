using Document;
using Shared.CQRS;

namespace Integration.Application.Features.UploadSessions.CreateUploadSession;

public record CreateUploadSessionCommand(
    string ClientReference,
    string ExternalCaseKey,
    string? UserAgent,
    string? IpAddress
) : ICommand<CreateUploadSessionResult>, ITransactionalCommand<IDocumentUnitOfWork>;