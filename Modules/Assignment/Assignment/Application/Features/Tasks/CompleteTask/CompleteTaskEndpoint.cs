using Shared.Messaging.Values;

namespace Assignment.Domain.Tasks.Features.CompleteTask;

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
                    request.CorrelationId, Enum.Parse<TaskName>(request.ActivityName), request.ActionTaken), cancellationToken);

                var response = new CompleteActivityResponse(result.IsSuccess);

                return Results.Ok(response);
            });
    }
}