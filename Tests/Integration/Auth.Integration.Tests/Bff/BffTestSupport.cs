using System.Net;
using System.Text.Json;
using FluentAssertions;
using Auth.Domain.Identity;
using Auth.Domain.Sessions;
using Auth.Infrastructure;
using Auth.Infrastructure.Session;
using Integration.WebApplicationFactories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Auth.Integration.Tests.Bff;

public sealed record TestUser(Guid Id, string UserName, string Password, Guid? CompanyId);

/// <summary>Creates users and sessions, and reads them back, for the browser-session tests.</summary>
public static class BffTestSupport
{
    public const string Spa = "https://localhost:3000";
    public const string Password = "Str0ng!Passw0rd#42";

    /// <param name="role">A role the seeder creates: Admin, RequestMaker, ExtAppraisalStaff, ...</param>
    public static async Task<TestUser> CreateUserAsync(IServiceProvider services, string role,
        Guid? companyId = null, bool withUserLevelPermissions = false, Action<ApplicationUser>? configure = null)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = $"bff{suffix}",
            Email = $"bff{suffix}@test.local",
            EmailConfirmed = true,
            FirstName = "Bff",
            LastName = "Tester",
            IsActive = true,
            AuthSource = AuthSources.Local,
            CompanyId = companyId
        };
        configure?.Invoke(user);

        var created = await users.CreateAsync(user, Password);
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
        var added = await users.AddToRoleAsync(user, role);
        if (!added.Succeeded)
            throw new InvalidOperationException($"Role '{role}': " + string.Join("; ", added.Errors.Select(e => e.Description)));

        if (withUserLevelPermissions)
        {
            // One grant the roles do not give, one deny of something the role does give.
            var codes = await db.Permissions.AsNoTracking().OrderBy(p => p.PermissionCode).Take(2).ToListAsync();
            db.UserPermissions.Add(new UserPermission { UserId = user.Id, PermissionId = codes[0].Id, IsGranted = true });
            db.UserPermissions.Add(new UserPermission { UserId = user.Id, PermissionId = codes[1].Id, IsGranted = false });
            await db.SaveChangesAsync();
        }

        return new TestUser(user.Id, user.UserName!, Password, companyId);
    }

    /// <summary>
    /// Signs the user in through the real cookie handler (so the session row is created by the real ticket
    /// store) and returns the <c>Cookie</c> header value a browser would replay.
    /// </summary>
    public static async Task<string> SignInAsync(HttpClient browser, TestUser user)
    {
        using var response = await browser.GetAsync($"/test/signin?userId={user.Id}&name={user.UserName}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-cas-session"));
        return setCookie[..setCookie.IndexOf(';')];
    }

    /// <summary>Sends a request carrying the given session cookie.</summary>
    public static Task<HttpResponseMessage> SendAsync(HttpClient browser, HttpRequestMessage request, string? cookie)
    {
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        return browser.SendAsync(request);
    }

    public static Task<HttpResponseMessage> GetAsync(HttpClient browser, string path, string? cookie,
        bool background = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (background) request.Headers.Add(AuthSchemes.BackgroundHeader, "1");
        return SendAsync(browser, request, cookie);
    }

    /// <summary>A request that looks like the SPA's: an allowed Origin.</summary>
    public static HttpRequestMessage SpaRequest(HttpMethod method, string path, string origin = Spa,
        bool background = false)
    {
        var request = new HttpRequestMessage(method, path);
        if (origin.Length > 0) request.Headers.Add("Origin", origin);
        if (background) request.Headers.Add(AuthSchemes.BackgroundHeader, "1");
        return request;
    }

    /// <summary>A real client_credentials access token; defaults to the seeded machine client <c>cls</c>.</summary>
    public static async Task<string> ClientCredentialsTokenAsync(HttpClient client, string scope = "appraisal.read",
        string? clientId = null, string? clientSecret = null)
    {
        if (clientId is null != clientSecret is null)
            throw new ArgumentException("Pass a client id and its secret together, or neither for the default cls client.");

        using var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId ?? "cls",
                ["client_secret"] = clientSecret ?? "CLS_SecretKey_2024!",
                ["scope"] = scope
            }), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    /// <summary>The (type, value) pairs of the claims that authorise a request.</summary>
    public static HashSet<(string Type, string Value)> AuthorizationClaims(JsonElement whoAmI) =>
        whoAmI.GetProperty("claims").EnumerateArray()
            .Select(c => (Type: c.GetProperty("type").GetString()!, Value: c.GetProperty("value").GetString()!))
            .Where(c => c.Type is "permissions" or "roles" or "company_id")
            .ToHashSet();

    public static async Task<UserSession> GetSessionAsync(IServiceProvider services, Guid userId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        return await db.UserSessions.AsNoTracking().Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt).FirstAsync();
    }

    public static async Task<List<UserSession>> GetSessionsAsync(IServiceProvider services, Guid userId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        return await db.UserSessions.AsNoTracking().Where(s => s.UserId == userId).ToListAsync();
    }

    public static async Task UpdateSessionAsync(IServiceProvider services, Guid sessionId,
        Action<UserSession> change)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var session = await db.UserSessions.FirstAsync(s => s.Id == sessionId);
        change(session);
        await db.SaveChangesAsync();
    }

    public static async Task UpdateUserAsync(IServiceProvider services, Guid userId, Action<ApplicationUser> change)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        change(user);
        await db.SaveChangesAsync();
    }
}
