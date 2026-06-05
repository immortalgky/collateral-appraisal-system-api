namespace Appraisal.Application.Features.SupportingDataMaintenance.DeleteSupportingDataById;

public class DeleteSupportingDataByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/supporting-data/{supportingId:guid}",
                async (Guid supportingId, ISender sender, CancellationToken cancellationToken) =>
                {
                    await sender.Send(new DeleteSupportingDataByIdCommand(supportingId), cancellationToken);
                    return Results.NoContent();
                })
            .WithName("DeleteSupportingDataById")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete supporting data by ID")
            .WithDescription("Deletes a supporting data record by its Id.")
            .WithTags("SupportingData")
            .RequireAuthorization();
    }
}