public class SaveDecisionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/tasks/{taskId:guid}/save-decision",
                async (Guid taskId, SaveDecisionRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new SaveDecisionCommand(
                            taskId,
                            request.DecisionType,
                            request.AssignNextToType,
                            request.CommentDecision),
                        cancellationToken);

                    return result.IsSuccess
                        ? Results.Ok(result)
                        : Results.BadRequest(result);
                }
            );
    }
}

public record SaveDecisionRequest(string DecisionType, string AssignNextToType, string CommentDecision);
