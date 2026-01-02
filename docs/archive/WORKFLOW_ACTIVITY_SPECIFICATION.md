# Workflow Activity Enhancement Specification

## Document Overview
This specification defines three enhanced workflow activities for the visual workflow designer:
1. **Enhanced TaskActivity** with conditional decision logic
2. **IfElseActivity** for binary transition-based routing  
3. **SwitchActivity** for multi-branch transition-based routing

**Important**: DecisionActivity has been **REMOVED** as it is redundant. These activities use pure transition-based routing - activities output decision values, and transitions use expression-based conditions to determine routing.

---

## 1. Enhanced TaskActivity Specification

### 1.1 Activity Type
- **Type**: `"TaskActivity"`
- **Category**: User Interaction + Conditional Logic
- **Icon Suggestion**: User with decision diamond overlay
- **Color Scheme**: Blue primary with green decision accent

### 1.2 Configuration Schema

```typescript
interface TaskActivityConfig {
  // Existing properties (maintain backward compatibility)
  title: string;
  assignTo?: string;
  assigneeRole: string;
  assigneeGroup?: string;
  assignmentStrategies?: string[];
  
  // New conditional decision feature
  decisionConditions?: {
    [outputDecision: string]: string; // condition expression
  };
  
  // Optional mappings
  inputMappings?: { [activityInput: string]: string };
  outputMappings?: { [activityOutput: string]: string };
}
```

### 1.3 Decision Conditions Usage

TaskActivity can now evaluate conditions and output decisions that transitions can use for routing:

```json
{
  "decisionConditions": {
    "approved": "amount < 50000 && documents_complete == true",
    "rejected": "credit_score < 600", 
    "review": "amount >= 50000"
  }
}
```

The activity will evaluate these conditions after user completion and output the first matching condition as `decision: "approved"`.

---

## 2. IfElseActivity Specification

### 2.1 Activity Type
- **Type**: `"IfElseActivity"`
- **Category**: Flow Control
- **Icon Suggestion**: Git branch or diamond with two paths
- **Color Scheme**: Orange/amber for decision-making

### 2.2 Purpose
Provides binary conditional routing based on boolean expression evaluation. **Outputs `result: true/false`** for transition-based routing. **NO direct activity targeting** - routing handled by transitions with conditions like `result == true`.

### 2.3 Configuration Schema

```typescript
interface IfElseActivityConfig {
  // Required condition expression
  condition: string; // Boolean expression to evaluate
  
  // Optional metadata
  title?: string;
  description?: string;
}
```

### 2.4 Output Data Structure

```typescript
interface IfElseActivityOutput {
  condition: string;        // Original condition expression
  result: boolean;         // Boolean result for transition evaluation
  evaluatedAt: string;      // ISO timestamp
}
```

### 2.5 Frontend Configuration UI

```typescript
interface IfElseActivityUI {
  conditionConfig: {
    condition: CodeEditor; // Expression editor with syntax highlighting
    validateButton: Button; // Test condition syntax
    
    // Help text explaining transition-based routing
    helpText: "This activity outputs result: true/false. Use transitions with conditions like 'result == true' to route to different activities."
  };
}
```

### 2.6 Workflow JSON Example

```json
{
  "activities": [
    {
      "id": "amount-check",
      "type": "IfElseActivity",
      "name": "Amount Check", 
      "properties": {
        "condition": "amount > 50000 && status == 'pending'"
      },
      "position": { "x": 300, "y": 200 }
    }
  ],
  "transitions": [
    {
      "id": "to-manager",
      "from": "amount-check",
      "to": "manager-approval",
      "condition": "result == true",
      "type": "Conditional"
    },
    {
      "id": "to-auto", 
      "from": "amount-check",
      "to": "auto-approve",
      "condition": "result == false",
      "type": "Conditional"
    }
  ]
}
```

---

## 3. SwitchActivity Specification

### 3.1 Activity Type
- **Type**: `"SwitchActivity"`
- **Category**: Flow Control  
- **Icon Suggestion**: Multi-way switch or decision tree
- **Color Scheme**: Purple for complex routing

### 3.2 Purpose
Provides multi-branch conditional routing with support for comparisons and value matching. **Outputs `case: "matched_condition"`** for transition-based routing. **NO direct activity targeting** - routing handled by transitions with conditions like `case == "approved"`.

### 3.3 Configuration Schema

```typescript
interface SwitchActivityConfig {
  // Required expression to evaluate
  expression: string; // Expression that returns a value to match
  
  // Case definitions - array of conditions only
  cases: string[]; // Array of conditions to match (e.g., ["< 10000", ">= 50000", "approved"])
  
  // Optional metadata
  title?: string;
  description?: string;
}
```

### 3.4 Output Data Structure

```typescript
interface SwitchActivityOutput {
  expression: string;           // Original expression
  expressionResult: any;        // Evaluation result
  case: string;                // Matched case condition or "default"
  evaluatedAt: string;          // ISO timestamp
}
```

