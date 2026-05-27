namespace Appraisal.Application.Features.SupportingDataMaintenance.CreateDraftSupportingData;

public class CreateDraftSupportingDataEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/supporting-data/draft", async (
            CreateDraftSupportingDataRequest request,
            ISender sender,
            CancellationToken cancellationToken
        ) =>
        {
            var command = request.Adapt<CreateDraftSupportingDataCommand>();

            var result = await sender.Send(command, cancellationToken);

            var response = result.Adapt<CreateDraftSupportingDataResponse>();

            return Results.Ok(response);
        })
        .WithName("CreateDraftSupportingData")
        .Produces<CreateDraftSupportingDataResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Create new draft supporting data")
        .WithDescription("Create a new draft supporting data record for appraisal reference.")
        .WithTags("SupportingData");
    }
}