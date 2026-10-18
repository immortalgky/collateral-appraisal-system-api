using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;
using Appraisal.Domain.Appraisals.Exceptions;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Appraisal.Tests.Application.Features;

public class GetAssignmentDefaultsQueryHandlerTests
{
    private readonly AppraisalDbContext _db = new(
        new DbContextOptionsBuilder<AppraisalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private GetAssignmentDefaultsQueryHandler Handler(string appraisalType, AssignmentDefaultsEntry entry)
    {
        var provider = Substitute.For<IAssignmentDefaultsProvider>();
        provider.Current.Returns(
            new AssignmentDefaultsSnapshot(new Dictionary<string, AssignmentDefaultsEntry> { [appraisalType] = entry }));
        return new GetAssignmentDefaultsQueryHandler(_db, provider);
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
}
