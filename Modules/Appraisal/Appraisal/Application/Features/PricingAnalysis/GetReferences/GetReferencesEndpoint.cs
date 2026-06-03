using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.GetReferences;

public class GetReferencesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/pricing-analysis/references",
                async (
                    [AsParameters] GetReferencesRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetReferencesQuery(
                        request.SubjectType,
                        request.AnchorId,
                        request.AnchorRefKey);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(new GetReferencesResponse(result.References));
                }
            )
            .WithName("GetReferences")
            .Produces<GetReferencesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("List market reference analyses for an anchor")
            .WithDescription(
                "Returns all saved reference PricingAnalyses for the given " +
                "(SubjectType, AnchorId[, AnchorRefKey]), including the methods and computed values.")
            .WithTags("PricingAnalysis");
    }
}
