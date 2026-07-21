using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.RemoveAppraisalDocument;

public class RemoveAppraisalDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/appraisals/{appraisalId:guid}/documents/{id:guid}",
                async (Guid appraisalId, Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new RemoveAppraisalDocumentCommand(appraisalId, id);
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<RemoveAppraisalDocumentResponse>();
                    return Results.Ok(response);
                })
            .WithName("RemoveAppraisalDocument")
            .Produces<RemoveAppraisalDocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a valuation document attachment")
            .WithDescription("Removes a document attachment from the appraisal's valuation document checklist.")
            .WithTags("Appraisal Documents");
    }
}
