using Appraisal.Application.Features.PricingAnalysis.CreateOrGetReference;
using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.CreateReferenceFromMethod;

public class CreateReferenceFromMethodEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/references/from-method",
                async (
                    CreateReferenceFromMethodRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateReferenceFromMethodCommand>();

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateOrGetReferenceResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CreateReferenceFromMethod")
            .Produces<CreateOrGetReferenceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a reference analysis by cloning an existing method")
            .WithDescription(
                "Deep-clones a Cost-approach method (WQS / SaleGrid / DirectComparison) into a new " +
                "IncomeLandRef reference PricingAnalysis, optionally overriding the land area. " +
                "Idempotent: returns the existing reference if one already exists for the anchor.")
            .WithTags("PricingAnalysis");
    }
}
