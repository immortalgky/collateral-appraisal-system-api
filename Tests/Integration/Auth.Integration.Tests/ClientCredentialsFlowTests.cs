using System.Text.Json;
using Integration.Fixtures;

namespace Integration.Auth.Integration.Tests;

public class ClientCredentialsFlowTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    // Use the seeded confidential machine client (client_credentials grant). Client registration is
    // now an authenticated admin operation (POST /auth/clients requires OAuthClientsManage), so the
    // flow is exercised against the pre-seeded "cls" client instead of registering one anonymously.
    private const string ClientId = "cls";
    private const string ClientSecret = "CLS_SecretKey_2024!";
    private const string Scope = "appraisal.read";

    [Fact]
    public async Task GetToken_ValidRequest_ReceiveAccessToken()
    {
        var jwtRequest = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret,
                    ["scope"] = Scope,
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
        var jwtRequest = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret + "1",
                    ["scope"] = Scope,
                }
            ),
        };

        var jwtResponse = await _authClient.SendAsync(
            jwtRequest,
            TestContext.Current.CancellationToken
        );
        Assert.False(jwtResponse.IsSuccessStatusCode);
    }
}
