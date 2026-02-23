using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.RemoveAppendixDocument;

public class RemoveAppendixDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/appraisals/{appraisalId:guid}/appendices/{appendixId:guid}/documents/{documentId:guid}",
                async (Guid appraisalId, Guid appendixId, Guid documentId,
                    ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new RemoveAppendixDocumentCommand(appendixId, documentId);
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<RemoveAppendixDocumentResponse>();
                    return Results.Ok(response);
                })
            .WithName("RemoveAppendixDocument")
            .Produces<RemoveAppendixDocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a document from an appendix")
            .WithDescription("Removes a document reference from the specified appendix.")
            .WithTags("Appendix");
    }
}
