using Carter;
using MediatR;

namespace Appraisal.Application.Features.DecisionSummary.UpdateForceSaleRate;

public class UpdateForceSaleRateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/decision-summary/force-sale-rate",
                async (
                    Guid appraisalId,
                    UpdateForceSaleRateRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UpdateForceSaleRateCommand(appraisalId, request.ForceSellingRateOverride);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateForceSaleRate")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Update force-sale rate")
            .WithDescription("Persists the per-appraisal force-sale rate override and immediately rewrites the derived ForcedSaleValue.")
            .WithTags("DecisionSummary");
    }
}
