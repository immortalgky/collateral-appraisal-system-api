namespace Appraisal.Application.Features.Fees.UpdatePayment;

public class UpdatePaymentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/appraisals/{appraisalId:guid}/fees/{feeId:guid}/payments/{paymentId:guid}",
            async (
                Guid feeId,
                Guid paymentId,
                UpdatePaymentRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdatePaymentCommand(feeId, paymentId, request.PaymentAmount, request.PaymentDate);

                await sender.Send(command, cancellationToken);

                return Results.NoContent();
            });
    }
}