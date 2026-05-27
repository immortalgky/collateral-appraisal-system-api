using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Appraisal.Application.Features.Appraisals.GetAppraisalMapPins;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.MarketComparables;
using Appraisal.Infrastructure;
using FluentAssertions;
using Integration.Fixtures;
using Integration.WebApplicationFactories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// Integration tests for GET /appraisals/{appraisalId}/map-pins.
///
/// Security invariant (from fix):
///   External (company) callers see collateral pins ONLY for appraisals assigned to
///   their company, and MC pins ONLY for MCs they own (CreatedByCompanyId).
///   Internal (bank) callers see all data.
/// </summary>
[Collection("Integration")]
public class GetAppraisalMapPinsEndpointTests(IntegrationTestFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private AppraisalDbContext GetAppraisalDbContext(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

    /// <summary>
    /// Creates an HttpClient that authenticates as an external company user with the
    /// given companyId injected as a "company_id" claim. Uses a transient derived factory
    /// that overrides the auth scheme — the underlying DB connection is inherited from the
    /// parent fixture's connection string, so the same test DB is used.
    /// </summary>
    private HttpClient CreateExternalCompanyClient(Guid companyId)
    {
        var factory = new ExternalCompanyWebApplicationFactory(
            fixture.ConnectionString,
            fixture.RabbitMq.GetConnectionString(),
            companyId);
        return factory.CreateClient();
    }

    // ── 401 — no auth ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMapPins_NoAuth_Returns401()
    {
        var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

        var response = await client.GetAsync($"/appraisals/{Guid.NewGuid()}/map-pins");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Internal happy path: land property + linked MC ───────────────────────

    [Fact]
    public async Task GetMapPins_InternalUser_WithLandPropertyAndLinkedMC_ReturnsBothPinLists()
    {
        Guid appraisalId;
        Guid propertyId;

        using (var seedScope = CreateScope())
        {
            var db = GetAppraisalDbContext(seedScope);

            var appraisal = AppraisalAggregate.Create(
                requestId: Guid.CreateVersion7(),
                appraisalType: "New",
                priority: "Normal",
                now: DateTime.Now);

            appraisal.SetAppraisalNumber($"MAPTEST-{Guid.NewGuid():N}"[..16]);

            var prop = appraisal.AddLandProperty();
            prop.LandDetail!.Update(
                coordinates: GpsCoordinate.Create(13.7563m, 100.5018m),
                address: AdministrativeAddress.Create(
                    subDistrict: "TEST-SD",
                    district: "TEST-D",
                    province: "TEST-P",
                    landOffice: null));

            db.Set<AppraisalAggregate>().Add(appraisal);
            await db.SaveChangesAsync();

            appraisalId = appraisal.Id;
            propertyId = prop.Id;
        }

        Guid mcId;

        using (var mcScope = CreateScope())
        {
            var db = GetAppraisalDbContext(mcScope);

            var mc = MarketComparable.Create(
                propertyType: "Land",
                surveyName: $"MapPinsMC-{Guid.NewGuid():N}",
                infoDateTime: DateTime.UtcNow.AddMonths(-6),
                sourceInfo: null,
                latitude: 13.7600m,
                longitude: 100.5050m,
                createdByCompanyId: null);

            db.Set<MarketComparable>().Add(mc);
            await db.SaveChangesAsync();

            mcId = mc.Id;
        }

        using (var linkScope = CreateScope())
        {
            var db = GetAppraisalDbContext(linkScope);

            var comparable = AppraisalComparable.Create(
                appraisalId: appraisalId,
                marketComparableId: mcId,
                sequenceNumber: 1,
                originalPricePerUnit: 10_000m);

            db.Set<AppraisalComparable>().Add(comparable);
            await db.SaveChangesAsync();
        }

        // Internal client (no company_id claim) — should see all data.
        using var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

        var response = await client.GetAsync($"/appraisals/{appraisalId}/map-pins");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GetAppraisalMapPinsResult>(json, JsonOpts);

        result.Should().NotBeNull();

        result!.Collateral.Should().ContainSingle(p =>
            p.AppraisalPropertyId == propertyId &&
            p.Lat == 13.7563m &&
            p.Lon == 100.5018m);

        result.MarketComparables.Should().ContainSingle(mc =>
            mc.MarketComparableId == mcId &&
            mc.Lat == 13.7600m &&
            mc.Lon == 100.5050m);
    }

    // ── Empty result for appraisal with no coords ─────────────────────────────

    [Fact]
    public async Task GetMapPins_AppraisalWithNoCoords_ReturnsBothListsEmpty()
    {
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var db = GetAppraisalDbContext(seedScope);

            var appraisal = AppraisalAggregate.Create(
                requestId: Guid.CreateVersion7(),
                appraisalType: "New",
                priority: "Normal",
                now: DateTime.Now);

            appraisal.SetAppraisalNumber($"NOPINS-{Guid.NewGuid():N}"[..14]);

            appraisal.AddLandProperty(); // no coordinates set

            db.Set<AppraisalAggregate>().Add(appraisal);
            await db.SaveChangesAsync();

            appraisalId = appraisal.Id;
        }

        using var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

        var response = await client.GetAsync($"/appraisals/{appraisalId}/map-pins");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GetAppraisalMapPinsResult>(json, JsonOpts);

        result.Should().NotBeNull();
        result!.Collateral.Should().BeEmpty();
        result.MarketComparables.Should().BeEmpty();
    }

    // ── Security: external company cannot see another company's appraisal ─────

    [Fact]
    public async Task GetMapPins_ExternalUser_AnotherCompanysAppraisal_ReturnsEmptyLists()
    {
        // Appraisal seeded with no AppraisalAssignment → vw_AppraisalList.AssigneeCompanyId
        // is NULL, so no external company's access check will match.
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var db = GetAppraisalDbContext(seedScope);

            var appraisal = AppraisalAggregate.Create(
                requestId: Guid.CreateVersion7(),
                appraisalType: "New",
                priority: "Normal",
                now: DateTime.Now);

            appraisal.SetAppraisalNumber($"SECTEST-{Guid.NewGuid():N}"[..15]);

            var prop = appraisal.AddLandProperty();
            prop.LandDetail!.Update(
                coordinates: GpsCoordinate.Create(13.7563m, 100.5018m));

            db.Set<AppraisalAggregate>().Add(appraisal);
            await db.SaveChangesAsync();

            appraisalId = appraisal.Id;
        }

        // Seed an MC owned by a DIFFERENT company and link it to the appraisal.
        var otherCompanyId = Guid.NewGuid();
        Guid foreignMcId;

        using (var mcScope = CreateScope())
        {
            var db = GetAppraisalDbContext(mcScope);

            var mc = MarketComparable.Create(
                propertyType: "Land",
                surveyName: $"OtherCoMC-{Guid.NewGuid():N}",
                infoDateTime: DateTime.UtcNow.AddMonths(-3),
                sourceInfo: null,
                latitude: 13.7700m,
                longitude: 100.5100m,
                createdByCompanyId: otherCompanyId);

            db.Set<MarketComparable>().Add(mc);
            await db.SaveChangesAsync();

            foreignMcId = mc.Id;
        }

        using (var linkScope = CreateScope())
        {
            var db = GetAppraisalDbContext(linkScope);

            var comparable = AppraisalComparable.Create(
                appraisalId: appraisalId,
                marketComparableId: foreignMcId,
                sequenceNumber: 1,
                originalPricePerUnit: 5_000m);

            db.Set<AppraisalComparable>().Add(comparable);
            await db.SaveChangesAsync();
        }

        // External caller with a DIFFERENT company id.
        var callerCompanyId = Guid.NewGuid(); // not the same as otherCompanyId, not assigned
        using var externalClient = CreateExternalCompanyClient(callerCompanyId);

        var response = await externalClient.GetAsync($"/appraisals/{appraisalId}/map-pins");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GetAppraisalMapPinsResult>(json, JsonOpts);

        result.Should().NotBeNull();

        // Collateral is empty because the appraisal is not assigned to callerCompanyId.
        result!.Collateral.Should().BeEmpty(
            "external user's company is not assigned to this appraisal");

        // MC list is empty because the MC belongs to otherCompanyId, not callerCompanyId.
        result.MarketComparables.Should().BeEmpty(
            "external user cannot see market comparables owned by another company");
    }
}

