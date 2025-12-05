using System.Text.Json;

namespace Auth.Auth.Features.Token;

public record TokenCommand(string GrantType, string ClientId, string Code, string CodeVerifier, string RedirectUri)
    : ICommand<TokenResult>;

public record TokenResult(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string Scope,
    string IdToken,
    string RefreshToken);

public class TokenHandler(IHttpClientFactory clientFactory) : ICommandHandler<TokenCommand, TokenResult>
{
    public async Task<TokenResult> Handle(TokenCommand request, CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient("CAS");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", request.GrantType },
            { "client_id", request.ClientId },
            { "code", request.Code },
            { "code_verifier", request.CodeVerifier },
            { "redirect_uri", request.RedirectUri }
        });

        var response = await client.PostAsync("/connect/token", content, cancellationToken);

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
            return JsonSerializer.Deserialize<TokenResult>(responseContent,
                       new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
                   ?? throw new Exception("Failed to deserialize token response.");

        throw new Exception($"Token request failed: {response.StatusCode}, {responseContent}");
    }
}