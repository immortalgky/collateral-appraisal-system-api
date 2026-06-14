using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Auth.Application.Features.Clients.UpdateClient;

public record UpdateClientRequest(
    string DisplayName,
    List<Uri> RedirectUris,
    List<Uri> PostLogoutRedirectUris,
    List<string> GrantTypes,
    List<string> Scopes
);

public class UpdateClientEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/auth/clients/{id}", async (
                string id,
                UpdateClientRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateClientCommand(
                    id,
                    request.DisplayName,
                    request.RedirectUris ?? [],
                    request.PostLogoutRedirectUris ?? [],
                    request.GrantTypes ?? [],
                    request.Scopes ?? []);

                await sender.Send(command, cancellationToken);
                return Results.NoContent();
            })
            .WithName("UpdateClient")
            .WithTags("Admin - OAuth Clients")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("OAuthClientsManage");
    }
}
