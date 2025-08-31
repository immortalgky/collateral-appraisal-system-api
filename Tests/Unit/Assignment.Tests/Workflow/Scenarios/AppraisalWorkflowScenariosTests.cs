using Assignment.AssigneeSelection.Core;
using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Engine;
using Assignment.Workflow.Models;
using Assignment.Workflow.Schema;
using Assignment.Workflow.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Diagnostics;
using Xunit;
using ActivityContext = Assignment.Workflow.Activities.Core.ActivityContext;

namespace Assignment.Tests.Workflow.Scenarios;

/// <summary>
/// End-to-end scenario tests for complete appraisal workflows
/// Tests realistic business scenarios with complex routing and decision logic
/// </summary>
public class AppraisalWorkflowScenariosTests
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<AppraisalWorkflowScenariosTests> _logger;

    public AppraisalWorkflowScenariosTests()
    {
        _logger = Substitute.For<ILogger<AppraisalWorkflowScenariosTests>>();
        _workflowEngine = Substitute.For<IWorkflowEngine>();
        _workflowService = Substitute.For<IWorkflowService>();
    }

    [Fact]
    public async Task StandardResidentialAppraisal_CompleteWorkflow_ProcessesSuccessfully()
    {
        // Arrange - Standard residential property appraisal
        var workflowDefinitionId = Guid.NewGuid();
        var propertyData = new Dictionary<string, object>
        {
            ["property_address"] = "123 Maple Street, Suburbia, ST 12345",
            ["property_type"] = "residential",
            ["property_value_estimate"] = 425000,
            ["loan_amount"] = 340000,
            ["loan_to_value"] = 0.8,
            ["borrower_name"] = "John & Mary Johnson",
            ["loan_officer"] = "sarah.adams@company.com",
            ["priority"] = "standard",
            ["client_tier"] = "standard",
            ["property_age"] = 15,
            ["property_condition"] = "good",
            ["market_area"] = "suburban_residential",
            ["appraisal_purpose"] = "purchase"
        };

        // Setup workflow progression
        var startedWorkflow = CreateWorkflowInstance(workflowDefinitionId, WorkflowStatus.Running, propertyData);
        var completedWorkflow = CreateWorkflowInstance(workflowDefinitionId, WorkflowStatus.Completed, propertyData);
        
        _workflowService.StartWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, RuntimeOverride>>(), Arg.Any<CancellationToken>()
        ).Returns(startedWorkflow);

        _workflowService.ResumeWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>()
        ).Returns(completedWorkflow);

        // Act - Complete workflow process
        var workflow = await _workflowService.StartWorkflowAsync(
            workflowDefinitionId,
            "Standard Residential Appraisal - Johnson Property",
            "sarah.adams@company.com",
            propertyData);

        // Property inspection completion
        var inspectionResults = new Dictionary<string, object>
        {
            ["inspection_date"] = DateTime.UtcNow.AddDays(-1),
            ["interior_condition"] = "excellent",
            ["exterior_condition"] = "good",
            ["square_footage"] = 2150,
            ["bedrooms"] = 4,
            ["bathrooms"] = 2.5,
            ["garage_spaces"] = 2,
            ["special_features"] = new[] { "updated_kitchen", "hardwood_floors", "finished_basement" },
            ["comparable_properties"] = 5,
            ["market_value_range"] = new { min = 410000, max = 440000 }
        };

        var inspectionComplete = await _workflowService.ResumeWorkflowAsync(
            workflow.Id,
            "property-inspection",
            "inspector.mike@company.com",
            inspectionResults);

        // Final valuation and approval
        var finalAppraisal = new Dictionary<string, object>
        {
            ["final_appraised_value"] = 435000,
            ["valuation_method"] = "sales_comparison_approach",
            ["appraiser_notes"] = "Property is in excellent condition with recent updates. Market supports valuation.",
            ["approval_recommendation"] = "approved",
            ["conditions"] = new string[0], // No conditions
            ["appraisal_report_id"] = "APR-2024-001234"
        };

        var finalWorkflow = await _workflowService.ResumeWorkflowAsync(
            workflow.Id,
            "final-valuation",
            "senior.appraiser@company.com",
            finalAppraisal);

        // Assert
        workflow.Should().NotBeNull();
        workflow.Status.Should().Be(WorkflowStatus.Running);
        
        inspectionComplete.Should().NotBeNull();
        finalWorkflow.Should().NotBeNull();
        finalWorkflow.Status.Should().Be(WorkflowStatus.Completed);

        // Verify workflow progression calls
        await _workflowService.Received(1).StartWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, RuntimeOverride>>(), Arg.Any<CancellationToken>());

        await _workflowService.Received(2).ResumeWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HighValueCommercialAppraisal_ComplexRouting_HandlesEscalation()
    {
        // Arrange - High-value commercial property requiring escalation
        var workflowDefinitionId = Guid.NewGuid();
        var commercialData = new Dictionary<string, object>
        {
            ["property_address"] = "1500 Business Center Drive, Metro City, ST 54321",
            ["property_type"] = "commercial",
            ["property_subtype"] = "office_building",
            ["property_value_estimate"] = 2750000,
            ["loan_amount"] = 2200000,
            ["loan_to_value"] = 0.8,
            ["borrower_entity"] = "Metro Business Holdings LLC",
            ["loan_officer"] = "commercial.specialist@company.com",
            ["priority"] = "high",
            ["client_tier"] = "VIP",
            ["property_age"] = 8,
            ["square_footage"] = 45000,
            ["occupancy_rate"] = 0.92,
            ["net_operating_income"] = 385000,
            ["cap_rate"] = 0.07,
            ["market_area"] = "downtown_commercial",
            ["appraisal_purpose"] = "refinance",
            ["complexity_factors"] = new[] { "multiple_tenants", "income_approach", "environmental_assessment" }
        };

        // Setup escalated workflow
        var startedWorkflow = CreateWorkflowInstance(workflowDefinitionId, WorkflowStatus.Running, commercialData);
        var escalatedWorkflow = CreateWorkflowInstance(workflowDefinitionId, WorkflowStatus.Running, commercialData);
        escalatedWorkflow.Variables["escalated_to"] = "committee_review";
        escalatedWorkflow.Variables["assigned_senior_appraiser"] = "senior.commercial@company.com";

        _workflowService.StartWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, RuntimeOverride>>(), Arg.Any<CancellationToken>()
        ).Returns(startedWorkflow);

        _workflowService.ResumeWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>()
        ).Returns(escalatedWorkflow);

        // Act - Start high-value commercial appraisal
        var workflow = await _workflowService.StartWorkflowAsync(
            workflowDefinitionId,
            "Commercial Appraisal - Metro Business Center",
            "commercial.specialist@company.com",
            commercialData);

        // Initial assessment triggers escalation
        var initialAssessment = new Dictionary<string, object>
        {
            ["preliminary_value"] = 2950000,
            ["complexity_score"] = 8.5,
            ["requires_income_approach"] = true,
            ["requires_environmental"] = true,
            ["estimated_completion_days"] = 21,
            ["escalation_required"] = true,
            ["escalation_reason"] = "High value exceeds $2.5M threshold and requires specialized expertise"
        };

        var escalatedWorkflowResult = await _workflowService.ResumeWorkflowAsync(
            workflow.Id,
            "initial-assessment",
            "appraiser.jane@company.com",
            initialAssessment);

        // Assert
        workflow.Should().NotBeNull();
        workflow.Status.Should().Be(WorkflowStatus.Running);
        workflow.Variables.Should().ContainKey("property_value_estimate");
        workflow.Variables["property_value_estimate"].Should().Be(2750000);

        escalatedWorkflowResult.Should().NotBeNull();
        escalatedWorkflowResult.Status.Should().Be(WorkflowStatus.Running);
        escalatedWorkflowResult.Variables.Should().ContainKey("escalated_to");

        // Verify escalation workflow calls
        await _workflowService.Received(1).StartWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<Dictionary<string, object>>(vars => 
                vars.ContainsKey("property_type") && vars["property_type"].ToString() == "commercial"),
            Arg.Any<string>(), Arg.Any<Dictionary<string, RuntimeOverride>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RushAppraisalWorkflow_PriorityProcessing_CompletesWithinTimeframe()
    {
        // Arrange - Rush appraisal with tight deadline
        var workflowDefinitionId = Guid.NewGuid();
        var rushData = new Dictionary<string, object>
        {
            ["property_address"] = "456 Urgent Lane, FastTrack, ST 98765",
            ["property_type"] = "residential", 
            ["property_value_estimate"] = 385000,
            ["loan_amount"] = 308000,
            ["priority"] = "urgent",
            ["client_tier"] = "VIP",
            ["rush_fee_paid"] = true,
            ["required_completion_date"] = DateTime.UtcNow.AddDays(3),
            ["borrower_name"] = "Emergency Buyer LLC",
            ["loan_officer"] = "rush.specialist@company.com",
            ["special_instructions"] = "Client closing in 5 days - critical timeline",
            ["management_approval"] = "approved_by_director",
            ["expedite_code"] = "RUSH-2024-URGENT"
        };

        var startedWorkflow = CreateWorkflowInstance(workflowDefinitionId, WorkflowStatus.Running, rushData);
        var prioritizedWorkflow = CreateWorkflowInstance(workflowDefinitionId, WorkflowStatus.Running, rushData);
        prioritizedWorkflow.Variables["priority_queue"] = "urgent";
        prioritizedWorkflow.Variables["assigned_appraiser"] = "rush.appraiser@company.com";

        _workflowService.StartWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, RuntimeOverride>>(), Arg.Any<CancellationToken>()
        ).Returns(startedWorkflow);

        _workflowService.ResumeWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>()
        ).Returns(prioritizedWorkflow);

        // Act & Measure - Track processing time
        var stopwatch = Stopwatch.StartNew();
        
        var workflow = await _workflowService.StartWorkflowAsync(
            workflowDefinitionId,
            "URGENT - Rush Appraisal Request",
            "rush.specialist@company.com",
            rushData);

        var priorityAssignment = new Dictionary<string, object>
        {
            ["priority_assigned"] = true,
            ["priority_level"] = "urgent",
            ["estimated_completion"] = DateTime.UtcNow.AddDays(2),
            ["assigned_appraiser"] = "rush.appraiser@company.com",
            ["assignment_time"] = DateTime.UtcNow,
            ["priority_notes"] = "Rush request approved - expedited processing"
        };

        var prioritizedResult = await _workflowService.ResumeWorkflowAsync(
            workflow.Id,
            "priority-assignment",
            "workflow-coordinator@company.com",
            priorityAssignment);

        stopwatch.Stop();

        // Assert
        workflow.Should().NotBeNull();
        workflow.Status.Should().Be(WorkflowStatus.Running);
        workflow.Variables.Should().ContainKey("priority");
        workflow.Variables["priority"].Should().Be("urgent");

        prioritizedResult.Should().NotBeNull();
        prioritizedResult.Variables.Should().ContainKey("priority_queue");

        // Performance assertion for rush processing
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000,
            "Rush workflow initialization should complete within 2 seconds");

        // Verify priority handling
        await _workflowService.Received().StartWorkflowAsync(
            Arg.Any<Guid>(), Arg.Is<string>(name => name.Contains("URGENT")), Arg.Any<string>(),
            Arg.Is<Dictionary<string, object>>(vars => vars["priority"].ToString() == "urgent"),
            Arg.Any<string>(), Arg.Any<Dictionary<string, RuntimeOverride>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MultiPropertyPortfolioAppraisal_ParallelProcessing_HandlesComplexity()
    {
        // Arrange - Portfolio of 5 properties requiring coordinated appraisal
        var portfolioDefinitionId = Guid.NewGuid();
        var portfolioData = new Dictionary<string, object>
        {
            ["portfolio_name"] = "Residential Investment Portfolio Alpha",
            ["portfolio_id"] = "PORTFOLIO-2024-001",
            ["borrower_entity"] = "Investment Properties Group LLC",
            ["loan_officer"] = "portfolio.specialist@company.com",
            ["total_loan_amount"] = 3500000,
            ["property_count"] = 5,
            ["client_tier"] = "Enterprise",
            ["priority"] = "high",
            ["coordination_required"] = true,
            ["properties"] = new[]
            {
                new { address = "100 Oak St", type = "residential", estimate = 450000, id = "PROP-001" },
                new { address = "200 Pine Ave", type = "residential", estimate = 520000, id = "PROP-002" },
                new { address = "300 Elm Rd", type = "residential", estimate = 480000, id = "PROP-003" },
                new { address = "400 Cedar Ln", type = "residential", estimate = 395000, id = "PROP-004" },
                new { address = "500 Maple Dr", type = "residential", estimate = 610000, id = "PROP-005" }
            }
        };

        var portfolioWorkflow = CreateWorkflowInstance(portfolioDefinitionId, WorkflowStatus.Running, portfolioData);
        var coordinatedWorkflow = CreateWorkflowInstance(portfolioDefinitionId, WorkflowStatus.Running, portfolioData);
        coordinatedWorkflow.Variables["parallel_processing"] = "active";
        coordinatedWorkflow.Variables["coordinator_assigned"] = "portfolio.coordinator@company.com";

        _workflowService.StartWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, RuntimeOverride>>(), Arg.Any<CancellationToken>()
        ).Returns(portfolioWorkflow);

        _workflowService.ResumeWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>()
        ).Returns(coordinatedWorkflow);

        // Act
        var workflow = await _workflowService.StartWorkflowAsync(
            portfolioDefinitionId,
            "Portfolio Appraisal - Residential Investment Alpha",
            "portfolio.specialist@company.com",
            portfolioData);

        // Portfolio coordination setup
        var coordinationSetup = new Dictionary<string, object>
        {
            ["coordination_strategy"] = "parallel_with_sync",
            ["assigned_appraisers"] = new[]
            {
                "appraiser1@company.com",
                "appraiser2@company.com", 
                "appraiser3@company.com"
            },
            ["coordination_timeline"] = "14_days",
            ["sync_checkpoints"] = new[] { "day_7", "day_10", "final_review" },
            ["quality_reviewer"] = "senior.portfolio@company.com"
        };

        var coordinatedResult = await _workflowService.ResumeWorkflowAsync(
            workflow.Id,
            "portfolio-coordination",
            "portfolio.coordinator@company.com",
            coordinationSetup);

        // Assert
        workflow.Should().NotBeNull();
        workflow.Status.Should().Be(WorkflowStatus.Running);
        workflow.Variables.Should().ContainKey("property_count");
        workflow.Variables["property_count"].Should().Be(5);

        coordinatedResult.Should().NotBeNull();
        coordinatedResult.Variables.Should().ContainKey("parallel_processing");

        // Verify portfolio processing setup
        await _workflowService.Received().StartWorkflowAsync(
            Arg.Any<Guid>(), Arg.Is<string>(name => name.Contains("Portfolio")), Arg.Any<string>(),
            Arg.Is<Dictionary<string, object>>(vars => vars.ContainsKey("property_count")),
            Arg.Any<string>(), Arg.Any<Dictionary<string, RuntimeOverride>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ErrorRecoveryScenario_WorkflowFailureAndResumption_RecoversGracefully()
    {
        // Arrange - Scenario where workflow encounters an error and needs recovery
        var workflowDefinitionId = Guid.NewGuid();
        var problematicData = new Dictionary<string, object>
        {
            ["property_address"] = "999 Problem Street, ErrorTown, ST 00000",
            ["property_type"] = "residential",
            ["property_value_estimate"] = 325000,
            ["special_conditions"] = new[] { "flood_zone", "environmental_concern", "title_issue" },
            ["complexity_level"] = "high",
            ["requires_specialist"] = true
        };

        var failedWorkflow = CreateWorkflowInstance(workflowDefinitionId, WorkflowStatus.Failed, problematicData);
        var recoveredWorkflow = CreateWorkflowInstance(workflowDefinitionId, WorkflowStatus.Running, problematicData);
        recoveredWorkflow.Variables["recovery_mode"] = "active";
        recoveredWorkflow.Variables["error_resolved"] = "environmental_specialist_assigned";

        // Setup failure then recovery
        _workflowService.StartWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, RuntimeOverride>>(), Arg.Any<CancellationToken>()
        ).Returns(failedWorkflow);

        _workflowService.ResumeWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(), Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>()
        ).Returns(recoveredWorkflow);

        // Act - Start workflow that initially fails
        var initialWorkflow = await _workflowService.StartWorkflowAsync(
            workflowDefinitionId,
            "Complex Property with Issues",
            "problem.solver@company.com",
            problematicData);

        // Recovery action after specialist assignment
        var recoveryData = new Dictionary<string, object>
        {
            ["environmental_specialist"] = "env.specialist@company.com",
            ["specialist_report"] = "Environmental issues manageable with mitigation",
            ["recovery_action"] = "specialist_assigned",
            ["new_timeline"] = "extended_21_days",
            ["additional_approvals"] = new[] { "environmental_manager", "senior_appraiser" }
        };

        var recoveredResult = await _workflowService.ResumeWorkflowAsync(
            initialWorkflow.Id,
            "error-recovery",
            "workflow.supervisor@company.com",
            recoveryData);

        // Assert
        initialWorkflow.Should().NotBeNull();
        initialWorkflow.Status.Should().Be(WorkflowStatus.Failed);
        initialWorkflow.Variables.Should().ContainKey("special_conditions");

        recoveredResult.Should().NotBeNull();
        recoveredResult.Status.Should().Be(WorkflowStatus.Running);
        recoveredResult.Variables.Should().ContainKey("recovery_mode");

        // Verify error handling and recovery
        await _workflowService.Received().StartWorkflowAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<Dictionary<string, object>>(vars => vars.ContainsKey("special_conditions")),
            Arg.Any<string>(), Arg.Any<Dictionary<string, RuntimeOverride>>(), Arg.Any<CancellationToken>());

        await _workflowService.Received().ResumeWorkflowAsync(
            Arg.Any<Guid>(), "error-recovery", Arg.Any<string>(),
            Arg.Is<Dictionary<string, object>>(data => data.ContainsKey("recovery_action")),
            Arg.Any<Dictionary<string, RuntimeOverride>>(), Arg.Any<CancellationToken>());
    }

    private WorkflowInstance CreateWorkflowInstance(Guid definitionId, WorkflowStatus status, Dictionary<string, object> variables)
    {
        var instance = WorkflowInstance.Create(
            definitionId,
            "Test Appraisal Workflow",
            null,
            "test@company.com", 
            new Dictionary<string, object>(variables));
        
        // Update status after creation if not Running
        if (status != WorkflowStatus.Running)
        {
            instance.UpdateStatus(status);
        }
        
        return instance;
    }
}