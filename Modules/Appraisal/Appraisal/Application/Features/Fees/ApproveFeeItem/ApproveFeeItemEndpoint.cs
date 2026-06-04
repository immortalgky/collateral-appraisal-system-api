namespace Appraisal.Application.Features.Fees.ApproveFeeItem;

public class ApproveFeeItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/appraisals/{appraisalId:guid}/fees/{feeId:guid}/items/{itemId:guid}/approve",
                async (
                    Guid appraisalId,
                    Guid feeId,
                    Guid itemId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    // Actor is stamped server-side from the authenticated user (see handler).
                    var command = new ApproveFeeItemCommand(
                        appraisalId,
                        feeId,
                        itemId);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("ApproveFeeItem")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Approve fee item")
            .WithDescription("Approve a pending fee item.")
            .WithTags("Fee");
    }
}
