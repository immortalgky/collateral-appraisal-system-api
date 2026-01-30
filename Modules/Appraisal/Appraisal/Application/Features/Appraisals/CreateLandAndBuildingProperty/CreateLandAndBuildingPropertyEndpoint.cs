using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.CreateLandAndBuildingProperty;

public class CreateLandAndBuildingPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/land-and-building-properties",
                async (
                    Guid appraisalId,
                    CreateLandAndBuildingPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateLandAndBuildingPropertyCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateLandAndBuildingPropertyResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties/{response.PropertyId}/land-and-building-detail",
                        response);
                }
            )
            .WithName("CreateLandAndBuildingProperty")
            .Produces<CreateLandAndBuildingPropertyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a land and building property")
            .WithDescription("Creates a new land and building property with its appraisal detail for an appraisal.")
            .WithTags("Appraisal Properties");
    }
}
