using Auth.Application.Features.Auth.PasswordPolicyAdmin.GetPasswordPolicyConfig;
using Auth.Application.Features.Auth.PasswordPolicyAdmin.UpdatePasswordPolicyConfig;

namespace Auth.Application.Features.Auth.PasswordPolicyAdmin;

/// <summary>
/// Admin endpoints for maintaining the password policy. Gated by the CanManagePasswordPolicy
/// policy (PASSWORD_POLICY_MANAGE permission).
/// </summary>
public class PasswordPolicyAdminEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/admin/password-policy",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetPasswordPolicyConfigQuery(), cancellationToken);
                    return Results.Ok(result);
                })
            .RequireAuthorization("CanManagePasswordPolicy")
            .WithName("GetPasswordPolicyConfig")
            .Produces<PasswordPolicyConfigDto>()
            .WithSummary("Get the full password policy configuration")
            .WithTags("Auth");

        app.MapPut(
                "/auth/admin/password-policy",
                async (
                    UpdatePasswordPolicyConfigCommand command,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    await sender.Send(command, cancellationToken);
                    return Results.Ok();
                })
            .RequireAuthorization("CanManagePasswordPolicy")
            .WithName("UpdatePasswordPolicyConfig")
            .Produces(StatusCodes.Status200OK)
            .WithSummary("Update the password policy configuration")
            .WithDescription("Complexity, expiry, history, and blocklist apply immediately; lockout settings apply after the next application restart.")
            .WithTags("Auth");
    }
}
