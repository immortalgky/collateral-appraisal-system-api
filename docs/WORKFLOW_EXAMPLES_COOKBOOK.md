# Workflow Engine Examples Cookbook

This cookbook provides practical, real-world examples of using the Advanced Workflow Engine. Each recipe includes complete JSON configurations and code examples you can use immediately.

## 🏠 Property Appraisal Workflows

### Recipe 1: Standard Appraisal Review Process

A typical property appraisal workflow with assignment preferences and notifications.

```json
{
  "workflowId": "standard-appraisal-review",
  "name": "Standard Property Appraisal Review",
  "version": "2.0",
  "description": "Complete appraisal review with quality checks and notifications",
  "variables": {
    "propertyValue": 0,
    "riskLevel": "Medium",
    "clientPriority": "Standard",
    "reviewDeadline": null
  },
  "activities": [
    {
      "id": "start",
      "type": "StartActivity",
      "name": "Initialize Appraisal Review",
      "nextActivities": ["initial-assignment"]
    },
    {
      "id": "initial-assignment",
      "type": "TaskActivity", 
      "name": "Initial Appraisal Review",
      "description": "Primary review of property appraisal documentation and methodology",
      "assignmentStrategies": [
        "client-preference",
        "workload-balancer", 
        "expertise-matcher",
        "supervisor",
        "manual"
      ],
      "timeoutMinutes": 2880,
      "configuration": {
        "requiredCertifications": ["Licensed Appraiser"],
        "minimumExperience": 3,
        "allowSelfAssignment": false
      },
      "onStart": [
        {
          "type": "SetWorkflowVariableAction",
          "name": "track-start",
          "configuration": {
            "variableName": "ReviewStartTime",
            "value": "${now()}"
          }
        },
        {
          "type": "SendNotificationAction",
          "name": "notify-assignee",
          "configuration": {
            "template": "appraisal-review-assigned",
            "recipientExpression": "${assignee.email}",
            "data": {
              "propertyAddress": "${workflowVariables.PropertyAddress}",
              "appraisalValue": "${formatCurrency(workflowVariables.PropertyValue)}",
              "dueDate": "${addDays(now(), 2)}",
              "priority": "${workflowVariables.ClientPriority}"
            }
          }
        },
        {
          "type": "CreateAuditEntryAction",
          "name": "log-assignment",
          "configuration": {
            "category": "Operational",
            "action": "TaskAssigned", 
            "description": "Appraisal review assigned to ${assignee.name}"
          }
        }
      ],
      "onComplete": [
        {
          "type": "UpdateEntityStatusAction",
          "name": "update-appraisal-status",
          "configuration": {
            "entityType": "Appraisal",
            "entityIdExpression": "${workflowVariables.AppraisalId}",
            "status": "Under Review",
            "additionalData": {
              "reviewedBy": "${assignee.id}",
              "reviewStarted": "${workflowVariables.ReviewStartTime}",
              "reviewCompleted": "${now()}"
            }
          }
        },
        {
          "type": "ConditionalAction",
          "name": "route-by-decision",
          "condition": "${activityOutput.Decision == 'Approved'}",
          "thenActions": [
            {
              "type": "PublishEventAction",
              "name": "publish-approval",
              "configuration": {
                "eventType": "AppraisalApproved",
                "eventData": {
                  "appraisalId": "${workflowVariables.AppraisalId}",
                  "approvedBy": "${assignee.id}",
                  "approvedValue": "${activityOutput.ApprovedValue}",
                  "notes": "${activityOutput.Notes}"
                }
              }
            }
          ],
          "elseActions": [
            {
              "type": "SendNotificationAction", 
              "name": "notify-rejection",
              "configuration": {
                "template": "appraisal-rejected",
                "recipientExpression": "${workflowVariables.ClientEmail}",
                "data": {
                  "rejectionReason": "${activityOutput.RejectionReason}",
                  "requiredChanges": "${activityOutput.RequiredChanges}"
                }
              }
            }
          ]
        }
      ],
      "onError": [
        {
          "type": "SendNotificationAction",
          "name": "escalate-error",
          "configuration": {
            "template": "review-error-escalation",
            "recipientExpression": "${supervisor.email}",
            "data": {
              "errorMessage": "${error.message}",
              "activityId": "${activityId}",
              "assignee": "${assignee.name}"
            }
          }
        }
      ],
      "nextActivities": ["quality-check", "revision-required"]
    },
    {
      "id": "quality-check",
      "type": "TaskActivity",
      "name": "Quality Assurance Review", 
      "condition": "${activityOutput.Decision == 'Approved'}",
      "assignmentStrategies": ["quality-specialist", "supervisor"],
      "timeoutMinutes": 1440,
      "onComplete": [
        {
          "type": "ConditionalAction",
          "name": "finalize-or-return",
          "condition": "${activityOutput.QualityApproved == true}",
          "thenActions": [
            {
              "type": "UpdateEntityStatusAction",
              "name": "finalize-appraisal",
              "configuration": {
                "entityType": "Appraisal", 
                "status": "Approved - Final",
                "additionalData": {
                  "qualityScore": "${activityOutput.QualityScore}",
                  "finalApprover": "${assignee.id}"
                }
              }
            }
          ],
          "elseActions": [
            {
              "type": "SetWorkflowVariableAction",
              "name": "set-rework-reason",
              "configuration": {
                "variableName": "ReworkReason", 
                "value": "${activityOutput.QualityIssues}"
              }
            }
          ]
        }
      ],
      "nextActivities": ["end", "initial-assignment"]
    },
    {
      "id": "revision-required",
      "type": "TaskActivity",
      "name": "Handle Revision Requirements",
      "condition": "${activityOutput.Decision == 'Revision Required'}",
      "assignmentStrategies": ["original-appraiser", "supervisor"],
      "onComplete": [
        {
          "type": "PublishEventAction",
          "name": "revision-completed",
          "configuration": {
            "eventType": "AppraisalRevised",
            "eventData": {
              "revisedBy": "${assignee.id}",
              "changes": "${activityOutput.ChangesApplied}"
            }
          }
        }
      ],
      "nextActivities": ["initial-assignment"]
    },
    {
      "id": "end",
      "type": "EndActivity",
      "name": "Complete Appraisal Review"
    }
  ]
}
```

