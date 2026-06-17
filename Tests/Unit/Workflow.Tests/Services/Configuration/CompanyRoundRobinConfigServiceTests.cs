using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.Data;
using Workflow.Services.Configuration;
using Workflow.Services.Configuration.Models;
using Xunit;

namespace Workflow.Tests.Services.Configuration;

/// <summary>
/// Covers create/resolve for the external-company round-robin pool config, including the
/// loan-type-specific-over-global resolution and weight clamping.
/// </summary>
public class CompanyRoundRobinConfigServiceTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly CompanyRoundRobinConfigService _sut;

    public CompanyRoundRobinConfigServiceTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WorkflowDbContext(options);
        _sut = new CompanyRoundRobinConfigService(
            _db,
            Substitute.For<ILogger<CompanyRoundRobinConfigService>>());
    }

    private CreateCompanyRoundRobinConfigurationRequest Request(string? loanType, params (Guid id, int weight)[] entries) => new()
    {
        LoanType = loanType,
        IsActive = true,
        CreatedBy = "tester",
        Entries = entries.Select(e => new CompanyWeightDto { CompanyId = e.id, Weight = e.weight }).ToList()
    };

    [Fact]
    public async Task ResolveAsync_PrefersLoanTypeSpecificPool_OverGlobal()
    {
        var globalCompany = Guid.NewGuid();
        var retailCompany = Guid.NewGuid();
        await _sut.CreateAsync(Request(null, (globalCompany, 1)));
        await _sut.CreateAsync(Request("Retail", (retailCompany, 2)));

        var resolved = await _sut.ResolveAsync("Retail");

        resolved!.LoanType.Should().Be("Retail");
        resolved.Entries.Should().ContainSingle().Which.CompanyId.Should().Be(retailCompany);
    }

    [Fact]
    public async Task ResolveAsync_FallsBackToGlobal_WhenNoLoanTypePool()
    {
        var globalCompany = Guid.NewGuid();
        await _sut.CreateAsync(Request(null, (globalCompany, 1)));

        var resolved = await _sut.ResolveAsync("IBG");

        resolved!.LoanType.Should().BeNull();
        resolved.Entries.Should().ContainSingle().Which.CompanyId.Should().Be(globalCompany);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNull_WhenNoActivePool()
    {
        var company = Guid.NewGuid();
        var created = await _sut.CreateAsync(Request(null, (company, 1)));
        await _sut.UpdateAsync(created.Id, new UpdateCompanyRoundRobinConfigurationRequest
        {
            LoanType = null,
            IsActive = false,
            UpdatedBy = "tester",
            Entries = created.Entries
        });

        var resolved = await _sut.ResolveAsync(null);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ClampsWeightsToAtLeastOne_AndDropsEmptyCompanyIds()
    {
        var company = Guid.NewGuid();
        var created = await _sut.CreateAsync(Request(null, (company, 0), (Guid.Empty, 5)));

        created.Entries.Should().ContainSingle();
        created.Entries[0].CompanyId.Should().Be(company);
        created.Entries[0].Weight.Should().Be(1);
    }

    public void Dispose() => _db.Dispose();
}
