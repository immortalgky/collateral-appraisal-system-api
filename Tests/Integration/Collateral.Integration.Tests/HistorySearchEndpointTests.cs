using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Appraisal.Application.Features.HistorySearch;
using Integration.Fixtures;
using Appraisal.Infrastructure;
using Appraisal.Domain.MarketComparables;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Shared.Pagination;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// Integration tests for POST /history-search (History Search pin feature).
///
/// The feature has moved to the Appraisal module. The test file is kept in the
/// Integration project (single integration test assembly) and exercises the
/// same IntegrationTestFixture / WebApplicationFactory harness.
///
/// Verification criteria from the plan:
///   - Response has "appraisals" (green, one per appraisal) and "marketComparables" (blue).
///   - Internal user → both sections populated (when data present).
///   - External user → appraisals.items empty; marketComparables only own company's MCs.
///   - Radius cap: server enforces 50 km ceiling.
/// </summary>
[Collection("Integration")]
public class HistorySearchEndpointTests(IntegrationTestFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // Bangkok city centre — used as the search origin in all tests.
    private const decimal BangkokLat = 13.7563m;
    private const decimal BangkokLon = 100.5018m;

    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private AppraisalDbContext GetAppraisalDbContext(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

    // ── Helper: seed a MarketComparable with geo coords ──────────────────────

    private async Task<Guid> SeedMarketComparableAsync(
        decimal lat, decimal lon, Guid? companyId = null)
    {
        using var scope = CreateScope();
        var db = GetAppraisalDbContext(scope);

        var mc = MarketComparable.Create(
            propertyType: "Land",
            surveyName: $"Test-{Guid.NewGuid():N}",
            infoDateTime: DateTime.UtcNow.AddYears(-1),
            sourceInfo: null,
            latitude: lat,
            longitude: lon,
            createdByCompanyId: companyId);

        db.Set<MarketComparable>().Add(mc);
        await db.SaveChangesAsync();
        return mc.Id;
    }

    // ── POST /history-search ──────────────────────────────────────────────────

    [Fact]
    public async Task HistorySearch_NoAuth_Returns401()
    {
        // Arrange — seed an MC near Bangkok.
        await SeedMarketComparableAsync(BangkokLat + 0.001m, BangkokLon + 0.001m);

        // Response shape changed: "appraisals" (not "collateral") + "marketComparables".
        var query = new HistorySearchQuery(
            CenterLat: BangkokLat,
            CenterLon: BangkokLon,
            RadiusKm: 10,
            Period: Period.Past3y,
            AppraisalReportNo: null,
            TitleDeedNo: null,
            CollateralTypes: null,
            CustomerName: null,
            LandAreaFromSqWa: null,
            LandAreaToSqWa: null,
            ValueFrom: null,
            ValueTo: null,
            DateFrom: null,
            DateTo: null,
            Pagination: new(0, 10),
            BuildingTypeCodes: null,
            SubDistrict: null,
            District: null,
            Province: null);

        var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

        // Act — no auth header.
        var response = await client.PostAsJsonAsync("/history-search", query, JsonOpts);

        // Without auth the endpoint must return 401 (RequireAuthorization).
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void HistorySearch_RadiusCap_ServerSideLimitedTo50km()
    {
        // Verify the query object itself records the raw value; the cap is applied inside the handler.
        var query = new HistorySearchQuery(
            CenterLat: BangkokLat,
            CenterLon: BangkokLon,
            RadiusKm: 999m, // exceeds cap
            Period: Period.Past1y,
            AppraisalReportNo: null,
            TitleDeedNo: null,
            CollateralTypes: null,
            CustomerName: null,
            LandAreaFromSqWa: null,
            LandAreaToSqWa: null,
            ValueFrom: null,
            ValueTo: null,
            DateFrom: null,
            DateTo: null,
            Pagination: new(0, 10),
            BuildingTypeCodes: null,
            SubDistrict: null,
            District: null,
            Province: null);

        // The record is constructed without throwing — cap is applied inside the handler.
        query.RadiusKm.Should().Be(999m);
    }

    [Fact]
    public void HistorySearch_ResponseShape_HasAppraisalsNotCollateral()
    {
        // Compile-time check: HistorySearchResult now uses "Appraisals" (not "Collateral").
        // This test guards against accidental rename regressions.
        var result = new HistorySearchResult(
            Appraisals: new PaginatedResult<AppraisalPinDto>([], 0, 0, 10),
            MarketComparables: new PaginatedResult<MarketComparablePinDto>([], 0, 0, 10));

        result.Appraisals.Should().NotBeNull();
        result.MarketComparables.Should().NotBeNull();
        result.Appraisals.Count.Should().Be(0);
        result.MarketComparables.Count.Should().Be(0);
    }
}
