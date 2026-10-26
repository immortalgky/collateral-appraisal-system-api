using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Integration.WebApplicationFactories;

namespace Integration.Auth.Integration.Tests.Bff;

public static class PasskeyTestSupport
{
    /// <summary>The SPA, where a passkey is registered from, and the IdP, where one signs in.</summary>
    public const string SpaOrigin = BffTestSupport.Spa;
    public const string IdpOrigin = "https://localhost:7111";

    /// <summary>Registers a passkey for the user through the real endpoints, as the profile page would.</summary>
    public static async Task<(SoftAuthenticator Authenticator, JsonElement Passkey)> RegisterAsync(
        BffWebApplicationFactory factory, TestUser user, string name = "Test key", Action<SoftAuthenticator>? configure = null)
    {
        using var browser = factory.CreateBrowser();
        var cookie = await BffTestSupport.SignInAsync(browser, user);
        var authenticator = new SoftAuthenticator();
        configure?.Invoke(authenticator);
        var passkey = await RegisterAsync(browser, cookie, authenticator, name);
        return (authenticator, passkey);
    }

    /// <summary>Runs both registration calls for an authenticator. Returns the passkey the server stored.</summary>
    public static async Task<JsonElement> RegisterAsync(HttpClient browser, string cookie, SoftAuthenticator authenticator, string name)
    {
        using var begin = await PostAsync(browser, cookie, "/auth/me/passkeys/register/options");
        begin.StatusCode.Should().Be(HttpStatusCode.OK, await begin.Content.ReadAsStringAsync());
        var started = (await BffTestSupport.ReadJsonAsync(begin)).RootElement.Clone();

        var credential = authenticator.Create(started.GetProperty("options").GetRawText(), SpaOrigin);
        using var finish = await PostAsync(browser, cookie, "/auth/me/passkeys/register", new
        {
            state = started.GetProperty("state").GetString(),
            name,
            credential = JsonDocument.Parse(credential).RootElement
        });
        finish.StatusCode.Should().Be(HttpStatusCode.OK, await finish.Content.ReadAsStringAsync());
        return (await BffTestSupport.ReadJsonAsync(finish)).RootElement.Clone();
    }

    public static Task<HttpResponseMessage> PostAsync(HttpClient browser, string cookie, string path, object? body = null)
    {
        var request = BffTestSupport.SpaRequest(HttpMethod.Post, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        return BffTestSupport.SendAsync(browser, request, cookie);
    }

}
