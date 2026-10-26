using Document;
using Shared.CQRS;

namespace Integration.Application.Features.UploadSessions.CancelUploadSession;

public record CancelUploadSessionCommand(Guid UploadSessionId)
    : ICommand, ITransactionalCommand<IDocumentUnitOfWork>;
