using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Parameter.Contracts.Parameters;
using Parameter.Contracts.Parameters.Dtos;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.Services;

/// <summary>Seeds the appraisal / assignment / fee graph the fee tests run against (in-memory EF).</summary>
internal static class FeeTestSeed
{
    private static readonly DateTime Stamp = new(2026, 1, 1);

    public static AppraisalDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>The three null-scoped bands seeded by FeeStructureConfiguration.</summary>
    public static void SeedGenericLadder(AppraisalDbContext db) =>
        db.FeeStructures.AddRange(
            FeeStructure.Create("01", 2_500m, 0m, 7_000_000m),
            FeeStructure.Create("01", 3_000m, 7_000_001m, 10_000_000m),
            FeeStructure.Create("01", 3_500m, 10_000_001m, null));

    public static async Task<AppraisalAggregate> AddAppraisalAsync(
        AppraisalDbContext db, string type, Guid? prevId = null, string? number = null)
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), type, "Normal", Stamp, prevAppraisalId: prevId);
        if (number is not null)
            appraisal.SetAppraisalNumber(number);
        db.Appraisals.Add(appraisal);
        // Audit columns are stamped by a SaveChanges interceptor that this bare context does not have.
        db.Entry(appraisal).Property("CreatedAt").CurrentValue = Stamp;
        db.Entry(appraisal).Property("CreatedBy").CurrentValue = "test";
        await db.SaveChangesAsync();
        return appraisal;
    }

    /// <summary>A Pending (admin-stage) assignment, or an Assigned one when <paramref name="assigned"/>.</summary>
    public static async Task<AppraisalAssignment> AddAssignmentAsync(
        AppraisalDbContext db, Guid appraisalId, bool assigned = false, string? companyId = null, string type = "Internal",
        DateTime? createdAt = null)
    {
        var assignment = AppraisalAssignment.Create(appraisalId, type, assigneeCompanyId: companyId, assignedBy: "test");
        if (assigned)
            assignment.Assign(type, assigneeUserId: companyId is null ? "U1" : null, assigneeCompanyId: companyId, assignedBy: "test");
        db.AppraisalAssignments.Add(assignment);
        db.Entry(assignment).Property("CreatedAt").CurrentValue = createdAt ?? Stamp;
        db.Entry(assignment).Property("CreatedBy").CurrentValue = "test";
        await db.SaveChangesAsync();
        return assignment;
    }

    /// <summary>
    /// A request's own fee row always has a payment type (Fee.Validate). Pass <c>paymentType: null</c> for the blank
    /// shell an admin's first edit creates when the request had no fee data.
    /// </summary>
    public const string DefaultPaymentType = "01";

    public static async Task<AppraisalFee> AddFeeAsync(
        AppraisalDbContext db, Guid assignmentId, decimal? sellingPrice = null, decimal? inspectionFee = null,
        string? paymentType = DefaultPaymentType)
    {
        var fee = AppraisalFee.Create(assignmentId, feePaymentType: paymentType, totalSellingPrice: sellingPrice);
        fee.SetConstructionInspectionFee(inspectionFee);
        db.AppraisalFees.Add(fee);
        await db.SaveChangesAsync();
        return fee;
    }

    /// <summary>
    /// A finalized RFQ for <paramref name="appraisalId"/> that <paramref name="winnerCompanyId"/> won, with the
    /// winner's fee for that appraisal. A zero <paramref name="feeAmount"/> with a <paramref name="quotedPrice"/> is a
    /// legacy row without a fee breakdown.
    /// </summary>
    public static async Task<QuotationRequest> AddFinalizedQuotationAsync(
        AppraisalDbContext db, Guid appraisalId, Guid winnerCompanyId, decimal feeAmount, decimal discount = 0m,
        decimal quotedPrice = 0m, string number = "RFQ-001")
    {
        var now = DateTime.Now;
        var rfq = QuotationRequest.Create(now.AddDays(1), "admin", now);
        rfq.SetQuotationNumber(number);
        rfq.AddAppraisal(appraisalId, "admin", now);
        var winner = CompanyQuotation.Create(rfq.Id, Guid.NewGuid(), winnerCompanyId, "QT-001", now);
        var item = winner.AddItem(Guid.NewGuid(), appraisalId, 1, quotedPrice > 0m ? quotedPrice : feeAmount * 1.07m, 3);
        if (feeAmount > 0m)
            item.SetFeeBreakdown(feeAmount, discount, vatPercent: 7m);
        winner.MarkAsWinner();
        rfq.AddQuotation(winner);
        typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.Status))!.SetValue(rfq, "Finalized");
        typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedCompanyId))!.SetValue(rfq, winnerCompanyId);
        typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedAt))!.SetValue(rfq, now);
        db.QuotationRequests.Add(rfq);
        // Audit columns are stamped by a SaveChanges interceptor that this bare context does not have.
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            if (entry.Metadata.FindProperty("CreatedAt") is not null) entry.Property("CreatedAt").CurrentValue = Stamp;
            if (entry.Metadata.FindProperty("CreatedBy") is not null) entry.Property("CreatedBy").CurrentValue = "test";
        }

        await db.SaveChangesAsync();
        return rfq;
    }

    public static AssignmentFeeService BuildService(AppraisalDbContext db)
    {
        var parameterLookup = Substitute.For<IParameterLookupService>();
        parameterLookup
            .GetDescriptionAsync(Arg.Any<ParameterDto>(), Arg.Any<CancellationToken>())
            .Returns((string?)null); // service falls back to the fee code as the name

        return new AssignmentFeeService(db, parameterLookup, NullLogger<AssignmentFeeService>.Instance);
    }
}
