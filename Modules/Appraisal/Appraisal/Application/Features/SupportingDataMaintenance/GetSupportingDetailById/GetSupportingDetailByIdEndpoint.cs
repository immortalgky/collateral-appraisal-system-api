namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDetailById;

public class GetSupportingDetailByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/supporting-data/{supportingId:guid}/details/{detailId:guid}", async (
            Guid supportingId,
            Guid detailId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetSupportingDetailByIdQuery(supportingId, detailId);

            var result = await sender.Send(query, cancellationToken);

            return Results.Ok(result);
        })
        .WithName("GetSupportingDetailById")
        .Produces<GetSupportingDetailByIdResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get supporting detail by ID")
        .WithDescription("Retrieves a specific supporting detail record by its unique identifier, scoped under its parent supporting data record.")
        .WithTags("SupportingData")
        .RequireAuthorization();
    }
}
