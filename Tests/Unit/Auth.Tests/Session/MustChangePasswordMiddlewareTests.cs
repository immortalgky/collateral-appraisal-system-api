using System.Security.Claims;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Tests.Session;

public class MustChangePasswordMiddlewareTests
{
    private static readonly string Self = Guid.NewGuid().ToString();

    private static Endpoint EndpointWith(params object[] metadata) =>
        new(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test");

    private static async Task<(bool Reached, int Status)> Send(Endpoint? endpoint, bool owesChange = true,
        string authenticationType = AuthSchemes.AppCookie, string? routeId = null)
    {
        var reached = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });
        var identity = new ClaimsIdentity(authenticationType);
        identity.AddClaim(new Claim("sub", Self));
        if (owesChange) identity.AddClaim(new Claim(AuthSchemes.MustChangePasswordClaim, "true"));

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        if (endpoint is not null) context.SetEndpoint(endpoint);
        if (routeId is not null) context.Request.RouteValues["id"] = routeId;

        await middleware.InvokeAsync(context);
        return (reached, context.Response.StatusCode);
    }

    [Fact]
    public async Task An_endpoint_marked_allowed_is_reachable() =>
        Assert.True((await Send(EndpointWith(new AllowWhilePasswordChangeRequiredAttribute()))).Reached);

    [Fact]
    public async Task An_unmarked_endpoint_answers_403()
    {
        var (reached, status) = await Send(EndpointWith("some other metadata"));
        Assert.False(reached);
        Assert.Equal(StatusCodes.Status403Forbidden, status);
    }

    [Fact]
    public async Task The_self_only_endpoint_is_allowed_for_the_callers_own_id() =>
        Assert.True((await Send(EndpointWith(new AllowWhilePasswordChangeRequiredAttribute("id")), routeId: Self)).Reached);

    [Fact]
    public async Task The_self_only_endpoint_refuses_someone_elses_id()
    {
        var (reached, status) = await Send(
            EndpointWith(new AllowWhilePasswordChangeRequiredAttribute("id")), routeId: Guid.NewGuid().ToString());
        Assert.False(reached);
        Assert.Equal(StatusCodes.Status403Forbidden, status);
    }

    [Fact]
    public async Task The_self_only_endpoint_refuses_a_request_with_no_id() =>
        Assert.False((await Send(EndpointWith(new AllowWhilePasswordChangeRequiredAttribute("id")))).Reached);

    [Fact]
    public async Task A_request_that_matched_no_endpoint_has_nothing_to_protect() =>
        Assert.True((await Send(endpoint: null)).Reached);

    [Fact]
    public async Task A_session_with_nothing_owed_is_untouched() =>
        Assert.True((await Send(EndpointWith(), owesChange: false)).Reached);

    [Fact]
    public async Task Principals_from_other_schemes_are_untouched() =>
        Assert.True((await Send(EndpointWith(), authenticationType: "Bearer")).Reached);
}
