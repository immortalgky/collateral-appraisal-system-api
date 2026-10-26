using System.Text.Json;
using Appraisal.Application.Features.Assignments.GetAssignments;
using Appraisal.Domain.Appraisals;
using Auth.Contracts.Users;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using static Appraisal.Tests.Application.Services.FeeTestSeed;

namespace Appraisal.Tests.Application.Features;

public class GetAssignmentsQueryHandlerTests
{
    private readonly Appraisal.Infrastructure.AppraisalDbContext _db = NewDb();
    private readonly IUserLookupService _users = Substitute.For<IUserLookupService>();

    private async Task<AssignmentDto> SingleAsync(
        string assignedBy, string? executor = null, string? followup = null, params UserLookupDto[] known)
    {
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var assignment = AppraisalAssignment.Create(
            appraisal.Id, "Internal", assigneeUserId: executor, internalAppraiserId: followup, assignedBy: assignedBy);
        _db.AppraisalAssignments.Add(assignment);
        _db.Entry(assignment).Property("CreatedAt").CurrentValue = new DateTime(2026, 1, 1);
        _db.Entry(assignment).Property("CreatedBy").CurrentValue = "test";
        await _db.SaveChangesAsync();

        _users.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(known.ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase));

        var result = await new GetAssignmentsQueryHandler(_db, _users, NullLogger<GetAssignmentsQueryHandler>.Instance)
            .Handle(new GetAssignmentsQuery(appraisal.Id), CancellationToken.None);
        return Assert.Single(result.Assignments);
    }

    [Fact]
    public async Task Known_employee_code_is_resolved_to_full_name_and_code_is_kept()
    {
        var dto = await SingleAsync("P5229", known: new UserLookupDto("P5229", "Somchai", "Jaidee"));

        Assert.Equal("Somchai Jaidee", dto.AssignedByName);
        Assert.Equal("P5229", dto.AssignedBy);
    }

    [Fact]
    public async Task Unknown_employee_code_gives_null_name_and_the_key_is_still_serialized()
    {
        var dto = await SingleAsync("P0000");

        Assert.Null(dto.AssignedByName);
        // The API drops nulls app-wide; the pin must keep the key present so the front end can tell "unknown".
        Assert.Contains("\"AssignedByName\":null", JsonSerializer.Serialize(dto,
            new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
    }

    [Fact]
    public async Task Executor_and_followup_staff_codes_are_resolved_in_the_same_single_lookup()
    {
        var dto = await SingleAsync(
            "P5229", executor: "int.staff1", followup: "int.staff2",
            known:
            [
                new UserLookupDto("P5229", "Somchai", "Jaidee"),
                new UserLookupDto("int.staff1", "Anan", "Meesuk"),
                new UserLookupDto("int.staff2", "Malee", "Sukjai")
            ]);

        Assert.Equal("Anan Meesuk", dto.AssigneeUserName);
        Assert.Equal("Malee Sukjai", dto.InternalAppraiserName);
        Assert.Equal("int.staff2", dto.InternalAppraiserId); // the code stays, for "Name (code)"
        await _users.Received(1).GetByUsernamesAsync(
            Arg.Is<IEnumerable<string>>(codes => codes.OrderBy(c => c, StringComparer.Ordinal).SequenceEqual(new[] { "P5229", "int.staff1", "int.staff2" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Absent_or_unknown_staff_codes_give_null_names_with_the_keys_still_serialized()
    {
        var dto = await SingleAsync("P5229", executor: null, followup: "int.ghost",
            known: new UserLookupDto("P5229", "Somchai", "Jaidee"));

        Assert.Null(dto.AssigneeUserName);
        Assert.Null(dto.InternalAppraiserName);
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        Assert.Contains("\"AssigneeUserName\":null", json);
        Assert.Contains("\"InternalAppraiserName\":null", json);
    }

    [Fact]
    public async Task A_failing_name_lookup_returns_the_rows_with_null_names_instead_of_failing_the_endpoint()
    {
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var assignment = AppraisalAssignment.Create(appraisal.Id, "Internal", assigneeUserId: "int.staff1", assignedBy: "P5229");
        _db.AppraisalAssignments.Add(assignment);
        _db.Entry(assignment).Property("CreatedAt").CurrentValue = new DateTime(2026, 1, 1);
        _db.Entry(assignment).Property("CreatedBy").CurrentValue = "test";
        await _db.SaveChangesAsync();
        _users.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyDictionary<string, UserLookupDto>>(_ => throw new InvalidOperationException("Auth is down"));

        var result = await new GetAssignmentsQueryHandler(_db, _users, NullLogger<GetAssignmentsQueryHandler>.Instance)
            .Handle(new GetAssignmentsQuery(appraisal.Id), CancellationToken.None);

        var dto = Assert.Single(result.Assignments);
        Assert.Equal("P5229", dto.AssignedBy);
        Assert.Null(dto.AssignedByName);
        Assert.Null(dto.AssigneeUserName);
    }

    private async Task<Guid> SeedOneAsync()
    {
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var assignment = AppraisalAssignment.Create(appraisal.Id, "Internal", assigneeUserId: "int.staff1", assignedBy: "P5229");
        _db.AppraisalAssignments.Add(assignment);
        _db.Entry(assignment).Property("CreatedAt").CurrentValue = new DateTime(2026, 1, 1);
        _db.Entry(assignment).Property("CreatedBy").CurrentValue = "test";
        await _db.SaveChangesAsync();
        // A lookup that only ends when its token is cancelled.
        _users.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyDictionary<string, UserLookupDto>>>(async call =>
            {
                await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
                return new Dictionary<string, UserLookupDto>();
            });
        return appraisal.Id;
    }

    [Fact]
    public async Task A_name_lookup_that_outlasts_its_budget_returns_null_names()
    {
        var appraisalId = await SeedOneAsync();

        var result = await new GetAssignmentsQueryHandler(_db, _users, NullLogger<GetAssignmentsQueryHandler>.Instance)
            { LookupTimeout = TimeSpan.FromMilliseconds(50) }
            .Handle(new GetAssignmentsQuery(appraisalId), CancellationToken.None);

        var dto = Assert.Single(result.Assignments);
        Assert.Equal("P5229", dto.AssignedBy);
        Assert.Null(dto.AssignedByName);
    }

    [Fact]
    public async Task The_callers_own_cancellation_still_stops_the_request()
    {
        var appraisalId = await SeedOneAsync();
        using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GetAssignmentsQueryHandler(_db, _users, NullLogger<GetAssignmentsQueryHandler>.Instance)
                { LookupTimeout = TimeSpan.FromSeconds(30) }
                .Handle(new GetAssignmentsQuery(appraisalId), caller.Token));
    }
}
