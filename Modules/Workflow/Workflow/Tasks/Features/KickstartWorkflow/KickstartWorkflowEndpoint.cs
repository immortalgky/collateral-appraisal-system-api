using Mapster;

namespace Workflow.Tasks.Features.KickstartWorkflow;

public record KickstartWorkflowRequest(long RequestId);

public class KickstartWorkflowEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/workflow/kickstart", async (KickstartWorkflowRequest request, ISender sender) =>
            {
                var result = await sender.Send(new KickstartWorkflowCommand(request.RequestId));

                var response = result.Adapt<KickstartWorkflowResponse>();
                return Results.Ok(response);
            })
            .WithName("KickstartWorkflow")
            .Produces<KickstartWorkflowResponse>()
            .WithSummary("Kickstart workflow")
            .WithDescription("Starts a new workflow for a request.")
            .WithTags("Workflow");
    }
}