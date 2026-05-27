namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDetailList;

public class GetSupportingDetailListEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/supporting-data/{supportingId:guid}/details", async (
            [AsParameters] PaginationRequest pagination,
            Guid supportingId,
            ISender sender,
            CancellationToken cancellationToken
        ) =>
        {
            var filter = new GetSupportingDetailListQuery(pagination.PageNumber, pagination.PageSize, supportingId);

            var result = await sender.Send(filter, cancellationToken);

            return Results.Ok(result);
        })
        .WithName("GetSupportingDetailList")
        .Produces<GetSupportingDetailListResult>()
        .WithSummary("Get supporting detail list")
        .WithDescription("Retrieves a paginated list of supporting detail records.");
    }
}
