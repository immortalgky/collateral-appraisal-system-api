using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.CreateVesselProperty;

public class CreateVesselPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/vessel-properties",
                async (
                    Guid appraisalId,
                    CreateVesselPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateVesselPropertyCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateVesselPropertyResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties/{response.PropertyId}/vessel-detail",
                        response);
                }
            )
            .WithName("CreateVesselProperty")
            .Produces<CreateVesselPropertyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a vessel property")
            .WithDescription("Creates a new vessel property with its appraisal detail for an appraisal.")
            .WithTags("Appraisal Properties");
    }
}