### Recipe 2: High-Value Property Special Review

For properties over $2M requiring enhanced security and multiple approvals.

```json
{
  "workflowId": "high-value-appraisal-review",
  "name": "High-Value Property Review",
  "version": "1.0",
  "condition": "${workflowVariables.PropertyValue > 2000000}",
  "activities": [
    {
      "id": "start", 
      "type": "StartActivity",
      "name": "Initialize High-Value Review",
      "onStart": [
        {
          "type": "LogSecurityEventAction",
          "name": "log-high-value-start",
          "configuration": {
            "eventType": "SensitiveDataAccessed",
            "description": "High-value property review initiated",
            "securityContext": {
              "propertyValue": "${workflowVariables.PropertyValue}",
              "classification": "Confidential"
            }
          }
        }
      ],
      "nextActivities": ["parallel-reviews"]
    },
    {
      "id": "parallel-reviews",
      "type": "ForkActivity",
      "name": "Parallel Review Process",
      "branches": [
        {
          "name": "technical-review",
          "activities": ["technical-validation"]
        },
        {
          "name": "compliance-review",
          "activities": ["compliance-validation"]
        },
        {
          "name": "market-analysis", 
          "activities": ["market-comparison"]
        }
      ],
      "nextActivities": ["consolidate-reviews"]
    },
    {
      "id": "technical-validation",
      "type": "TaskActivity",
      "name": "Technical Property Validation",
      "assignmentStrategies": ["technical-specialist", "senior-appraiser"],
      "timeoutMinutes": 4320,
      "configuration": {
        "requiredCertifications": ["MAI", "SRA"],
        "minimumExperience": 10
      },
      "onComplete": [
        {
          "type": "CreateAuditEntryAction",
          "name": "log-technical-review",
          "configuration": {
            "category": "Technical",
            "action": "TechnicalValidationCompleted",
            "description": "High-value property technical validation: ${activityOutput.TechnicalRating}"
          }
        }
      ]
    },
    {
      "id": "compliance-validation",
      "type": "TaskActivity", 
      "name": "Regulatory Compliance Check",
      "assignmentStrategies": ["compliance-officer", "senior-supervisor"],
      "onComplete": [
        {
          "type": "ConditionalAction",
          "name": "handle-compliance-issues",
          "condition": "${activityOutput.ComplianceIssues.length > 0}",
          "thenActions": [
            {
              "type": "SendNotificationAction",
              "name": "escalate-compliance",
              "configuration": {
                "template": "compliance-issues-found",
                "recipientExpression": "${complianceManager.email}",
                "priority": "High"
              }
            }
          ]
        }
      ]
    },
    {
      "id": "market-comparison",
      "type": "TaskActivity",
      "name": "Market Analysis & Comparables",
      "assignmentStrategies": ["market-analyst", "senior-appraiser"],
      "onComplete": [
        {
          "type": "UpdateEntityStatusAction",
          "name": "update-market-data",
          "configuration": {
            "entityType": "AppraisalMarketData",
            "additionalData": {
              "comparableProperties": "${activityOutput.Comparables}",
              "marketTrends": "${activityOutput.MarketTrends}"
            }
          }
        }
      ]
    },
    {
      "id": "consolidate-reviews",
      "type": "JoinActivity", 
      "name": "Consolidate All Reviews",
      "waitForAll": true,
      "nextActivities": ["senior-approval"]
    },
    {
      "id": "senior-approval",
      "type": "TaskActivity",
      "name": "Senior Management Approval",
      "assignmentStrategies": ["senior-management", "department-head"],
      "configuration": {
        "requiresDigitalSignature": true,
        "minimumApprovalLevel": "Manager"
      },
      "onComplete": [
        {
          "type": "ConditionalAction",
          "name": "finalize-high-value",
          "condition": "${activityOutput.Approved == true}",
          "thenActions": [
            {
              "type": "PublishEventAction",
              "name": "publish-approval", 
              "configuration": {
                "eventType": "HighValueAppraisalApproved",
                "eventData": {
                  "propertyValue": "${workflowVariables.PropertyValue}",
                  "approver": "${assignee.id}",
                  "technicalRating": "${parallelResults.technical-review.TechnicalRating}",
                  "complianceStatus": "${parallelResults.compliance-review.ComplianceStatus}"
                }
              }
            },
            {
              "type": "CreateAuditEntryAction",
              "name": "log-final-approval",
              "configuration": {
                "category": "Financial",
                "action": "HighValueApprovalGranted",
                "description": "High-value appraisal approved: ${formatCurrency(workflowVariables.PropertyValue)}"
              }
            }
          ]
        }
      ],
      "nextActivities": ["end"]
    },
    {
      "id": "end",
      "type": "EndActivity", 
      "name": "Complete High-Value Review"
    }
  ]
}
```

