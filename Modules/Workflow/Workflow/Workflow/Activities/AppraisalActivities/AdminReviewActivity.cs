using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Models;
using Workflow.AssigneeSelection.Engine;
using Workflow.AssigneeSelection.Services;
using Workflow.Services.Configuration;
using Workflow.Workflow.Actions.Core;
using Workflow.Workflow.Services;

namespace Workflow.Workflow.Activities.AppraisalActivities;

/// <summary>
/// Admin review activity that inherits from HumanTaskActivityBase for proper human task behavior.
/// Uses simple role-based assignment and handles approval/rejection logic.
/// </summary>
public class AdminReviewActivity : HumanTaskActivityBase
{
    public AdminReviewActivity(
        IWorkflowBookmarkService bookmarkService,
        IWorkflowAuditService auditService,
        ILogger<AdminReviewActivity> logger)
        : base(bookmarkService, auditService, logger)
    {
    }

    public override string ActivityType => AppraisalActivityTypes.AdminReview;
    public override string Name => "Admin Review";
    public override string Description => "Admin reviews and approves/rejects the appraisal request";

    /// <summary>
    /// Override assignment logic to use simple role-based assignment for admin review
    /// </summary>
    protected override Task<AssignmentResult> DetermineAssigneeAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        // Simple assignment logic: always assign to Admin role
        // This overrides the complex assignment logic from base class
        var adminRole = GetProperty<string>(context, "assigneeRole", "Admin");
        
        _logger.LogInformation("Assigning admin review task to role: {AdminRole}", adminRole);
        
        return Task.FromResult(new AssignmentResult
        {
            IsSuccess = true,
            AssigneeId = adminRole,
            Strategy = "AdminRoleAssignment",
            Metadata = new Dictionary<string, object> 
            { 
                ["role"] = adminRole,
                ["activityType"] = ActivityType,
                ["assignmentType"] = "role-based"
            }
        });
    }

    /// <summary>
    /// Add custom data during execution, including auto-approval logic
    /// </summary>
    protected override async Task<Dictionary<string, object>> OnExecuteAsync(
        ActivityContext context, 
        string assignee, 
        AssignmentResult assignmentResult, 
        CancellationToken cancellationToken)
    {
        var customData = new Dictionary<string, object>();
        
        var reviewDeadline = GetProperty<DateTime?>(context, "reviewDeadline");
        var autoApprovalThreshold = GetProperty<decimal?>(context, "autoApprovalThreshold");
        var estimatedValue = GetVariable<decimal>(context, "estimatedValue", 0);

        customData["estimatedValue"] = estimatedValue;
        
        if (reviewDeadline.HasValue)
        {
            customData["reviewDeadline"] = reviewDeadline.Value;
        }

        // Check for auto-approval if threshold is set
        if (autoApprovalThreshold.HasValue && estimatedValue > 0 && estimatedValue <= autoApprovalThreshold.Value)
        {
            customData["autoApprovalEligible"] = true;
            customData["autoApprovalThreshold"] = autoApprovalThreshold.Value;
            
            _logger.LogInformation(
                "Request with value ${EstimatedValue:N0} is eligible for auto-approval (threshold: ${Threshold:N0})",
                estimatedValue, autoApprovalThreshold.Value);
        }
        else
        {
            customData["autoApprovalEligible"] = false;
            if (autoApprovalThreshold.HasValue)
            {
                customData["autoApprovalThreshold"] = autoApprovalThreshold.Value;
            }
        }
        
        return customData;
    }

    /// <summary>
    /// Custom resume processing for admin review with approval/rejection validation
    /// </summary>
    protected override Task<ActivityResult> OnResumeAsync(ActivityContext context, Dictionary<string, object> resumeInput, CancellationToken cancellationToken)
    {
        var decision = resumeInput.TryGetValue("decision", out var decisionValue) ? decisionValue?.ToString()?.ToLower() : null;
        var approved = resumeInput.TryGetValue("approved", out var approvedValue) && Convert.ToBoolean(approvedValue);
        var comments = resumeInput.TryGetValue("comments", out var commentsValue) ? commentsValue?.ToString() : "";
        var rejectionReason = resumeInput.TryGetValue("rejectionReason", out var reasonValue) ? reasonValue?.ToString() : "";
        var reviewedBy = resumeInput.TryGetValue("completedBy", out var reviewedByValue) ? reviewedByValue?.ToString() : "Unknown";

        // Normalize decision
        if (string.IsNullOrEmpty(decision))
        {
            decision = approved ? "approved" : "rejected";
        }

        // Validation: rejection requires a reason
        if (decision == "rejected" && string.IsNullOrEmpty(rejectionReason) && string.IsNullOrEmpty(comments))
        {
            _logger.LogWarning("Admin review rejection attempted without reason for workflow {WorkflowId}", 
                context.WorkflowInstance.Id);
            return Task.FromResult(ActivityResult.Failed("Rejection reason is required when rejecting a request"));
        }

        // Prepare output data
        var outputData = new Dictionary<string, object>
        {
            ["decision"] = decision,
            ["approved"] = decision == "approved",
            ["reviewedBy"] = reviewedBy,
            ["reviewedAt"] = DateTime.UtcNow,
            ["comments"] = comments,
            ["adminDecision"] = decision,
            ["adminComments"] = comments
        };

        if (decision == "rejected")
        {
            outputData["rejectionReason"] = rejectionReason ?? comments;
            outputData["rejected"] = true;
        }

        // Add activity-specific output variables
        outputData[$"{NormalizeActivityId(context.ActivityId)}_decision"] = decision;
        outputData[$"{NormalizeActivityId(context.ActivityId)}_reviewedBy"] = reviewedBy;

        _logger.LogInformation(
            "Admin review completed for workflow {WorkflowId}: {Decision} by {ReviewedBy}",
            context.WorkflowInstance.Id, decision, reviewedBy);

        return Task.FromResult(ActivityResult.Success(outputData));
    }

    /// <summary>
    /// Validation for admin review activity
    /// </summary>
    public override Task<Core.ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        
        // Check that either assigneeRole is set or use default
        var adminRole = GetProperty<string>(context, "assigneeRole");
        if (string.IsNullOrEmpty(adminRole))
        {
            // This is OK, we'll default to "Admin"
            _logger.LogDebug("No assigneeRole specified for AdminReviewActivity, will default to 'Admin'");
        }
        
        // Validate auto-approval threshold if specified
        var threshold = GetProperty<decimal?>(context, "autoApprovalThreshold");
        if (threshold.HasValue && threshold.Value < 0)
        {
            errors.Add("Auto-approval threshold must be non-negative");
        }
        
        return Task.FromResult(errors.Any()
            ? Core.ValidationResult.Failure(errors.ToArray())
            : Core.ValidationResult.Success());
    }
}