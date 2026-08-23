using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Parameter.Contracts.Parameters;
using Parameter.Contracts.Parameters.Dtos;

namespace Appraisal.Tests.Application.Services;

/// <summary>
/// Covers the TierBased branch of <see cref="AssignmentFeeService"/> — specifically the
/// AppraisalType-scoped ladder. A tier scoped to an appraisal type replaces the generic
/// (null-scoped) ladder for that type rather than competing with it, which is what lets
/// PreAppraisal (block / M-F project) carry a flat rate at any selling price.
/// </summary>
public class AssignmentFeeServiceTierTests
{
    private const decimal PreAppraisalFlatFee = 10_000m;

    [Theory]
    [InlineData(0)]
    [InlineData(1_000_000)]
    [InlineData(500_000_000)]
    public async Task PreAppraisal_UsesFlatTypeScopedTier_RegardlessOfSellingPrice(decimal sellingPrice)
    {
        await using var db = NewDb();
        SeedGenericLadder(db);
        db.FeeStructures.Add(FeeStructure.Create(
            "01", PreAppraisalFlatFee, 0m, null, true, AppraisalTypes.PreAppraisal));
        var assignmentId = SeedFeeShell(db, sellingPrice);
        await db.SaveChangesAsync();

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            Guid.NewGuid(), assignmentId,
            new AssignmentFeeSource.TierBased(AppraisalTypes.PreAppraisal),
            CancellationToken.None);

        var fee = db.AppraisalFees.Include(f => f.Items).Single();
        var item = Assert.Single(fee.Items);
        Assert.Equal("01", item.FeeCode);
        Assert.Equal(PreAppraisalFlatFee, item.FeeAmount);
        // Ex-VAT in, VAT added on top by the aggregate.
        Assert.Equal(PreAppraisalFlatFee, fee.TotalFeeBeforeVAT);
        Assert.Equal(700m, fee.VATAmount);
        Assert.Equal(10_700m, fee.TotalFeeAfterVAT);
    }

    [Theory]
    [InlineData(AppraisalTypes.New, 1_000_000, 2_500)]
    [InlineData(AppraisalTypes.New, 8_000_000, 3_000)]
    [InlineData(AppraisalTypes.ReAppraisal, 50_000_000, 3_500)]
    // A type with no scoped rows of its own still resolves to the generic ladder.
    [InlineData(AppraisalTypes.Progressive, 1_000_000, 2_500)]
    public async Task TypesWithoutTheirOwnLadder_FallBackToGenericBands(
        string appraisalType, decimal sellingPrice, decimal expectedAmount)
    {
        await using var db = NewDb();
        SeedGenericLadder(db);
        db.FeeStructures.Add(FeeStructure.Create(
            "01", PreAppraisalFlatFee, 0m, null, true, AppraisalTypes.PreAppraisal));
        var assignmentId = SeedFeeShell(db, sellingPrice);
        await db.SaveChangesAsync();

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            Guid.NewGuid(), assignmentId,
            new AssignmentFeeSource.TierBased(appraisalType),
            CancellationToken.None);

        var fee = db.AppraisalFees.Include(f => f.Items).Single();
        Assert.Equal(expectedAmount, Assert.Single(fee.Items).FeeAmount);
    }

    [Fact]
    public async Task NoTierConfiguredAtAll_LeavesFeeWithoutItems_AndDoesNotThrow()
    {
        // Guards the assignment consumer: an admin deleting every FeeCode "01" row used to throw
        // out of the fee service and nack the integration event into a retry loop.
        await using var db = NewDb();
        var assignmentId = SeedFeeShell(db, 1_000_000m);
        await db.SaveChangesAsync();

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            Guid.NewGuid(), assignmentId,
            new AssignmentFeeSource.TierBased(AppraisalTypes.New),
            CancellationToken.None);

        var fee = db.AppraisalFees.Include(f => f.Items).Single();
        Assert.Empty(fee.Items);
        Assert.Equal(0m, fee.TotalFeeAfterVAT);
    }

    [Fact]
    public async Task SellingPriceInTheGapBetweenBands_FallsBackToTheHighestBand()
    {
        // The seeded generic bands are 0–7,000,000 then 7,000,001–… so a fractional value in
        // between matches nothing. Existing behaviour: take the highest band.
        await using var db = NewDb();
        SeedGenericLadder(db);
        var assignmentId = SeedFeeShell(db, 7_000_000.50m);
        await db.SaveChangesAsync();

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            Guid.NewGuid(), assignmentId,
            new AssignmentFeeSource.TierBased(AppraisalTypes.New),
            CancellationToken.None);

        var fee = db.AppraisalFees.Include(f => f.Items).Single();
        Assert.Equal(3_500m, Assert.Single(fee.Items).FeeAmount);
    }

    // ── Helpers ──

    private static AppraisalDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>The three null-scoped bands seeded by FeeStructureConfiguration.</summary>
    private static void SeedGenericLadder(AppraisalDbContext db)
    {
        db.FeeStructures.AddRange(
            FeeStructure.Create("01", 2_500m, 0m, 7_000_000m),
            FeeStructure.Create("01", 3_000m, 7_000_001m, 10_000_000m),
            FeeStructure.Create("01", 3_500m, 10_000_001m, null));
    }

    private static Guid SeedFeeShell(AppraisalDbContext db, decimal totalSellingPrice)
    {
        var assignmentId = Guid.CreateVersion7();
        db.AppraisalFees.Add(AppraisalFee.Create(assignmentId, totalSellingPrice: totalSellingPrice));
        return assignmentId;
    }

    private static AssignmentFeeService BuildService(AppraisalDbContext db)
    {
        var parameterLookup = Substitute.For<IParameterLookupService>();
        parameterLookup
            .GetDescriptionAsync(Arg.Any<ParameterDto>(), Arg.Any<CancellationToken>())
            .Returns((string?)null); // service falls back to the fee code as the name

        return new AssignmentFeeService(
            db,
            parameterLookup,
            NullLogger<AssignmentFeeService>.Instance);
    }
}
