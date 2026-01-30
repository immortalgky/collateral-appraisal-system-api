using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.CreateMachineryProperty;

public class CreateMachineryPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/machinery-properties",
                async (
                    Guid appraisalId,
                    CreateMachineryPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateMachineryPropertyCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateMachineryPropertyResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties/{response.PropertyId}/machinery-detail",
                        response);
                }
            )
            .WithName("CreateMachineryProperty")
            .Produces<CreateMachineryPropertyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a machinery property")
            .WithDescription("Creates a new machinery property with its appraisal detail for an appraisal.")
            .WithTags("Appraisal Properties");
    }
}
