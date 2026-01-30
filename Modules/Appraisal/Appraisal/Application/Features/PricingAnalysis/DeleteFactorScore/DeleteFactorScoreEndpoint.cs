using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.DeleteFactorScore;

public class DeleteFactorScoreEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/pricing-analysis/{id}/factor-scores/{scoreId}",
                async (
                    Guid id,
                    Guid scoreId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new DeleteFactorScoreCommand(id, scoreId);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<DeleteFactorScoreResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("DeleteFactorScore")
            .Produces<DeleteFactorScoreResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete factor score")
            .WithDescription("Removes a factor score from a pricing calculation and resequences remaining scores.")
            .WithTags("PricingAnalysis");
    }
}
