using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Schema;

namespace Assignment.Workflow.Activities.AppraisalActivities;

public class AdminReviewActivity : WorkflowActivityBase
{
    public override string ActivityType => AppraisalActivityTypes.AdminReview;
    public override string Name => "Admin Review";
    public override string Description => "Admin reviews and approves/rejects the appraisal request";

    public override async Task<ActivityResult> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        // This is a human task that requires external completion
        // The workflow engine will wait for admin input via API
        
        var reviewDeadline = GetProperty<DateTime?>(context, "reviewDeadline");
        var autoApprovalThreshold = GetProperty<decimal?>(context, "autoApprovalThreshold");
        var estimatedValue = GetVariable<decimal>(context, "estimatedValue");

        var outputData = new Dictionary<string, object>
        {
            ["assignedAt"] = DateTime.UtcNow,
            ["assigneeRole"] = "Admin",
            ["estimatedValue"] = estimatedValue
        };

        if (reviewDeadline.HasValue)
        {
            outputData["reviewDeadline"] = reviewDeadline.Value;
        }

        // Check for auto-approval if threshold is set
        if (autoApprovalThreshold.HasValue && estimatedValue <= autoApprovalThreshold.Value)
        {
            outputData["decision"] = "auto-approved";
            outputData["autoApproval"] = true;
            outputData["reviewedAt"] = DateTime.UtcNow;
            
            var variableUpdates = new Dictionary<string, object>
            {
                ["adminDecision"] = "approved",
                ["autoApproved"] = true
            };

            return new ActivityResult
            {
                Status = ActivityResultStatus.Completed,
                OutputData = outputData,
                VariableUpdates = variableUpdates,
                NextActivityId = "staff-assignment",
                Comments = $"Auto-approved due to value ${estimatedValue:N0} being below threshold ${autoApprovalThreshold.Value:N0}"
            };
        }

        // Manual review required - return pending
        return ActivityResult.Pending(outputData: outputData);
    }

    public override Task<Core.ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        // Admin review activity doesn't require specific validation
        return Task.FromResult(Core.ValidationResult.Success());
    }

    // This method would be called when admin completes the review via API
    public static ActivityResult CompleteReview(string decision, string comments, string reviewedBy)
    {
        var outputData = new Dictionary<string, object>
        {
            ["decision"] = decision,
            ["reviewedBy"] = reviewedBy,
            ["reviewedAt"] = DateTime.UtcNow,
            ["comments"] = comments
        };

        var variableUpdates = new Dictionary<string, object>
        {
            ["adminDecision"] = decision,
            ["adminComments"] = comments,
            ["reviewedBy"] = reviewedBy
        };

        var nextActivity = decision.ToLower() switch
        {
            "approved" => "staff-assignment",
            "rejected" => "end-rejected",
            _ => null
        };

        return new ActivityResult
        {
            Status = ActivityResultStatus.Completed,
            OutputData = outputData,
            VariableUpdates = variableUpdates,
            NextActivityId = nextActivity,
            Comments = $"Admin {decision.ToLower()} the request: {comments}"
        };
    }
}