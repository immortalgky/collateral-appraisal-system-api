using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.CreateCondoProperty;

public class CreateCondoPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/condo-properties",
                async (
                    Guid appraisalId,
                    CreateCondoPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateCondoPropertyCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateCondoPropertyResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties/{response.PropertyId}/condo-detail",
                        response);
                }
            )
            .WithName("CreateCondoProperty")
            .Produces<CreateCondoPropertyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a condo property")
            .WithDescription("Creates a new condo property with its appraisal detail for an appraisal.")
            .WithTags("Appraisal Properties");
    }
}
