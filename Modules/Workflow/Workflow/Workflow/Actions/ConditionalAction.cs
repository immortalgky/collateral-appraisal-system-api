using Workflow.Workflow.Actions.Core;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine.Expression;

namespace Workflow.Workflow.Actions;

/// <summary>
/// Action that executes different actions based on conditional logic
/// Enables complex decision-making and branching in workflow actions
/// </summary>
public class ConditionalAction : WorkflowActionBase
{
    private readonly IWorkflowActionExecutor _actionExecutor;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    public ConditionalAction(
        IWorkflowActionExecutor actionExecutor,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<ConditionalAction> logger) : base(logger)
    {
        _actionExecutor = actionExecutor;
        _expressionEvaluator = expressionEvaluator;
    }

    public override string ActionType => "ConditionalAction";
    public override string Name => "Conditional Action";
    public override string Description => "Executes different actions based on conditional expressions and workflow state";

    protected override async Task<ActionExecutionResult> ExecuteActionAsync(
        ActivityContext context,
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        var conditionalRules = GetParameter<List<ConditionalRule>>(actionParameters, "rules", new List<ConditionalRule>());
        var defaultActions = GetParameter<List<WorkflowActionConfiguration>>(actionParameters, "defaultActions", new List<WorkflowActionConfiguration>());
        var executeMode = GetParameter<string>(actionParameters, "executeMode", "FirstMatch"); // FirstMatch, AllMatches, BestMatch
        var continueOnError = GetParameter<bool>(actionParameters, "continueOnError", true);

        Logger.LogDebug("Executing conditional action with {RuleCount} rules for activity {ActivityId}",
            conditionalRules.Count, context.ActivityId);

        try
        {
            var matchedRules = new List<(ConditionalRule Rule, int Index, double Score)>();
            var evaluationResults = new List<ConditionalEvaluationResult>();

            // Evaluate all conditional rules
            for (int i = 0; i < conditionalRules.Count; i++)
            {
                var rule = conditionalRules[i];
                var evaluationResult = await EvaluateRuleAsync(rule, context, cancellationToken);
                evaluationResults.Add(evaluationResult);

                if (evaluationResult.IsMatch)
                {
                    var score = CalculateRuleScore(rule, evaluationResult);
                    matchedRules.Add((rule, i, score));

                    Logger.LogDebug("Rule {RuleIndex} matched with score {Score}: {Condition}",
                        i, score, rule.Condition);

                    // Stop after first match if in FirstMatch mode
                    if (executeMode == "FirstMatch")
                    {
                        break;
                    }
                }
            }

            // Determine which rules to execute based on mode
            var rulesToExecute = DetermineRulesToExecute(matchedRules, executeMode);

            var allResults = new List<ActionExecutionResult>();
            var executedRuleCount = 0;

            // Execute actions from matched rules
            if (rulesToExecute.Any())
            {
                foreach (var (rule, index, score) in rulesToExecute)
                {
                    Logger.LogInformation("Executing actions from rule {RuleIndex} (score: {Score}): {RuleName}",
                        index, score, rule.Name ?? $"Rule{index}");

                    var ruleResult = await ExecuteRuleActionsAsync(rule, context, continueOnError, cancellationToken);
                    allResults.AddRange(ruleResult.Results);
                    executedRuleCount++;

                    // Stop on first failure if continueOnError is false
                    if (!ruleResult.IsSuccess && !continueOnError)
                    {
                        break;
                    }
                }
            }
            else if (defaultActions.Any())
            {
                Logger.LogInformation("No rules matched, executing {DefaultActionCount} default actions", defaultActions.Count);

                var defaultResult = await _actionExecutor.ExecuteActionsAsync(
                    context, defaultActions, ActivityLifecycleEvent.OnStart, cancellationToken);
                allResults.AddRange(defaultResult.Results);
            }
            else
            {
                Logger.LogDebug("No conditional rules matched and no default actions specified");
            }

            // Calculate overall result
            var successfulResults = allResults.Count(r => r.IsSuccess);
            var failedResults = allResults.Count(r => !r.IsSuccess);
            var isOverallSuccess = failedResults == 0 || continueOnError;

            var resultMessage = executedRuleCount > 0 
                ? $"Executed {executedRuleCount} conditional rule(s): {successfulResults} successful, {failedResults} failed"
                : $"Executed {defaultActions.Count} default action(s): {successfulResults} successful, {failedResults} failed";

            var outputData = new Dictionary<string, object>
            {
                ["matchedRules"] = matchedRules.Count,
                ["executedRules"] = executedRuleCount,
                ["totalActions"] = allResults.Count,
                ["successfulActions"] = successfulResults,
                ["failedActions"] = failedResults,
                ["executeMode"] = executeMode,
                ["evaluationResults"] = evaluationResults.Select(r => new {
                    condition = r.Condition,
                    isMatch = r.IsMatch,
                    evaluatedValue = r.EvaluatedValue,
                    error = r.ErrorMessage
                }).ToList(),
                ["executionTimestamp"] = DateTime.UtcNow
            };

            // Include detailed results if not too many
            if (allResults.Count <= 10)
            {
                outputData["actionResults"] = allResults.Select(r => new {
                    success = r.IsSuccess,
                    message = r.ResultMessage ?? r.ErrorMessage,
                    duration = r.ExecutionDuration.TotalMilliseconds
                }).ToList();
            }

            Logger.LogInformation("Conditional action completed for activity {ActivityId}: {MatchedRules} matched, {ExecutedRules} executed",
                context.ActivityId, matchedRules.Count, executedRuleCount);

            return isOverallSuccess
                ? ActionExecutionResult.Success(resultMessage, outputData)
                : ActionExecutionResult.Failed($"Conditional action failed: {resultMessage}");
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error executing conditional action: {ex.Message}";
            Logger.LogError(ex, "Error executing conditional action for activity {ActivityId}",
                context.ActivityId);
            
            return ActionExecutionResult.Failed(errorMessage);
        }
    }

