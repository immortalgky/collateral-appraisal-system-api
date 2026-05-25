using Common.Application.Features.Monitoring.Shared;
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
    public void Internal_Staff_ResolvesToIntAppraisalExecution()
    {
        var ids = MonitoringActivityMap.Internal["staff"];
        ids.Should().ContainSingle().Which.Should().Be("int-appraisal-execution");
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
    public void Internal_Approver_ResolvesToAppraisalBookVerification()
    {
        var ids = MonitoringActivityMap.Internal["approver"];
        ids.Should().ContainSingle().Which.Should().Be("appraisal-book-verification");
    }

    [Fact]
    public void Internal_Admin_ResolvesToFrontOfFunnelActivities()
    {
        var ids = MonitoringActivityMap.Internal["admin"];
        ids.Should().BeEquivalentTo(
            new[] { "appraisal-initiation-check", "appraisal-initiation", "appraisal-assignment" });
    }

    // ── External map ──────────────────────────────────────────────────────────

    [Fact]
    public void External_AppraisalStaff_ResolvesToExtAppraisalExecution()
    {
        var ids = MonitoringActivityMap.External["appraisal-staff"];
        ids.Should().ContainSingle().Which.Should().Be("ext-appraisal-execution");
    }

    [Fact]
    public void External_AppraisalChecker_ResolvesToExtAppraisalCheck()
    {
        var ids = MonitoringActivityMap.External["appraisal-checker"];
        ids.Should().ContainSingle().Which.Should().Be("ext-appraisal-check");
    }

    [Fact]
    public void External_AppraisalVerifier_ResolvesToExtAppraisalVerification()
    {
        var ids = MonitoringActivityMap.External["appraisal-verifier"];
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
        var currentUser = new FakeCurrentUserService(["monitoring:pending-internal:checker"]);
        var service = new MonitoringScopeService(currentUser);

        var ids = service.GetInternalActivityIds();

        ids.Should().ContainSingle().Which.Should().Be("int-appraisal-check");
    }

    [Fact]
    public void ScopeService_WhenUserHasNoInternalPermission_ReturnsEmpty()
    {
        var currentUser = new FakeCurrentUserService(["monitoring:pending-external:admin"]);
        var service = new MonitoringScopeService(currentUser);

        var ids = service.GetInternalActivityIds();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void ScopeService_WhenUserHasMultipleLayers_ReturnsUnionOfActivityIds()
    {
        var currentUser = new FakeCurrentUserService([
            "monitoring:pending-internal:checker",
            "monitoring:pending-internal:verifier"
        ]);
        var service = new MonitoringScopeService(currentUser);

        var ids = service.GetInternalActivityIds();

        ids.Should().BeEquivalentTo(new[] { "int-appraisal-check", "int-appraisal-verification" });
    }
}

/// <summary>
/// Minimal ICurrentUserService stub for unit tests — avoids full DI setup.
/// </summary>
internal sealed class FakeCurrentUserService(IEnumerable<string> permissions) : Shared.Identity.ICurrentUserService
{
    private readonly IReadOnlyList<string> _permissions = permissions.ToList().AsReadOnly();

    public Guid? UserId => null;
    public string? Username => "test";
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
