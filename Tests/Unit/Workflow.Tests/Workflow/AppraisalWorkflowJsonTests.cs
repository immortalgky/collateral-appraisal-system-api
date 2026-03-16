using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Workflow.Tests.Workflow;

/// <summary>
/// Validates the structural integrity of appraisal-workflow.json:
/// all transitions reference existing activities and path-aware conditions are correct.
/// </summary>
public class AppraisalWorkflowJsonTests
{
    private static readonly JsonDocument WorkflowDoc = LoadWorkflowJson();

    private static JsonDocument LoadWorkflowJson()
    {
        var jsonPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "Modules", "Workflow", "Workflow", "Workflow", "Config", "appraisal-workflow.json");

        var fullPath = Path.GetFullPath(jsonPath);
        var json = File.ReadAllText(fullPath);
        return JsonDocument.Parse(json);
    }

    private static HashSet<string> GetActivityIds()
    {
        return WorkflowDoc.RootElement
            .GetProperty("workflowSchema")
            .GetProperty("activities")
            .EnumerateArray()
            .Select(a => a.GetProperty("id").GetString()!)
            .ToHashSet();
    }

    private static JsonElement[] GetTransitions()
    {
        return WorkflowDoc.RootElement
            .GetProperty("workflowSchema")
            .GetProperty("transitions")
            .EnumerateArray()
            .ToArray();
    }

    [Fact]
    public void AllTransitionTargets_ReferenceExistingActivities()
    {
        var activityIds = GetActivityIds();
        var transitions = GetTransitions();

        foreach (var t in transitions)
        {
            var from = t.GetProperty("from").GetString()!;
            var to = t.GetProperty("to").GetString()!;
            var id = t.GetProperty("id").GetString()!;

            activityIds.Should().Contain(from,
                $"transition '{id}' references non-existent source activity '{from}'");
            activityIds.Should().Contain(to,
                $"transition '{id}' references non-existent target activity '{to}'");
        }
    }

    [Fact]
    public void AllActivities_HaveAtLeastOneTransition()
    {
        var activityIds = GetActivityIds();
        var transitions = GetTransitions();

        var activitiesWithTransitions = transitions
            .SelectMany(t => new[]
            {
                t.GetProperty("from").GetString()!,
                t.GetProperty("to").GetString()!
            })
            .ToHashSet();

        foreach (var activityId in activityIds)
        {
            activitiesWithTransitions.Should().Contain(activityId,
                $"activity '{activityId}' has no transitions (not referenced as from or to)");
        }
    }

    [Fact]
    public void AdminActivity_HasPathAwareProceedTransitions()
    {
        var transitions = GetTransitions();
        var adminProceed = transitions
            .Where(t => t.GetProperty("from").GetString() == "admin")
            .Where(t =>
            {
                var condition = t.GetProperty("condition").GetString();
                return condition != null && condition.Contains("proceed");
            })
            .ToList();

        adminProceed.Should().HaveCount(2, "admin should have two proceed transitions (external + internal)");

        var conditions = adminProceed
            .Select(t => t.GetProperty("condition").GetString()!)
            .ToList();

        conditions.Should().Contain(c => c.Contains("routingPath == 'external'"));
        conditions.Should().Contain(c => c.Contains("routingPath == 'internal'"));
    }

    [Fact]
    public void IntAppraisalStaff_HasPathAwareRollbackTransitions()
    {
        var transitions = GetTransitions();
        var intStaffRollback = transitions
            .Where(t => t.GetProperty("from").GetString() == "int-appraisal-staff")
            .Where(t =>
            {
                var condition = t.GetProperty("condition").GetString();
                return condition != null && condition.Contains("route_back");
            })
            .ToList();

        intStaffRollback.Should().HaveCount(2,
            "int-appraisal-staff should have two rollback transitions (external→ext-staff, internal→admin)");

        var targets = intStaffRollback
            .Select(t => t.GetProperty("to").GetString()!)
            .ToList();

        targets.Should().Contain("ext-appraisal-staff");
        targets.Should().Contain("admin");
    }

    [Fact]
    public void ExternalChain_IsFullyConnected()
    {
        var transitions = GetTransitions();

        AssertTransitionExists(transitions, "ext-appraisal-staff", "ext-appraisal-checker", "proceed");
        AssertTransitionExists(transitions, "ext-appraisal-checker", "ext-appraisal-verifier", "proceed");
        AssertTransitionExists(transitions, "ext-appraisal-verifier", "int-appraisal-staff", "proceed");

        // Rollbacks
        AssertTransitionExists(transitions, "ext-appraisal-checker", "ext-appraisal-staff", "route_back");
        AssertTransitionExists(transitions, "ext-appraisal-verifier", "ext-appraisal-checker", "route_back");
        AssertTransitionExists(transitions, "ext-appraisal-staff", "admin", "route_back");
    }

    [Fact]
    public void InternalChain_IsFullyConnected()
    {
        var transitions = GetTransitions();

        AssertTransitionExists(transitions, "int-appraisal-staff", "int-appraisal-checker", "proceed");
        AssertTransitionExists(transitions, "int-appraisal-checker", "int-appraisal-verifier", "proceed");
        AssertTransitionExists(transitions, "int-appraisal-verifier", "pending-approval", "proceed");

        // Rollbacks
        AssertTransitionExists(transitions, "int-appraisal-checker", "int-appraisal-staff", "route_back");
        AssertTransitionExists(transitions, "int-appraisal-verifier", "int-appraisal-checker", "route_back");
    }

    [Fact]
    public void PendingApproval_CanApproveOrRollback()
    {
        var transitions = GetTransitions();

        AssertTransitionExists(transitions, "pending-approval", "workflow-completed", "approve");
        AssertTransitionExists(transitions, "pending-approval", "int-appraisal-staff", "route_back");
    }

    [Fact]
    public void WorkflowVariables_ContainsRoutingPath()
    {
        var variables = WorkflowDoc.RootElement
            .GetProperty("workflowSchema")
            .GetProperty("variables");

        variables.TryGetProperty("routingPath", out _).Should().BeTrue(
            "workflow variables should include routingPath for path-aware transitions");
    }

    private static void AssertTransitionExists(
        JsonElement[] transitions, string from, string to, string conditionContains)
    {
        var match = transitions.Any(t =>
            t.GetProperty("from").GetString() == from &&
            t.GetProperty("to").GetString() == to &&
            (t.GetProperty("condition").GetString() ?? "").Contains(conditionContains));

        match.Should().BeTrue(
            $"expected transition from '{from}' to '{to}' with condition containing '{conditionContains}'");
    }
}
