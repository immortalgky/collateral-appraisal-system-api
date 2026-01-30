using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.CreateBuildingProperty;

public class CreateBuildingPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/building-properties",
                async (
                    Guid appraisalId,
                    CreateBuildingPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateBuildingPropertyCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateBuildingPropertyResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties/{response.PropertyId}/building-detail",
                        response);
                }
            )
            .WithName("CreateBuildingProperty")
            .Produces<CreateBuildingPropertyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a building property")
            .WithDescription("Creates a new building property with its appraisal detail for an appraisal.")
            .WithTags("Appraisal Properties");
    }
}
