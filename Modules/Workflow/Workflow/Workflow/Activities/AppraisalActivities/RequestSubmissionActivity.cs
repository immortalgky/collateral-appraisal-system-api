using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Activities.AppraisalActivities;

public class RequestSubmissionActivity : WorkflowActivityBase
{
    public override string ActivityType => AppraisalActivityTypes.RequestSubmission;
    public override string Name => "Request Submission";
    public override string Description => "Handles the initial appraisal request submission";

    protected override async Task<ActivityResult> OnExecuteAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var propertyType = GetProperty<string>(context, "propertyType");
        var propertyAddress = GetProperty<string>(context, "propertyAddress");
        var estimatedValue = GetProperty<decimal>(context, "estimatedValue");
        var purpose = GetProperty<string>(context, "purpose");
        var requestorId = GetProperty<string>(context, "requestorId");

        // Validate required fields
        var errors = new List<string>();
        if (string.IsNullOrEmpty(propertyType)) errors.Add("Property type is required");
        if (string.IsNullOrEmpty(propertyAddress)) errors.Add("Property address is required");
        if (estimatedValue <= 0) errors.Add("Estimated value must be greater than 0");
        if (string.IsNullOrEmpty(purpose)) errors.Add("Purpose is required");

        if (errors.Any())
        {
            return ActivityResult.Failed(string.Join(", ", errors));
        }

        var outputData = new Dictionary<string, object>
        {
            ["requestId"] = Guid.NewGuid().ToString(),
            ["propertyType"] = propertyType,
            ["propertyAddress"] = propertyAddress,
            ["estimatedValue"] = estimatedValue,
            ["purpose"] = purpose,
            ["requestorId"] = requestorId ?? string.Empty,
            ["submittedAt"] = DateTime.Now,
            ["status"] = "submitted"
        };

        return new ActivityResult
        {
            Status = ActivityResultStatus.Completed,
            OutputData = outputData,
            NextActivityId = "admin-review",
            Comments = $"Request submitted for {propertyType} property at {propertyAddress}"
        };
    }

    public override Task<Core.ValidationResult> ValidateAsync(ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Validate that required properties exist in the activity definition
        if (!context.Properties.ContainsKey("propertyType")) errors.Add("propertyType property is required");
        if (!context.Properties.ContainsKey("propertyAddress")) errors.Add("propertyAddress property is required");
        if (!context.Properties.ContainsKey("estimatedValue")) errors.Add("estimatedValue property is required");
        if (!context.Properties.ContainsKey("purpose")) errors.Add("purpose property is required");

        return Task.FromResult(errors.Any()
            ? Core.ValidationResult.Failure(errors.ToArray())
            : Core.ValidationResult.Success());
    }
}