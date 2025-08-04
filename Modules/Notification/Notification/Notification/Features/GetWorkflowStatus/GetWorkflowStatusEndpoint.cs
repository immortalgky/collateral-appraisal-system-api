using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Notification.Notification.Features.GetWorkflowStatus;

public class GetWorkflowStatusEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workflow/{requestId:long}/status", GetWorkflowStatus)
            .WithName("GetWorkflowStatus")
            .WithSummary("Get workflow status")
            .WithDescription("Retrieves the current workflow status for a request")
            .Produces<GetWorkflowStatusResponse>()
            .RequireAuthorization();
    }

    private static async Task<IResult> GetWorkflowStatus(
        long requestId,
        ISender sender)
    {
        var query = new GetWorkflowStatusQuery(requestId);
        var result = await sender.Send(query);

        return Results.Ok(result);
    }
}