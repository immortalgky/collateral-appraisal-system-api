using Common.Application.Features.Monitoring.Shared;
using Dapper;
using FluentAssertions;

namespace Common.Tests.Monitoring;

/// <summary>
/// Unit tests for MonitoringActivityMap — verifies that each layer suffix
/// resolves to the expected activity IDs.
/// </summary>
public class MonitoringActivityMapTests
{
    // ── Internal map ──────────────────────────────────────────────────────────

    [Fact]
    public void Internal_Staff_ResolvesToExecutionAndBookVerification()
    {
        var ids = MonitoringActivityMap.Internal["staff"];
        ids.Should().BeEquivalentTo(new[] { "int-appraisal-execution", "appraisal-book-verification" });
    }

    [Fact]
    public void Internal_Checker_ResolvesToIntAppraisalCheck()
    {
        var ids = MonitoringActivityMap.Internal["checker"];
        ids.Should().ContainSingle().Which.Should().Be("int-appraisal-check");
    }

    [Fact]
    public void Internal_Verifier_ResolvesToIntAppraisalVerification()
    {
        var ids = MonitoringActivityMap.Internal["verifier"];
        ids.Should().ContainSingle().Which.Should().Be("int-appraisal-verification");
    }

    [Fact]
    public void Internal_Approver_ResolvesToPendingApproval()
    {
        var ids = MonitoringActivityMap.Internal["approver"];
        ids.Should().ContainSingle().Which.Should().Be("pending-approval");
    }

    [Fact]
    public void Internal_Admin_ResolvesToAppraisalAssignment()
    {
        var ids = MonitoringActivityMap.Internal["admin"];
        ids.Should().ContainSingle().Which.Should().Be("appraisal-assignment");
    }

    // ── External map ──────────────────────────────────────────────────────────

    [Fact]
    public void External_Staff_ResolvesToExtAppraisalExecution()
    {
        var ids = MonitoringActivityMap.External["staff"];
        ids.Should().ContainSingle().Which.Should().Be("ext-appraisal-execution");
    }

    [Fact]
    public void External_Checker_ResolvesToExtAppraisalCheck()
    {
        var ids = MonitoringActivityMap.External["checker"];
        ids.Should().ContainSingle().Which.Should().Be("ext-appraisal-check");
    }

    [Fact]
    public void External_Verifier_ResolvesToExtAppraisalVerification()
    {
        var ids = MonitoringActivityMap.External["verifier"];
        ids.Should().ContainSingle().Which.Should().Be("ext-appraisal-verification");
    }

    [Fact]
    public void External_Admin_ResolvesToExtAppraisalAssignment()
    {
        var ids = MonitoringActivityMap.External["admin"];
        ids.Should().ContainSingle().Which.Should().Be("ext-appraisal-assignment");
    }

    // ── MonitoringScopeService ─────────────────────────────────────────────────

    [Fact]
    public void ScopeService_WhenUserHasCheckerPermission_ReturnsIntAppraisalCheck()
    {
        var currentUser = new FakeCurrentUserService(["MONITORING:PENDING_INTERNAL:CHECKER"]);
        var service = new MonitoringScopeService(currentUser);

        var scope = service.ResolveInternalScope();

        scope.AllActivityIds.Should().ContainSingle().Which.Should().Be("int-appraisal-check");
        scope.TeamActivityIds.Should().BeEmpty();
    }

