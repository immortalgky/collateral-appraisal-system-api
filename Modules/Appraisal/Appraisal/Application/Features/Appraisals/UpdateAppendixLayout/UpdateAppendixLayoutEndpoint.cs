using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.UpdateAppendixLayout;

public class UpdateAppendixLayoutEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/appraisals/{appraisalId:guid}/appendices/{appendixId:guid}/layout",
                async (Guid appraisalId, Guid appendixId, UpdateAppendixLayoutRequest request,
                    ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateAppendixLayoutCommand(appendixId, request.LayoutColumns);
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<UpdateAppendixLayoutResponse>();
                    return Results.Ok(response);
                })
            .WithName("UpdateAppendixLayout")
            .Produces<UpdateAppendixLayoutResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Update appendix layout columns")
            .WithDescription("Updates the layout column count (1, 2, or 3) for a specific appendix.")
            .WithTags("Appendix");
    }
}
