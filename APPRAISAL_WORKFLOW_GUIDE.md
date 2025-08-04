# Collateral Appraisal System - Workflow Implementation Guide

## Overview

This guide demonstrates how to implement a comprehensive appraisal workflow system using **Elsa 3.4.2** with **Carter API** integration. The workflow covers all stakeholder roles in the appraisal process.

## 🏢 Appraisal Workflow Roles & Process

### Stakeholder Roles

1. **👤 Request Maker** - Initiates appraisal request
2. **👨‍💼 Admin** - Reviews and approves initial request
3. **👥 Staff** - Conducts property appraisal work
4. **🔎 Checker** - Reviews and validates appraisal work
5. **🎯 Verifier** - Additional verification for high-value properties
6. **🏛️ Committee** - Reviews very high-value appraisals (>$1M)

### Workflow Process Flow

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   REQUEST       │───▶│   ADMIN         │───▶│   STAFF         │
│   MAKER         │    │   REVIEW        │    │   ASSIGNMENT    │
│                 │    │                 │    │                 │
│ • Submit        │    │ • Review docs   │    │ • Assign staff  │
│   request       │    │ • Approve/      │    │ • Set due date  │
│ • Provide docs  │    │   Reject        │    │ • Notify staff  │
│ • Set purpose   │    │ • Comments      │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                        │                        │
         │               ┌─────────────────┐    ┌─────────────────┐
         │               │   REJECTION     │    │   STAFF         │
         │               │   NOTIFICATION  │    │   APPRAISAL     │
         │               │                 │    │                 │
         │               │ • Notify        │    │ • Inspect       │
         │               │   requester     │    │ • Value         │
         │               │ • Provide       │    │ • Document      │
         │               │   reason        │    │ • Submit        │
         │               └─────────────────┘    └─────────────────┘
         │                                                │
         │                                                ▼
         │                                    ┌─────────────────┐
         │                                    │   CHECKER       │
         │                                    │   REVIEW        │
         │                                    │                 │
         │                                    │ • Review work   │
         │                                    │ • Validate      │
         │                                    │ • Approve/      │
         │                                    │   Reject/Return │
         │                                    └─────────────────┘
         │                                                │
         │                                                ▼
         │                                    ┌─────────────────┐
         │                                    │   VERIFIER      │
         │                                    │   REVIEW        │
         │                                    │   (High Value)  │
         │                                    │                 │
         │                                    │ • Risk assess   │
         │                                    │ • Compliance    │
         │                                    │ • Final verify  │
         │                                    └─────────────────┘
         │                                                │
         │                                                ▼
         │                                    ┌─────────────────┐
         │                                    │   COMMITTEE     │
         │                                    │   REVIEW        │
         │                                    │   (Very High)   │
         │                                    │                 │
         │                                    │ • Board review  │
         │                                    │ • Final decision│
         │                                    │ • Compliance    │
         │                                    └─────────────────┘
         │                                                │
         │                                                ▼
         └─────────────────┐                ┌─────────────────┐
                           │                │   FINAL         │
                           │                │   APPROVAL      │
                           │                │                 │
                           │                │ • System        │
                           │                │   approval      │
                           │                │ • Notifications │
                           │                │ • Documentation │
                           └───────────────▶└─────────────────┘
```

## 🛠️ Technical Implementation

### 1. Workflow Engine Configuration

**File**: `Modules/Workflow/Workflow/WorkflowModule.cs`

```csharp
services.AddElsa(elsa =>
{
    // Database configuration
    elsa.UseWorkflowManagement(management => management.UseEntityFrameworkCore(ef =>
        ef.UseSqlServer(connectionString)));

    elsa.UseWorkflowRuntime(runtime => runtime.UseEntityFrameworkCore(ef =>
        ef.UseSqlServer(connectionString)));

    // Authentication & Security
    elsa.UseIdentity(identity =>
    {
        identity.TokenOptions = options => options.SigningKey = "your-secret-key";
        identity.UseAdminUserProvider();
    });

    // API & Expression Support
    elsa.UseDefaultAuthentication(auth => auth.UseAdminApiKey());
    elsa.UseJavaScript();
    elsa.UseCSharp();
    elsa.UseLiquid();
    elsa.UseHttp();
});
```

### 2. Workflow Activities Structure

#### Core Activities for Each Role:

```csharp
// Request Maker Activities
public class SubmitAppraisalRequest : CodeActivity
{
    [Input] public Input<string> RequestMakerId { get; set; }
    [Input] public Input<string> PropertyType { get; set; }
    [Input] public Input<string> PropertyAddress { get; set; }
    [Input] public Input<decimal> EstimatedValue { get; set; }
    [Input] public Input<string> Purpose { get; set; }
    
    [Output] public Output<string> AppraisalId { get; set; }
    [Output] public Output<string> Status { get; set; }
}

