using Workflow.AssigneeSelection.Core;
using Workflow.AssigneeSelection.Engine;
using Workflow.AssigneeSelection.Pipeline;
using Workflow.AssigneeSelection.Teams;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Workflow.Tests.AssigneeSelection.Pipeline;

/// <summary>
/// Tests the full AssignmentPipeline orchestration:
/// Stage 1 (context build) → Stage 2 (filter) → Stage 3 (select) → Stage 4 (validate) → Stage 5 (finalize).
/// All external dependencies are mocked.
/// </summary>
public class AssignmentPipelineTests
{
    // Dependencies
    private readonly IAssignmentContextBuilder _contextBuilder;
    private readonly ICascadingAssignmentEngine _engine;
    private readonly IAssignmentFinalizer _finalizer;
    private readonly List<IAssignmentFilter> _filters;
    private readonly List<IAssignmentValidator> _validators;

    // Concrete filters & validators (for fine-grained tests)
    private readonly ITeamService _teamService;

    private readonly AssignmentPipeline _pipeline;

    public AssignmentPipelineTests()
    {
        _contextBuilder = Substitute.For<IAssignmentContextBuilder>();
        _engine = Substitute.For<ICascadingAssignmentEngine>();
        _finalizer = Substitute.For<IAssignmentFinalizer>();
        _teamService = Substitute.For<ITeamService>();

        _filters = new List<IAssignmentFilter>();
        _validators = new List<IAssignmentValidator>();

        var logger = Substitute.For<ILogger<AssignmentPipeline>>();

        _pipeline = new AssignmentPipeline(
            _contextBuilder,
            _filters,
            _engine,
            _validators,
            _finalizer,
            logger);
    }

    // ── Helpers ──

    private static ActivityContext CreateActivityContext(
        string activityId = "test-activity",
        Dictionary<string, object>? properties = null,
        Dictionary<string, object>? variables = null,
        RuntimeOverride? runtimeOverride = null)
    {
        var instance = WorkflowInstance.Create(
            Guid.NewGuid(),
            "Test Workflow",
            null,
            "test@company.com",
            variables ?? new Dictionary<string, object>());

        return new ActivityContext
        {
            WorkflowInstanceId = instance.Id,
            ActivityId = activityId,
            Properties = properties ?? new Dictionary<string, object>(),
            Variables = variables ?? new Dictionary<string, object>(),
            WorkflowInstance = instance,
            RuntimeOverrides = runtimeOverride
        };
    }

    private static TeamMemberInfo Member(string userId, string teamId, params string[] activityGroups)
        => new(userId, $"User {userId}", teamId, activityGroups.ToList());

