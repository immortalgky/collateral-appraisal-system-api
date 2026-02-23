namespace Appraisal.Application.Features.Fees.RemoveFeeItem;

public class RemoveFeeItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/appraisals/{appraisalId:guid}/fees/{feeId:guid}/items/{feeItemId:guid}",
            async (
                Guid appraisalId,
                Guid feeId,
                Guid feeItemId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new RemoveFeeItemCommand(feeId, feeItemId);

                await sender.Send(command, cancellationToken);

                return Results.NoContent();
            });
    }
}