// Admin Activities
public class AdminReview : CodeActivity
{
    [Input] public Input<string> AppraisalId { get; set; }
    [Input] public Input<string> AdminId { get; set; }
    [Input] public Input<string> Decision { get; set; } // "approve" or "reject"
    [Input] public Input<string> Comments { get; set; }
    
    [Output] public Output<string> Status { get; set; }
    [Output] public Output<bool> IsApproved { get; set; }
}

// Staff Activities
public class StaffAppraisalWork : CodeActivity
{
    [Input] public Input<string> AppraisalId { get; set; }
    [Input] public Input<string> StaffId { get; set; }
    [Input] public Input<decimal> AppraisedValue { get; set; }
    [Input] public Input<string> AppraisalMethod { get; set; }
    [Input] public Input<string> Findings { get; set; }
    [Input] public Input<string> Recommendations { get; set; }
    
    [Output] public Output<object> AppraisalData { get; set; }
}

// Checker Activities
public class CheckerReview : CodeActivity
{
    [Input] public Input<string> AppraisalId { get; set; }
    [Input] public Input<string> CheckerId { get; set; }
    [Input] public Input<string> Decision { get; set; } // "approve", "reject", "return-to-staff"
    [Input] public Input<string> Comments { get; set; }
    
    [Output] public Output<string> CheckerDecision { get; set; }
}

// Verifier Activities
public class VerifierReview : CodeActivity
{
    [Input] public Input<string> AppraisalId { get; set; }
    [Input] public Input<string> VerifierId { get; set; }
    [Input] public Input<string> Decision { get; set; } // "approve", "reject", "return-to-checker"
    [Input] public Input<string> Comments { get; set; }
    
    [Output] public Output<string> VerifierDecision { get; set; }
}

// Committee Activities
public class CommitteeReview : CodeActivity
{
    [Input] public Input<string> AppraisalId { get; set; }
    [Input] public Input<string> CommitteeId { get; set; }
    [Input] public Input<string> Decision { get; set; } // "approve", "reject"
    [Input] public Input<string> CommitteeComments { get; set; }
    
    [Output] public Output<string> CommitteeDecision { get; set; }
}
```

### 3. Workflow States & Transitions

#### Workflow States:
- `REQUEST_SUBMITTED`
- `ADMIN_REVIEW`
- `ADMIN_APPROVED` / `ADMIN_REJECTED`
- `STAFF_ASSIGNED`
- `STAFF_WORKING`
- `STAFF_COMPLETED`
- `CHECKER_REVIEW`
- `CHECKER_APPROVED` / `CHECKER_REJECTED` / `RETURNED_TO_STAFF`
- `VERIFIER_REVIEW` (for high-value properties)
- `VERIFIER_APPROVED` / `VERIFIER_REJECTED` / `RETURNED_TO_CHECKER`
- `COMMITTEE_REVIEW` (for very high-value properties)
- `COMMITTEE_APPROVED` / `COMMITTEE_REJECTED`
- `FINAL_APPROVED`
- `WORKFLOW_COMPLETED`

#### Decision Logic:
```csharp
// High-value property verification
.Then<If>(condition => condition
    .WithCondition(context => propertyValue.Get(context) > 500000)
    .WithDisplayName("Check if High Value"))

// Very high-value committee review
.Then<If>(condition => condition
    .WithCondition(context => propertyValue.Get(context) > 1000000)
    .WithDisplayName("Check if Committee Review Required"))
```

### 4. API Integration with Carter

#### Sample Endpoints:

```csharp
// Start appraisal workflow
app.MapPost("/api/appraisal/submit", async (AppraisalRequest request, 
    IWorkflowRuntime workflowRuntime) =>
{
    var workflowRequest = new StartWorkflowRequest
    {
        WorkflowDefinitionHandle = WorkflowDefinitionHandle.ByName("AppraisalWorkflow"),
        Input = new Dictionary<string, object>
        {
            ["requestMakerId"] = request.RequestMakerId,
            ["propertyType"] = request.PropertyType,
            ["propertyAddress"] = request.PropertyAddress,
            ["estimatedValue"] = request.EstimatedValue,
            ["purpose"] = request.Purpose
        }
    };
    
    var response = await workflowRuntime.StartWorkflowAsync(workflowRequest);
    return Results.Ok(new { WorkflowInstanceId = response.WorkflowInstanceId });
});