    private void SetupDefaultContextBuilder(AssignmentPipelineContext? overrideCtx = null)
    {
        _contextBuilder.BuildAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var ctx = ci.ArgAt<AssignmentPipelineContext>(0);
                if (overrideCtx != null)
                {
                    ctx.Rules = overrideCtx.Rules;
                    ctx.TeamId = overrideCtx.TeamId;
                    ctx.RuntimeOverride = overrideCtx.RuntimeOverride;
                    ctx.PriorAssignees = overrideCtx.PriorAssignees;
                }
                return Task.CompletedTask;
            });
    }

    private void SetupEngineSuccess(string assigneeId, string strategy = "round_robin")
    {
        _engine.ExecuteAsync(Arg.Any<AssignmentContext>(), Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Success(assigneeId, new Dictionary<string, object>
            {
                ["SuccessfulStrategy"] = strategy,
                ["CascadingStrategies"] = new List<string> { strategy },
                ["StrategyPosition"] = 1
            }));
    }

    private void SetupEngineFailure(string error = "No assignee found")
    {
        _engine.ExecuteAsync(Arg.Any<AssignmentContext>(), Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Failure(error));
    }

    private void SetupFinalizerPassthrough()
    {
        _finalizer.FinalizeAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var ctx = ci.ArgAt<AssignmentPipelineContext>(0);
                return new AssignmentResult
                {
                    IsSuccess = true,
                    AssigneeId = ctx.SelectedAssignee ?? "Unassigned",
                    Strategy = ctx.SelectionStrategy ?? "Pipeline",
                    Metadata = new Dictionary<string, object> { ["pipeline"] = true }
                };
            });
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. BASIC PIPELINE FLOW
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task AssignAsync_HappyPath_ReturnsSuccessWithAssignee()
    {
        // Arrange
        var context = CreateActivityContext(properties: new Dictionary<string, object>
        {
            ["assignmentStrategy"] = "round_robin",
            ["assigneeGroup"] = "Admin"
        });

        SetupDefaultContextBuilder();
        SetupEngineSuccess("user-001", "round_robin");
        SetupFinalizerPassthrough();

        // Act
        var result = await _pipeline.AssignAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("user-001");
    }

    [Fact]
    public async Task AssignAsync_InvokesAllStagesInOrder()
    {
        var context = CreateActivityContext();
        SetupDefaultContextBuilder();
        SetupEngineSuccess("user-001");
        SetupFinalizerPassthrough();

        await _pipeline.AssignAsync(context);

        // Stage 1: context builder called
        await _contextBuilder.Received(1).BuildAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>());
        // Stage 3: engine called
        await _engine.Received(1).ExecuteAsync(Arg.Any<AssignmentContext>(), Arg.Any<CancellationToken>());
        // Stage 5: finalizer called
        await _finalizer.Received(1).FinalizeAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_EngineFailure_ReturnsFailureWithoutFinalizer()
    {
        var context = CreateActivityContext();
        SetupDefaultContextBuilder();
        SetupEngineFailure("All strategies failed");

        var result = await _pipeline.AssignAsync(context);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("All strategies failed");
        await _finalizer.DidNotReceive().FinalizeAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>());
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. RUNTIME OVERRIDE — manual pick bypasses engine
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task AssignAsync_RuntimeOverrideAssignee_BypassesEngine()
    {
        var runtimeOverride = RuntimeOverride.ForAssignee("manual-user", "Manager pick", "admin");
        var context = CreateActivityContext(runtimeOverride: runtimeOverride);

        _contextBuilder.BuildAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var ctx = ci.ArgAt<AssignmentPipelineContext>(0);
                ctx.RuntimeOverride = runtimeOverride;
                return Task.CompletedTask;
            });

        SetupFinalizerPassthrough();

        var result = await _pipeline.AssignAsync(context);

        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("manual-user");
        // Engine should NOT be called since manual pick takes priority
        await _engine.DidNotReceive().ExecuteAsync(Arg.Any<AssignmentContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_RuntimeOverrideStrategies_PassesToEngine()
    {
        var runtimeOverride = RuntimeOverride.ForStrategies(
            new List<string> { "workload_based" }, "Custom strategy", "admin");
        var context = CreateActivityContext(runtimeOverride: runtimeOverride);

        _contextBuilder.BuildAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var ctx = ci.ArgAt<AssignmentPipelineContext>(0);
                ctx.RuntimeOverride = runtimeOverride;
                return Task.CompletedTask;
            });

        SetupEngineSuccess("user-wl", "workload_based");
        SetupFinalizerPassthrough();

        var result = await _pipeline.AssignAsync(context);

        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("user-wl");

        // Verify engine was called with overridden strategy
        await _engine.Received(1).ExecuteAsync(
            Arg.Is<AssignmentContext>(c => c.AssignmentStrategies.Contains("workload_based")),
            Arg.Any<CancellationToken>());
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. STRATEGY RESOLUTION FROM PROPERTIES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task AssignAsync_NoStrategyInProperties_DefaultsToRoundRobinAndWorkload()
    {
        var context = CreateActivityContext(); // No assignmentStrategy property
        SetupDefaultContextBuilder();
        SetupEngineSuccess("user-default");
        SetupFinalizerPassthrough();

        await _pipeline.AssignAsync(context);

        await _engine.Received(1).ExecuteAsync(
            Arg.Is<AssignmentContext>(c =>
                c.AssignmentStrategies.Contains("round_robin") &&
                c.AssignmentStrategies.Contains("workload_based") &&
                c.AssignmentStrategies.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_SingleStrategyInProperties_UsesIt()
    {
        var context = CreateActivityContext(properties: new Dictionary<string, object>
        {
            ["assignmentStrategy"] = "manual"
        });
        SetupDefaultContextBuilder();
        SetupEngineSuccess("user-manual", "manual");
        SetupFinalizerPassthrough();

        await _pipeline.AssignAsync(context);

        await _engine.Received(1).ExecuteAsync(
            Arg.Is<AssignmentContext>(c =>
                c.AssignmentStrategies.Count == 1 &&
                c.AssignmentStrategies[0] == "manual"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_StrategyListInProperties_UsesAll()
    {
        var strategies = new List<string> { "previous_owner", "round_robin", "workload_based" };
        var context = CreateActivityContext(properties: new Dictionary<string, object>
        {
            ["assignmentStrategies"] = strategies
        });
        SetupDefaultContextBuilder();
        SetupEngineSuccess("user-cascaded", "round_robin");
        SetupFinalizerPassthrough();

        await _pipeline.AssignAsync(context);

        await _engine.Received(1).ExecuteAsync(
            Arg.Is<AssignmentContext>(c => c.AssignmentStrategies.SequenceEqual(strategies)),
            Arg.Any<CancellationToken>());
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. FILTER STAGES (using real filter implementations)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task TeamFilter_NotTeamConstrained_LoadsAllMembers()
    {
        var teamFilter = new TeamFilter(_teamService, Substitute.For<ILogger<TeamFilter>>());
        var members = new List<TeamMemberInfo>
        {
            Member("u1", "team-A", "TestRole"),
            Member("u2", "team-B", "TestRole")
        };

        _teamService.GetAllMembersForActivityAsync("TestRole", Arg.Any<CancellationToken>())
            .Returns(members);

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = CreateActivityContext(properties: new Dictionary<string, object>
            {
                ["assigneeGroup"] = "TestRole"
            }),
            Rules = new ActivityAssignmentRules(TeamConstrained: false, ExcludeAssigneesFrom: [])
        };

        var result = await teamFilter.FilterAsync(ctx, new List<TeamMemberInfo>());

        result.Should().HaveCount(2);
        await _teamService.Received(1).GetAllMembersForActivityAsync("TestRole", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TeamFilter_TeamConstrained_WithTeamId_ScopesToTeam()
    {
        var teamFilter = new TeamFilter(_teamService, Substitute.For<ILogger<TeamFilter>>());
        var teamMembers = new List<TeamMemberInfo>
        {
            Member("u1", "team-A", "TestRole")
        };

        _teamService.GetTeamMembersForActivityAsync("team-A", "TestRole", Arg.Any<CancellationToken>())
            .Returns(teamMembers);

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = CreateActivityContext(properties: new Dictionary<string, object>
            {
                ["assigneeGroup"] = "TestRole"
            }),
            Rules = new ActivityAssignmentRules(TeamConstrained: true, ExcludeAssigneesFrom: []),
            TeamId = "team-A"
        };

        var result = await teamFilter.FilterAsync(ctx, new List<TeamMemberInfo>());

        result.Should().HaveCount(1);
        result[0].UserId.Should().Be("u1");
    }

    [Fact]
    public async Task TeamFilter_TeamConstrained_NoTeamIdYet_LoadsAllMembers()
    {
        var teamFilter = new TeamFilter(_teamService, Substitute.For<ILogger<TeamFilter>>());
        var allMembers = new List<TeamMemberInfo>
        {
            Member("u1", "team-A", "TestRole"),
            Member("u2", "team-B", "TestRole")
        };

        _teamService.GetAllMembersForActivityAsync("TestRole", Arg.Any<CancellationToken>())
            .Returns(allMembers);

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = CreateActivityContext(properties: new Dictionary<string, object>
            {
                ["assigneeGroup"] = "TestRole"
            }),
            Rules = new ActivityAssignmentRules(TeamConstrained: true, ExcludeAssigneesFrom: []),
            TeamId = null // Not set yet
        };

        var result = await teamFilter.FilterAsync(ctx, new List<TeamMemberInfo>());

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExclusionFilter_ExcludesPriorAssignees()
    {
        var exclusionFilter = new ExclusionFilter(Substitute.For<ILogger<ExclusionFilter>>());

        var candidates = new List<TeamMemberInfo>
        {
            Member("u1", "t", "a"),
            Member("u2", "t", "a"),
            Member("u3", "t", "a")
        };

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = CreateActivityContext(),
            Rules = new ActivityAssignmentRules(false, ["ext-appraisal-staff"]),
            PriorAssignees = new Dictionary<string, string>
            {
                ["ext-appraisal-staff"] = "u2"
            }
        };

        var result = await exclusionFilter.FilterAsync(ctx, candidates);

        result.Should().HaveCount(2);
        result.Should().NotContain(c => c.UserId == "u2");
    }

    [Fact]
    public async Task ExclusionFilter_NoExclusionRules_ReturnsSameCandidates()
    {
        var exclusionFilter = new ExclusionFilter(Substitute.For<ILogger<ExclusionFilter>>());

        var candidates = new List<TeamMemberInfo>
        {
            Member("u1", "t", "a"),
            Member("u2", "t", "a")
        };

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = CreateActivityContext(),
            Rules = ActivityAssignmentRules.Default
        };

        var result = await exclusionFilter.FilterAsync(ctx, candidates);

        result.Should().HaveCount(2);
    }

    // ActivityRoleFilter removed — group membership filtering is now handled by
    // CompanyTeamService SQL queries (auth.Groups/auth.GroupUsers joins).

    // ═══════════════════════════════════════════════════════════════
    // 5. VALIDATORS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task TeamMembershipValidator_NotTeamConstrained_AlwaysValid()
    {
        var validator = new TeamMembershipValidator(_teamService, Substitute.For<ILogger<TeamMembershipValidator>>());

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = CreateActivityContext(),
            Rules = ActivityAssignmentRules.Default,
            SelectedAssignee = "u1"
        };

        var result = await validator.ValidateAsync(ctx);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task TeamMembershipValidator_TeamConstrained_NoTeamYet_Valid()
    {
        var validator = new TeamMembershipValidator(_teamService, Substitute.For<ILogger<TeamMembershipValidator>>());

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = CreateActivityContext(),
            Rules = new ActivityAssignmentRules(true, []),
            SelectedAssignee = "u1",
            TeamId = null
        };

        var result = await validator.ValidateAsync(ctx);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task TeamMembershipValidator_TeamConstrained_MatchesTeam_Valid()
    {
        var validator = new TeamMembershipValidator(_teamService, Substitute.For<ILogger<TeamMembershipValidator>>());

        _teamService.GetTeamForUserAsync("u1", Arg.Any<CancellationToken>())
            .Returns(new TeamInfo("team-A", "Team A", TeamType.Internal, true));

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = CreateActivityContext(),
            Rules = new ActivityAssignmentRules(true, []),
            SelectedAssignee = "u1",
            TeamId = "team-A"
        };

        var result = await validator.ValidateAsync(ctx);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task TeamMembershipValidator_TeamConstrained_WrongTeam_Invalid()
    {
        var validator = new TeamMembershipValidator(_teamService, Substitute.For<ILogger<TeamMembershipValidator>>());

        _teamService.GetTeamForUserAsync("u1", Arg.Any<CancellationToken>())
            .Returns(new TeamInfo("team-B", "Team B", TeamType.Internal, true));

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = CreateActivityContext(),
            Rules = new ActivityAssignmentRules(true, []),
            SelectedAssignee = "u1",
            TeamId = "team-A"
        };

        var result = await validator.ValidateAsync(ctx);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("team");
    }

    [Fact]
    public async Task TeamMembershipValidator_TeamConstrained_NoTeamForUser_Invalid()
    {
        var validator = new TeamMembershipValidator(_teamService, Substitute.For<ILogger<TeamMembershipValidator>>());

        _teamService.GetTeamForUserAsync("u1", Arg.Any<CancellationToken>())
            .Returns((TeamInfo?)null);

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = CreateActivityContext(),
            Rules = new ActivityAssignmentRules(true, []),
            SelectedAssignee = "u1",
            TeamId = "team-A"
        };

        var result = await validator.ValidateAsync(ctx);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("does not belong");
    }

    [Fact]
    public async Task ExclusionRuleValidator_AssigneeNotExcluded_Valid()
    {
        var validator = new ExclusionRuleValidator(Substitute.For<ILogger<ExclusionRuleValidator>>());

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = CreateActivityContext(),
            Rules = new ActivityAssignmentRules(false, ["ext-appraisal-staff"]),
            SelectedAssignee = "u1",
            PriorAssignees = new Dictionary<string, string>
            {
                ["ext-appraisal-staff"] = "u2" // u2 is excluded, not u1
            }
        };

        var result = await validator.ValidateAsync(ctx);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExclusionRuleValidator_AssigneeIsExcluded_Invalid()
    {
        var validator = new ExclusionRuleValidator(Substitute.For<ILogger<ExclusionRuleValidator>>());

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = CreateActivityContext(),
            Rules = new ActivityAssignmentRules(false, ["ext-appraisal-staff"]),
            SelectedAssignee = "u2",
            PriorAssignees = new Dictionary<string, string>
            {
                ["ext-appraisal-staff"] = "u2"
            }
        };

        var result = await validator.ValidateAsync(ctx);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("excluded");
    }

    [Fact]
    public async Task ExclusionRuleValidator_NoExclusionRules_Valid()
    {
        var validator = new ExclusionRuleValidator(Substitute.For<ILogger<ExclusionRuleValidator>>());

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = CreateActivityContext(),
            Rules = ActivityAssignmentRules.Default,
            SelectedAssignee = "u1"
        };

        var result = await validator.ValidateAsync(ctx);

        result.IsValid.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. VALIDATION RETRY LOOP
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task AssignAsync_ValidationFails_RetriesUpTo3Times()
    {
        var context = CreateActivityContext();
        SetupDefaultContextBuilder();
        SetupFinalizerPassthrough();

        // Engine always succeeds
        _engine.ExecuteAsync(Arg.Any<AssignmentContext>(), Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Success("bad-user"));

        // Add a validator that always fails
        var alwaysFailValidator = Substitute.For<IAssignmentValidator>();
        alwaysFailValidator.ValidateAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>())
            .Returns(AssignmentValidationResult.Invalid("User not eligible"));
        _validators.Add(alwaysFailValidator);

        var result = await _pipeline.AssignAsync(context);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("validation failed after 3 attempts");

        // Engine called 3 times (once per retry)
        await _engine.Received(3).ExecuteAsync(Arg.Any<AssignmentContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_ValidationFailsThenPasses_Succeeds()
    {
        var context = CreateActivityContext();
        SetupDefaultContextBuilder();
        SetupFinalizerPassthrough();

        // Engine returns different users on subsequent calls
        _engine.ExecuteAsync(Arg.Any<AssignmentContext>(), Arg.Any<CancellationToken>())
            .Returns(
                AssigneeSelectionResult.Success("bad-user"),
                AssigneeSelectionResult.Success("good-user"));

        var callCount = 0;
        var flippingValidator = Substitute.For<IAssignmentValidator>();
        flippingValidator.ValidateAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return callCount == 1
                    ? AssignmentValidationResult.Invalid("Not eligible")
                    : AssignmentValidationResult.Valid();
            });
        _validators.Add(flippingValidator);

        var result = await _pipeline.AssignAsync(context);

        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("good-user");
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. EMPTY CANDIDATE POOL WITH TEAM CONSTRAINT
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task AssignAsync_TeamConstrained_EmptyCandidatePool_FailsEarly()
    {
        var context = CreateActivityContext();

        _contextBuilder.BuildAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var ctx = ci.ArgAt<AssignmentPipelineContext>(0);
                ctx.Rules = new ActivityAssignmentRules(TeamConstrained: true, ExcludeAssigneesFrom: []);
                ctx.CandidatePool = []; // Starts empty, filters won't add any
                return Task.CompletedTask;
            });

        var result = await _pipeline.AssignAsync(context);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No eligible candidates");
        // Engine and finalizer should NOT be called
        await _engine.DidNotReceive().ExecuteAsync(Arg.Any<AssignmentContext>(), Arg.Any<CancellationToken>());
        await _finalizer.DidNotReceive().FinalizeAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>());
    }

    // ═══════════════════════════════════════════════════════════════
    // 8. PIPELINE WITH REAL FILTERS (integration-style)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task AssignAsync_WithRealFilters_FiltersCorrectly()
    {
        // Build pipeline with real filters (ActivityRoleFilter removed — group filtering is in SQL)
        var teamFilter = new TeamFilter(_teamService, Substitute.For<ILogger<TeamFilter>>());
        var exclusionFilter = new ExclusionFilter(Substitute.For<ILogger<ExclusionFilter>>());

        var filters = new List<IAssignmentFilter> { teamFilter, exclusionFilter };
        var logger = Substitute.For<ILogger<AssignmentPipeline>>();

        var pipeline = new AssignmentPipeline(
            _contextBuilder, filters, _engine, _validators, _finalizer, logger);

        // Setup: team-constrained with exclusion
        // DB query only returns members in the target group (no wrong-group members)
        var teamMembers = new List<TeamMemberInfo>
        {
            Member("u1", "team-A", "CheckerRole"),
            Member("u2", "team-A", "CheckerRole")
        };

        _teamService.GetTeamMembersForActivityAsync("team-A", "CheckerRole", Arg.Any<CancellationToken>())
            .Returns(teamMembers);

        _contextBuilder.BuildAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var ctx = ci.ArgAt<AssignmentPipelineContext>(0);
                ctx.Rules = new ActivityAssignmentRules(true, ["staff-activity"]);
                ctx.TeamId = "team-A";
                ctx.PriorAssignees = new Dictionary<string, string>
                {
                    ["staff-activity"] = "u1" // Exclude u1
                };
                return Task.CompletedTask;
            });

        SetupEngineSuccess("u2", "round_robin");
        SetupFinalizerPassthrough();

        var context = CreateActivityContext(activityId: "checker-activity", properties: new Dictionary<string, object>
        {
            ["assigneeGroup"] = "CheckerRole"
        });
        var result = await pipeline.AssignAsync(context);

        result.IsSuccess.Should().BeTrue();

        // Verify engine received filtered pool: only u2 should remain
        // (u1 excluded by ExclusionFilter)
        await _engine.Received(1).ExecuteAsync(
            Arg.Is<AssignmentContext>(c => c.CandidatePool != null && c.CandidatePool.Count == 1),
            Arg.Any<CancellationToken>());
    }

    // ═══════════════════════════════════════════════════════════════
    // 9. FINALIZER — TeamId PROPAGATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Finalizer_TeamConstrained_NoTeamYet_SetsTeamFromAssignee()
    {
        var finalizer = new AssignmentFinalizer(_teamService, Substitute.For<ILogger<AssignmentFinalizer>>());

        _teamService.GetTeamForUserAsync("u1", Arg.Any<CancellationToken>())
            .Returns(new TeamInfo("team-A", "Team A", TeamType.Internal, true));

        var instance = WorkflowInstance.Create(Guid.NewGuid(), "Test", null, "admin");
        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = new ActivityContext
            {
                WorkflowInstanceId = instance.Id,
                ActivityId = "first-activity",
                WorkflowInstance = instance,
                Properties = new Dictionary<string, object>(),
                Variables = new Dictionary<string, object>()
            },
            Rules = new ActivityAssignmentRules(TeamConstrained: true, ExcludeAssigneesFrom: []),
            TeamId = null, // Not set yet
            SelectedAssignee = "u1",
            SelectionStrategy = "round_robin",
            CandidatePool = [Member("u1", "team-A", "first-activity")]
        };

        var result = await finalizer.FinalizeAsync(ctx);

        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("u1");
        // TeamId should now be set on the context
        ctx.TeamId.Should().Be("team-A");
        // And on the workflow instance variables
        instance.Variables.Should().ContainKey("TeamId");
        instance.Variables["TeamId"].Should().Be("team-A");
    }

    [Fact]
    public async Task Finalizer_TeamConstrained_TeamAlreadySet_DoesNotOverwrite()
    {
        var finalizer = new AssignmentFinalizer(_teamService, Substitute.For<ILogger<AssignmentFinalizer>>());

        var instance = WorkflowInstance.Create(Guid.NewGuid(), "Test", null, "admin",
            new Dictionary<string, object> { ["TeamId"] = "team-A" });

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = new ActivityContext
            {
                WorkflowInstanceId = instance.Id,
                ActivityId = "second-activity",
                WorkflowInstance = instance,
                Properties = new Dictionary<string, object>(),
                Variables = new Dictionary<string, object> { ["TeamId"] = "team-A" }
            },
            Rules = new ActivityAssignmentRules(TeamConstrained: true, ExcludeAssigneesFrom: []),
            TeamId = "team-A", // Already set
            SelectedAssignee = "u2",
            SelectionStrategy = "round_robin",
            CandidatePool = [Member("u2", "team-A", "second-activity")]
        };

        var result = await finalizer.FinalizeAsync(ctx);

        result.IsSuccess.Should().BeTrue();
        // GetTeamForUserAsync should NOT be called since TeamId is already set
        await _teamService.DidNotReceive().GetTeamForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finalizer_NotTeamConstrained_DoesNotSetTeamId()
    {
        var finalizer = new AssignmentFinalizer(_teamService, Substitute.For<ILogger<AssignmentFinalizer>>());

        var instance = WorkflowInstance.Create(Guid.NewGuid(), "Test", null, "admin");

        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = new ActivityContext
            {
                WorkflowInstanceId = instance.Id,
                ActivityId = "some-activity",
                WorkflowInstance = instance,
                Properties = new Dictionary<string, object>(),
                Variables = new Dictionary<string, object>()
            },
            Rules = ActivityAssignmentRules.Default,
            SelectedAssignee = "u1",
            CandidatePool = []
        };

        var result = await finalizer.FinalizeAsync(ctx);

        result.IsSuccess.Should().BeTrue();
        instance.Variables.Should().NotContainKey("TeamId");
    }

    // ═══════════════════════════════════════════════════════════════
    // 10. GetEligibleAssigneesAsync — stages 1+2 only
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetEligibleAssigneesAsync_RunsContextBuildAndFiltersOnly()
    {
        var context = CreateActivityContext();

        _contextBuilder.BuildAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var pipelineCtx = await _pipeline.GetEligibleAssigneesAsync(context);

        pipelineCtx.Should().NotBeNull();
        await _contextBuilder.Received(1).BuildAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>());
        // Engine and finalizer should NOT be called
        await _engine.DidNotReceive().ExecuteAsync(Arg.Any<AssignmentContext>(), Arg.Any<CancellationToken>());
        await _finalizer.DidNotReceive().FinalizeAsync(Arg.Any<AssignmentPipelineContext>(), Arg.Any<CancellationToken>());
    }

    // ═══════════════════════════════════════════════════════════════
    // 11. FINALIZER METADATA
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Finalizer_IncludesPipelineMetadata()
    {
        var finalizer = new AssignmentFinalizer(_teamService, Substitute.For<ILogger<AssignmentFinalizer>>());

        var instance = WorkflowInstance.Create(Guid.NewGuid(), "Test", null, "admin");
        var ctx = new AssignmentPipelineContext
        {
            ActivityContext = new ActivityContext
            {
                WorkflowInstanceId = instance.Id,
                ActivityId = "a",
                WorkflowInstance = instance,
                Properties = new Dictionary<string, object>(),
                Variables = new Dictionary<string, object>()
            },
            Rules = new ActivityAssignmentRules(true, ["staff-activity"]),
            TeamId = "team-X",
            SelectedAssignee = "u1",
            SelectionStrategy = "round_robin",
            SelectionMetadata = new Dictionary<string, object> { ["extra"] = "data" },
            CandidatePool = [Member("u1", "team-X", "a"), Member("u2", "team-X", "a")]
        };

        var result = await finalizer.FinalizeAsync(ctx);

        result.Metadata.Should().ContainKey("pipeline").WhoseValue.Should().Be(true);
        result.Metadata.Should().ContainKey("teamConstrained").WhoseValue.Should().Be(true);
        result.Metadata.Should().ContainKey("teamId").WhoseValue.Should().Be("team-X");
        result.Metadata.Should().ContainKey("excludeAssigneesFrom");
        result.Metadata.Should().ContainKey("candidatePoolSize").WhoseValue.Should().Be(2);
        result.Metadata.Should().ContainKey("extra").WhoseValue.Should().Be("data");
    }
}
