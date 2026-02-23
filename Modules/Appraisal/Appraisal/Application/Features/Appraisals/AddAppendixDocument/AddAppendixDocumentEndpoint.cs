using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.AddAppendixDocument;

public class AddAppendixDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/appraisals/{appraisalId:guid}/appendices/{appendixId:guid}/documents",
                async (Guid appraisalId, Guid appendixId, AddAppendixDocumentRequest request,
                    ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new AddAppendixDocumentCommand(
                        appendixId,
                        request.GalleryPhotoId,
                        request.DisplaySequence);

                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<AddAppendixDocumentResponse>();
                    return Results.Created(
                        $"/appraisals/{appraisalId}/appendices/{appendixId}/documents/{response.DocumentId}",
                        response);
                })
            .WithName("AddAppendixDocument")
            .Produces<AddAppendixDocumentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Attach a document to an appendix")
            .WithDescription("Adds a document reference to the specified appendix.")
            .WithTags("Appendix");
    }
}
