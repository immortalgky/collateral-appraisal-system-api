using OpenIddict.Abstractions;

namespace Auth.Application.Features.Scopes;

public class ScopeDto
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public List<string> Resources { get; set; } = [];
}

public static class ScopeProjection
{
    public static async Task<ScopeDto> ToDtoAsync(
        IOpenIddictScopeManager manager, object scope, CancellationToken cancellationToken) => new()
    {
        Id = await manager.GetIdAsync(scope, cancellationToken) ?? "",
        Name = await manager.GetNameAsync(scope, cancellationToken) ?? "",
        DisplayName = await manager.GetDisplayNameAsync(scope, cancellationToken),
        Description = await manager.GetDescriptionAsync(scope, cancellationToken),
        Resources = [.. (await manager.GetResourcesAsync(scope, cancellationToken))]
    };
}
