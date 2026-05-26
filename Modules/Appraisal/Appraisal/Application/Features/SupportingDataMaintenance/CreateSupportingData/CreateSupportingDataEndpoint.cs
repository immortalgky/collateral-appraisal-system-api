namespace Appraisal.Application.Features.SupportingDataMaintenance.CreateSupportingData;

public class CreateSupportingDataEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/supporting-data", async (
            CreateSupportingDataRequest request,
            ISender sender,
            CancellationToken cancellationToken
        ) =>
        {
            var command = request.Adapt<CreateSupportingDataCommand>();

            var result = await sender.Send(command, cancellationToken);

            var response = result.Adapt<CreateSupportingDataResponse>();

            return Results.Ok(response);
        })
        .WithName("CreateSupportingData")
        .Produces<CreateSupportingDataResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Create new supporting data")
        .WithDescription("Create a new supporting data record for appraisal reference.")
        .WithTags("SupportingData");
    }
}