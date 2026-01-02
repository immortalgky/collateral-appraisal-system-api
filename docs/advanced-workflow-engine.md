# Advanced Workflow Engine Documentation

## Overview

The advanced workflow engine extends the existing workflow system with sophisticated expression evaluation and parallel execution capabilities. This document provides comprehensive details for developers working with the enhanced workflow features.

## Table of Contents

1. [Expression Engine](#expression-engine)
2. [Fork/Join Activities](#forkjoin-activities)
3. [API Reference](#api-reference)
4. [Usage Examples](#usage-examples)
5. [Migration Guide](#migration-guide)
6. [Best Practices](#best-practices)
7. [Troubleshooting](#troubleshooting)

## Expression Engine

### Supported Operators

The expression engine supports the following operators with proper precedence:

#### Comparison Operators
- `==` - Equal (case-insensitive for strings)
- `!=` - Not equal
- `>` - Greater than
- `>=` - Greater than or equal
- `<` - Less than  
- `<=` - Less than or equal
- `contains` - String containment (case-insensitive)

#### Logical Operators
- `AND` - Logical AND
- `OR` - Logical OR
- `NOT` - Logical NOT

#### Grouping
- `()` - Parentheses for grouping and precedence control

### Data Types

The expression engine automatically handles type conversions for:
- **Strings** - Text values, quoted with `'` or `"`
- **Numbers** - Integers and decimals
- **Booleans** - `true`/`false` (case-insensitive)
- **Dates** - DateTime objects for temporal comparisons
- **Null** - `null` literal

### Expression Examples

```javascript
// Simple comparisons
"status == 'approved'"
"amount > 1000"
"age >= 18"
"name contains 'admin'"

// Complex logic with grouping
"(amount > 1000 AND status == 'pending') OR priority == 'high'"
"NOT (status == 'rejected' OR amount < 100)"
"(role == 'manager' OR role == 'admin') AND department == 'IT'"

// Mixed data types
"created_date > '2023-01-01' AND amount <= 50000"
"is_active == true AND (score >= 80 OR priority == 'critical')"
```

### Architecture

```
Expression Input → TokenLexer → ExpressionParser → ExpressionTree → Evaluator
```

#### Key Components

- **`ExpressionEvaluator`** - Main entry point with caching
- **`TokenLexer`** - Converts expression strings to tokens
- **`ExpressionParser`** - Builds abstract syntax trees
- **`ExpressionTree`** - Node-based evaluation structures

## Fork/Join Activities

### Fork Activity

Splits workflow execution into multiple parallel branches.

#### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `branches` | Array | Yes | List of branch definitions |
| `forkType` | String | No | Execution strategy: `all`, `any`, `conditional` |
| `maxConcurrency` | Number | No | Max concurrent branches (0 = unlimited) |

#### Branch Definition

```json
{
  "id": "branch_1",
  "name": "Approval Branch",
  "description": "Handles approval workflow",
  "condition": "amount > 1000 AND priority == 'high'",
  "properties": {
    "assignee": "approver_role"
  },
  "variableUpdates": {
    "branchType": "approval"
  },
  "priority": 1
}
```

#### Fork Types

- **`all`** - Execute all branches regardless of conditions
- **`any`** - Execute any branch that meets conditions  
- **`conditional`** - Execute only branches with true conditions

### Join Activity

Synchronizes and merges parallel branches.

#### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `forkId` | String | Yes | ID of the fork activity to join |
| `joinType` | String | No | Synchronization strategy |
| `timeoutMinutes` | Number | No | Timeout in minutes (0 = no timeout) |
| `mergeStrategy` | String | No | Data merging approach |
| `timeoutAction` | String | No | Action on timeout: `fail`, `proceed` |

#### Join Types

- **`all`** - Wait for all branches to complete
- **`any`** - Proceed when any branch completes
- **`first`** - Proceed with first completed branch
- **`majority`** - Wait for majority of branches

#### Merge Strategies

- **`combine`** - Merge all outputs (later overrides earlier)
- **`override`** - Use last completed branch output
- **`first`** - Use first completed branch output
- **`last`** - Same as override

## API Reference

### WorkflowActivityBase

Enhanced base class for all workflow activities.

```csharp
public abstract class WorkflowActivityBase : IWorkflowActivity
{
    // Enhanced expression evaluation
    protected bool EvaluateCondition(ActivityContext context, string? condition)
    
    // Expression validation
    protected bool ValidateExpression(string expression, out string? errorMessage)
    
    // Existing property and variable accessors remain unchanged
    protected T GetProperty<T>(ActivityContext context, string key, T defaultValue = default!)
    protected T GetVariable<T>(ActivityContext context, string key, T defaultValue = default!)
}
```

### ExpressionEvaluator

Main expression evaluation engine.

```csharp
public class ExpressionEvaluator
{
    // Evaluate expression returning boolean result
    public bool EvaluateExpression(string expression, Dictionary<string, object> variables)
    
    // Evaluate expression with typed result
    public T EvaluateExpression<T>(string expression, Dictionary<string, object> variables)
    
    // Validate expression syntax
    public bool ValidateExpression(string expression, out string? errorMessage)
    
    // Clear compiled expression cache
    public void ClearCache()
}
```

### WorkflowEngine

Enhanced workflow engine with expression support.

```csharp
public class WorkflowEngine : IWorkflowEngine
{
    // Existing methods remain unchanged
    // Enhanced transition condition evaluation using expression engine
    private bool EvaluateTransitionCondition(TransitionDefinition transition, string? decisionValue, WorkflowInstance workflowInstance)
}
```

## Usage Examples

### Basic Decision Activity with Advanced Expressions

```json
{
  "id": "approval_decision",
  "type": "DecisionActivity",
  "name": "Approval Decision",
  "properties": {
    "conditions": {
      "auto_approve": "(amount <= 10000 AND credit_score >= 700) OR priority == 'low'",
      "manual_review": "amount > 10000 AND amount <= 50000",
      "committee_review": "amount > 50000 OR (credit_score < 600 AND amount > 5000)"
    },
    "defaultDecision": "manual_review"
  }
}
```

### Fork Activity for Parallel Processing

```json
{
  "id": "parallel_checks",
  "type": "ForkActivity", 
  "name": "Parallel Verification",
  "properties": {
    "forkType": "conditional",
    "maxConcurrency": 3,
    "branches": [
      {
        "id": "credit_check",
        "name": "Credit Verification",
        "condition": "amount > 1000",
        "variableUpdates": {
          "checkType": "credit"
        }
      },
      {
        "id": "income_verification", 
        "name": "Income Verification",
        "condition": "employment_status == 'employed' AND annual_income > 30000"
      },
      {
        "id": "collateral_assessment",
        "name": "Collateral Assessment", 
        "condition": "loan_type == 'secured'"
      }
    ]
  }
}
```

### Join Activity for Synchronization

```json
{
  "id": "verification_join",
  "type": "JoinActivity",
  "name": "Verification Complete",
  "properties": {
    "forkId": "parallel_checks",
    "joinType": "all",
    "timeoutMinutes": 60,
    "mergeStrategy": "combine",
    "timeoutAction": "proceed"
  }
}
```

### Workflow Definition with Fork/Join

```json
{
  "name": "Advanced Loan Processing",
  "activities": [
    {
      "id": "start",
      "type": "StartActivity",
      "isStartActivity": true
    },
    {
      "id": "initial_check",
      "type": "DecisionActivity",
      "properties": {
        "conditions": {
          "proceed": "amount >= 1000 AND applicant_age >= 18",
          "reject": "amount < 1000 OR applicant_age < 18"
        }
      }
    },
    {
      "id": "parallel_verification",
      "type": "ForkActivity",
      "properties": {
        "branches": [
          {"id": "credit", "name": "Credit Check"},
          {"id": "income", "name": "Income Verification"},  
          {"id": "employment", "name": "Employment Check"}
        ]
      }
    },
    {
      "id": "verification_complete",
      "type": "JoinActivity", 
      "properties": {
        "forkId": "parallel_verification",
        "joinType": "all"
      }
    },
    {
      "id": "final_decision",
      "type": "DecisionActivity",
      "properties": {
        "conditions": {
          "approve": "credit_score >= 650 AND income_verified == true AND employment_verified == true",
          "reject": "credit_score < 500 OR (income_verified == false AND employment_verified == false)"
        },
        "defaultDecision": "manual_review"
      }
    }
  ],
  "transitions": [
    {"from": "start", "to": "initial_check"},
    {"from": "initial_check", "to": "parallel_verification", "condition": "proceed"},
    {"from": "initial_check", "to": "end", "condition": "reject"},
    {"from": "parallel_verification", "to": "verification_complete"},
    {"from": "verification_complete", "to": "final_decision"},
    {"from": "final_decision", "to": "end"}
  ]
}
```

## Migration Guide

### From Legacy Expressions

Legacy expressions continue to work unchanged:

```javascript
// Legacy (still supported)
"status == 'approved'"
"amount > 1000"

// Enhanced (new capabilities)
"(status == 'approved' OR status == 'pending') AND amount > 1000"
"NOT (rejected == true) AND priority contains 'high'"
```

### Updating Decision Activities

1. **Simple Update** - Add complex conditions:
```json
// Before
"conditions": {
  "approve": "status == 'ready'"
}

// After  
"conditions": {
  "approve": "(status == 'ready' AND amount <= 10000) OR priority == 'urgent'"
}
```

2. **Expression Validation** - The system now validates expressions:
```csharp
// Validation errors will be caught during workflow definition validation
// Invalid: "status = 'approved'" (should be ==)
// Valid: "status == 'approved'"
```

### Adding Fork/Join to Existing Workflows

1. **Identify Parallel Opportunities** - Look for activities that can run concurrently
2. **Add Fork Activity** - Define branches for parallel execution
3. **Add Join Activity** - Synchronize results before continuing
4. **Update Transitions** - Route through fork/join activities

## Best Practices

### Expression Writing

1. **Use Parentheses** - Make precedence explicit
   ```javascript
   // Good
   "(status == 'pending' AND priority == 'high') OR amount > 10000"
   
   // Avoid - unclear precedence  
   "status == 'pending' AND priority == 'high' OR amount > 10000"
   ```

2. **Quote String Literals** - Always quote string values
   ```javascript
   // Good
   "department == 'IT' AND role == 'manager'"
   
   // Bad - unquoted strings
   "department == IT AND role == manager"
   ```

3. **Use Meaningful Variable Names** - Clear, descriptive variables
   ```javascript
   // Good
   "loan_amount > minimum_threshold AND credit_score >= required_score"
   
   // Avoid - unclear variables
   "amt > min AND score >= req"
   ```

### Fork/Join Design

1. **Minimize Branch Dependencies** - Keep branches independent
2. **Set Appropriate Timeouts** - Prevent indefinite waiting
3. **Choose Correct Join Type** - Match business requirements
4. **Handle Branch Failures** - Plan for partial completion scenarios

### Performance Considerations

1. **Expression Caching** - Expressions are automatically cached
2. **Complex Expressions** - Very complex expressions may impact performance
3. **Branch Concurrency** - Set `maxConcurrency` for resource management
4. **Join Timeouts** - Use timeouts to prevent workflow blocking

## Troubleshooting

### Common Expression Errors

1. **Syntax Errors**
   ```
   Error: "Unexpected character '=' at position 6"
   Fix: Use '==' instead of '=' for equality
   ```

2. **Unmatched Parentheses**
   ```
   Error: "Missing closing parenthesis"
   Fix: Ensure all '(' have matching ')'
   ```

3. **Invalid Operators**
   ```
   Error: "Unknown binary operator: EQUALS"
   Fix: Use '==' instead of 'EQUALS'
   ```

### Fork/Join Issues

1. **Missing Fork Context**
   ```
   Error: "Fork context not found for forkId: xyz"
   Fix: Ensure forkId matches the actual fork activity ID
   ```

2. **Join Timeout**
   ```
   Error: "Join activity timed out"
   Fix: Increase timeout or change timeoutAction to 'proceed'
   ```

3. **Branch Validation Failures**
   ```
   Error: "All branches must have a unique ID"
   Fix: Ensure each branch has a unique ID within the fork
   ```

### Debugging Tips

1. **Enable Logging** - Check workflow engine logs for detailed execution info
2. **Test Expressions** - Validate expressions before deploying workflows
3. **Monitor Variables** - Check workflow variables during execution
4. **Use Simple Conditions First** - Start with basic expressions and add complexity

### Performance Monitoring

1. **Expression Evaluation Time** - Monitor complex expression performance
2. **Branch Execution Duration** - Track parallel branch completion times
3. **Join Wait Times** - Monitor how long joins wait for branches
4. **Memory Usage** - Watch for expression cache growth

## Error Handling

### Expression Evaluation Errors

The system provides graceful fallbacks:

1. **Invalid Expressions** - Fall back to legacy evaluation
2. **Missing Variables** - Return false for missing variables
3. **Type Conversion** - Automatic type conversion with sensible defaults

### Fork/Join Error Recovery

1. **Branch Failures** - Continue with successful branches
2. **Timeout Handling** - Configurable timeout actions
3. **Invalid Configurations** - Validation prevents deployment of invalid workflows

## Version History

- **v1.0** - Initial expression engine and fork/join implementation
- **Backward Compatibility** - All existing workflows continue to function unchanged

This documentation covers the essential aspects of the advanced workflow engine. For additional support or questions, refer to the API documentation or contact the development team.