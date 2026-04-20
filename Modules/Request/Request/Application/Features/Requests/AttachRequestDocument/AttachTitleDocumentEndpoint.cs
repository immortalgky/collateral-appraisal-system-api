using Shared.Identity;

namespace Request.Application.Features.Requests.AttachRequestDocument;

public class AttachTitleDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{requestId:guid}/titles/{titleId:guid}/documents", async (
                Guid requestId,
                Guid titleId,
                AttachRequestDocumentRequest request,
                ISender sender,
                IRequestTitleRepository titleRepository,
                ICurrentUserService currentUser,
                IDateTimeProvider dateTimeProvider,
                CancellationToken cancellationToken) =>
            {
                var title = await titleRepository.GetByIdWithDocumentsAsync(titleId, cancellationToken);
                if (title is null)
                    throw new RequestTitleNotFoundException(titleId);

                if (title.RequestId != requestId)
                    throw new BadRequestException("Title does not belong to the specified request.");

                var documentData = new TitleDocumentData
                {
                    DocumentId = request.DocumentId,
                    DocumentType = request.DocumentType,
                    FileName = request.FileName,
                    Set = 1,
                    UploadedBy = currentUser.Username,
                    UploadedByName = currentUser.Username,
                    UploadedAt = dateTimeProvider.Now
                };

                title.AddDocument(documentData);
                await titleRepository.SaveChangesAsync(cancellationToken);

                return Results.Ok(new AttachRequestDocumentResponse(true));
            })
            .WithName("AttachTitleDocument")
            .WithSummary("Attach a document to a request title")
            .WithDescription("Links an already-uploaded document to a specific title within a request.")
            .Produces<AttachRequestDocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Requests");
    }
}