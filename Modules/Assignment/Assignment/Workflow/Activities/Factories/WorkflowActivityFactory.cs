using Assignment.Workflow.Activities.AppraisalActivities;
using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Schema;

namespace Assignment.Workflow.Activities.Factories;

public class WorkflowActivityFactory : IWorkflowActivityFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _activityTypes;
    private readonly Dictionary<string, ActivityTypeDefinition> _activityDefinitions;

    public WorkflowActivityFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _activityTypes = new Dictionary<string, Type>();
        _activityDefinitions = new Dictionary<string, ActivityTypeDefinition>();

        RegisterActivities();
        RegisterActivityDefinitions();
    }

    public IWorkflowActivity CreateActivity(string activityType)
    {
        if (!_activityTypes.TryGetValue(activityType, out var type))
        {
            throw new ArgumentException($"Unknown activity type: {activityType}");
        }

        return (IWorkflowActivity)Activator.CreateInstance(type)!;
    }

    public IEnumerable<string> GetAvailableActivityTypes()
    {
        return _activityTypes.Keys;
    }

    public ActivityTypeDefinition GetActivityTypeDefinition(string activityType)
    {
        if (!_activityDefinitions.TryGetValue(activityType, out var definition))
        {
            throw new ArgumentException($"Unknown activity type: {activityType}");
        }

        return definition;
    }

    private void RegisterActivities()
    {
        // Core activities
        _activityTypes[ActivityTypes.TaskActivity] = typeof(TaskActivity);
        _activityTypes[ActivityTypes.DecisionActivity] = typeof(DecisionActivity);
        _activityTypes[ActivityTypes.StartActivity] = typeof(StartActivity);
        _activityTypes[ActivityTypes.EndActivity] = typeof(EndActivity);

        // Appraisal-specific activities
        _activityTypes[AppraisalActivityTypes.RequestSubmission] = typeof(RequestSubmissionActivity);
        _activityTypes[AppraisalActivityTypes.AdminReview] = typeof(AdminReviewActivity);
    }

    private void RegisterActivityDefinitions()
    {
        // Start Activity Definition
        _activityDefinitions[ActivityTypes.StartActivity] = new ActivityTypeDefinition
        {
            Type = ActivityTypes.StartActivity,
            Name = "Start Activity",
            Description = "Initial activity to start the workflow",
            Category = "Control Flow",
            Icon = "play-circle",
            Color = "#34d399",
            Properties = new List<ActivityPropertyDefinition>()
        };

        // End Activity Definition
        _activityDefinitions[ActivityTypes.EndActivity] = new ActivityTypeDefinition
        {
            Type = ActivityTypes.EndActivity,
            Name = "End Activity",
            Description = "Final activity to end the workflow",
            Category = "Control Flow",
            Icon = "stop-circle",
            Color = "#ef4444",
            Properties = new List<ActivityPropertyDefinition>()
        };

        // Task Activity Definition
        _activityDefinitions[ActivityTypes.TaskActivity] = new ActivityTypeDefinition
        {
            Type = ActivityTypes.TaskActivity,
            Name = "Task Activity",
            Description = "Assigns a task to a user or role for completion",
            Category = "Tasks",
            Icon = "user-circle",
            Color = "#10b981",
            Properties = new List<ActivityPropertyDefinition>
            {
                new()
                {
                    Name = "assigneeRole", DisplayName = "Assignee Role", Type = "string", Required = true,
                    Description = "Role or user to assign the task to"
                },
                new()
                {
                    Name = "formFields", DisplayName = "Form Fields", Type = "array", Required = false,
                    Description = "List of form fields to display"
                },
                new()
                {
                    Name = "requiresApproval", DisplayName = "Requires Approval", Type = "boolean",
                    DefaultValue = "false", Description = "Whether the task requires approval"
                },
                new()
                {
                    Name = "timeoutDuration", DisplayName = "Timeout (hours)", Type = "number", Required = false,
                    Description = "Task timeout in hours"
                }
            }
        };

        // Decision Activity Definition
        _activityDefinitions[ActivityTypes.DecisionActivity] = new ActivityTypeDefinition
        {
            Type = ActivityTypes.DecisionActivity,
            Name = "Decision Activity",
            Description = "Routes workflow based on conditions",
            Category = "Control Flow",
            Icon = "code-branch",
            Color = "#f59e0b",
            Properties = new List<ActivityPropertyDefinition>
            {
                new()
                {
                    Name = "conditions", DisplayName = "Conditions", Type = "object", Required = true,
                    Description = "Key-value pairs of route-condition mappings"
                },
                new()
                {
                    Name = "defaultRoute", DisplayName = "Default Route", Type = "string", Required = false,
                    Description = "Default activity if no conditions match"
                }
            }
        };

        // Request Submission Activity Definition
        _activityDefinitions[AppraisalActivityTypes.RequestSubmission] = new ActivityTypeDefinition
        {
            Type = AppraisalActivityTypes.RequestSubmission,
            Name = "Request Submission",
            Description = "Initial appraisal request submission",
            Category = "Appraisal",
            Icon = "document-plus",
            Color = "#3b82f6",
            Properties = new List<ActivityPropertyDefinition>
            {
                new()
                {
                    Name = "propertyType", DisplayName = "Property Type", Type = "string", Required = true,
                    Options = new List<string> { "Residential", "Commercial", "Industrial", "Land" }
                },
                new() { Name = "propertyAddress", DisplayName = "Property Address", Type = "string", Required = true },
                new() { Name = "estimatedValue", DisplayName = "Estimated Value", Type = "number", Required = true },
                new()
                {
                    Name = "purpose", DisplayName = "Appraisal Purpose", Type = "string", Required = true,
                    Options = new List<string> { "Mortgage", "Insurance", "Tax Assessment", "Sale" }
                },
                new() { Name = "requestorId", DisplayName = "Requestor ID", Type = "string", Required = false }
            }
        };

        // Admin Review Activity Definition
        _activityDefinitions[AppraisalActivityTypes.AdminReview] = new ActivityTypeDefinition
        {
            Type = AppraisalActivityTypes.AdminReview,
            Name = "Admin Review",
            Description = "Administrative review and approval",
            Category = "Appraisal",
            Icon = "shield-check",
            Color = "#8b5cf6",
            Properties = new List<ActivityPropertyDefinition>
            {
                new()
                {
                    Name = "reviewDeadline", DisplayName = "Review Deadline", Type = "string", Required = false,
                    Description = "ISO date string for review deadline"
                },
                new()
                {
                    Name = "autoApprovalThreshold", DisplayName = "Auto Approval Threshold", Type = "number",
                    Required = false, Description = "Value below which requests are auto-approved"
                }
            }
        };
    }
}