## 🏢 Commercial Property Workflows

### Recipe 3: Commercial Property Risk Assessment

Complex workflow with dynamic routing based on risk factors.

```json
{
  "workflowId": "commercial-risk-assessment",
  "name": "Commercial Property Risk Assessment",
  "version": "1.0",
  "variables": {
    "riskScore": 0,
    "riskFactors": [],
    "requiresInsurance": false
  },
  "activities": [
    {
      "id": "start",
      "type": "StartActivity",
      "name": "Begin Risk Assessment",
      "nextActivities": ["initial-risk-calculation"]
    },
    {
      "id": "initial-risk-calculation",
      "type": "TaskActivity",
      "name": "Calculate Initial Risk Score",
      "assignmentStrategies": ["risk-analyst", "automated-system"],
      "timeoutMinutes": 60,
      "onComplete": [
        {
          "type": "SetWorkflowVariableAction",
          "name": "set-risk-score",
          "configuration": {
            "variableName": "RiskScore",
            "value": "${activityOutput.CalculatedRiskScore}"
          }
        },
        {
          "type": "SetWorkflowVariableAction",
          "name": "set-risk-factors", 
          "configuration": {
            "variableName": "RiskFactors",
            "value": "${activityOutput.IdentifiedRiskFactors}"
          }
        }
      ],
      "nextActivities": ["risk-routing"]
    },
    {
      "id": "risk-routing",
      "type": "SwitchActivity",
      "name": "Route by Risk Level",
      "switchExpression": "${workflowVariables.RiskScore}",
      "cases": [
        {
          "condition": "${workflowVariables.RiskScore <= 30}",
          "name": "low-risk",
          "activities": ["automated-approval"]
        },
        {
          "condition": "${workflowVariables.RiskScore > 30 && workflowVariables.RiskScore <= 70}",
          "name": "medium-risk", 
          "activities": ["standard-review"]
        },
        {
          "condition": "${workflowVariables.RiskScore > 70}",
          "name": "high-risk",
          "activities": ["enhanced-due-diligence"]
        }
      ]
    },
    {
      "id": "automated-approval",
      "type": "TaskActivity",
      "name": "Automated Low-Risk Approval",
      "assignmentStrategies": ["automated-system"],
      "onComplete": [
        {
          "type": "UpdateEntityStatusAction",
          "name": "approve-low-risk",
          "configuration": {
            "entityType": "CommercialProperty",
            "status": "Approved - Low Risk",
            "additionalData": {
              "riskScore": "${workflowVariables.RiskScore}",
              "approvalType": "Automated"
            }
          }
        },
        {
          "type": "SendNotificationAction",
          "name": "notify-approval",
          "configuration": {
            "template": "low-risk-approval",
            "recipientExpression": "${workflowVariables.ClientEmail}"
          }
        }
      ],
      "nextActivities": ["end"]
    },
    {
      "id": "standard-review",
      "type": "TaskActivity",
      "name": "Standard Risk Review",
      "assignmentStrategies": ["commercial-analyst", "senior-appraiser"],
      "timeoutMinutes": 2880,
      "onComplete": [
        {
          "type": "ConditionalAction",
          "name": "standard-decision",
          "condition": "${activityOutput.Approved == true}",
          "thenActions": [
            {
              "type": "UpdateEntityStatusAction",
              "name": "approve-standard",
              "configuration": {
                "entityType": "CommercialProperty",
                "status": "Approved - Standard Review"
              }
            }
          ],
          "elseActions": [
            {
              "type": "SetWorkflowVariableAction",
              "name": "escalate-to-enhanced",
              "configuration": {
                "variableName": "EscalationReason",
                "value": "${activityOutput.EscalationReason}"
              }
            }
          ]
        }
      ],
      "nextActivities": ["end", "enhanced-due-diligence"]
    },
    {
      "id": "enhanced-due-diligence",
      "type": "TaskActivity",
      "name": "Enhanced Due Diligence Review",
      "assignmentStrategies": ["senior-commercial-analyst", "department-head"],
      "timeoutMinutes": 7200,
      "configuration": {
        "requiresMultipleApprovers": true,
        "minimumApprovers": 2
      },
      "onStart": [
        {
          "type": "SendNotificationAction",
          "name": "notify-high-risk",
          "configuration": {
            "template": "high-risk-review-started",
            "recipientExpression": "${departmentHead.email}",
            "data": {
              "riskScore": "${workflowVariables.RiskScore}",
              "riskFactors": "${workflowVariables.RiskFactors}"
            }
          }
        }
      ],
      "onComplete": [
        {
          "type": "ConditionalAction",
          "name": "enhanced-decision",
          "condition": "${activityOutput.RequiresInsurance == true}",
          "thenActions": [
            {
              "type": "SetWorkflowVariableAction",
              "name": "flag-insurance",
              "configuration": {
                "variableName": "RequiresInsurance",
                "value": true
              }
            }
          ]
        },
        {
          "type": "CreateAuditEntryAction",
          "name": "log-enhanced-review",
          "configuration": {
            "category": "Compliance",
            "action": "EnhancedDueDiligenceCompleted",
            "description": "High-risk commercial property review completed"
          }
        }
      ],
      "nextActivities": ["insurance-verification", "end"]
    },
    {
      "id": "insurance-verification",
      "type": "TaskActivity",
      "name": "Insurance Requirements Verification",
      "condition": "${workflowVariables.RequiresInsurance == true}",
      "assignmentStrategies": ["insurance-specialist"],
      "onComplete": [
        {
          "type": "UpdateEntityStatusAction",
          "name": "update-insurance-status",
          "configuration": {
            "entityType": "CommercialProperty",
            "additionalData": {
              "insuranceRequired": true,
              "insuranceType": "${activityOutput.RequiredInsuranceType}",
              "minimumCoverage": "${activityOutput.MinimumCoverage}"
            }
          }
        }
      ],
      "nextActivities": ["end"]
    },
    {
      "id": "end",
      "type": "EndActivity",
      "name": "Complete Risk Assessment"
    }
  ]
}
```

