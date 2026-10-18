using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.FailedMessages.GetFailedMessage;

public class GetFailedMessageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/failed-messages/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetFailedMessageQuery(id), cancellationToken);

                return Results.Ok(result);
            })
            .WithName("GetFailedMessage")
            .WithTags("Admin - Failed Messages")
            .Produces<FailedMessageDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("FailedMessageView");
    }
}
