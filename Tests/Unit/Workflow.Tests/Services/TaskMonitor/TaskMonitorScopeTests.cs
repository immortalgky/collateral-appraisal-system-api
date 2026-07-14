using System.Linq;
using Dapper;
using FluentAssertions;
using NSubstitute;
using Shared.Data;
using Shared.Identity;
using Workflow.Services.TaskMonitor;
using Xunit;

namespace Workflow.Tests.Services.TaskMonitor;

public class TeamScopePredicateTests
{
    private static ICurrentUserService User(bool isExternal, Guid? companyId, string? username, params string[] permissions)
    {
        var u = Substitute.For<ICurrentUserService>();
        u.IsExternal.Returns(isExternal);
        u.CompanyId.Returns(companyId);
        u.Username.Returns(username);
        u.Permissions.Returns(permissions);
        return u;
    }

    [Fact]
    public void Build_ExternalUser_ScopesByCompanyId()
    {
        var companyId = Guid.NewGuid();
        var p = new DynamicParameters();

        var sql = TeamScopePredicate.Build(User(isExternal: true, companyId, username: "ext.admin1"), p);

        sql.Should().Contain("auth.AspNetUsers");
        sql.Should().Contain("CompanyId = @ScopeCompanyId");
        p.ParameterNames.Should().Contain("ScopeCompanyId");
        p.Get<Guid>("ScopeCompanyId").Should().Be(companyId);
    }

    [Fact]
    public void Build_InternalUser_ScopesByTeamMembership()
    {
        var p = new DynamicParameters();

        var sql = TeamScopePredicate.Build(User(isExternal: false, companyId: null, username: "int.chk1"), p);

        sql.Should().Contain("auth.TeamMembers");
        sql.Should().Contain("@MeNorm");
        p.Get<string>("MeNorm").Should().Be("INT.CHK1");
    }

    [Fact]
    public void Build_ExternalUserWithoutCompanyId_FailsClosed()
    {
        var p = new DynamicParameters();

        var sql = TeamScopePredicate.Build(User(isExternal: true, companyId: null, username: "ext.admin1"), p);

        sql.Should().Be("1 = 0");
    }

    [Fact]
    public void Build_InternalUserWithoutUsername_FailsClosed()
    {
        var p = new DynamicParameters();

        var sql = TeamScopePredicate.Build(User(isExternal: false, companyId: null, username: null), p);

        sql.Should().Be("1 = 0");
    }

    [Fact]
    public void Build_SameUserAcrossCalls_RegistersIdentityParamOnce()
    {
        var p = new DynamicParameters();
        var user = User(isExternal: false, companyId: null, username: "int.chk1");

        TeamScopePredicate.Build(user, p);
        TeamScopePredicate.Build(user, p);

        p.ParameterNames.Count(n => n == "MeNorm").Should().Be(1);
    }
}

public class TaskMonitorScopeTests
{
    private const string Base = "TASK_MONITOR_VIEW";
    private const string Team = "TASK_MONITOR_VIEW:TEAM";

    private static TaskMonitorScope BuildScope(ICurrentUserService user)
        => new(user, Substitute.For<ISqlConnectionFactory>());

    private static ICurrentUserService UserWithPermissions(params string[] permissions)
    {
        var u = Substitute.For<ICurrentUserService>();
        u.Permissions.Returns(permissions);
        return u;
    }

    [Fact]
    public void IsTeamScoped_TeamVariantOnly_True()
        => BuildScope(UserWithPermissions(Team)).IsTeamScoped(Base).Should().BeTrue();

    [Fact]
    public void IsTeamScoped_BaseOnly_False()
        => BuildScope(UserWithPermissions(Base)).IsTeamScoped(Base).Should().BeFalse();

    [Fact]
    public void IsTeamScoped_BothVariants_BaseWins_False()
        => BuildScope(UserWithPermissions(Base, Team)).IsTeamScoped(Base).Should().BeFalse();

    [Fact]
    public void IsTeamScoped_NeitherVariant_False()
        => BuildScope(UserWithPermissions("SOMETHING_ELSE")).IsTeamScoped(Base).Should().BeFalse();

    [Fact]
    public void BuildScopeClause_NotTeamScoped_ReturnsNull()
        => BuildScope(UserWithPermissions(Base)).BuildScopeClause(Base, new DynamicParameters()).Should().BeNull();

    [Fact]
    public void BuildScopeClause_TeamScopedExternal_ReturnsCompanyPredicate()
    {
        var u = Substitute.For<ICurrentUserService>();
        u.Permissions.Returns([Team]);
        u.IsExternal.Returns(true);
        u.CompanyId.Returns(Guid.NewGuid());

        var clause = BuildScope(u).BuildScopeClause(Base, new DynamicParameters());

        clause.Should().NotBeNull();
        clause.Should().Contain("CompanyId = @ScopeCompanyId");
    }
}
