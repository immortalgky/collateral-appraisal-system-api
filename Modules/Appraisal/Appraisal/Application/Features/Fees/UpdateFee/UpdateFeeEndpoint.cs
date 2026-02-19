namespace Appraisal.Application.Features.Fees.UpdateFee;

public class UpdateFeeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/appraisals/{appraisalId:guid}/fees/{feeId:guid}",
            async (
                Guid feeId,
                UpdateFeeRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = request.Adapt<UpdateFeeCommand>() with { FeeId = feeId };

                await sender.Send(command, cancellationToken);

                return Results.NoContent();
            });
    }
}