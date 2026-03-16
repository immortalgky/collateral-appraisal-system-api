using System.Text.Json;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine;
using Workflow.Workflow.Schema;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Workflow.Tests.Workflow.Scenarios;

/// <summary>
/// Tests that FlowControlManager routes each activity+decision combination
/// in appraisal-workflow.json to the correct next activity.
/// </summary>
public class AppraisalWorkflowRoutingTests
{
    private static readonly Lazy<WorkflowSchema> CachedSchema = new(LoadWorkflowSchema);
    private readonly FlowControlManager _flowControl;

    public AppraisalWorkflowRoutingTests()
    {
        var logger = Substitute.For<ILogger<FlowControlManager>>();
        _flowControl = new FlowControlManager(logger);
    }

    private static WorkflowSchema LoadWorkflowSchema()
    {
        var jsonPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "Modules", "Workflow", "Workflow", "Workflow", "Config", "appraisal-workflow.json");

        var json = File.ReadAllText(Path.GetFullPath(jsonPath));

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        var wrapper = JsonSerializer.Deserialize<JsonElement>(json, options);
        var schemaElement = wrapper.GetProperty("workflowSchema");
        var schemaOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        return JsonSerializer.Deserialize<WorkflowSchema>(schemaElement.GetRawText(), schemaOptions)!;
    }

    private async Task<string?> Route(string currentActivityId, Dictionary<string, object> variables)
    {
        var schema = CachedSchema.Value;
        var result = ActivityResult.Success();
        return await _flowControl.DetermineNextActivityAsync(schema, currentActivityId, result, variables);
    }

    // ── Routing & Admin (6 tests) ──

    [Fact]
    public async Task Start_NormalTransition_GoesToInitialRouting()
    {
        var next = await Route("start", new Dictionary<string, object>());
        next.Should().Be("initial-routing");
    }

    [Theory]
    [InlineData("admin_review")]
    [InlineData("auto_assign_external")]
    public async Task InitialRouting_AnyDecision_GoesToAdmin(string decision)
    {
        var next = await Route("initial-routing", new Dictionary<string, object>
        {
            ["decision"] = decision
        });
        next.Should().Be("admin");
    }

    [Fact]
    public async Task Admin_RequestMoreInfo_GoesToRequestMaker()
    {
        var next = await Route("admin", new Dictionary<string, object>
        {
            ["decision"] = "request_more_info"
        });
        next.Should().Be("request-maker");
    }

    [Fact]
    public async Task Admin_Reject_GoesToWorkflowRejected()
    {
        var next = await Route("admin", new Dictionary<string, object>
        {
            ["decision"] = "reject"
        });
        next.Should().Be("workflow-rejected");
    }

    [Fact]
    public async Task RequestMaker_NormalTransition_GoesToAdmin()
    {
        var next = await Route("request-maker", new Dictionary<string, object>());
        next.Should().Be("admin");
    }

    // ── Admin branching (2 tests) ──

    [Theory]
    [InlineData("external", "ext-appraisal-staff")]
    [InlineData("internal", "int-appraisal-staff")]
    public async Task Admin_Proceed_RoutesToPathBasedActivity(string routingPath, string expectedNext)
    {
        var next = await Route("admin", new Dictionary<string, object>
        {
            ["decision"] = "proceed",
            ["routingPath"] = routingPath
        });
        next.Should().Be(expectedNext);
    }

    // ── External chain — forward (3 tests) ──

    [Theory]
    [InlineData("ext-appraisal-staff", "ext-appraisal-checker")]
    [InlineData("ext-appraisal-checker", "ext-appraisal-verifier")]
    [InlineData("ext-appraisal-verifier", "int-appraisal-staff")]
    public async Task ExternalChain_Proceed_RoutesToNextActivity(string from, string expectedNext)
    {
        var next = await Route(from, new Dictionary<string, object>
        {
            ["decision"] = "proceed"
        });
        next.Should().Be(expectedNext);
    }

    // ── External chain — rollback (3 tests) ──

    [Theory]
    [InlineData("ext-appraisal-staff", "admin")]
    [InlineData("ext-appraisal-checker", "ext-appraisal-staff")]
    [InlineData("ext-appraisal-verifier", "ext-appraisal-checker")]
    public async Task ExternalChain_RouteBack_RoutesToPreviousActivity(string from, string expectedNext)
    {
        var next = await Route(from, new Dictionary<string, object>
        {
            ["decision"] = "route_back"
        });
        next.Should().Be(expectedNext);
    }

    // ── Internal chain — forward (3 tests) ──

    [Theory]
    [InlineData("int-appraisal-staff", "int-appraisal-checker")]
    [InlineData("int-appraisal-checker", "int-appraisal-verifier")]
    [InlineData("int-appraisal-verifier", "pending-approval")]
    public async Task InternalChain_Proceed_RoutesToNextActivity(string from, string expectedNext)
    {
        var next = await Route(from, new Dictionary<string, object>
        {
            ["decision"] = "proceed"
        });
        next.Should().Be(expectedNext);
    }

    // ── Internal chain — rollback (2 tests) ──

    [Theory]
    [InlineData("int-appraisal-checker", "int-appraisal-staff")]
    [InlineData("int-appraisal-verifier", "int-appraisal-checker")]
    public async Task InternalChain_RouteBack_RoutesToPreviousActivity(string from, string expectedNext)
    {
        var next = await Route(from, new Dictionary<string, object>
        {
            ["decision"] = "route_back"
        });
        next.Should().Be(expectedNext);
    }

    // ── Path-aware int-staff rollback (2 tests) ──

    [Theory]
    [InlineData("external", "ext-appraisal-staff")]
    [InlineData("internal", "admin")]
    public async Task IntAppraisalStaff_RouteBack_RoutesBasedOnPath(string routingPath, string expectedNext)
    {
        var next = await Route("int-appraisal-staff", new Dictionary<string, object>
        {
            ["decision"] = "route_back",
            ["routingPath"] = routingPath
        });
        next.Should().Be(expectedNext);
    }

    // ── Approval (2 tests) ──

    [Fact]
    public async Task PendingApproval_Approve_GoesToWorkflowCompleted()
    {
        var next = await Route("pending-approval", new Dictionary<string, object>
        {
            ["decision"] = "approve"
        });
        next.Should().Be("workflow-completed");
    }

    [Fact]
    public async Task PendingApproval_RouteBack_GoesToIntAppraisalStaff()
    {
        var next = await Route("pending-approval", new Dictionary<string, object>
        {
            ["decision"] = "route_back"
        });
        next.Should().Be("int-appraisal-staff");
    }
}
