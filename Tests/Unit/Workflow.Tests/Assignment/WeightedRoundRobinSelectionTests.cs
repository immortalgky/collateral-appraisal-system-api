using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Data;
using Workflow.Data;
using Workflow.Data.Repository;
using Workflow.Tasks.Models;
using Xunit;

namespace Workflow.Tests.Assignment;

/// <summary>
/// Verifies the weighted round-robin selection in <see cref="AssignmentRepository.SelectNextUserWithRoundResetAsync"/>:
/// distribution is proportional to weight, and weight 1 reproduces plain even rotation.
/// </summary>
public class WeightedRoundRobinSelectionTests : IDisposable
{
    private const string Activity = "CompanyRouting";
    private const string Group = "hash-1";

    private readonly WorkflowDbContext _db;
    private readonly AssignmentRepository _sut;

    public WeightedRoundRobinSelectionTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WorkflowDbContext(options);
        _sut = new AssignmentRepository(_db, Substitute.For<ISqlConnectionFactory>());
    }

    private void SeedUser(string userId, int weight)
    {
        _db.RoundRobinQueue.Add(new RoundRobinQueue
        {
            ActivityName = Activity,
            GroupsHash = Group,
            GroupsList = "list",
            UserId = userId,
            AssignmentCount = 0,
            Weight = weight,
            LastAssignedAt = DateTime.Now,
            IsActive = true
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Selection_IsProportionalToWeight()
    {
        SeedUser("aaaa", 3);
        SeedUser("bbbb", 1);

        var picks = new Dictionary<string, int> { ["aaaa"] = 0, ["bbbb"] = 0 };
        for (var i = 0; i < 8; i++)
        {
            var selected = await _sut.SelectNextUserWithRoundResetAsync(Activity, Group);
            picks[selected!]++;
        }

        // Two full rounds of a 3:1 pool → 6 vs 2.
        picks["aaaa"].Should().Be(6);
        picks["bbbb"].Should().Be(2);
    }

    [Fact]
    public async Task EqualWeights_RotateEvenly()
    {
        SeedUser("aaaa", 1);
        SeedUser("bbbb", 1);

        var picks = new Dictionary<string, int> { ["aaaa"] = 0, ["bbbb"] = 0 };
        for (var i = 0; i < 4; i++)
        {
            var selected = await _sut.SelectNextUserWithRoundResetAsync(Activity, Group);
            picks[selected!]++;
        }

        picks["aaaa"].Should().Be(2);
        picks["bbbb"].Should().Be(2);
    }

    public void Dispose() => _db.Dispose();
}
