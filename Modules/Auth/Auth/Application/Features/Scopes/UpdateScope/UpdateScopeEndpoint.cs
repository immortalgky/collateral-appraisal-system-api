using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Auth.Application.Features.Scopes.UpdateScope;

public record UpdateScopeRequest(string? DisplayName, string? Description, List<string> Resources);

public class UpdateScopeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/auth/scopes/{id}", async (
                string id,
                UpdateScopeRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateScopeCommand(
                    id, request.DisplayName, request.Description, request.Resources ?? []);
                await sender.Send(command, cancellationToken);
                return Results.NoContent();
            })
            .WithName("UpdateScope")
            .WithTags("Admin - OAuth Scopes")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("OAuthScopesManage");
    }
}
