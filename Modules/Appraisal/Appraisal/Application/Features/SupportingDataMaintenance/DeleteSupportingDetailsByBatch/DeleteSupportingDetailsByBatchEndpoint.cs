namespace Appraisal.Application.Features.SupportingDataMaintenance.DeleteSupportingDetailsByBatch;

public class DeleteSupportingDetailsByBatchEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
            "/supporting-data/{supportingId:guid}/details/batch",
            async (
                Guid supportingId,
                [FromBody] DeleteSupportingDetailsByBatchRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new DeleteSupportingDetailsByBatchCommand(supportingId, request.SupportingDetailIds), cancellationToken);

                return Results.NoContent();
            }
        )
        .WithName("DeleteSupportingDetailsByBatch")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete supporting detail by batch")
        .WithDescription("Deletes a supporting detail record by batch.")
        .WithTags("SupportingData")
        .RequireAuthorization();
    }
}