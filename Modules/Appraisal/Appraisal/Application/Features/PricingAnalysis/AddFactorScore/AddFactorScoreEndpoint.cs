using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.AddFactorScore;

public class AddFactorScoreEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{id}/calculations/{calcId}/factor-scores",
                async (
                    Guid id,
                    Guid calcId,
                    AddFactorScoreRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AddFactorScoreCommand(
                        id,
                        calcId,
                        request.FactorId,
                        request.FactorWeight,
                        request.SubjectValue,
                        request.SubjectScore,
                        request.ComparableValue,
                        request.ComparableScore,
                        request.AdjustmentPct,
                        request.Remarks
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<AddFactorScoreResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("AddFactorScore")
            .Produces<AddFactorScoreResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add factor score to pricing calculation")
            .WithDescription("Adds a new factor score to a WQS/SaleGrid pricing calculation for factor-level comparison.")
            .WithTags("PricingAnalysis");
    }
}