    public override async Task<ActionValidationResult> ValidateAsync(
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var conditionalRules = GetParameter<List<ConditionalRule>>(actionParameters, "rules", new List<ConditionalRule>());
        var defaultActions = GetParameter<List<WorkflowActionConfiguration>>(actionParameters, "defaultActions", new List<WorkflowActionConfiguration>());
        var executeMode = GetParameter<string>(actionParameters, "executeMode", "FirstMatch");

        // Validate execute mode
        var validModes = new[] { "FirstMatch", "AllMatches", "BestMatch" };
        if (!validModes.Contains(executeMode))
        {
            errors.Add($"Invalid executeMode '{executeMode}'. Valid modes: {string.Join(", ", validModes)}");
        }

        // Must have rules or default actions
        if (!conditionalRules.Any() && !defaultActions.Any())
        {
            errors.Add("ConditionalAction must have either 'rules' or 'defaultActions' specified");
        }

        // Validate each conditional rule
        for (int i = 0; i < conditionalRules.Count; i++)
        {
            var rule = conditionalRules[i];
            var ruleValidation = await ValidateConditionalRuleAsync(rule, i, cancellationToken);
            errors.AddRange(ruleValidation.Errors);
            warnings.AddRange(ruleValidation.Warnings);
        }

        // Validate default actions
        if (defaultActions.Any())
        {
            var defaultActionsValidation = await _actionExecutor.ValidateActionsAsync(defaultActions, cancellationToken);
            if (!defaultActionsValidation.IsValid)
            {
                errors.AddRange(defaultActionsValidation.AllErrors.Select(e => $"Default action error: {e}"));
            }
            warnings.AddRange(defaultActionsValidation.AllWarnings.Select(w => $"Default action warning: {w}"));
        }

        // Warn about performance with many rules
        if (conditionalRules.Count > 20)
        {
            warnings.Add($"Large number of conditional rules ({conditionalRules.Count}) may impact performance. Consider optimizing rule order or using simpler conditions.");
        }

        return errors.Any() ? 
            ActionValidationResult.Invalid(errors, warnings) : 
            ActionValidationResult.Valid(warnings);
    }

    private async Task<ConditionalEvaluationResult> EvaluateRuleAsync(
        ConditionalRule rule, 
        ActivityContext context, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Build variables for expression evaluation
            var variables = context.Variables
                .Concat(context.Properties)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Add workflow context variables
            variables["workflow.instanceId"] = context.WorkflowInstanceId.ToString();
            variables["workflow.activityId"] = context.ActivityId;
            variables["workflow.assignee"] = context.CurrentAssignee ?? "";

            // Evaluate the condition
            var result = await _expressionEvaluator.EvaluateBooleanAsync(rule.Condition, variables, cancellationToken);

            return new ConditionalEvaluationResult
            {
                Condition = rule.Condition,
                IsMatch = result,
                EvaluatedValue = result.ToString(),
                Variables = variables
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to evaluate conditional rule: {Condition}", rule.Condition);
            
            return new ConditionalEvaluationResult
            {
                Condition = rule.Condition,
                IsMatch = false,
                ErrorMessage = ex.Message,
                Variables = new Dictionary<string, object>()
            };
        }
    }

