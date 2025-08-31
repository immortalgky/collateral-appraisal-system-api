# Workflow Expression Examples

This document provides practical examples of using the advanced expression engine in workflow activities.

## Basic Expressions

### Simple Comparisons

```javascript
// String equality (case-insensitive)
"status == 'approved'"
"role == 'manager'"

// Numeric comparisons
"amount > 1000"
"age >= 18" 
"score <= 100"

// String containment
"name contains 'admin'"
"description contains 'urgent'"

// Boolean checks
"is_active == true"
"requires_approval != false"
```

### Logical Operations

```javascript
// AND operations
"status == 'pending' AND amount > 5000"
"is_active == true AND role == 'user'"

// OR operations  
"priority == 'high' OR amount > 10000"
"role == 'admin' OR role == 'manager'"

// NOT operations
"NOT (status == 'rejected')"
"NOT (amount < 100 OR priority == 'low')"
```

## Complex Expressions

### Grouping with Parentheses

```javascript
// Financial approval logic
"(amount <= 10000 AND credit_score >= 700) OR (priority == 'urgent' AND amount <= 5000)"

// Role-based access
"(role == 'manager' OR role == 'admin') AND (department == 'IT' OR department == 'Finance')"

// Risk assessment
"NOT ((credit_score < 600 AND amount > 50000) OR (employment_status == 'unemployed' AND amount > 10000))"
```

### Multi-Condition Logic

```javascript
// Loan processing
"applicant_age >= 18 AND annual_income > 30000 AND (employment_status == 'employed' OR employment_status == 'self-employed') AND credit_score >= 650"

// Document validation
"(passport_valid == true OR license_valid == true) AND address_verified == true AND income_documents_complete == true"

// Approval workflow
"(amount <= auto_approval_limit AND risk_score <= acceptable_risk) OR (manual_review_required == true AND approver_available == true)"
```

## Real-World Scenarios

### 1. Loan Application Processing

```json
{
  "id": "loan_decision",
  "type": "DecisionActivity",
  "properties": {
    "conditions": {
      "auto_approve": "(amount <= 25000 AND credit_score >= 720 AND debt_to_income <= 0.3) OR (amount <= 10000 AND credit_score >= 650)",
      "manual_review": "(amount > 25000 AND amount <= 100000 AND credit_score >= 600) OR (credit_score >= 550 AND debt_to_income <= 0.4)",
      "escalate_committee": "amount > 100000 OR (amount > 50000 AND (credit_score < 600 OR debt_to_income > 0.5))",
      "reject": "credit_score < 500 OR debt_to_income > 0.6 OR bankruptcy_history == true"
    },
    "defaultDecision": "manual_review"
  }
}
```

### 2. Insurance Claim Processing

```json
{
  "id": "claim_routing",
  "type": "DecisionActivity", 
  "properties": {
    "conditions": {
      "auto_process": "(claim_amount <= 1000 AND claim_type == 'minor_damage' AND no_prior_claims == true) OR (claim_amount <= 500)",
      "investigator_review": "(claim_amount > 5000 OR suspicious_indicators > 0) AND claim_type != 'routine_maintenance'",
      "adjuster_review": "claim_amount > 1000 AND claim_amount <= 5000 AND claim_complexity == 'standard'",
      "legal_review": "liability_disputed == true OR claim_involves_injury == true OR claim_amount > 25000"
    }
  }
}
```

### 3. Employee Onboarding Workflow

```json
{
  "id": "onboarding_fork",
  "type": "ForkActivity",
  "properties": {
    "forkType": "conditional",
    "branches": [
      {
        "id": "it_setup",
        "name": "IT Equipment Setup",
        "condition": "role != 'contractor' AND work_location == 'office'"
      },
      {
        "id": "security_clearance", 
        "name": "Security Clearance",
        "condition": "(department == 'Security' OR department == 'Finance') AND clearance_level >= 2"
      },
      {
        "id": "training_assignment",
        "name": "Training Assignment", 
        "condition": "is_new_hire == true OR role_changed == true"
      },
      {
        "id": "mentor_assignment",
        "name": "Mentor Assignment",
        "condition": "experience_level == 'junior' AND department_size > 5"
      }
    ]
  }
}
```

### 4. Purchase Order Approval

```json
{
  "id": "po_approval_decision",
  "type": "DecisionActivity",
  "properties": {
    "conditions": {
      "auto_approve": "(amount <= manager_approval_limit AND budget_available >= amount AND vendor_approved == true) OR (amount <= 500 AND category == 'office_supplies')",
      "manager_approval": "amount > 500 AND amount <= manager_approval_limit AND requester_level >= 'senior'",
      "director_approval": "(amount > manager_approval_limit AND amount <= director_approval_limit) OR (category == 'capital_equipment' AND amount > 1000)",
      "board_approval": "amount > director_approval_limit OR (category == 'strategic_investment')",
      "finance_review": "(budget_impact > 0.1 AND fiscal_year_remaining < 3) OR requires_new_vendor == true"
    },
    "defaultDecision": "manager_approval"
  }
}
```

## Advanced Patterns

### 1. Time-Based Conditions

