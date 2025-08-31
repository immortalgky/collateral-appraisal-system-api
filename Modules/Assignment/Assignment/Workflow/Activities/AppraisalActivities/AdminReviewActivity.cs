using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Schema;
using Assignment.Workflow.Models;

namespace Assignment.Workflow.Activities.AppraisalActivities;

public class AdminReviewActivity : WorkflowActivityBase
{
    public override string ActivityType => AppraisalActivityTypes.AdminReview;
    public override string Name => "Admin Review";
    public override string Description => "Admin reviews and approves/rejects the appraisal request";

    protected override async Task<ActivityResult> ExecuteActivityAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        // This is a human task that requires external completion
        // The workflow engine will wait for admin input via API

        var reviewDeadline = GetProperty<DateTime?>(context, "reviewDeadline");
        var autoApprovalThreshold = GetProperty<decimal?>(context, "autoApprovalThreshold");
        var estimatedValue = GetVariable<decimal>(context, "estimatedValue");

        var outputData = new Dictionary<string, object>
        {
            ["assignedAt"] = DateTime.Now,
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
            outputData["reviewedAt"] = DateTime.Now;

            // Add workflow variables to output data
            outputData["adminDecision"] = "approved";
            outputData["autoApproved"] = true;

            return new ActivityResult
            {
                Status = ActivityResultStatus.Completed,
                OutputData = outputData,
                NextActivityId = "staff-assignment",
                Comments =
                    $"Auto-approved due to value ${estimatedValue:N0} being below threshold ${autoApprovalThreshold.Value:N0}"
            };
        }

        // Manual review required - return pending
        return ActivityResult.Pending(outputData: outputData);
    }

    public override Task<Core.ValidationResult> ValidateAsync(ActivityContext context,
        CancellationToken cancellationToken = default)
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
            ["reviewedAt"] = DateTime.Now,
            ["comments"] = comments
        };

        // Add workflow variables to output data
        outputData["adminDecision"] = decision;
        outputData["adminComments"] = comments;
        outputData["reviewedBy"] = reviewedBy;

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
            NextActivityId = nextActivity,
            Comments = $"Admin {decision.ToLower()} the request: {comments}"
        };
    }
}