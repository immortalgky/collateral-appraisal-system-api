using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.FailedMessages.GetFailedMessagesSummary;

public class GetFailedMessagesSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/failed-messages/summary", async (
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetFailedMessagesSummaryQuery(), cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetFailedMessagesSummary")
            .WithTags("Admin - Failed Messages")
            .Produces<FailedMessagesSummaryDto>(StatusCodes.Status200OK)
            .RequireAuthorization("FailedMessageView");
    }
}
