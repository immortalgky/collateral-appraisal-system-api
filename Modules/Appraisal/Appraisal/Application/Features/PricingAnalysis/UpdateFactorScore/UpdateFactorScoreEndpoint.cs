using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateFactorScore;

public class UpdateFactorScoreEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{id}/factor-scores/{scoreId}",
                async (
                    Guid id,
                    Guid scoreId,
                    UpdateFactorScoreRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UpdateFactorScoreCommand(
                        id,
                        scoreId,
                        request.SubjectValue,
                        request.SubjectScore,
                        request.ComparableValue,
                        request.ComparableScore,
                        request.FactorWeight,
                        request.AdjustmentPct,
                        request.Remarks
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UpdateFactorScoreResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("UpdateFactorScore")
            .Produces<UpdateFactorScoreResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update factor score")
            .WithDescription("Updates subject values, comparable values, weight, or adjustment percentage for a factor score.")
            .WithTags("PricingAnalysis");
    }
}
