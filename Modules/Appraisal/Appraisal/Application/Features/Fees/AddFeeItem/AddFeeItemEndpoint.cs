namespace Appraisal.Application.Features.Fees.AddFeeItem;

public class AddFeeItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/fees/{feeId:guid}/items",
                async (
                    Guid appraisalId,
                    Guid feeId,
                    AddFeeItemRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AddFeeItemCommand(
                        appraisalId,
                        feeId,
                        request.FeeCode,
                        request.FeeDescription,
                        request.FeeAmount);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("AddFeeItem")
            .Produces<AddFeeItemResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add fee item")
            .WithDescription("Add an individual fee item (e.g. Travel Fee, Urgent Fee) to an existing appraisal fee.")
            .WithTags("Fee");

        // No fee id: the admin's first fee item on an appraisal that has no fee row yet. Creates the fee row.
        app.MapPost(
                "/appraisals/{appraisalId:guid}/fees/items",
                async (
                    Guid appraisalId,
                    AddFeeItemRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AddFeeItemCommand(
                        appraisalId,
                        null,
                        request.FeeCode,
                        request.FeeDescription,
                        request.FeeAmount);

                    return Results.Ok(await sender.Send(command, cancellationToken));
                }
            )
            .WithName("AddFeeItemToAwaitingAssignmentFee")
            .Produces<AddFeeItemResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Add fee item before assignment")
            .WithDescription(
                "Admin stage only: adds a fee item to the appraisal's awaiting-assignment fee, creating the fee row " +
                "when the request carried none. Returns the fee id so the caller can address it afterwards.")
            .WithTags("Fee");
    }
}
