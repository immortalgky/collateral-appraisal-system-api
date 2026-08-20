using Appraisal.Application.Features.DecisionSummary;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Consistency guard for the appraisal-level INSURANCE total, which is computed by two independent
/// implementations that deliberately cannot be collapsed into one:
///   • EF/LINQ over tracked entities inside AppraisalValuationSummaryService.RecomputeAsync — the
///     pre-save write path, which must see uncommitted values in the same transaction, so it cannot
///     use a separate Dapper connection;
///   • the Dapper SQL in BuildingInsuranceCalculator — the read/save path.
/// Both files carry KEEP-IN-SYNC comments. This test fails the day one formula changes and the other
/// does not (e.g. a new insurable structure type, or a change to the IsBuilding / land-exclusion
/// rule) — the divergence that the comment notes "is what caused condo to report 0".
/// </summary>
[Collection("Integration")]
public class InsuranceConsistencyTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    [Fact]
    public async Task Insurance_total_EF_summary_path_matches_Dapper_read_path()
    {
        var ct = TestContext.Current.CancellationToken;

        // ── Arrange: a non-block appraisal with a building (one insurable + one non-insurable
        //    depreciation row) and a condo carrying a building-insurance price. ──
        Guid appraisalId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"INS-{Guid.NewGuid():N}"[..18]);
            // CompletedAt has a private setter and no domain path sets it here; the schema is
            // permissive but we mirror the proven appraisal-seed helper for safety.
            typeof(AppraisalAggregate).GetProperty(nameof(AppraisalAggregate.CompletedAt))!
                .SetValue(appraisal, DateTime.Now);

            var building = appraisal.AddBuildingProperty();
            // Insurable structure — counts toward the total.
            building.BuildingDetail!.AddDepreciationDetail(
                "Gross", isBuilding: true, priceAfterDepreciation: 700_000m);
            // Non-building row (e.g. fence / pool) — MUST be excluded by BOTH paths.
            building.BuildingDetail!.AddDepreciationDetail(
                "Gross", isBuilding: false, priceAfterDepreciation: 999_999m);

            var condo = appraisal.AddCondoProperty();
            condo.CondoDetail!.Update(
                ownerName: "Test Owner", // required NOT NULL by DB schema
                address: Address.Create("Sub", "Dist", "Prov"),
                landOffice: "LO");
            // BuildingInsurancePrice is server-derived (private setter); set it directly for the fixture.
            typeof(CondoAppraisalDetail).GetProperty(nameof(CondoAppraisalDetail.BuildingInsurancePrice))!
                .SetValue(condo.CondoDetail, 300_000m);

            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(ct);
            appraisalId = appraisal.Id;
        }

        // Building-insurable 700k + condo 300k; the non-building 999_999 must be excluded.
        const decimal expected = 700_000m + 300_000m;

        // ── Act: EF write path — RecomputeAsync stamps ValuationAnalyses.InsuranceValue. ──
        decimal efInsurance;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var summaryService = scope.ServiceProvider.GetRequiredService<AppraisalValuationSummaryService>();

            await summaryService.RecomputeAsync(appraisalId, ct);
            await db.SaveChangesAsync(ct);

            var row = await db.ValuationAnalyses.AsNoTracking()
                .FirstAsync(v => v.AppraisalId == appraisalId, ct);
            efInsurance = row.InsuranceValue ?? 0m;
        }

        // ── Act: Dapper read path. ──
        decimal dapperInsurance;
        using (var scope = CreateScope())
        {
            var sqlFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
            dapperInsurance = await BuildingInsuranceCalculator.ComputeAsync(sqlFactory, appraisalId);
        }

        // ── Assert: both paths agree with each other and with the expected total (IsBuilding filter applied). ──
        Assert.Equal(expected, efInsurance);
        Assert.Equal(expected, dapperInsurance);
        Assert.Equal(efInsurance, dapperInsurance);
    }
}