    [Fact]
    public void ScopeService_WhenUserHasNoInternalPermission_ReturnsEmpty()
    {
        var currentUser = new FakeCurrentUserService(["MONITORING:PENDING_EXTERNAL:ADMIN"]);
        var service = new MonitoringScopeService(currentUser);

        service.ResolveInternalScope().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ScopeService_WhenUserHasMultipleLayers_ReturnsUnionOfActivityIds()
    {
        var currentUser = new FakeCurrentUserService([
            "MONITORING:PENDING_INTERNAL:CHECKER",
            "MONITORING:PENDING_INTERNAL:VERIFIER"
        ]);
        var service = new MonitoringScopeService(currentUser);

        var scope = service.ResolveInternalScope();

        scope.AllActivityIds.Should().BeEquivalentTo(new[] { "int-appraisal-check", "int-appraisal-verification" });
        scope.TeamActivityIds.Should().BeEmpty();
    }

    [Fact]
    public void ScopeService_WhenLayerHasTeamModifier_RoutesActivityToTeamScope()
    {
        var currentUser = new FakeCurrentUserService(["MONITORING:PENDING_INTERNAL:CHECKER:TEAM"]);
        var service = new MonitoringScopeService(currentUser);

        var scope = service.ResolveInternalScope();

        scope.AllActivityIds.Should().BeEmpty();
        scope.TeamActivityIds.Should().ContainSingle().Which.Should().Be("int-appraisal-check");
    }

    [Fact]
    public void ScopeService_WhenLayerGrantedBothAllAndTeam_AllWins()
    {
        var currentUser = new FakeCurrentUserService([
            "MONITORING:PENDING_INTERNAL:CHECKER",
            "MONITORING:PENDING_INTERNAL:CHECKER:TEAM"
        ]);
        var service = new MonitoringScopeService(currentUser);

        var scope = service.ResolveInternalScope();

        scope.AllActivityIds.Should().ContainSingle().Which.Should().Be("int-appraisal-check");
        scope.TeamActivityIds.Should().BeEmpty();
    }

    // ── BuildActivityScopeSql (SQL-fragment + parameter registration) ──────────

    [Fact]
    public void BuildActivityScopeSql_AllScopeOnly_EmitsParenthesizedClauseWithoutMeNorm()
    {
        var service = new MonitoringScopeService(new FakeCurrentUserService([]));
        var scope = new MonitoringScope(["a", "b"], []);
        var parameters = new DynamicParameters();

        var sql = service.BuildActivityScopeSql(scope, parameters);

        sql.Should().Be("(ActivityId IN @AllActivityIds)");
        parameters.ParameterNames.Should().Contain("AllActivityIds");
        parameters.ParameterNames.Should().NotContain("MeNorm");
    }

    [Fact]
    public void BuildActivityScopeSql_TeamScope_RegistersMeNormOnceAcrossSuffixedBranches()
    {
        var service = new MonitoringScopeService(new FakeCurrentUserService([], "alice"));
        var parameters = new DynamicParameters();

        var internalSql = service.BuildActivityScopeSql(new MonitoringScope([], ["x"]), parameters, "Internal");
        var externalSql = service.BuildActivityScopeSql(new MonitoringScope([], ["y"]), parameters, "External");

        internalSql.Should().Contain("@TeamInternalActivityIds").And.Contain("@MeNorm");
        externalSql.Should().Contain("@TeamExternalActivityIds").And.Contain("@MeNorm");
        // @MeNorm is shared, not suffixed — must be registered exactly once.
        parameters.ParameterNames.Count(n => n == "MeNorm").Should().Be(1);
        parameters.Get<string>("MeNorm").Should().Be("ALICE");
    }

    [Fact]
    public void BuildActivityScopeSql_TeamScopeWithNoUsername_DropsTeamBranchAndFailsClosed()
    {
        var service = new MonitoringScopeService(new FakeCurrentUserService([], username: null));
        var parameters = new DynamicParameters();

        // Team-only scope + no username ⇒ no visible activities (null fragment, no MeNorm leaked).
        var sql = service.BuildActivityScopeSql(new MonitoringScope([], ["x"]), parameters);

        sql.Should().BeNull();
        parameters.ParameterNames.Should().NotContain("MeNorm");
    }
}

/// <summary>
/// Minimal ICurrentUserService stub for unit tests — avoids full DI setup.
/// </summary>
internal sealed class FakeCurrentUserService(IEnumerable<string> permissions, string? username = "test")
    : Shared.Identity.ICurrentUserService
{
    private readonly IReadOnlyList<string> _permissions = permissions.ToList().AsReadOnly();

    public Guid? UserId => null;
    public string? Username => username;
    public string? UserCode => username;
    public bool IsAuthenticated => true;
    public IReadOnlyList<string> Permissions => _permissions;
    public IReadOnlyList<string> Roles => [];
    public Guid? CompanyId => null;
    public bool IsExternal => CompanyId.HasValue;

    public bool HasPermission(string permission) =>
        _permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    public bool HasAnyPermission(params string[] permissions) =>
        permissions.Any(p => _permissions.Contains(p, StringComparer.OrdinalIgnoreCase));

    public bool HasAllPermissions(params string[] permissions) =>
        permissions.All(p => _permissions.Contains(p, StringComparer.OrdinalIgnoreCase));

    public bool IsInRole(string role) => false;
}
