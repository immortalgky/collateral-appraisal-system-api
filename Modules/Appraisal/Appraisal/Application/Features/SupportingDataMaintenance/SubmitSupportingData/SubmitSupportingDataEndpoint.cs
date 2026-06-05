namespace Appraisal.Application.Features.SupportingDataMaintenance.SubmitSupportingData;

public class SubmitSupportingDataEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/supporting-data/submit/", async (
            SubmitSupportingDataRequest request,
            ISender sender,
            CancellationToken cancellationToken
        ) =>
        {
            var command = new SubmitSupportingDataCommand(request.SupportingId, new SupportingDataHeaderDto(
                request.Header.ImportChannel,
                request.Header.ImportDate,
                request.Header.SourceOfData,
                request.Header.Description,
                request.Header.Decision,
                request.Header.Remark)
                );

            var result = await sender.Send(command, cancellationToken);

            var response = result.Adapt<SubmitSupportingDataResponse>();

            return Results.Ok(response);
        })
        .WithName("SubmitSupportingData")
        .Produces<SubmitSupportingDataResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Submit an existing supporting data")
        .WithDescription("Submit an existing supporting data record for appraisal reference.")
        .WithTags("SupportingData")
        .RequireAuthorization();
    }
}