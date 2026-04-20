using Request.Contracts.RequestDocuments;
using Request.Domain.RequestTitles;
using Shared.Identity;

namespace Request.Application.Services;

internal class RequestDocumentAttacher(
    IRequestRepository requestRepository,
    IRequestTitleRepository titleRepository,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider
) : IRequestDocumentAttacher
{
    public async Task AttachToRequestAsync(
        Guid requestId,
        AttachedDocumentInput input,
        CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdWithDocumentsAsync(requestId, cancellationToken)
                      ?? throw new RequestNotFoundException(requestId);

        var data = new RequestDocumentData(
            input.DocumentId,
            input.DocumentType,
            input.FileName,
            null,
            1,
            null,
            null,
            "FOLLOWUP",
            false,
            input.UploadedBy ?? currentUser.Username,
            input.UploadedByName ?? currentUser.Username,
            dateTimeProvider.Now);

        request.AddDocument(data);
        await requestRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task AttachToTitleAsync(
        Guid requestId,
        Guid titleId,
        AttachedDocumentInput input,
        CancellationToken cancellationToken = default)
    {
        var title = await titleRepository.GetByIdWithDocumentsAsync(titleId, cancellationToken)
                    ?? throw new BadRequestException($"Title {titleId} not found.");

        if (title.RequestId != requestId)
            throw new BadRequestException("Title does not belong to the specified request.");

        var data = new TitleDocumentData
        {
            DocumentId = input.DocumentId,
            DocumentType = input.DocumentType,
            FileName = input.FileName,
            Set = 1,
            UploadedBy = input.UploadedBy ?? currentUser.Username,
            UploadedByName = input.UploadedByName ?? currentUser.Username,
            UploadedAt = dateTimeProvider.Now
        };

        title.AddDocument(data);
        await titleRepository.SaveChangesAsync(cancellationToken);
    }
}