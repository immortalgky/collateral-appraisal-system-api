namespace Assignment.Tasks.Features.KickstartWorkflow;

public record KickstartWorkflowRequest(long RequestId);

public class KickstartWorkflowEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/workflow/kickstart", async (KickstartWorkflowRequest request, ISender sender) =>
        {
            var result = await sender.Send(new KickstartWorkflowCommand(request.RequestId));

            return Results.Ok(result);
        });
    }
}