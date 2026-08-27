using System.Net;
using System.Net.Http.Json;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Behaviour of RejectClosedAppraisalWriteFilter over HTTP.
///
/// The guard exists because the "closed appraisals are read-only" rule used to live only in the
/// frontend — the API happily accepted property writes on a Completed appraisal, with no reason
/// recorded and no audit row. These tests pin down both halves of the contract:
///   * a normal property write on a closed appraisal is refused (409 APPRAISAL_CLOSED)
///   * a write on a live appraisal is untouched — the guard must not become a general blocker
///
/// The correction endpoint deliberately does not carry the filter; that path is covered by
/// PropertyCorrectionAuditTests and the handler unit tests.
/// </summary>
[Collection("Integration")]
public class ClosedAppraisalWriteGuardTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    /// <summary>Seeds a land appraisal, optionally cancelling it so it counts as closed.</summary>
    private async Task<(Guid AppraisalId, Guid PropertyId)> SeedAsync(bool closed, CancellationToken ct)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"GRD-{Guid.NewGuid():N}"[..18]);

        var property = appraisal.AddLandProperty();
        property.LandDetail!.Update(propertyName: "Guard Plot", ownerName: "Owner A");

        if (closed)
            appraisal.Cancel("EMP999", DateTime.Now, "seeded as closed");

        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(ct);

        return (appraisal.Id, property.Id);
    }

    private async Task<HttpResponseMessage> UpdateLandDetailAsync(
        Guid appraisalId, Guid propertyId, string ownerName, CancellationToken ct)
        => await _client.PutAsJsonAsync(
            $"/appraisals/{appraisalId}/properties/{propertyId}/land-detail",
            new { ownerNameLand = ownerName },
            ct);

    [Fact]
    public async Task Property_write_on_a_closed_appraisal_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, propertyId) = await SeedAsync(closed: true, ct);

        var response = await UpdateLandDetailAsync(appraisalId, propertyId, "Owner B", ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("APPRAISAL_CLOSED", body);

        // And nothing was written.
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var stored = await db.Appraisals
            .AsNoTracking()
            .Include(a => a.Properties)
            .FirstAsync(a => a.Id == appraisalId, ct);

        Assert.Equal("Owner A", stored.Properties.Single().LandDetail!.OwnerName);
    }

    [Fact]
    public async Task Property_write_on_a_live_appraisal_still_works()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, propertyId) = await SeedAsync(closed: false, ct);

        var response = await UpdateLandDetailAsync(appraisalId, propertyId, "Owner B", ct);

        // The guard must be invisible to normal work. If this ever returns 409, the filter has
        // started blocking live appraisals and every appraiser is locked out.
        Assert.NotEqual(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Guard_ignores_routes_without_an_appraisal_id()
    {
        var ct = TestContext.Current.CancellationToken;

        // A route with no {appraisalId} must pass straight through rather than being guessed at.
        var response = await _client.GetAsync("/appraisals?pageNumber=0&pageSize=1", ct);

        Assert.NotEqual(HttpStatusCode.Conflict, response.StatusCode);
    }
}
