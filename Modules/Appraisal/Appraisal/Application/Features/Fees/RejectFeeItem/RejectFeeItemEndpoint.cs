namespace Appraisal.Application.Features.Fees.RejectFeeItem;

public class RejectFeeItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/appraisals/{appraisalId:guid}/fees/{feeId:guid}/items/{itemId:guid}/reject",
                async (
                    Guid appraisalId,
                    Guid feeId,
                    Guid itemId,
                    RejectFeeItemRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RejectFeeItemCommand(
                        appraisalId,
                        feeId,
                        itemId,
                        request.RejectedBy,
                        request.Reason);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("RejectFeeItem")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Reject fee item")
            .WithDescription("Reject a pending fee item with a reason.")
            .WithTags("Fee");
    }
}