## 🔧 Custom Assignment Service Examples

### Example 1: Workload Balancing Service

```csharp
using Assignment.Workflow.AssigneeSelection.Core;

public class WorkloadBalancerAssignmentService : ICustomAssignmentService
{
    private readonly IUserService _userService;
    private readonly IWorkflowActivityExecutionRepository _executionRepository;
    private readonly ILogger<WorkloadBalancerAssignmentService> _logger;

    public string ServiceName => "workload-balancer";

    public WorkloadBalancerAssignmentService(
        IUserService userService,
        IWorkflowActivityExecutionRepository executionRepository,
        ILogger<WorkloadBalancerAssignmentService> logger)
    {
        _userService = userService;
        _executionRepository = executionRepository;
        _logger = logger;
    }

    public async Task<CustomAssignmentResult> AssignAsync(
        AssignmentContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get eligible users based on activity requirements
            var eligibleUsers = await GetEligibleUsers(context);
            
            if (!eligibleUsers.Any())
            {
                return CustomAssignmentResult.Skip("No eligible users found");
            }

            // Calculate current workload for each user
            var userWorkloads = await CalculateUserWorkloads(eligibleUsers, cancellationToken);
            
            // Find user with lowest workload
            var selectedUser = userWorkloads
                .OrderBy(x => x.ActiveTasks)
                .ThenBy(x => x.OverdueTasks)
                .First();

            _logger.LogInformation(
                "Workload balancer assigned user {UserId} with {ActiveTasks} active tasks", 
                selectedUser.UserId, selectedUser.ActiveTasks);

            return CustomAssignmentResult.Success(
                selectedUser.UserId,
                $"Assigned to {selectedUser.UserName} (workload: {selectedUser.ActiveTasks} active tasks)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in workload balancer assignment");
            return CustomAssignmentResult.Error($"Assignment failed: {ex.Message}");
        }
    }

    private async Task<List<UserInfo>> GetEligibleUsers(AssignmentContext context)
    {
        var users = await _userService.GetActiveUsersAsync();
        
        // Filter by activity requirements
        if (context.ActivityDefinition.Configuration?.ContainsKey("requiredCertifications") == true)
        {
            var requiredCerts = context.ActivityDefinition.Configuration["requiredCertifications"] as string[];
            users = users.Where(u => requiredCerts.All(cert => u.Certifications.Contains(cert))).ToList();
        }
        
        if (context.ActivityDefinition.Configuration?.ContainsKey("minimumExperience") == true)
        {
            var minExperience = Convert.ToInt32(context.ActivityDefinition.Configuration["minimumExperience"]);
            users = users.Where(u => u.YearsOfExperience >= minExperience).ToList();
        }

        return users;
    }

    private async Task<List<UserWorkload>> CalculateUserWorkloads(
        List<UserInfo> users, 
        CancellationToken cancellationToken)
    {
        var workloads = new List<UserWorkload>();

        foreach (var user in users)
        {
            var activeTasksCount = await _executionRepository.CountActiveTasksByUserAsync(
                user.Id, cancellationToken);
            
            var overdueTasksCount = await _executionRepository.CountOverdueTasksByUserAsync(
                user.Id, cancellationToken);

            workloads.Add(new UserWorkload
            {
                UserId = user.Id,
                UserName = user.Name,
                ActiveTasks = activeTasksCount,
                OverdueTasks = overdueTasksCount
            });
        }

        return workloads;
    }

    private class UserWorkload
    {
        public string UserId { get; set; } = default!;
        public string UserName { get; set; } = default!;
        public int ActiveTasks { get; set; }
        public int OverdueTasks { get; set; }
    }
}
```

