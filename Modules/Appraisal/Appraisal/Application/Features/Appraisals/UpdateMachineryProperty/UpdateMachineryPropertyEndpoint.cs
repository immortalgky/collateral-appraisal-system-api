using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.UpdateMachineryProperty;

public class UpdateMachineryPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/machinery-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    UpdateMachineryPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateMachineryPropertyCommand>()
                        with { AppraisalId = appraisalId, PropertyId = propertyId };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateMachineryProperty")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update machinery property detail")
            .WithDescription("Update the detail of a machinery property.")
            .WithTags("Appraisal Properties");
    }
}