### 3.5 Frontend Configuration UI

```typescript
interface SwitchActivityUI {
  expressionConfig: {
    expression: CodeEditor; // Expression editor
    testButton: Button;     // Test expression evaluation
  };
  
  casesConfig: {
    cases: string[];        // Dynamic list of case conditions
    addCaseButton: Button;  // Add new case condition
    
    // Help text explaining transition-based routing
    helpText: "This activity outputs case: 'matched_condition'. Use transitions with conditions like 'case == \"approved\"' to route to different activities."
  };
}
```

### 3.6 Case Editor Component

```typescript
interface CaseEditorProps {
  condition: string;           // Case condition (e.g., "< 10000")
  onConditionChange: (value: string) => void;
  onRemove: () => void;
}
```

### 3.7 Workflow JSON Example

```json
{
  "activities": [
    {
      "id": "amount-router",
      "type": "SwitchActivity",
      "name": "Amount-Based Routing",
      "properties": {
        "expression": "amount",
        "cases": ["< 10000", ">= 50000", "== 25000"]
      },
      "position": { "x": 400, "y": 300 }
    }
  ],
  "transitions": [
    {
      "id": "to-auto",
      "from": "amount-router",
      "to": "auto-approve",
      "condition": "case == '< 10000'",
      "type": "Conditional"
    },
    {
      "id": "to-manager",
      "from": "amount-router", 
      "to": "manager-review",
      "condition": "case == '>= 50000'",
      "type": "Conditional"
    },
    {
      "id": "to-special",
      "from": "amount-router",
      "to": "special-review", 
      "condition": "case == '== 25000'",
      "type": "Conditional"
    },
    {
      "id": "to-default",
      "from": "amount-router",
      "to": "standard-review",
      "condition": "case == 'default'",
      "type": "Conditional"
    }
  ]
}
```

---

## 4. Transition-Based Routing System

### 4.1 Core Principles
1. **Activities evaluate and output** - they don't specify target activities
2. **Transitions handle routing** - they use expression-based conditions
3. **Clean separation** - business logic in activities, flow control in transitions

### 4.2 Activity Output Patterns
- **IfElseActivity**: `result: boolean` 
- **SwitchActivity**: `case: string`
- **TaskActivity**: `decision: string` (when using decisionConditions)

### 4.3 Transition Condition Examples
```json
{
  "transitions": [
    // IfElse routing
    { "condition": "result == true", "to": "approval-activity" },
    { "condition": "result == false", "to": "rejection-activity" },
    
    // Switch routing  
    { "condition": "case == '< 10000'", "to": "auto-approve" },
    { "condition": "case == 'approved'", "to": "finalize" },
    { "condition": "case == 'default'", "to": "manual-review" },
    
    // Task decision routing
    { "condition": "decision == 'approved'", "to": "complete-task" },
    { "condition": "decision == 'rejected'", "to": "notify-rejection" }
  ]
}
```

### 4.4 Expression Evaluation Context
The workflow engine merges activity outputs into the expression evaluation context:

```javascript
// For IfElse activity
const context = {
  ...workflowVariables,
  result: true  // From IfElse output
};

// For Switch activity  
const context = {
  ...workflowVariables,
  case: "< 10000"  // From Switch output
};
```

---

## 5. Migration Guide

### 5.1 From DecisionActivity
**DecisionActivity is REMOVED**. Replace with:
- Simple binary decisions → IfElseActivity
- Multi-branch decisions → SwitchActivity  
- Task-based decisions → Enhanced TaskActivity

### 5.2 From Direct Activity Targeting
Remove `trueActivity`, `falseActivity`, and case target specifications. Use transitions instead:

```json
// OLD (removed)
{
  "type": "IfElseActivity",
  "properties": {
    "condition": "amount > 50000",
    "trueActivity": "manager-review",
    "falseActivity": "auto-approve"
  }
}

// NEW (transition-based)
{
  "activities": [
    {
      "type": "IfElseActivity", 
      "properties": {
        "condition": "amount > 50000"
      }
    }
  ],
  "transitions": [
    { "condition": "result == true", "to": "manager-review" },
    { "condition": "result == false", "to": "auto-approve" }
  ]
}
```

---

## 6. Implementation Notes

### 6.1 Backend Activity Outputs
- IfElseActivity outputs: `{ result: boolean }`
- SwitchActivity outputs: `{ case: string }`  
- WorkflowEngine handles transition evaluation using these outputs

### 6.2 Frontend Considerations
- Remove activity selector dropdowns from IfElse/Switch configuration
- Add transition condition helpers and validation
- Update visual connectors to show expression-based routing
- Provide expression editor with autocomplete for activity outputs

### 6.3 Validation Rules
- IfElse must have transitions checking `result == true` and `result == false`
- Switch must have transitions for each case condition + default
- Expression syntax validation for both activity conditions and transition conditions