### Example 2: Expertise Matching Service

```csharp
public class ExpertiseMatcherAssignmentService : ICustomAssignmentService
{
    private readonly IUserService _userService;
    private readonly IPropertyService _propertyService;

    public string ServiceName => "expertise-matcher";

    public async Task<CustomAssignmentResult> AssignAsync(
        AssignmentContext context, 
        CancellationToken cancellationToken = default)
    {
        // Get property details from workflow variables
        var propertyType = context.WorkflowVariables.GetValue<string>("PropertyType");
        var propertyValue = context.WorkflowVariables.GetValue<decimal>("PropertyValue");
        var location = context.WorkflowVariables.GetValue<string>("PropertyLocation");

        // Find users with matching expertise
        var users = await _userService.GetUsersByExpertiseAsync(propertyType);
        
        // Score users based on expertise match
        var scoredUsers = await ScoreUsersByExpertise(users, propertyType, propertyValue, location);
        
        var bestMatch = scoredUsers.OrderByDescending(x => x.Score).FirstOrDefault();
        
        if (bestMatch?.Score >= 80) // Minimum expertise threshold
        {
            return CustomAssignmentResult.Success(
                bestMatch.UserId,
                $"Matched to {bestMatch.UserName} - expertise score: {bestMatch.Score}%");
        }

        return CustomAssignmentResult.Skip("No users with sufficient expertise match found");
    }

    private async Task<List<UserExpertiseScore>> ScoreUsersByExpertise(
        List<UserInfo> users, 
        string propertyType, 
        decimal propertyValue, 
        string location)
    {
        var scoredUsers = new List<UserExpertiseScore>();

        foreach (var user in users)
        {
            var score = 0;
            
            // Property type expertise (40 points)
            if (user.Specializations.Contains(propertyType))
                score += 40;
            else if (user.Specializations.Any(s => IsRelatedPropertyType(s, propertyType)))
                score += 20;

            // Value range experience (30 points)
            if (IsInPreferredValueRange(user.ValueRangeExperience, propertyValue))
                score += 30;
            else if (IsInAcceptableValueRange(user.ValueRangeExperience, propertyValue))
                score += 15;

            // Location familiarity (20 points)
            if (user.GeographicAreas.Contains(location))
                score += 20;

            // Recent experience (10 points)
            var recentExperience = await GetRecentExperienceCount(user.Id, propertyType);
            if (recentExperience >= 5)
                score += 10;
            else if (recentExperience >= 2)
                score += 5;

            scoredUsers.Add(new UserExpertiseScore
            {
                UserId = user.Id,
                UserName = user.Name,
                Score = score
            });
        }

        return scoredUsers;
    }

    private class UserExpertiseScore
    {
        public string UserId { get; set; } = default!;
        public string UserName { get; set; } = default!;
        public int Score { get; set; }
    }
}
```

