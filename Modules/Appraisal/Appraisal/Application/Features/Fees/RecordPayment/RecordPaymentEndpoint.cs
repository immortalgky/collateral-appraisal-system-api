namespace Appraisal.Application.Features.Fees.RecordPayment;

public class RecordPaymentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/fees/{feeId:guid}/payments",
                async (
                    Guid appraisalId,
                    Guid feeId,
                    RecordPaymentRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RecordPaymentCommand(
                        appraisalId,
                        feeId,
                        request.PaymentAmount,
                        request.PaymentDate,
                        request.PaymentMethod,
                        request.PaymentReference,
                        request.Remarks);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("RecordPayment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Record payment")
            .WithDescription("Record a payment against an appraisal fee.")
            .WithTags("Fee");
    }
}
