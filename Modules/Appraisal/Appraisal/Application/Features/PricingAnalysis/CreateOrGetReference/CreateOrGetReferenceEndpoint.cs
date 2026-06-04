using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.CreateOrGetReference;

public class CreateOrGetReferenceEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/references",
                async (
                    CreateOrGetReferenceRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateOrGetReferenceCommand>();

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateOrGetReferenceResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CreateOrGetReference")
            .Produces<CreateOrGetReferenceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Find or create a market reference analysis")
            .WithDescription(
                "Idempotent. Returns the existing reference PricingAnalysis for the given " +
                "(SubjectType, AnchorId, AnchorRefKey) triple, creating a new one (with a Market " +
                "approach pre-added) if none exists.")
            .WithTags("PricingAnalysis");
    }
}
