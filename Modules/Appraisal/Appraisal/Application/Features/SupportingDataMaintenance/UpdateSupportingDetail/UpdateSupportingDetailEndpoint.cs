namespace Appraisal.Application.Features.SupportingDataMaintenance.UpdateSupportingDetail;

public class UpdateSupportingDetailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/supporting-data/{supportingId:guid}/details/{id:guid}", async (
            Guid supportingId,
            Guid id,
            UpdateSupportingDetailRequest request,
            ISender sender,
            CancellationToken cancellationToken
        ) =>
        {
            var command = new UpdateSupportingDetailCommand(supportingId, id, request.Detail);

            var result = await sender.Send(command, cancellationToken);

            return Results.Ok(result);
        })
        .WithName("UpdateSupportingDetail")
        .Produces<UpdateSupportingDetailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update an existing supporting detail")
        .WithDescription("Update an existing supporting detail record under its parent supporting data record.")
        .WithTags("SupportingData")
        .RequireAuthorization();
    }
}