## 🎯 Advanced Action Examples

### Example 1: Custom Business Logic Action

```csharp
using Assignment.Workflow.Actions.Core;

public class CalculateCommissionAction : IWorkflowAction
{
    private readonly ICommissionService _commissionService;
    private readonly IUserService _userService;

    public string ActionType => "CalculateCommissionAction";
    public string Name { get; }
    public Dictionary<string, object> Configuration { get; }

    public CalculateCommissionAction(
        string name, 
        Dictionary<string, object> configuration,
        ICommissionService commissionService,
        IUserService userService)
    {
        Name = name;
        Configuration = configuration;
        _commissionService = commissionService;
        _userService = userService;
    }

    public async Task<ActionExecutionResult> ExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var propertyValue = context.WorkflowVariables.GetValue<decimal>("PropertyValue");
            var assigneeId = context.AssigneeId;
            
            if (string.IsNullOrEmpty(assigneeId))
            {
                return ActionExecutionResult.Failed("No assignee found for commission calculation");
            }

            var user = await _userService.GetUserByIdAsync(assigneeId);
            var commissionRate = await _commissionService.GetCommissionRateAsync(user.Level);
            
            var commissionAmount = propertyValue * (commissionRate / 100);
            
            // Set commission as workflow variable
            context.WorkflowVariables.SetValue("CalculatedCommission", commissionAmount);
            context.WorkflowVariables.SetValue("CommissionRate", commissionRate);
            
            return ActionExecutionResult.Succeeded($"Commission calculated: {commissionAmount:C}");
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Failed($"Commission calculation failed: {ex.Message}");
        }
    }
}
```

### Example 2: Dynamic Form Generation Action

```csharp
public class GenerateDynamicFormAction : IWorkflowAction
{
    private readonly IFormService _formService;
    private readonly ITemplateEngine _templateEngine;

    public string ActionType => "GenerateDynamicFormAction";
    public string Name { get; }
    public Dictionary<string, object> Configuration { get; }

    public async Task<ActionExecutionResult> ExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var formTemplate = Configuration.GetValue<string>("formTemplate");
            var propertyType = context.WorkflowVariables.GetValue<string>("PropertyType");
            
            // Generate form based on property type
            var formDefinition = await _formService.GenerateFormAsync(formTemplate, new
            {
                PropertyType = propertyType,
                RequiredFields = GetRequiredFieldsForPropertyType(propertyType),
                ValidationRules = GetValidationRulesForPropertyType(propertyType)
            });

            // Store form definition in workflow variables
            context.WorkflowVariables.SetValue("DynamicFormDefinition", formDefinition);
            
            return ActionExecutionResult.Succeeded("Dynamic form generated successfully");
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Failed($"Form generation failed: {ex.Message}");
        }
    }

    private List<string> GetRequiredFieldsForPropertyType(string propertyType)
    {
        return propertyType switch
        {
            "Residential" => new List<string> { "SquareFootage", "Bedrooms", "Bathrooms", "YearBuilt" },
            "Commercial" => new List<string> { "SquareFootage", "ZoningType", "ParkingSpaces", "AnnualIncome" },
            "Industrial" => new List<string> { "SquareFootage", "CeilingHeight", "LoadingDocks", "PowerCapacity" },
            _ => new List<string> { "SquareFootage", "YearBuilt" }
        };
    }
}
```

