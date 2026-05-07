namespace Appraisal.Application.Features.Fees.UpdateConstructionInspectionFee;

public class UpdateConstructionInspectionFeeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/appraisals/{appraisalId:guid}/fees/{feeId:guid}/construction-inspection-fee",
            async (
                Guid feeId,
                UpdateConstructionInspectionFeeRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateConstructionInspectionFeeCommand(feeId, request.Amount);

                await sender.Send(command, cancellationToken);

                return Results.NoContent();
            });
    }
}
