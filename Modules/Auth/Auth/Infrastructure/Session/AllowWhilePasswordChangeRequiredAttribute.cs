namespace Auth.Infrastructure.Session;

/// <summary>
/// Marks an endpoint that must stay reachable while the signed-in user still owes a password change (see
/// <see cref="MustChangePasswordMiddleware"/>): the change-password screen's own calls, and whatever replaces or
/// ends the session. Apply as an attribute on controllers and Razor pages, or with
/// <see cref="AllowWhilePasswordChangeRequiredExtensions.AllowWhilePasswordChangeRequired{TBuilder}"/> on a minimal endpoint.
/// </summary>
/// <param name="selfRouteValue">
/// When set, the endpoint is only allowed if this route value is the caller's own user id — changing your own
/// password, not someone else's.
/// </param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowWhilePasswordChangeRequiredAttribute(string? selfRouteValue = null) : Attribute
{
    public string? SelfRouteValue { get; } = selfRouteValue;
}

public static class AllowWhilePasswordChangeRequiredExtensions
{
    public static TBuilder AllowWhilePasswordChangeRequired<TBuilder>(this TBuilder builder,
        string? selfRouteValue = null) where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new AllowWhilePasswordChangeRequiredAttribute(selfRouteValue));
}
