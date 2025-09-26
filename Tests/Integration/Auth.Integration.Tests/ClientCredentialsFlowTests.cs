using System.Text;
using System.Text.Json;
using Auth.Auth.Features.RegisterClient;
using Integration.Fixtures;
using Integration.Helpers;

namespace Integration.Auth.Integration.Tests;

public class ClientCredentialsFlowTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task GetToken_ValidRequest_ReceiveAccessToken()
    {
        // Register new client
        var registerClientResponseInstance = await RegisterNewClient(_authClient);

        // Request access token
        var jwtRequest = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = registerClientResponseInstance.ClientId!,
                    ["client_secret"] = registerClientResponseInstance.ClientSecret!,
                    ["scope"] = "offline_access",
                }
            ),
        };

        var jwtResponse = await _authClient.SendAsync(
            jwtRequest,
            TestContext.Current.CancellationToken
        );
        jwtResponse.EnsureSuccessStatusCode();

        var jwtJson = await jwtResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken
        );
        var jwtPayload = JsonDocument.Parse(jwtJson);

        var tokenType = jwtPayload.RootElement.GetProperty("token_type").GetString();
        Assert.Equal("Bearer", tokenType);
        var token = jwtPayload.RootElement.GetProperty("access_token").GetString();
        Assert.NotNull(token);
    }

    [Fact]
    public async Task GetToken_InvalidRequest_DoesNotReceiveAccessToken()
    {
        // Register new client
        var registerClientResponseInstance = await RegisterNewClient(_authClient);

        // Request access token
        var jwtRequest = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = registerClientResponseInstance.ClientId!,
                    ["client_secret"] = registerClientResponseInstance.ClientSecret! + "1",
                    ["scope"] = "offline_access",
                }
            ),
        };

        var jwtResponse = await _authClient.SendAsync(
            jwtRequest,
            TestContext.Current.CancellationToken
        );
        Assert.False(jwtResponse.IsSuccessStatusCode);
    }

    private static async Task<RegisterClientResponse> RegisterNewClient(HttpClient authClient)
    {
        var registerClientJson = await JsonHelper.JsonFileToJson(
            "Auth.Integration.Tests",
            "GetToken_RegisterClient.json"
        );
        var registerClientContent = new StringContent(
            registerClientJson,
            Encoding.UTF8,
            "application/json"
        );
        var registerClientResponse = await authClient.PostAsync(
            "/auth/clients",
            registerClientContent,
            TestContext.Current.CancellationToken
        );

        var statusCodeException = Record.Exception(registerClientResponse.EnsureSuccessStatusCode);
        Assert.Null(statusCodeException);

        var registerClientResponseBody = await registerClientResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken
        );
        var registerClientResponseInstance = JsonSerializer.Deserialize<RegisterClientResponse>(
            registerClientResponseBody,
            JsonHelper.Options
        );
        Assert.NotNull(registerClientResponseInstance);
        Assert.NotNull(registerClientResponseInstance.ClientId);
        Assert.NotNull(registerClientResponseInstance.ClientSecret);

        return registerClientResponseInstance;
    }
}
