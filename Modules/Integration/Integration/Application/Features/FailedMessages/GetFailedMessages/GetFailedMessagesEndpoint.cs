using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Integration.Application.Features.FailedMessages.GetFailedMessages;

public class GetFailedMessagesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/failed-messages", async (
                int? pageNumber,
                int? pageSize,
                string? status,
                string? queue,
                string? node,
                string? exceptionType,
                string? search,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetFailedMessagesQuery(
                    pageNumber ?? 1,
                    pageSize ?? 20,
                    status ?? "Pending",
                    queue,
                    node,
                    exceptionType,
                    search);

                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetFailedMessages")
            .WithTags("Admin - Failed Messages")
            .Produces<PaginatedResult<FailedMessageListDto>>(StatusCodes.Status200OK)
            .RequireAuthorization("FailedMessageView");
    }
}
