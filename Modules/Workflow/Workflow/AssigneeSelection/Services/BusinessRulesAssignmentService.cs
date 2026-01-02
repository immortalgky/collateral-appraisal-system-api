namespace Workflow.AssigneeSelection.Services;

/// <summary>
/// Custom assignment service that applies complex business rules for task assignment
/// Demonstrates loan amount thresholds, risk levels, and specialized team routing
/// </summary>
public class BusinessRulesAssignmentService : ICustomAssignmentService
{
    private readonly ILogger<BusinessRulesAssignmentService> _logger;

    // Business rule thresholds
    private const decimal HIGH_VALUE_THRESHOLD = 1_000_000m;
    private const decimal MEDIUM_VALUE_THRESHOLD = 500_000m;
    private const decimal CRITICAL_VALUE_THRESHOLD = 5_000_000m;

    public BusinessRulesAssignmentService(ILogger<BusinessRulesAssignmentService> logger)
    {
        _logger = logger;
    }

    public async Task<CustomAssignmentResult> GetAssignmentContextAsync(
        string workflowInstanceId, 
        string activityId, 
        Dictionary<string, object> workflowVariables, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Evaluating business rules for assignment in workflow {WorkflowInstanceId}, activity {ActivityId}", 
                workflowInstanceId, activityId);

            // Extract key variables for business logic
            var loanAmount = GetLoanAmount(workflowVariables);
            var riskLevel = GetRiskLevel(workflowVariables);
            var propertyType = GetPropertyType(workflowVariables);
            var isRefinancing = GetBooleanValue(workflowVariables, "isRefinancing", false);
            var applicantCreditScore = GetIntValue(workflowVariables, "creditScore", 0);

            _logger.LogDebug("Business rule inputs - Amount: {Amount}, Risk: {Risk}, Property: {Property}, Credit: {Credit}", 
                loanAmount, riskLevel, propertyType, applicantCreditScore);

            // Apply business rules in priority order

            // Rule 1: Critical value loans require committee review
            if (loanAmount >= CRITICAL_VALUE_THRESHOLD)
            {
                _logger.LogInformation("Critical value loan (${Amount:N0}) requires committee review", loanAmount);
                
                return new CustomAssignmentResult
                {
                    UseCustomAssignment = true,
                    SpecificGroup = "EXECUTIVE_COMMITTEE",
                    CustomStrategies = new List<string> { "Manual", "Supervisor" },
                    Reason = $"Critical value loan ${loanAmount:N0} requires executive committee review",
                    Metadata = new Dictionary<string, object>
                    {
                        ["LoanAmount"] = loanAmount,
                        ["RequiredApprovals"] = 3,
                        ["EscalationLevel"] = "Executive"
                    }
                };
            }

            // Rule 2: High-value + High-risk requires senior review
            if (loanAmount >= HIGH_VALUE_THRESHOLD && riskLevel == "High")
            {
                _logger.LogInformation("High-value high-risk loan (${Amount:N0}) requires senior committee", loanAmount);
                
                return new CustomAssignmentResult
                {
                    UseCustomAssignment = true,
                    SpecificGroup = "SENIOR_REVIEW_COMMITTEE",
                    CustomStrategies = new List<string> { "Supervisor", "WorkloadBased" },
                    CustomProperties = new Dictionary<string, object>
                    {
                        ["skillsRequired"] = new[] { "Senior_Underwriting", "Risk_Analysis" },
                        ["priorityLevel"] = "High",
                        ["taskWeight"] = 3
                    },
                    Reason = $"High-value (${loanAmount:N0}) high-risk loan requires senior expertise",
                    Metadata = new Dictionary<string, object>
                    {
                        ["LoanAmount"] = loanAmount,
                        ["RiskLevel"] = riskLevel,
                        ["RequiredExperience"] = "5+ years"
                    }
                };
            }

            // Rule 3: Commercial property requires specialized team
            if (propertyType == "Commercial" && loanAmount >= MEDIUM_VALUE_THRESHOLD)
            {
                _logger.LogInformation("Commercial property loan (${Amount:N0}) requires commercial specialists", loanAmount);
                
                return new CustomAssignmentResult
                {
                    UseCustomAssignment = true,
                    SpecificGroup = "COMMERCIAL_LENDING_TEAM",
                    CustomStrategies = new List<string> { "SkillBased", "RoundRobin" },
                    CustomProperties = new Dictionary<string, object>
                    {
                        ["skillsRequired"] = new[] { "Commercial_Real_Estate", "Financial_Analysis" },
                        ["priorityLevel"] = "Medium",
                        ["taskWeight"] = 2
                    },
                    Reason = $"Commercial property loan requires specialized commercial lending expertise",
                    Metadata = new Dictionary<string, object>
                    {
                        ["PropertyType"] = propertyType,
                        ["LoanAmount"] = loanAmount,
                        ["SpecializationRequired"] = "Commercial Real Estate"
                    }
                };
            }

