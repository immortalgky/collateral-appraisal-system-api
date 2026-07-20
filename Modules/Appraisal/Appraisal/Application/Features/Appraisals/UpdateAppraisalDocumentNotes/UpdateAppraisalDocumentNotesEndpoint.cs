using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.UpdateAppraisalDocumentNotes;

public class UpdateAppraisalDocumentNotesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/appraisals/{appraisalId:guid}/documents/{id:guid}",
                async (Guid appraisalId, Guid id, UpdateAppraisalDocumentNotesRequest request,
                    ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateAppraisalDocumentNotesCommand(appraisalId, id, request.Notes);
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<UpdateAppraisalDocumentNotesResponse>();
                    return Results.Ok(response);
                })
            .WithName("UpdateAppraisalDocumentNotes")
            .Produces<UpdateAppraisalDocumentNotesResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update notes on a valuation document attachment")
            .WithDescription("Updates the Notes field of a single valuation document checklist attachment.")
            .WithTags("Appraisal Documents");
    }
}