```javascript
// Business hours check (assuming time variables are available)
"current_hour >= 9 AND current_hour <= 17 AND is_weekend == false"

// Seasonal processing
"current_month >= 11 OR current_month <= 2" // Winter months

// Deadline checking  
"days_until_deadline <= 3 AND priority != 'low'"
```

### 2. Array and Collection Checks

```javascript
// Assuming collection properties are converted to counts or flags
"required_documents_count == total_documents_count"
"missing_signatures_count == 0"
"all_conditions_met == true"
```

### 3. Nested Business Rules

```javascript
// Complex eligibility check
"((age >= 18 AND age <= 65) AND (citizenship == 'US' OR visa_type == 'work')) AND ((annual_income >= 25000 AND employment_months >= 12) OR (assets >= 100000 AND investment_income >= 10000)) AND NOT (bankruptcy_recent == true OR foreclosure_recent == true)"

// Multi-tier approval logic
"(tier == 'gold' AND (amount <= 50000 OR (amount <= 100000 AND relationship_years >= 5))) OR (tier == 'platinum' AND amount <= 200000) OR (tier == 'private' AND amount <= 500000)"
```

## Fork/Join Patterns

### 1. Parallel Document Processing

```json
{
  "id": "document_processing_fork",
  "type": "ForkActivity",
  "properties": {
    "forkType": "conditional",
    "maxConcurrency": 4,
    "branches": [
      {
        "id": "identity_verification",
        "name": "Identity Document Verification",
        "condition": "identity_docs_provided == true",
        "priority": 1
      },
      {
        "id": "income_verification", 
        "name": "Income Document Processing",
        "condition": "income_docs_provided == true AND employment_verification_required == true",
        "priority": 2
      },
      {
        "id": "credit_check",
        "name": "Credit History Check", 
        "condition": "credit_check_authorized == true",
        "priority": 1
      },
      {
        "id": "reference_check",
        "name": "Reference Verification",
        "condition": "references_provided == true AND reference_check_required == true", 
        "priority": 3
      }
    ]
  }
}
```

### 2. Quality Assurance Workflow

```json
{
  "id": "qa_parallel_checks",
  "type": "ForkActivity",
  "properties": {
    "forkType": "all",
    "branches": [
      {
        "id": "code_review",
        "name": "Code Review",
        "condition": "code_changes_count > 0"
      },
      {
        "id": "security_scan",
        "name": "Security Vulnerability Scan", 
        "condition": "security_scan_required == true OR code_changes_security_sensitive == true"
      },
      {
        "id": "performance_test",
        "name": "Performance Testing",
        "condition": "performance_critical_changes == true OR major_release == true"
      },
      {
        "id": "integration_test", 
        "name": "Integration Testing",
        "condition": "api_changes == true OR database_changes == true"
      }
    ]
  }
}
```

### 3. Multi-Level Approval Join

```json
{
  "id": "approval_synchronization",
  "type": "JoinActivity",
  "properties": {
    "forkId": "parallel_approvals",
    "joinType": "majority", 
    "timeoutMinutes": 1440,
    "mergeStrategy": "combine",
    "timeoutAction": "proceed"
  }
}
```

## Expression Testing

### Test Cases for Validation

```javascript
// Valid expressions
"status == 'active'"                          // Simple equality
"(a == 1 AND b == 2) OR c == 3"             // Grouped logic
"NOT (rejected == true)"                      // Negation
"amount >= 1000 AND amount <= 5000"          // Range check
"name contains 'test' OR description contains 'sample'" // String operations

// Invalid expressions (will cause validation errors)
"status = 'active'"                          // Single = instead of ==
"(status == 'active'"                       // Missing closing parenthesis  
"amount >> 1000"                             // Invalid operator
"AND status == 'active'"                     // Missing left operand
```

### Common Validation Patterns

```csharp
// In activity validation code
public override Task<ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default)
{
    var errors = new List<string>();
    var conditions = GetProperty<Dictionary<string, string>>(context, "conditions", new());
    
    foreach (var condition in conditions)
    {
        if (!ValidateExpression(condition.Value, out var error))
        {
            errors.Add($"Invalid expression '{condition.Key}': {error}");
        }
    }
    
    return Task.FromResult(errors.Any() 
        ? ValidationResult.Failure(errors.ToArray()) 
        : ValidationResult.Success());
}
```

## Performance Considerations

### Efficient Expression Writing

```javascript
// Good - specific and efficient
"status == 'approved' AND amount <= 10000"

// Less efficient - broad string operations  
"status contains 'app' AND description contains 'process'"

// Good - early termination with AND
"is_active == true AND complex_calculation_result > threshold"

// Good - simple checks first
"priority == 'urgent' OR (complex_condition_1 AND complex_condition_2)"
```

### Optimized Fork/Join Usage

```json
{
  "forkType": "conditional",
  "maxConcurrency": 3,
  "branches": [
    {
      "id": "quick_check",
      "condition": "simple_flag == true",
      "priority": 1
    },
    {
      "id": "detailed_analysis", 
      "condition": "detailed_analysis_required == true AND quick_check_passed == true",
      "priority": 2
    }
  ]
}
```

This comprehensive collection of examples should help developers understand and implement complex workflow expressions effectively.