    private static double CalculateRuleScore(ConditionalRule rule, ConditionalEvaluationResult evaluationResult)
    {
        // Base score for matching
        double score = 1.0;

        // Add priority bonus
        score += rule.Priority * 0.1;

        // Add specificity bonus (more complex conditions get higher scores)
        var conditionComplexity = rule.Condition.Count(c => c == '&' || c == '|' || c == '=' || c == '>' || c == '<');
        score += conditionComplexity * 0.05;

        // Bonus for named rules (assume they are more important)
        if (!string.IsNullOrEmpty(rule.Name))
        {
            score += 0.1;
        }

        return Math.Round(score, 2);
    }

    private static List<(ConditionalRule Rule, int Index, double Score)> DetermineRulesToExecute(
        List<(ConditionalRule Rule, int Index, double Score)> matchedRules,
        string executeMode)
    {
        return executeMode switch
        {
            "FirstMatch" => matchedRules.Take(1).ToList(),
            "BestMatch" => matchedRules.OrderByDescending(r => r.Score).Take(1).ToList(),
            "AllMatches" => matchedRules.OrderByDescending(r => r.Score).ToList(),
            _ => matchedRules.Take(1).ToList()
        };
    }

    private async Task<ActionBatchExecutionResult> ExecuteRuleActionsAsync(
        ConditionalRule rule,
        ActivityContext context,
        bool continueOnError,
        CancellationToken cancellationToken)
    {
        if (!rule.Actions.Any())
        {
            return ActionBatchExecutionResult.FromResults(new List<ActionExecutionResult>(), TimeSpan.Zero);
        }

        // Execute all actions in the rule
        return await _actionExecutor.ExecuteActionsAsync(
            context, rule.Actions, ActivityLifecycleEvent.OnStart, cancellationToken);
    }

    private async Task<ActionValidationResult> ValidateConditionalRuleAsync(
        ConditionalRule rule,
        int ruleIndex,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate condition
        if (string.IsNullOrEmpty(rule.Condition))
        {
            errors.Add($"Rule {ruleIndex}: Condition cannot be empty");
        }
        else
        {
            // Validate condition syntax
            var conditionValidation = await _expressionEvaluator.ValidateExpressionAsync(rule.Condition);
            if (!conditionValidation.IsValid)
            {
                errors.Add($"Rule {ruleIndex}: Invalid condition '{rule.Condition}': {conditionValidation.ErrorMessage}");
            }
            warnings.AddRange(conditionValidation.Warnings.Select(w => $"Rule {ruleIndex}: {w}"));
        }

        // Validate actions
        if (!rule.Actions.Any())
        {
            warnings.Add($"Rule {ruleIndex}: No actions specified - rule will not perform any operations when matched");
        }
        else
        {
            var actionsValidation = await _actionExecutor.ValidateActionsAsync(rule.Actions, cancellationToken);
            if (!actionsValidation.IsValid)
            {
                errors.AddRange(actionsValidation.AllErrors.Select(e => $"Rule {ruleIndex} action error: {e}"));
            }
            warnings.AddRange(actionsValidation.AllWarnings.Select(w => $"Rule {ruleIndex} action warning: {w}"));
        }

        // Validate priority
        if (rule.Priority < 0 || rule.Priority > 1000)
        {
            warnings.Add($"Rule {ruleIndex}: Priority {rule.Priority} is outside recommended range (0-1000)");
        }

        return errors.Any() ? 
            ActionValidationResult.Invalid(errors, warnings) : 
            ActionValidationResult.Valid(warnings);
    }
}

/// <summary>
/// Represents a conditional rule with condition and actions to execute
/// </summary>
public class ConditionalRule
{
    /// <summary>
    /// Optional name for the rule (for logging and debugging)
    /// </summary>
    public string? Name { get; init; }
    
    /// <summary>
    /// Condition expression to evaluate
    /// </summary>
    public string Condition { get; init; } = default!;
    
    /// <summary>
    /// Actions to execute if condition is true
    /// </summary>
    public List<WorkflowActionConfiguration> Actions { get; init; } = new();
    
    /// <summary>
    /// Priority for rule execution order (higher = higher priority)
    /// </summary>
    public int Priority { get; init; } = 0;
    
    /// <summary>
    /// Description of what this rule does
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Result of evaluating a conditional rule
/// </summary>
public class ConditionalEvaluationResult
{
    public string Condition { get; init; } = default!;
    public bool IsMatch { get; init; }
    public string? EvaluatedValue { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object> Variables { get; init; } = new();
}