namespace Appraisal.Application.Features.PricingAnalysis.UpdateRemark;

public class UpdateRemarkEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{id:guid}/remark",
                async (
                    Guid id,
                    UpdateRemarkRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UpdateRemarkCommand(
                        id,
                        request.Remark
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UpdateRemarkResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("UpdateRemark")
            .Produces<UpdateRemarkResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update remark for pricing analysis")
            .WithDescription("Creates or updates the remark.")
            .WithTags("PricingAnalysis");
    }
}
