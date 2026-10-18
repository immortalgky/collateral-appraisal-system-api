using System.Security.Claims;

namespace Auth.Application.Services;

/// <summary>
/// Result of a token grant: either a built access-token <see cref="Principal"/> when the grant may
/// proceed, or a non-null <see cref="Rejection"/> error description to deny it.
/// </summary>
public record TokenGrantResult(ClaimsPrincipal? Principal, string? Rejection);

public interface ITokenService
{
    /// <summary>
    /// Re-validates the account and builds the access-token principal for an authorization-code
    /// exchange. The code is minted from the Identity cookie, which outlives a deactivation or a
    /// lapsed access window, so the account state has to be checked here too.
    /// </summary>
    public Task<TokenGrantResult> CreateAuthCodeFlowAccessTokenPrincipal(
        OpenIddictRequest request,
        ClaimsPrincipal principal
    );
    public Task<ClaimsPrincipal> CreateClientCredFlowAccessTokenPrincipal(
        OpenIddictRequest request
    );

    /// <summary>
    /// Re-validates the account (deactivated / access window lapsed / must-change-password / expired
    /// password) and, when the refresh may proceed, builds the new access-token principal — both from
    /// a single user load.
    /// </summary>
    public Task<TokenGrantResult> CreateRefreshFlowPrincipalAsync(
        OpenIddictRequest request,
        ClaimsPrincipal principal
    );
}
