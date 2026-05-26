namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataById;

public class GetSupportingDataByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/supporting-data/{supportingId:guid}", async (Guid supportingId,
        ISender sender,
        CancellationToken cancellationToken) =>
        {
            var query = new GetSupportingDataByIdQuery(supportingId);

            var result = await sender.Send(query, cancellationToken);

            var response = result.Adapt<GetSupportingDataByIdResponse>();

            return Results.Ok(response);
        })
        .WithName("GetSupportingDataById")
        .Produces<GetSupportingDataByIdResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get supporting data by ID")
        .WithDescription("Retrieves a specific supporting data record by its unique identifier.")
        .WithTags("SupportingData");
    }
}