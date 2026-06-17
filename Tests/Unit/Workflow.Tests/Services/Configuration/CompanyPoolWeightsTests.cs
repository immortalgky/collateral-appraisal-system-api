using FluentAssertions;
using Workflow.Services.Configuration;
using Workflow.Services.Configuration.Models;
using Xunit;

namespace Workflow.Tests.Services.Configuration;

public class CompanyPoolWeightsTests
{
    private static CompanyWeightDto E(Guid id, int weight) => new() { CompanyId = id, Weight = weight };

    [Fact]
    public void Normalize_GcdReducesEqualWeightsToOne()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var result = CompanyPoolWeights.Normalize([E(a, 100), E(b, 100)]);

        result.Should().AllSatisfy(e => e.Weight.Should().Be(1));
    }

    [Fact]
    public void Normalize_GcdReducesProportionally()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var result = CompanyPoolWeights.Normalize([E(a, 100), E(b, 50)]);

        result.Single(e => e.CompanyId == a).Weight.Should().Be(2);
        result.Single(e => e.CompanyId == b).Weight.Should().Be(1);
    }

    [Fact]
    public void Normalize_ClampsBelowOne_AndDropsEmptyIds()
    {
        var a = Guid.NewGuid();

        var result = CompanyPoolWeights.Normalize([E(a, 0), E(Guid.Empty, 5)]);

        result.Should().ContainSingle();
        result[0].CompanyId.Should().Be(a);
        result[0].Weight.Should().Be(1);
    }

    [Fact]
    public void Normalize_DedupesByCompanyId_KeepingFirst()
    {
        var a = Guid.NewGuid();

        var result = CompanyPoolWeights.Normalize([E(a, 4), E(a, 8)]);

        result.Should().ContainSingle();
        // First weight (4) kept; single entry → GCD reduces to 1.
        result[0].Weight.Should().Be(1);
    }
}
