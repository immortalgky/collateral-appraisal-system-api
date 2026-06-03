namespace Appraisal.Application.Features.Fees.UpdateFeeItem;

public class UpdateFeeItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/appraisals/{appraisalId:guid}/fees/{feeId:guid}/items/{feeItemId:guid}", async (
            Guid appraisalId,
            Guid feeId,
            Guid feeItemId,
            UpdateFeeItemRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = request.Adapt<UpdateFeeItemCommand>() with { AppraisalId = appraisalId, FeeId = feeId, FeeItemId = feeItemId };

            await sender.Send(command, cancellationToken);

            return Results.NoContent();
        });
    }
}