// Admin review endpoint
app.MapPost("/api/appraisal/{appraisalId}/admin-review", 
    async (string appraisalId, AdminReviewRequest request, 
    IWorkflowRuntime workflowRuntime) =>
{
    // Resume workflow with admin decision
    var resumeRequest = new ResumeWorkflowRequest
    {
        WorkflowInstanceId = appraisalId,
        ActivityHandle = ActivityHandle.FromActivityId("AdminReview"),
        Input = new Dictionary<string, object>
        {
            ["adminId"] = request.AdminId,
            ["decision"] = request.Decision,
            ["comments"] = request.Comments
        }
    };
    
    await workflowRuntime.ResumeWorkflowAsync(resumeRequest);
    return Results.Ok();
});
```

## 🔄 Workflow Execution Flow

### 1. Request Submission
```http
POST /api/appraisal/submit
{
    "requestMakerId": "REQ001",
    "propertyType": "Residential",
    "propertyAddress": "123 Main St",
    "estimatedValue": 350000,
    "purpose": "Mortgage"
}
```

### 2. Admin Review
```http
POST /api/appraisal/{appraisalId}/admin-review
{
    "adminId": "ADMIN001",
    "decision": "approve",
    "comments": "Documentation complete, approved for processing"
}
```

### 3. Staff Assignment
```http
POST /api/appraisal/{appraisalId}/assign-staff
{
    "staffId": "STAFF001",
    "assignedBy": "ADMIN001",
    "dueDate": "2024-08-15"
}
```

### 4. Staff Work Completion
```http
POST /api/appraisal/{appraisalId}/staff-submit
{
    "staffId": "STAFF001",
    "appraisedValue": 340000,
    "appraisalMethod": "Comparative Market Analysis",
    "findings": "Property in excellent condition",
    "recommendations": "Approve for requested amount"
}
```

### 5. Checker Review
```http
POST /api/appraisal/{appraisalId}/checker-review
{
    "checkerId": "CHECKER001",
    "decision": "approve",
    "comments": "Methodology and valuation are sound"
}
```

### 6. Verifier Review (High-Value)
```http
POST /api/appraisal/{appraisalId}/verifier-review
{
    "verifierId": "VERIFIER001",
    "decision": "approve",
    "comments": "High-value property verified successfully"
}
```

### 7. Committee Review (Very High-Value)
```http
POST /api/appraisal/{appraisalId}/committee-review
{
    "committeeId": "COMMITTEE001",
    "decision": "approve",
    "comments": "Board approves the appraisal"
}
```

## 📊 Workflow Monitoring

### Status Tracking
```http
GET /api/appraisal/{appraisalId}/status
```

**Response:**
```json
{
    "appraisalId": "APR-20240708-1234",
    "status": "CHECKER_REVIEW",
    "requestMaker": "REQ001",
    "assignedStaff": "STAFF001",
    "checker": "CHECKER001",
    "verifier": null,
    "committee": null,
    "currentStage": "Checker Review",
    "progress": 60,
    "estimatedCompletion": "2024-08-15T10:00:00Z"
}
```

### Workflow History
```http
GET /api/appraisal/{appraisalId}/history
```

**Response:**
```json
{
    "appraisalId": "APR-20240708-1234",
    "history": [
        {
            "stage": "REQUEST_SUBMITTED",
            "timestamp": "2024-08-08T09:00:00Z",
            "actor": "REQ001",
            "action": "submit_request",
            "comments": "Initial request submitted"
        },
        {
            "stage": "ADMIN_APPROVED",
            "timestamp": "2024-08-08T09:30:00Z",
            "actor": "ADMIN001",
            "action": "approve_request",
            "comments": "Documentation complete"
        },
        {
            "stage": "STAFF_ASSIGNED",
            "timestamp": "2024-08-08T10:00:00Z",
            "actor": "ADMIN001",
            "action": "assign_staff",
            "comments": "Assigned to STAFF001"
        }
    ]
}
```

## 🚀 Next Steps

### 1. **Elsa Studio Integration**
- Add visual workflow designer
- Configure Studio with your API endpoints
- Enable drag-and-drop workflow creation

### 2. **Notification System**
- Email notifications for each stage
- SMS alerts for urgent actions
- Push notifications for mobile apps

### 3. **Document Management**
- File upload/download capabilities
- Digital signature integration
- Document versioning

### 4. **Reporting & Analytics**
- Performance metrics
- Workflow duration analysis
- Bottleneck identification

### 5. **Advanced Features**
- Parallel processing for multiple appraisals
- Conditional routing based on property type
- Integration with external valuation services

## 🔧 Configuration Files

### Database Configuration
```json
{
    "ConnectionStrings": {
        "Database": "Server=localhost;Database=CollateralAppraisalDB;Trusted_Connection=true;",
        "Redis": "localhost:6379"
    },
    "Elsa": {
        "SigningKey": "your-256-bit-secret-key-here",
        "AdminApiKey": "your-admin-api-key-here"
    }
}
```

### CORS Configuration
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("ElsaPolicy",
        policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});
```

## 📋 Summary

This comprehensive appraisal workflow system provides:

✅ **Complete role-based workflow** covering all appraisal stakeholders
✅ **Elsa 3.4.2 integration** with Carter API
✅ **Flexible routing** based on property value and risk
✅ **Real-time status tracking** and history
✅ **RESTful API endpoints** for all workflow actions
✅ **Scalable architecture** ready for production use

The system is now ready for testing and can be extended with additional features like notifications, document management, and advanced reporting capabilities.