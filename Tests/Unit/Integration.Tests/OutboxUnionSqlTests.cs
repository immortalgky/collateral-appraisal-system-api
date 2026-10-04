using FluentAssertions;
using Integration.Application.Features.OutboxMessages;

namespace Integration.Tests;

/// <summary>
/// The list/summary predicates must sit INSIDE each per-module branch of the UNION (so a branch can seek its
/// own index and never touches rows the filter would discard), not on the UNION's result.
/// </summary>
public class OutboxUnionSqlTests
{
    private static string[] Branches(string sql) => sql.Split("\nUNION ALL\n");

    [Fact]
    public void Build_WhereClause_IsAppliedInsideEveryBranch()
    {
        var branches = Branches(OutboxUnionSql.Build("Id", "o.Status = 'Failed'"));

        branches.Should().HaveCount(OutboxModuleWhitelist.Modules.Length);
        branches.Should().OnlyContain(b => b.EndsWith("WHERE o.Status = 'Failed'"));
    }

    [Fact]
    public void Build_AliasesEachBranchAsO_SoPredicatesCanQualifyColumns()
    {
        Branches(OutboxUnionSql.Build("Id")).Should().OnlyContain(b => b.Contains("].[IntegrationEventOutbox] o"));
    }

    [Fact]
    public void Build_PerModuleWhereClause_ReceivesTheBranchModule()
    {
        var branches = Branches(OutboxUnionSql.Build("Id", module => $"o.X = N'{module}'"));

        for (var i = 0; i < branches.Length; i++)
            branches[i].Should().EndWith($"WHERE o.X = N'{OutboxModuleWhitelist.Modules[i]}'");
    }

    [Fact]
    public void Build_OnlyModule_EmitsASingleBranch()
    {
        var branches = Branches(OutboxUnionSql.Build("Id", onlyModule: "workflow"));

        branches.Should().ContainSingle().Which.Should().Contain("[workflow].[IntegrationEventOutbox]");
    }

    [Fact]
    public void Build_OnlyModule_NotWhitelisted_Throws()
    {
        var act = () => OutboxUnionSql.Build("Id", onlyModule: "request]; DROP TABLE x--");

        act.Should().Throw<FluentValidation.ValidationException>();
    }

    [Fact]
    public void StuckPredicate_IsUsableInBranchAndInOuterQuery()
    {
        // Qualified with the `o` alias that both the branch and the summary's derived table use.
        OutboxUnionSql.StuckPredicate.Should().Contain("o.Status = 'Processing'").And.Contain("@StuckThreshold");
    }
}
