namespace Assignment.Tasks.Features.CompleteTask;

public record CompleteActivityRequest(Guid CorrelationId, string ActivityName, string ActionTaken);

public record CompleteActivityResponse(bool IsSuccess);

public class CompleteTaskEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/workflow/complete",
            async (CompleteActivityRequest request, ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new CompleteActivityCommand(
                    request.CorrelationId, request.ActivityName, request.ActionTaken), cancellationToken);

                var response = new CompleteActivityResponse(result.IsSuccess);

                return Results.Ok(response);
            });
    }
}