## 📊 Monitoring & Analytics Examples

### Example 1: Performance Dashboard Workflow

```json
{
  "workflowId": "performance-analytics",
  "name": "Generate Performance Analytics",
  "version": "1.0",
  "schedule": "0 0 * * 1", 
  "activities": [
    {
      "id": "collect-metrics",
      "type": "TaskActivity",
      "name": "Collect Performance Metrics",
      "assignmentStrategies": ["automated-system"],
      "onComplete": [
        {
          "type": "CallWebhookAction",
          "name": "send-to-analytics",
          "configuration": {
            "url": "https://analytics.company.com/api/workflow-metrics",
            "method": "POST",
            "headers": {
              "Authorization": "Bearer ${secrets.analyticsApiKey}",
              "Content-Type": "application/json"
            },
            "payload": {
              "period": "${activityOutput.Period}",
              "metrics": "${activityOutput.Metrics}",
              "timestamp": "${now()}"
            }
          }
        }
      ]
    }
  ]
}
```

### Example 2: SLA Monitoring Workflow

```json
{
  "workflowId": "sla-monitoring",
  "name": "Monitor SLA Compliance",
  "version": "1.0",
  "activities": [
    {
      "id": "check-sla-violations",
      "type": "TaskActivity", 
      "name": "Check for SLA Violations",
      "assignmentStrategies": ["automated-system"],
      "onComplete": [
        {
          "type": "ConditionalAction",
          "name": "handle-violations",
          "condition": "${activityOutput.Violations.length > 0}",
          "thenActions": [
            {
              "type": "SendNotificationAction",
              "name": "alert-management",
              "configuration": {
                "template": "sla-violations-detected",
                "recipientExpression": "${managementTeam.emails}",
                "priority": "High",
                "data": {
                  "violationCount": "${activityOutput.Violations.length}",
                  "violations": "${activityOutput.Violations}"
                }
              }
            },
            {
              "type": "CreateAuditEntryAction",
              "name": "log-violations",
              "configuration": {
                "category": "Compliance",
                "action": "SLAViolationDetected",
                "description": "${activityOutput.Violations.length} SLA violations detected"
              }
            }
          ]
        }
      ]
    }
  ]
}
```

## 🚀 Integration Examples

### Example 1: External System Integration

```csharp
public class ExternalSystemIntegrationAction : IWorkflowAction
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExternalSystemIntegrationAction> _logger;

    public string ActionType => "ExternalSystemIntegrationAction";
    public string Name { get; }
    public Dictionary<string, object> Configuration { get; }

    public async Task<ActionExecutionResult> ExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var systemName = Configuration.GetValue<string>("systemName");
            var operation = Configuration.GetValue<string>("operation");
            var endpoint = Configuration.GetValue<string>("endpoint");

            using var httpClient = _httpClientFactory.CreateClient(systemName);
            
            var requestPayload = BuildRequestPayload(context, operation);
            var response = await httpClient.PostAsync(endpoint, requestPayload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                context.WorkflowVariables.SetValue($"{systemName}_Response", responseContent);
                
                return ActionExecutionResult.Succeeded($"Successfully integrated with {systemName}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("External system integration failed: {Error}", errorContent);
                return ActionExecutionResult.Failed($"External system returned error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during external system integration");
            return ActionExecutionResult.Failed($"Integration failed: {ex.Message}");
        }
    }

    private StringContent BuildRequestPayload(ActivityContext context, string operation)
    {
        var payload = new
        {
            Operation = operation,
            WorkflowInstanceId = context.WorkflowInstanceId,
            ActivityId = context.ActivityId,
            Data = context.WorkflowVariables.Variables,
            Timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(payload);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
```

This cookbook provides practical, real-world examples that you can adapt for your specific use cases. Each example demonstrates different aspects of the workflow engine's capabilities, from basic task routing to complex business logic integration.