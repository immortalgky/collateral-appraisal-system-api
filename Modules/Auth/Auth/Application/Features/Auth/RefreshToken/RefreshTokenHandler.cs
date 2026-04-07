using System.Text.Json;

namespace Auth.Domain.Auth.Features.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : ICommand<RefreshTokenResult>;

public record RefreshTokenResult(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string Scope,
    string IdToken,
    string RefreshToken);

public class RefreshTokenHandler(IHttpClientFactory clientFactory) : ICommandHandler<RefreshTokenCommand, RefreshTokenResult>
{
    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient("CAS");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", "spa" },
            { "refresh_token", request.RefreshToken }
        });

        var response = await client.PostAsync("/connect/token", content, cancellationToken);

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
            return JsonSerializer.Deserialize<RefreshTokenResult>(responseContent,
                       new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
                   ?? throw new Exception("Failed to deserialize refresh token response.");

        throw new Exception($"Refresh token request failed with status {response.StatusCode}");
    }
}
