using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.AddMethod;

public class AddMethodEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{id}/approaches/{approachId}/methods",
                async (
                    Guid id,
                    Guid approachId,
                    AddMethodRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AddMethodCommand(
                        id,
                        approachId,
                        request.MethodType,
                        request.Status ?? "Selected");

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<AddMethodResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("AddMethod")
            .Produces<AddMethodResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add method to approach")
            .WithDescription("Adds a new method (WQS, SaleGrid, DirectComparison, etc.) to an approach.")
            .WithTags("PricingAnalysis");
    }
}
