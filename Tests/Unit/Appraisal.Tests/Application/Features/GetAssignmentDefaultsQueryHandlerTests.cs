using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Appraisal.Infrastructure;
using Auth.Contracts.Companies;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Appraisal.Tests.Application.Features;

public class GetAssignmentDefaultsQueryHandlerTests
{
    private readonly AppraisalDbContext _db = new(
        new DbContextOptionsBuilder<AppraisalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private readonly ICompanyLookupService _companyLookup = Substitute.For<ICompanyLookupService>();

    private GetAssignmentDefaultsQueryHandler Handler(string appraisalType, AssignmentDefaultsEntry entry)
    {
        var provider = Substitute.For<IAssignmentDefaultsProvider>();
        provider.Current.Returns(
            new AssignmentDefaultsSnapshot(new Dictionary<string, AssignmentDefaultsEntry> { [appraisalType] = entry }));
        return new GetAssignmentDefaultsQueryHandler(_db, provider, _companyLookup);
    }

    [Fact]
    public async Task Known_appraisal_gets_the_values_resolved_for_its_type()
    {
        var appraisal = Appraisal.Domain.Appraisals.Appraisal.Create(
            requestId: Guid.NewGuid(), appraisalType: "ReAppraisal", priority: "Normal", now: new DateTime(2026, 1, 1));
        _db.Appraisals.Add(appraisal);
        // Audit columns are stamped by a SaveChanges interceptor that this bare context does not have.
        _db.Entry(appraisal).Property("CreatedAt").CurrentValue = new DateTime(2026, 1, 1);
        _db.Entry(appraisal).Property("CreatedBy").CurrentValue = "test";
        await _db.SaveChangesAsync();

        var result = await Handler("ReAppraisal", new AssignmentDefaultsEntry(true, "Internal", "Manual", "Manual"))
            .Handle(new GetAssignmentDefaultsQuery(appraisal.Id), CancellationToken.None);

        Assert.Equal(new GetAssignmentDefaultsResult("Internal", "Manual", "Manual"), result);
    }

    [Fact]
    public async Task Soft_deleted_appraisal_throws_not_found()
    {
        var appraisal = Appraisal.Domain.Appraisals.Appraisal.Create(
            requestId: Guid.NewGuid(), appraisalType: "New", priority: "Normal", now: new DateTime(2026, 1, 1));
        appraisal.Delete(Guid.NewGuid());
        _db.Appraisals.Add(appraisal);
        _db.Entry(appraisal).Property("CreatedAt").CurrentValue = new DateTime(2026, 1, 1);
        _db.Entry(appraisal).Property("CreatedBy").CurrentValue = "test";
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<AppraisalNotFoundException>(() =>
            Handler("New", new AssignmentDefaultsEntry())
                .Handle(new GetAssignmentDefaultsQuery(appraisal.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_appraisal_throws_not_found()
    {
        await Assert.ThrowsAsync<AppraisalNotFoundException>(() =>
            Handler("New", new AssignmentDefaultsEntry())
                .Handle(new GetAssignmentDefaultsQuery(Guid.NewGuid()), CancellationToken.None));
    }

    // ── previousCompany (construction-inspection requests) ───────────────────────

    private static readonly DateTime Stamp = new(2026, 1, 1);

    private async Task<Appraisal.Domain.Appraisals.Appraisal> SeedAsync(
        string type, Guid? prevId = null, string? number = null, int? inspectionNumber = null, DateTime? completedAt = null)
    {
        var appraisal = Appraisal.Domain.Appraisals.Appraisal.Create(
            requestId: Guid.NewGuid(), appraisalType: type, priority: "Normal", now: Stamp, prevAppraisalId: prevId);
        if (number is not null) appraisal.SetAppraisalNumber(number);
        if (inspectionNumber is { } n) appraisal.SetInspectionNumber(n);
        if (completedAt is { } c)
            typeof(Appraisal.Domain.Appraisals.Appraisal).GetProperty("CompletedAt")!.SetValue(appraisal, c);
        _db.Appraisals.Add(appraisal);
        _db.Entry(appraisal).Property("CreatedAt").CurrentValue = Stamp;
        _db.Entry(appraisal).Property("CreatedBy").CurrentValue = "test";
        await _db.SaveChangesAsync();
        return appraisal;
    }

    private async Task AssignAsync(Guid appraisalId, string type, string? companyId, DateTime assignedAt)
    {
        var assignment = AppraisalAssignment.Create(appraisalId, type, assigneeCompanyId: companyId, assignedBy: "test");
        typeof(AppraisalAssignment).GetProperty("AssignedAt")!.SetValue(assignment, assignedAt);
        _db.AppraisalAssignments.Add(assignment);
        _db.Entry(assignment).Property("CreatedAt").CurrentValue = assignedAt;
        _db.Entry(assignment).Property("CreatedBy").CurrentValue = "test";
        await _db.SaveChangesAsync();
    }

    private Task<GetAssignmentDefaultsResult> RunAsync(Guid appraisalId) =>
        Handler("Progressive", new AssignmentDefaultsEntry())
            .Handle(new GetAssignmentDefaultsQuery(appraisalId), CancellationToken.None);

    [Fact]
    public async Task Progressive_whose_picked_prior_was_done_by_a_company_reports_it()
    {
        var companyId = Guid.NewGuid();
        var completed = new DateTime(2026, 5, 12, 10, 0, 0);
        var prior = await SeedAsync("Progressive", number: "AP-0003", inspectionNumber: 3, completedAt: completed);
        await AssignAsync(prior.Id, "External", companyId.ToString(), Stamp);
        var current = await SeedAsync("Progressive", prevId: prior.Id);
        _companyLookup.GetByIdAsync(companyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(companyId, "Acme Appraisal", null, IsAssignable: true));

        var result = await RunAsync(current.Id);

        Assert.Equal(
            new PreviousCompanyDto(companyId, "Acme Appraisal", prior.Id, "AP-0003", 3, completed, MouActive: true),
            result.PreviousCompany);
    }

    [Fact]
    public async Task Previous_company_outside_its_MOU_window_is_still_reported_but_not_active()
    {
        var companyId = Guid.NewGuid();
        var prior = await SeedAsync("New", number: "AP-0001");
        await AssignAsync(prior.Id, "External", companyId.ToString(), Stamp);
        var current = await SeedAsync("Progressive", prevId: prior.Id);
        _companyLookup.GetByIdAsync(companyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(companyId, "Lapsed Co", null, IsAssignable: false));

        var result = await RunAsync(current.Id);

        Assert.False(result.PreviousCompany!.MouActive);
        Assert.Null(result.PreviousCompany.InspectionNumber); // the original appraisal has no round
    }

    [Fact]
    public async Task Internally_appraised_prior_has_no_previous_company()
    {
        var prior = await SeedAsync("New");
        await AssignAsync(prior.Id, "Internal", null, Stamp);
        var current = await SeedAsync("Progressive", prevId: prior.Id);

        Assert.Null((await RunAsync(current.Id)).PreviousCompany);
    }

    [Fact]
    public async Task Non_progressive_appraisal_with_a_prior_has_no_previous_company()
    {
        var companyId = Guid.NewGuid();
        var prior = await SeedAsync("New");
        await AssignAsync(prior.Id, "External", companyId.ToString(), Stamp);
        var current = await SeedAsync("ReAppraisal", prevId: prior.Id);

        var result = await Handler("ReAppraisal", new AssignmentDefaultsEntry())
            .Handle(new GetAssignmentDefaultsQuery(current.Id), CancellationToken.None);

        Assert.Null(result.PreviousCompany);
        await _companyLookup.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task Newest_live_assignment_decides_so_an_internal_reassignment_hides_the_older_company()
    {
        var companyId = Guid.NewGuid();
        var prior = await SeedAsync("New");
        await AssignAsync(prior.Id, "External", companyId.ToString(), Stamp);
        await AssignAsync(prior.Id, "Internal", null, Stamp.AddDays(1));
        var current = await SeedAsync("Progressive", prevId: prior.Id);

        Assert.Null((await RunAsync(current.Id)).PreviousCompany);
        await _companyLookup.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task Company_that_no_longer_exists_is_not_reported()
    {
        // A soft-deleted company: the lookup (which carries the Company soft-delete filter) finds nothing.
        var prior = await SeedAsync("New");
        await AssignAsync(prior.Id, "External", Guid.NewGuid().ToString(), Stamp);
        var current = await SeedAsync("Progressive", prevId: prior.Id);
        // _companyLookup returns null by default.

        Assert.Null((await RunAsync(current.Id)).PreviousCompany);
    }

    [Fact]
    public async Task Prior_without_any_assignment_has_no_previous_company()
    {
        var prior = await SeedAsync("New");
        var current = await SeedAsync("Progressive", prevId: prior.Id);

        Assert.Null((await RunAsync(current.Id)).PreviousCompany);
    }

    [Fact]
    public void Serialized_response_keeps_previousCompany_as_null_and_dates_without_Z()
    {
        // The API drops nulls app-wide (Program.cs); the key must survive anyway, as agreed with the front end.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var none = JsonSerializer.Serialize(new GetAssignmentDefaultsResult("External", "Quotation", "RoundRobin"), options);
        Assert.Contains("\"previousCompany\":null", none);

        var company = new PreviousCompanyDto(
            Guid.NewGuid(), "Acme", Guid.NewGuid(), "AP-1", null, new DateTime(2026, 5, 12, 10, 0, 0), true);
        var some = JsonSerializer.Serialize(
            new GetAssignmentDefaultsResult("External", "Quotation", "RoundRobin", company), options);
        Assert.Contains("\"completedAt\":\"2026-05-12T10:00:00\"", some);
        Assert.Contains("\"inspectionNumber\":null", some);
    }
}
