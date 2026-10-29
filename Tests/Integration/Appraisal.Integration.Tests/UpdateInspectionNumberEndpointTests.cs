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
/// PUT /appraisals/{appraisalId}/inspection-number over HTTP: the appraiser corrects the round
/// number of one Progressive appraisal. Closed appraisals are refused by RejectClosedAppraisalWriteFilter (409).
/// </summary>
[Collection("Integration")]
public class UpdateInspectionNumberEndpointTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private async Task<Guid> SeedAsync(string type, int? inspectionNumber, bool cancelled, CancellationToken ct, bool completed = false)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), type, "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"INS-{Guid.NewGuid():N}"[..18]);
        if (inspectionNumber is not null) appraisal.SetInspectionNumber(inspectionNumber.Value);
        if (completed) appraisal.SyncStatusFromWorkflow(AppraisalStatus.Completed);
        if (cancelled) appraisal.Cancel("EMP999", DateTime.Now, "seeded as closed");

        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(ct);
        return appraisal.Id;
    }

    private async Task<int?> StoredNumberAsync(Guid id, CancellationToken ct)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        return await db.Appraisals.AsNoTracking().Where(a => a.Id == id).Select(a => a.InspectionNumber)
            .FirstAsync(ct);
    }

    private Task<HttpResponseMessage> PutAsync(Guid id, int number, CancellationToken ct)
        => _client.PutAsJsonAsync($"/appraisals/{id}/inspection-number", new { inspectionNumber = number }, ct);

    [Fact]
    public async Task Progressive_appraisal_number_is_updated_and_persisted()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await SeedAsync(AppraisalTypes.Progressive, 1, cancelled: false, ct);

        var response = await PutAsync(id, 3, ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(3, await StoredNumberAsync(id, ct));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_number_is_rejected_with_400_and_nothing_is_written(int number)
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await SeedAsync(AppraisalTypes.Progressive, 2, cancelled: false, ct);

        var response = await PutAsync(id, number, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2, await StoredNumberAsync(id, ct));
    }

    [Fact]
    public async Task Non_progressive_appraisal_is_rejected_with_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await SeedAsync(AppraisalTypes.New, null, cancelled: false, ct);

        var response = await PutAsync(id, 2, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await StoredNumberAsync(id, ct));
    }

    [Fact]
    public async Task Missing_appraisal_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await PutAsync(Guid.NewGuid(), 2, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closed_appraisal_is_refused_by_the_filter_with_409(bool completed)
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await SeedAsync(AppraisalTypes.Progressive, 1, cancelled: !completed, ct, completed);

        var response = await PutAsync(id, 5, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("APPRAISAL_CLOSED", await response.Content.ReadAsStringAsync(ct));
        Assert.Equal(1, await StoredNumberAsync(id, ct));
    }
}
