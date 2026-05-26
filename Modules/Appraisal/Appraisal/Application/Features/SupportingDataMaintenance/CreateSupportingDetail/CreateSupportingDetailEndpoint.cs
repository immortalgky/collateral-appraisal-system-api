namespace Appraisal.Application.Features.SupportingDataMaintenance.CreateSupportingDetail;

public class CreateSupportingDetailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/supporting-data/{supportingId:guid}/detail", async (
            Guid supportingId,
            CreateSupportingDetailRequest request,
            ISender sender,
            CancellationToken cancellationToken
        ) =>
        {
            var command = new CreateSupportingDetailCommand(supportingId, request.Detail);

            var result = await sender.Send(command, cancellationToken);

            var response = result.Adapt<CreateSupportingDetailResponse>();
            return Results.Ok(response);
        })
        .WithName("CreateSupportingDetail")
        .Produces<CreateSupportingDetailResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Create new supporting detail")
        .WithDescription("Create a new supporting detail record for appraisal reference.")
        .WithTags("SupportingData");
    }
}