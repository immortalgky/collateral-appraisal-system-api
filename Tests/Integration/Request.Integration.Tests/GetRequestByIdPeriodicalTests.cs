using System.Text.Json;
using Integration.Collateral.Integration.Tests;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;

namespace Integration.Request.Integration.Tests;

/// <summary>
/// GET /requests/{id} tells the form whether the request was created by Initiate for a COLLATREV book
/// (purpose and prior appraisal locked) — the fact only, not the book or collateral id.
/// </summary>
public class GetRequestByIdPeriodicalTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task<JsonElement> GetAsync(string? book, string? collateralId)
    {
        Guid id;
        using (var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
            id = await CollateralResultSeed.InsertRequestAsync(
                scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>(),
                purpose: "03", book: book, collateralId: collateralId);

        var response = await _client.GetAsync($"/requests/{id}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .RootElement;
    }

    [Fact]
    public async Task AnInitiateDraft_ReportsIsPeriodicalReappraisalTrue_WithoutTheBookOrCollateralId()
    {
        var json = await GetAsync(book: "69900010", collateralId: "60099010");

        Assert.True(json.GetProperty("isPeriodicalReappraisal").GetBoolean());
        Assert.False(json.TryGetProperty("reappraisalBookNumber", out _));
        Assert.False(json.TryGetProperty("reappraisalCollateralId", out _));
    }

    [Fact]
    public async Task AManualDraft_ReportsIsPeriodicalReappraisalFalse()
    {
        var json = await GetAsync(book: null, collateralId: null);

        Assert.False(json.GetProperty("isPeriodicalReappraisal").GetBoolean());
    }
}
