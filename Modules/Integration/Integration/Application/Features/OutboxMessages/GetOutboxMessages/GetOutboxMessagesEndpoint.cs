using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Integration.Application.Features.OutboxMessages.GetOutboxMessages;

public class GetOutboxMessagesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/outbox-messages", async (
                int? pageNumber,
                int? pageSize,
                string? status,
                string? module,
                string? search,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetOutboxMessagesQuery(
                    pageNumber ?? 1,
                    pageSize ?? 20,
                    status ?? "Failed",
                    module,
                    search);

                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetOutboxMessages")
            .WithTags("Admin - Outbox Messages")
            .Produces<PaginatedResult<OutboxMessageListDto>>(StatusCodes.Status200OK)
            .RequireAuthorization("FailedMessageView");
    }
}
