using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Land titles come back in the order the appraiser entered them. LandTitles.Id is NEWSEQUENTIALID() and EF
/// inserts added rows in temp-key order, so without the stored SequenceNumber the read order is a shuffle.
/// </summary>
[Collection("Integration")]
public class LandTitleOrderTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static LandTitleItemData Item(string number, Guid? id = null)
        => new(id, number, "DEED", Rai: 1m);

    private async Task<(Guid AppraisalId, Guid PropertyId)> SeedLandAsync(
        List<LandTitleItemData> titles, CancellationToken ct)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"ORD-{Guid.NewGuid():N}"[..18]);
        var property = appraisal.AddLandProperty();
        LandDetailSync.SyncTitles(property.LandDetail!, titles);

        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(ct);
        return (appraisal.Id, property.Id);
    }

    private async Task<List<LandTitle>> LoadTitlesAsync(Guid appraisalId, Guid propertyId, CancellationToken ct)
    {
        using var scope = CreateScope();
        var appraisal = await scope.ServiceProvider.GetRequiredService<IAppraisalRepository>()
            .GetByIdWithPropertiesAsync(appraisalId, ct);
        return appraisal!.GetProperty(propertyId)!.LandDetail!.Titles.ToList();
    }

    [Fact]
    public async Task Eight_titles_are_read_back_in_the_order_they_were_entered()
    {
        var ct = TestContext.Current.CancellationToken;
        var numbers = Enumerable.Range(1, 8).Select(i => $"T{i}").ToList();

        var (appraisalId, propertyId) = await SeedLandAsync(numbers.Select(n => Item(n)).ToList(), ct);

        var titles = await LoadTitlesAsync(appraisalId, propertyId, ct);
        Assert.Equal(numbers, titles.Select(t => t.TitleNumber));
        Assert.Equal(Enumerable.Range(1, 8), titles.Select(t => t.SequenceNumber));
    }

    [Fact]
    public async Task A_sync_that_updates_in_place_and_adds_rows_follows_the_incoming_order()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, propertyId) = await SeedLandAsync(
            Enumerable.Range(1, 8).Select(i => Item($"T{i}")).ToList(), ct);
        var idOf = (await LoadTitlesAsync(appraisalId, propertyId, ct)).ToDictionary(t => t.TitleNumber, t => t.Id);

        // Existing rows reordered and edited in place, two brand-new rows in between, T3/T4/T6/T7 removed.
        var incoming = new List<LandTitleItemData>
        {
            Item("T8", idOf["T8"]),
            Item("N1"),
            Item("T2", idOf["T2"]),
            Item("T5", idOf["T5"]),
            Item("N2"),
            Item("T1", idOf["T1"]),
        };

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = await scope.ServiceProvider.GetRequiredService<IAppraisalRepository>()
                .GetByIdWithPropertiesAsync(appraisalId, ct);
            LandDetailSync.SyncTitles(appraisal!.GetProperty(propertyId)!.LandDetail!, incoming);
            await db.SaveChangesAsync(ct);
        }

        var titles = await LoadTitlesAsync(appraisalId, propertyId, ct);
        Assert.Equal(["T8", "N1", "T2", "T5", "N2", "T1"], titles.Select(t => t.TitleNumber));
        Assert.Equal(idOf["T8"], titles[0].Id); // updated in place, not deleted and re-created
    }
}
