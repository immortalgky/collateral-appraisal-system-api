namespace Appraisal.Application.Features.Appraisals.NotifyExternalSystem;

public class NotifyExternalSystemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/documents/notify-external",
                async (
                    Guid appraisalId,
                    NotifyExternalSystemRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new NotifyExternalSystemCommand(appraisalId, request.Reason), cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("NotifyExternalSystem")
            .Produces<NotifyExternalSystemResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Re-notify the source system that the appraisal result is ready")
            .WithDescription(
                "Publishes AppraisalResultReadyIntegrationEvent for a Completed appraisal that came from an " +
                "external system, and records an audit entry in the same transaction. Nothing is regenerated.")
            .WithTags("Appraisal Data Correction")
            .RequireAuthorization("appraisal.data-correction");
    }
}
