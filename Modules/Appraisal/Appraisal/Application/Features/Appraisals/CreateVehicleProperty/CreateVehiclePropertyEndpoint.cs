using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.CreateVehicleProperty;

public class CreateVehiclePropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/vehicle-properties",
                async (
                    Guid appraisalId,
                    CreateVehiclePropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateVehiclePropertyCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateVehiclePropertyResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties/{response.PropertyId}/vehicle-detail",
                        response);
                }
            )
            .WithName("CreateVehicleProperty")
            .Produces<CreateVehiclePropertyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a vehicle property")
            .WithDescription("Creates a new vehicle property with its appraisal detail for an appraisal.")
            .WithTags("Appraisal Properties");
    }
}
