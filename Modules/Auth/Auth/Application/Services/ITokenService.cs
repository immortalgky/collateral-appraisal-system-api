using System.Security.Claims;

namespace Auth.Application.Services;

/// <summary>
/// Result of a refresh-token exchange: either a built access-token <see cref="Principal"/> when the
/// refresh may proceed, or a non-null <see cref="Rejection"/> error description to deny the grant.
/// </summary>
public record RefreshTokenResult(ClaimsPrincipal? Principal, string? Rejection);

public interface ITokenService
{
    public Task<ClaimsPrincipal> CreateAuthCodeFlowAccessTokenPrincipal(
        OpenIddictRequest request,
        ClaimsPrincipal principal
    );
    public Task<ClaimsPrincipal> CreateClientCredFlowAccessTokenPrincipal(
        OpenIddictRequest request
    );

    /// <summary>
    /// Re-validates the account (deactivated / must-change-password / expired password) and, when the
    /// refresh may proceed, builds the new access-token principal — both from a single user load.
    /// </summary>
    public Task<RefreshTokenResult> CreateRefreshFlowPrincipalAsync(
        OpenIddictRequest request,
        ClaimsPrincipal principal
    );
}
