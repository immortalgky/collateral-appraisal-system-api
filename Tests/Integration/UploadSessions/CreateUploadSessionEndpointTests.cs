using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Integration.Auth.Integration.Tests.Bff;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Shared.Configurations;

namespace Integration.UploadSessions;

/// <summary>
/// POST /api/v1/upload-sessions ignores the request body: callers that still send the retired
/// clientReference/externalCaseKey body, an empty object, or nothing at all all get a session.
/// Uses the real authentication (BFF factory) with a real client_credentials token.
/// </summary>
[Collection("Integration")]
public class CreateUploadSessionEndpointTests(IntegrationTestFixture fixture)
{
    private const string Url = "/api/v1/upload-sessions";
    private const string LifetimeKey = "cas:upload_session_lifetime";

    private async Task<HttpClient> AuthenticatedClientAsync(string? clientId = null, string? secret = null)
    {
        var client = fixture.BffWebApplicationFactory.CreateBrowser(handleCookies: false);
        try
        {
            var token = await BffTestSupport.ClientCredentialsTokenAsync(client, "integration", clientId, secret);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public static TheoryData<string?> Bodies => new()
    {
        """{"clientReference":"x","externalCaseKey":"y"}""",
        "{}",
        null
    };

    [Theory]
    [MemberData(nameof(Bodies))]
    public async Task Any_body_or_none_creates_a_session(string? body)
    {
        using var client = await AuthenticatedClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, Url);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("uploadSessionId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_client_with_its_own_lifetime_gets_a_session_of_that_length_and_others_get_the_default()
    {
        const int ClientHours = 5;
        var clientId = $"ul-{Guid.NewGuid():N}"[..11];
        const string Secret = "UploadLifetime_Secret_2024!";
        await using var scope = fixture.BffWebApplicationFactory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = Secret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            // Kept to 10 characters: it becomes the token's name claim, which the audit interceptor writes
            // into CreatedBy (max length 10).
            DisplayName = "ULT",
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + "integration"
            },
            Settings = { [LifetimeKey] = TimeSpan.FromHours(ClientHours).ToString() }
        };
        await manager.CreateAsync(descriptor, TestContext.Current.CancellationToken);
        try
        {
            var now = DateTime.Now;
            var defaultHours = scope.ServiceProvider.GetRequiredService<IOptions<FileStorageConfiguration>>()
                .Value.Cleanup.TempSessionExpirationHours;
            defaultHours.Should().NotBe(ClientHours);

            var own = await ExpiresAtAsync(await AuthenticatedClientAsync(clientId, Secret));
            var seeded = await ExpiresAtAsync(await AuthenticatedClientAsync());

            own.Should().BeCloseTo(now.AddHours(ClientHours), TimeSpan.FromMinutes(2));
            seeded.Should().BeCloseTo(now.AddHours(defaultHours), TimeSpan.FromMinutes(2));
        }
        finally
        {
            if (await manager.FindByClientIdAsync(clientId, TestContext.Current.CancellationToken) is { } app)
                await manager.DeleteAsync(app, TestContext.Current.CancellationToken);
        }
    }

    private static async Task<DateTime> ExpiresAtAsync(HttpClient client)
    {
        using var _ = client;
        using var response = await client.PostAsync(Url, null, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        return DateTime.ParseExact(
            json.RootElement.GetProperty("expiresAt").GetString()!, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
    }
}