            // Rule 4: Low credit score requires manual review
            if (applicantCreditScore > 0 && applicantCreditScore < 600)
            {
                _logger.LogInformation("Low credit score ({Score}) requires manual underwriting review", applicantCreditScore);
                
                return new CustomAssignmentResult
                {
                    UseCustomAssignment = true,
                    CustomStrategies = new List<string> { "Manual", "Supervisor" },
                    CustomProperties = new Dictionary<string, object>
                    {
                        ["skillsRequired"] = new[] { "Credit_Analysis", "Risk_Assessment" },
                        ["priorityLevel"] = "High",
                        ["requiresConfirmation"] = true
                    },
                    Reason = $"Low credit score ({applicantCreditScore}) requires specialized underwriting review",
                    Metadata = new Dictionary<string, object>
                    {
                        ["CreditScore"] = applicantCreditScore,
                        ["ReviewType"] = "Manual Underwriting",
                        ["AdditionalDocumentationRequired"] = true
                    }
                };
            }

            // Rule 5: High-value refinancing gets priority processing
            if (isRefinancing && loanAmount >= MEDIUM_VALUE_THRESHOLD)
            {
                _logger.LogInformation("High-value refinancing (${Amount:N0}) gets priority processing", loanAmount);
                
                return new CustomAssignmentResult
                {
                    UseCustomAssignment = true,
                    CustomStrategies = new List<string> { "WorkloadBased", "RoundRobin" },
                    CustomProperties = new Dictionary<string, object>
                    {
                        ["priorityLevel"] = "High",
                        ["taskWeight"] = 2,
                        ["skillsRequired"] = new[] { "Refinancing_Specialist" }
                    },
                    Reason = $"High-value refinancing gets priority processing",
                    Metadata = new Dictionary<string, object>
                    {
                        ["IsRefinancing"] = isRefinancing,
                        ["LoanAmount"] = loanAmount,
                        ["ProcessingPriority"] = "High"
                    }
                };
            }

            // No custom business rules apply
            _logger.LogDebug("No business rules matched for workflow {WorkflowInstanceId} - using standard assignment", workflowInstanceId);
            return CustomAssignmentResult.NoCustomAssignment("No specific business rules apply to this case");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating business rules for workflow {WorkflowInstanceId}, activity {ActivityId}", 
                workflowInstanceId, activityId);
            
            return CustomAssignmentResult.NoCustomAssignment($"Error in business rules evaluation: {ex.Message}");
        }
    }

    private decimal GetLoanAmount(Dictionary<string, object> variables)
    {
        var possibleKeys = new[] { "loanAmount", "LoanAmount", "loan_amount", "amount", "requestedAmount" };
        
        foreach (var key in possibleKeys)
        {
            if (variables.TryGetValue(key, out var value) && value != null)
            {
                if (decimal.TryParse(value.ToString(), out var amount))
                {
                    return amount;
                }
            }
        }
        
        return 0m;
    }

    private string GetRiskLevel(Dictionary<string, object> variables)
    {
        var possibleKeys = new[] { "riskLevel", "RiskLevel", "risk_level", "riskAssessment" };
        
        foreach (var key in possibleKeys)
        {
            if (variables.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value?.ToString()))
            {
                return value.ToString()!;
            }
        }
        
        return "Medium"; // Default risk level
    }

    private string GetPropertyType(Dictionary<string, object> variables)
    {
        var possibleKeys = new[] { "propertyType", "PropertyType", "property_type", "collateralType" };
        
        foreach (var key in possibleKeys)
        {
            if (variables.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value?.ToString()))
            {
                return value.ToString()!;
            }
        }
        
        return "Residential"; // Default property type
    }

    private bool GetBooleanValue(Dictionary<string, object> variables, string key, bool defaultValue)
    {
        if (variables.TryGetValue(key, out var value))
        {
            if (value is bool boolValue)
                return boolValue;
                
            if (bool.TryParse(value?.ToString(), out var parsedValue))
                return parsedValue;
        }
        
        return defaultValue;
    }

    private int GetIntValue(Dictionary<string, object> variables, string key, int defaultValue)
    {
        if (variables.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var intValue))
        {
            return intValue;
        }
        
        return defaultValue;
    }
}