// ── Helper: external-company WebApplicationFactory ───────────────────────────

/// <summary>
/// Derived factory that authenticates as an external company user by injecting a
/// "company_id" claim. Reuses the shared DB/RabbitMQ connection strings from the
/// test fixture so the same container database is targeted.
/// </summary>
file sealed class ExternalCompanyWebApplicationFactory(
    string mssqlConnectionString,
    string rabbitMqConnectionString,
    Guid companyId
) : IntegrationTestWebApplicationFactory(mssqlConnectionString, rabbitMqConnectionString)
{
    protected override void ConfigureAuthServices(IServiceCollection services)
    {
        services
            .AddAuthentication("Test")
            .AddScheme<ExternalCompanyAuthOptions, ExternalCompanyAuthHandler>(
                "Test",
                options => { options.CompanyId = companyId; });
        services.AddAuthorization();
        services.Configure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = "Test";
            options.DefaultChallengeScheme = "Test";
        });
    }
}

file sealed class ExternalCompanyAuthOptions : AuthenticationSchemeOptions
{
    public Guid CompanyId { get; set; }
}

file sealed class ExternalCompanyAuthHandler(
    IOptionsMonitor<ExternalCompanyAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<ExternalCompanyAuthOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "external-test-user"),
            new Claim("permissions", "request:read"),
            new Claim("company_id", Options.CompanyId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
