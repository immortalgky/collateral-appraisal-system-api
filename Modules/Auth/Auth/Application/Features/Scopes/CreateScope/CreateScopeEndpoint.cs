using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Auth.Application.Features.Scopes.CreateScope;

public record CreateScopeRequest(string Name, string? DisplayName, string? Description, List<string> Resources);

public class CreateScopeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/scopes", async (
                CreateScopeRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateScopeCommand(
                    request.Name, request.DisplayName, request.Description, request.Resources ?? []);
                var result = await sender.Send(command, cancellationToken);
                return Results.Created($"/auth/scopes/{result.Id}", result);
            })
            .WithName("CreateScope")
            .WithTags("Admin - OAuth Scopes")
            .Produces<CreateScopeResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization("OAuthScopesManage");
    }
}
