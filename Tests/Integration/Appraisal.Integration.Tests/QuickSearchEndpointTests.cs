using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using Integration.WebApplicationFactories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Data;
using Dapper;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// End-to-end cover for <c>GET /search</c>.
///
/// The endpoint this replaced was <c>AllowAnonymous</c> and served customer names, phone numbers and
/// title deeds to anyone who could reach the host, so the auth cases here are the point of the file
/// rather than an afterthought.
///
/// The happy path also pins the thing that made the old search useless: a result has to carry a
/// route that exists. Property hits used to build <c>/requests/{id}/titles/{id}</c>, which no router
/// entry matches, so every one of them landed on NotFound.
/// </summary>
[Collection("Integration")]
public class QuickSearchEndpointTests(IntegrationTestFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private sealed record Match(string Field, string Value);

    private sealed record Appraisal(
        Guid AppraisalId,
        string? AppraisalNumber,
        Guid RequestId,
        string? RequestNumber,
        string? CustomerName,
        string? Status,
        string NavigateTo,
        List<Match> MatchedOn);

    private sealed record Group(
        string MatchKind,
        string MatchLabel,
        string MatchField,
        int AppraisalCount,
        List<Appraisal> Appraisals);

    private sealed record Result(List<Group> Groups, bool HasMore, int TotalMatchedAppraisals);

    /// <summary>
    /// Seeds an appraisal plus the request rows the search arms read.
    ///
    /// The request side is inserted with Dapper rather than through the Request aggregate: the
    /// arms only need columns, this test asserts nothing about request invariants, and going
    /// through the aggregate would couple it to whatever the Request module currently requires.
    /// </summary>
    private async Task<(Guid AppraisalId, string AppraisalNumber, string Token)> SeedAsync(string label)
    {
        // Unique per run: the integration database is shared by the whole collection and never
        // cleaned between tests, so a fixed term would match rows left by earlier runs.
        var token = $"QS{Guid.NewGuid():N}"[..12];
        var requestId = Guid.CreateVersion7();
        var appraisalNumber = $"{label}{token}"[..16];

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(
                requestId: requestId,
                appraisalType: "New",
                priority: "Normal",
                now: DateTime.Now);
            appraisal.SetAppraisalNumber(appraisalNumber);
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync();

            var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
            var connection = connectionFactory.GetOpenConnection();
            await connection.ExecuteAsync(
                """
                INSERT INTO request.Requests
                    (Id, RequestNumber, Status, Requestor, RequestorName, Creator, CreatorName,
                     IsPma, IsDeleted, CreatedAt)
                VALUES
                    (@Id, @RequestNumber, 'Draft', 'P0000', @RequestorName, 'P0000', @RequestorName,
                     0, 0, SYSDATETIME());

                INSERT INTO request.RequestCustomers (RequestId, Name, ContactNumber)
                VALUES (@Id, @CustomerName, '0800000000');
                """,
                new
                {
                    Id = requestId,
                    RequestNumber = $"REQ-{token}",
                    RequestorName = $"Requestor {token}",
                    CustomerName = $"Customer {token}"
                });

            return (appraisal.Id, appraisalNumber, token);
        }
    }

    private async Task<Result> SearchAsync(HttpClient client, string q, string? scope = null)
    {
        var url = $"/search?q={Uri.EscapeDataString(q)}" + (scope is null ? "" : $"&scope={scope}");
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"GET {url} -> {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<Result>(body, Json)!;
    }

    // ── authentication ───────────────────────────────────────────────────────

    [Fact]
    public async Task Rejects_an_unauthenticated_caller()
    {
        // The predecessor answered this request with customer names and phone numbers.
        //
        // Needs its own host: the shared fixture's bypass handler authenticates every request
        // regardless of headers, so there is no way to be anonymous against it.
        await using var factory = new AnonymousWebApplicationFactory(
            fixture.ConnectionString, fixture.RabbitMq.GetConnectionString());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/search?q=anything");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── validation ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("69")]
    public async Task Rejects_a_term_too_short_to_be_selective(string term)
    {
        // "69" is a prefix of every appraisal number in the system: accepting it would return the
        // whole table dressed up as a search result.
        using var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

        var response = await client.GetAsync($"/search?q={term}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_an_unknown_scope()
    {
        using var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

        var response = await client.GetAsync("/search?q=abcdef&scope=everything");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── results ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Finds_an_appraisal_by_its_number_and_returns_a_route_that_exists()
    {
        var (appraisalId, appraisalNumber, _) = await SeedAsync("QSNUM");
        using var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

        var result = await SearchAsync(client, appraisalNumber);

        var hit = result.Groups.SelectMany(g => g.Appraisals).Single(a => a.AppraisalId == appraisalId);
        // Built from the appraisal id, which is the whole point: /requests/{id}/titles/{id} was not
        // a route, so the old property results always 404'd.
        Assert.Equal($"/appraisals/{appraisalId}", hit.NavigateTo);
        Assert.Equal(appraisalNumber, hit.AppraisalNumber);
    }

    [Fact]
    public async Task Finds_an_appraisal_by_its_request_number()
    {
        // The old implementation never looked at RequestNumber at all.
        var (appraisalId, _, token) = await SeedAsync("QSREQ");
        using var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

        var result = await SearchAsync(client, $"REQ-{token}");

        Assert.Contains(result.Groups.SelectMany(g => g.Appraisals), a => a.AppraisalId == appraisalId);
    }

    [Fact]
    public async Task Finds_an_appraisal_by_customer_name_and_says_why_it_matched()
    {
        var (appraisalId, _, token) = await SeedAsync("QSCUS");
        using var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

        var result = await SearchAsync(client, $"Customer {token}");

        var group = result.Groups.Single(g => g.Appraisals.Any(a => a.AppraisalId == appraisalId));
        Assert.Equal("customer", group.MatchKind);
        Assert.Equal("customerName", group.MatchField);
        var hit = group.Appraisals.Single(a => a.AppraisalId == appraisalId);
        Assert.Contains(hit.MatchedOn, m => m.Field == "customerName" && m.Value == $"Customer {token}");
    }

    [Fact]
    public async Task Scope_restricts_which_columns_are_searched()
    {
        var (appraisalId, _, token) = await SeedAsync("QSSCO");
        using var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

        // The term only ever appears in a customer name, so the documents scope must not find it.
        var customers = await SearchAsync(client, $"Customer {token}", "customers");
        var documents = await SearchAsync(client, $"Customer {token}", "documents");

        Assert.Contains(customers.Groups.SelectMany(g => g.Appraisals), a => a.AppraisalId == appraisalId);
        Assert.DoesNotContain(documents.Groups.SelectMany(g => g.Appraisals), a => a.AppraisalId == appraisalId);
    }

    [Fact]
    public async Task Matches_by_prefix_rather_than_substring_by_default()
    {
        var (appraisalId, _, token) = await SeedAsync("QSPFX");
        using var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

        // "Customer <token>" — searching the token alone is a mid-string match, which the default
        // prefix pattern must not find. This is what keeps the filtered indexes seekable.
        var prefix = await SearchAsync(client, token);
        var substring = await SearchAsync(client, $"*{token}");

        Assert.DoesNotContain(prefix.Groups.SelectMany(g => g.Appraisals), a => a.AppraisalId == appraisalId);
        Assert.Contains(substring.Groups.SelectMany(g => g.Appraisals), a => a.AppraisalId == appraisalId);
    }

    [Fact]
    public async Task A_typed_wildcard_is_matched_literally_and_does_not_return_everything()
    {
        await SeedAsync("QSESC");
        using var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

        // Unescaped, '%%%' is "match anything" against every searched column on every table.
        var result = await SearchAsync(client, "%%%");

        Assert.Empty(result.Groups);
        Assert.Equal(0, result.TotalMatchedAppraisals);
    }
}


/// <summary>
/// A host whose authentication scheme never authenticates anyone, so endpoints can be tested for
/// requiring authorization at all. Reuses the fixture's container connection strings.
/// </summary>
file sealed class AnonymousWebApplicationFactory(
    string mssqlConnectionString,
    string rabbitMqConnectionString
) : IntegrationTestWebApplicationFactory(mssqlConnectionString, rabbitMqConnectionString)
{
    protected override void ConfigureAuthServices(IServiceCollection services)
    {
        services
            .AddAuthentication("Anonymous")
            .AddScheme<AuthenticationSchemeOptions, AnonymousAuthHandler>("Anonymous", _ => { });
        services.AddAuthorization();
        services.Configure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = "Anonymous";
            options.DefaultChallengeScheme = "Anonymous";
        });
    }
}

file sealed class AnonymousAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.NoResult());
}
