using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Auth.Application.Features.Clients.RegisterClient;

public record RegisterClientRequest(
    string? ClientId,
    string DisplayName,
    string ClientType,
    List<Uri> RedirectUris,
    List<Uri> PostLogoutRedirectUris,
    List<string> GrantTypes,
    List<string> Scopes
);

public class RegisterClientEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/clients", async (
                RegisterClientRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new RegisterClientCommand(
                    request.ClientId,
                    request.DisplayName,
                    request.ClientType,
                    request.RedirectUris ?? [],
                    request.PostLogoutRedirectUris ?? [],
                    request.GrantTypes ?? [],
                    request.Scopes ?? []);

                var result = await sender.Send(command, cancellationToken);
                return Results.Created($"/auth/clients/{result.Id}", result);
            })
            .WithName("RegisterClient")
            .WithTags("Admin - OAuth Clients")
            .Produces<RegisterClientResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization("OAuthClientsManage");
    }
}
