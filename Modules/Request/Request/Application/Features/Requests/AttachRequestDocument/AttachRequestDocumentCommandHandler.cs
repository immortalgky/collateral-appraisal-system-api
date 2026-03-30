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
            DocumentId: command.DocumentId,
            DocumentType: command.DocumentType,
            FileName: command.FileName,
            Prefix: null,
            Set: 1,
            Notes: null,
            FilePath: null,
            Source: command.Source ?? "REQUEST",
            IsRequired: false,
            UploadedBy: currentUser.UserId?.ToString(),
            UploadedByName: currentUser.Username,
            UploadedAt: dateTimeProvider.Now
        );

        request.AddDocument(documentData);

        return new AttachRequestDocumentResult(true);
    }
}
