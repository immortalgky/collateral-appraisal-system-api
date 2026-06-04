namespace Appraisal.Application.Features.SupportingDataMaintenance.DeleteSupportingDetailById;

public class DeleteSupportingDetailByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/supporting-data/{supportingId:guid}/details/{detailId:guid}",
                async (Guid supportingId, Guid detailId, ISender sender, CancellationToken cancellationToken) =>
                {
                    await sender.Send(new DeleteSupportingDetailByIdCommand(supportingId, detailId), cancellationToken);
                    return Results.NoContent();
                })
            .WithName("DeleteSupportingDetailById")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete supporting detail by ID")
            .WithDescription("Deletes a supporting detail record by its Id.")
            .WithTags("SupportingData")
            .RequireAuthorization();
    }
}