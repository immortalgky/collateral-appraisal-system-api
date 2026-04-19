using Shared.Identity;

namespace Request.Application.Features.Requests.AttachRequestDocument;

internal class AttachRequestDocumentCommandHandler(
    IRequestRepository requestRepository,
    IDateTimeProvider dateTimeProvider,
    ICurrentUserService currentUser
) : ICommandHandler<AttachRequestDocumentCommand, AttachRequestDocumentResult>
{
    public async Task<AttachRequestDocumentResult> Handle(
        AttachRequestDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdWithDocumentsAsync(command.RequestId, cancellationToken);
        if (request is null) throw new RequestNotFoundException(command.RequestId);

        var documentData = new RequestDocumentData(
            command.DocumentId,
            command.DocumentType,
            command.FileName,
            null,
            1,
            null,
            null,
            command.Source ?? "REQUEST",
            false,
            currentUser.Username,
            currentUser.Username,
            dateTimeProvider.Now
        );

        request.AddDocument(documentData);

        return new AttachRequestDocumentResult(true);
    }
}