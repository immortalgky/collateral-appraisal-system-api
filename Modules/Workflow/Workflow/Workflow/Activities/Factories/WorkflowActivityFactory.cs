using Workflow.Workflow.Activities.AppraisalActivities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Activities.Factories;

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

        // Use service provider for dependency injection when available
        var serviceInstance = _serviceProvider.GetService(type) as IWorkflowActivity;
        if (serviceInstance != null)
        {
            return serviceInstance;
        }

        // Fallback to activator for types without dependencies
        try
        {
            return (IWorkflowActivity)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create instance of activity type '{activityType}'. Ensure the type has a parameterless constructor or is registered in DI.", ex);
        }
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
        _activityTypes[ActivityTypes.IfElseActivity] = typeof(IfElseActivity);
        _activityTypes[ActivityTypes.SwitchActivity] = typeof(SwitchActivity);
        _activityTypes[ActivityTypes.StartActivity] = typeof(StartActivity);
        _activityTypes[ActivityTypes.EndActivity] = typeof(EndActivity);
        _activityTypes[ActivityTypes.ForkActivity] = typeof(ForkActivity);
        _activityTypes[ActivityTypes.JoinActivity] = typeof(JoinActivity);

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
            Description = "Assigns a task to a user or role for completion using various strategies",
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
                    Name = "assignmentStrategy", DisplayName = "Assignment Strategy", Type = "string", Required = false,
                    DefaultValue = "Manual", Description = "Strategy for assigning tasks",
                    Options = new List<string> { "Manual", "RoundRobin", "Random", "WorkloadBased" }
                },
                new()
                {
                    Name = "userGroups", DisplayName = "User Groups", Type = "array", Required = false,
                    Description = "List of user groups eligible for assignment"
                },
                new()
                {
                    Name = "activityName", DisplayName = "Activity Name", Type = "string", Required = false,
                    Description = "Name of the activity for assignment tracking"
                },
                new()
                {
                    Name = "formFields", DisplayName = "Form Fields", Type = "array", Required = false,
                    Description = "List of form fields to display"
                },
                new()
                {
                    Name = "inputMappings", DisplayName = "Input Variable Mappings", Type = "object", Required = false,
                    Description = "Map input field names to workflow variable names (e.g. {\"propertyValue\": \"estimatedValue\", \"decision\": \"{activityId}_actionTaken\"})"
                },
                new()
                {
                    Name = "outputMappings", DisplayName = "Output Variable Mappings", Type = "object", Required = false,
                    Description = "Map activity outputs to workflow variables (e.g. {\"calculatedValue\": \"finalPropertyValue\"})"
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


        // IfElse Activity Definition
        _activityDefinitions[ActivityTypes.IfElseActivity] = new ActivityTypeDefinition
        {
            Type = ActivityTypes.IfElseActivity,
            Name = "If-Else Decision",
            Description = "Binary conditional routing based on boolean expression evaluation. Outputs result: true/false for transition-based routing.",
            Category = "Flow Control",
            Icon = "git-branch",
            Color = "#fb923c",
            Properties = new List<ActivityPropertyDefinition>
            {
                new()
                {
                    Name = "condition", DisplayName = "Condition", Type = "string", Required = true,
                    Description = "Boolean expression to evaluate (e.g., 'amount > 50000 && status == \"approved\"'). Use transitions with conditions like 'result == true' to route."
                }
            }
        };

        // Switch Activity Definition
        _activityDefinitions[ActivityTypes.SwitchActivity] = new ActivityTypeDefinition
        {
            Type = ActivityTypes.SwitchActivity,
            Name = "Switch Decision",
            Description = "Multi-branch conditional routing with support for comparisons and value matching. Outputs case: matched_condition for transition-based routing.",
            Category = "Flow Control", 
            Icon = "share-2",
            Color = "#a855f7",
            Properties = new List<ActivityPropertyDefinition>
            {
                new()
                {
                    Name = "expression", DisplayName = "Expression", Type = "string", Required = true,
                    Description = "Expression to evaluate for switch routing (e.g., 'amount', 'status', 'category')"
                },
                new()
                {
                    Name = "cases", DisplayName = "Cases", Type = "array", Required = true,
                    Description = "Array of case conditions to match (e.g., ['< 10000', '>= 50000', 'approved']). Use transitions with conditions like 'case == \"< 10000\"' to route."
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

        // Fork Activity Definition
        _activityDefinitions[ActivityTypes.ForkActivity] = new ActivityTypeDefinition
        {
            Type = ActivityTypes.ForkActivity,
            Name = "Fork Activity",
            Description = "Splits workflow execution into multiple parallel branches",
            Category = "Control Flow",
            Icon = "git-branch",
            Color = "#8b5cf6",
            Properties = new List<ActivityPropertyDefinition>
            {
                new()
                {
                    Name = "branches", DisplayName = "Branches", Type = "array", Required = true,
                    Description = "List of parallel branches to execute"
                },
                new()
                {
                    Name = "forkType", DisplayName = "Fork Type", Type = "string", Required = false,
                    DefaultValue = "all", Description = "Type of fork execution (all, any, conditional)",
                    Options = new List<string> { "all", "any", "conditional" }
                },
                new()
                {
                    Name = "maxConcurrency", DisplayName = "Max Concurrency", Type = "number", Required = false,
                    DefaultValue = "0", Description = "Maximum number of concurrent branches (0 = unlimited)"
                }
            }
        };

        // Join Activity Definition  
        _activityDefinitions[ActivityTypes.JoinActivity] = new ActivityTypeDefinition
        {
            Type = ActivityTypes.JoinActivity,
            Name = "Join Activity",
            Description = "Synchronizes and merges multiple parallel workflow branches",
            Category = "Control Flow",
            Icon = "git-merge",
            Color = "#10b981",
            Properties = new List<ActivityPropertyDefinition>
            {
                new()
                {
                    Name = "forkId", DisplayName = "Fork ID", Type = "string", Required = true,
                    Description = "ID of the fork activity to join"
                },
                new()
                {
                    Name = "joinType", DisplayName = "Join Type", Type = "string", Required = false,
                    DefaultValue = "all", Description = "Join synchronization strategy",
                    Options = new List<string> { "all", "any", "first", "majority" }
                },
                new()
                {
                    Name = "timeoutMinutes", DisplayName = "Timeout (minutes)", Type = "number", Required = false,
                    DefaultValue = "0", Description = "Timeout in minutes (0 = no timeout)"
                },
                new()
                {
                    Name = "mergeStrategy", DisplayName = "Merge Strategy", Type = "string", Required = false,
                    DefaultValue = "combine", Description = "How to merge branch outputs",
                    Options = new List<string> { "combine", "override", "first", "last" }
                },
                new()
                {
                    Name = "timeoutAction", DisplayName = "Timeout Action", Type = "string", Required = false,
                    DefaultValue = "fail", Description = "Action to take on timeout",
                    Options = new List<string> { "fail", "proceed" }
                }
            }
        };
    }
}