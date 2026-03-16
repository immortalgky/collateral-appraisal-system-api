using System.Security.Claims;

namespace Auth.Application.Services;

public interface ITokenService
{
    public Task<ClaimsPrincipal> CreateAuthCodeFlowAccessTokenPrincipal(
        OpenIddictRequest request,
        ClaimsPrincipal principal
    );
    public Task<ClaimsPrincipal> CreateClientCredFlowAccessTokenPrincipal(
        OpenIddictRequest request
    );
}
