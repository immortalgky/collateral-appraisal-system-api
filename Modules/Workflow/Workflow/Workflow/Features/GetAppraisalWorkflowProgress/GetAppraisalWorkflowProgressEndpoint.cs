using Carter;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Workflow.Workflow.Features.GetAppraisalWorkflowProgress;

public class GetAppraisalWorkflowProgressEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/workflows/appraisals/{appraisalId:guid}/progress",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new GetAppraisalWorkflowProgressQuery(appraisalId),
                        cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("GetAppraisalWorkflowProgress")
            .Produces<GetAppraisalWorkflowProgressResponse>()
            .WithSummary("Get workflow progress for an appraisal")
            .WithTags("Workflow")
            .RequireAuthorization();
    }
}
