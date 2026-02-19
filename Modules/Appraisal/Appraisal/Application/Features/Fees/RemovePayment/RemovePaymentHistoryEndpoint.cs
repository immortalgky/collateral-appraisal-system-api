namespace Appraisal.Application.Features.Fees.RemovePayment;

public class RemovePaymentHistoryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/appraisals/{appraisalId:guid}/fees/{feeId:guid}/payments/{paymentId:guid}",
            async (
                Guid feeId,
                Guid paymentId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new RemovePaymentCommand(feeId, paymentId);

                await sender.Send(command, cancellationToken);

                return Results.NoContent();
            });
    }
}