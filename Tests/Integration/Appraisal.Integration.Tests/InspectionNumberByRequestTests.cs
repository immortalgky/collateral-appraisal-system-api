using Appraisal.Contracts.Appraisals;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// <see cref="GetInspectionNumberByRequestQuery"/> over a real database: the newest non-cancelled, non-deleted
/// appraisal of the request wins; null when there is none. Every test seeds its own request id.
/// </summary>
[Collection("Integration")]
public class InspectionNumberByRequestTests(IntegrationTestFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceScope CreateScope() => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static async Task<AppraisalAggregate> SeedAsync(
        AppraisalDbContext db, Guid requestId, int? inspectionNumber,
        AppraisalStatus? status = null, bool deleted = false)
    {
        var a = AppraisalAggregate.Create(
            requestId, AppraisalTypes.Progressive, "Normal", DateTime.Now, purpose: "06");
        a.SetAppraisalNumber($"AP-{Guid.NewGuid():N}"[..14]);
        if (inspectionNumber is { } n) a.SetInspectionNumber(n);
        if (status is not null)
            typeof(AppraisalAggregate).GetProperty("Status")!.SetValue(a, status);
        if (deleted) a.Delete(Guid.NewGuid());

        db.Appraisals.Add(a);
        await db.SaveChangesAsync(Ct);
        return a;
    }

    private static Task<int?> InspectionOf(IServiceScope scope, Guid requestId) =>
        scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetInspectionNumberByRequestQuery(requestId), Ct);

    [Fact]
    public async Task Returns_the_inspection_number_of_the_requests_appraisal()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var requestId = Guid.NewGuid();
        await SeedAsync(db, requestId, 3);

        Assert.Equal(3, await InspectionOf(scope, requestId));
    }

    [Fact]
    public async Task A_cancelled_newer_appraisal_is_ignored()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var requestId = Guid.NewGuid();
        await SeedAsync(db, requestId, 3);
        await SeedAsync(db, requestId, 4, status: AppraisalStatus.Cancelled);

        Assert.Equal(3, await InspectionOf(scope, requestId));
    }

    [Fact]
    public async Task A_soft_deleted_newer_appraisal_is_ignored()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var requestId = Guid.NewGuid();
        await SeedAsync(db, requestId, 3);
        await SeedAsync(db, requestId, 4, deleted: true);

        Assert.Equal(3, await InspectionOf(scope, requestId));
    }

    [Fact]
    public async Task The_newest_live_appraisal_wins()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var requestId = Guid.NewGuid();
        await SeedAsync(db, requestId, 2);
        await SeedAsync(db, requestId, 3);

        Assert.Equal(3, await InspectionOf(scope, requestId));
    }

    [Fact]
    public async Task Null_when_the_request_has_no_appraisal_or_an_unnumbered_one()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var unnumbered = Guid.NewGuid();
        await SeedAsync(db, unnumbered, null);

        Assert.Null(await InspectionOf(scope, Guid.NewGuid()));
        Assert.Null(await InspectionOf(scope, unnumbered));
    }

    [Fact]
    public async Task Another_requests_appraisal_is_not_returned()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var mine = Guid.NewGuid();
        await SeedAsync(db, Guid.NewGuid(), 7);

        Assert.Null(await InspectionOf(scope, mine));

        await SeedAsync(db, mine, 3);
        Assert.Equal(3, await InspectionOf(scope, mine));
    }
}
