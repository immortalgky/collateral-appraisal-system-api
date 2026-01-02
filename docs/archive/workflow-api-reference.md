# Workflow API Reference
## Complete REST API Documentation with Examples

### Table of Contents
1. [API Overview](#api-overview)
2. [Authentication](#authentication)
3. [Workflow Management](#workflow-management)
4. [Activity Management](#activity-management)
5. [Workflow Definitions](#workflow-definitions)
6. [Monitoring and Status](#monitoring-and-status)
7. [Error Handling](#error-handling)
8. [SDK Examples](#sdk-examples)

---

## API Overview

The Workflow API provides RESTful endpoints for managing workflow instances, activities, and definitions. All endpoints follow consistent patterns and return structured responses.

### Base URL
```
Production:  https://api.yourcompany.com/api
Staging:     https://api-staging.yourcompany.com/api  
Development: https://localhost:5001/api
```

### API Versioning
```
Current Version: v1 (implicit in URL)
Future Versions: /api/v2/workflows/...
```

### Content Type
```
Request:  Content-Type: application/json
Response: Content-Type: application/json; charset=utf-8
```

### Common Headers
```http
Authorization: Bearer {jwt-token}
Content-Type: application/json
X-Correlation-ID: {unique-request-id}
X-User-Agent: YourApp/1.0
```

---

## Authentication

### JWT Token Authentication
All API endpoints require a valid JWT token in the Authorization header.

```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Required Claims
```json
{
  "sub": "user@example.com",
  "roles": ["workflow-user", "manager"],
  "name": "John Doe",
  "exp": 1699123456
}
```

### Permissions
- `workflow-user`: Basic workflow operations
- `workflow-admin`: Administrative operations
- `manager`: Assignment override capabilities

---

## Workflow Management

### Start Workflow

**Endpoint**: `POST /workflows/instances/start`

**Description**: Starts a new workflow instance from a workflow definition.

**Request**:
```json
{
  "workflowDefinitionId": "12345678-1234-1234-1234-123456789012",
  "instanceName": "Property Appraisal AR-2024-001",
  "startedBy": "user@example.com",
  "initialVariables": {
    "RequestId": "AR-2024-001",
    "PropertyType": "Residential",
    "PropertyValue": 450000,
    "Priority": "High",
    "ClientName": "John Smith",
    "DueDate": "2024-12-31T23:59:59Z"
  },
  "correlationId": "correlation-12345",
  "assignmentOverrides": {
    "admin-review": {
      "runtimeAssignee": "admin@example.com",
      "overrideReason": "Urgent request requires senior admin review"
    },
    "staff-assignment": {
      "runtimeAssigneeGroup": "senior-appraisers",
      "runtimeAssignmentStrategies": ["WorkloadBased", "PreviousOwner"],
      "overrideReason": "High-value property requires experienced appraiser"
    }
  }
}
```

**Response**: `200 OK`
```json
{
  "workflowInstanceId": "87654321-4321-4321-4321-876543210987",
  "instanceName": "Property Appraisal AR-2024-001",
  "status": "Suspended",
  "nextActivityId": "admin-review",
  "nextAssignee": "admin@example.com",
  "startedOn": "2024-09-08T10:30:15.123Z"
}
```

**cURL Example**:
```bash
curl -X POST "https://api.example.com/api/workflows/instances/start" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: correlation-12345" \
  -d '{
    "workflowDefinitionId": "12345678-1234-1234-1234-123456789012",
    "instanceName": "Property Appraisal AR-2024-001",
    "startedBy": "user@example.com",
    "initialVariables": {
      "RequestId": "AR-2024-001",
      "PropertyType": "Residential"
    }
  }'
```

### Get Workflow Instance

**Endpoint**: `GET /workflows/instances/{workflowInstanceId}`

**Description**: Retrieves details of a specific workflow instance.

**Response**: `200 OK`
```json
{
  "id": "87654321-4321-4321-4321-876543210987",
  "workflowDefinitionId": "12345678-1234-1234-1234-123456789012",
  "name": "Property Appraisal AR-2024-001",
  "status": "Suspended",
  "startedOn": "2024-09-08T10:30:15.123Z",
  "completedOn": null,
  "startedBy": "user@example.com",
  "completedBy": null,
  "currentActivityId": "admin-review",
  "currentAssignee": "admin@example.com",
  "correlationId": "correlation-12345",
  "variables": {
    "RequestId": "AR-2024-001",
    "PropertyType": "Residential",
    "PropertyValue": 450000,
    "Status": "Pending",
    "ApprovedBy": null,
    "ApprovalDate": null
  },
  "errorMessage": null,
  "executionHistory": [
    {
      "activityId": "start",
      "activityName": "Start Process",
      "status": "Completed",
      "startedOn": "2024-09-08T10:30:15.123Z",
      "completedOn": "2024-09-08T10:30:15.234Z",
      "assignedTo": null,
      "completedBy": "System"
    },
    {
      "activityId": "admin-review",
      "activityName": "Admin Review",
      "status": "Pending",
      "startedOn": "2024-09-08T10:30:15.234Z",
      "completedOn": null,
      "assignedTo": "admin@example.com",
      "completedBy": null
    }
  ]
}
```

### List Workflow Instances

**Endpoint**: `GET /workflows/instances`

**Query Parameters**:
- `status` (optional): Filter by status (Pending, Suspended, Completed, Failed, Cancelled)
- `assignedTo` (optional): Filter by assigned user
- `correlationId` (optional): Find by correlation ID
- `startedBy` (optional): Filter by who started the workflow
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Items per page (default: 20, max: 100)
- `sortBy` (optional): Sort field (startedOn, completedOn, status)
- `sortOrder` (optional): Sort direction (asc, desc)

**Example Request**:
```http
GET /workflows/instances?status=Suspended&assignedTo=admin@example.com&page=1&pageSize=10&sortBy=startedOn&sortOrder=desc
```

**Response**: `200 OK`
```json
{
  "items": [
    {
      "id": "87654321-4321-4321-4321-876543210987",
      "name": "Property Appraisal AR-2024-001",
      "status": "Suspended",
      "startedOn": "2024-09-08T10:30:15.123Z",
      "startedBy": "user@example.com",
      "currentActivityId": "admin-review",
      "currentAssignee": "admin@example.com",
      "correlationId": "correlation-12345"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 10,
    "totalItems": 1,
    "totalPages": 1,
    "hasNextPage": false,
    "hasPreviousPage": false
  }
}
```

### Cancel Workflow

**Endpoint**: `POST /workflows/instances/{workflowInstanceId}/cancel`

**Request**:
```json
{
  "cancelledBy": "manager@example.com",
  "reason": "Client withdrew application",
  "notifyAssignees": true
}
```

**Response**: `200 OK`
```json
{
  "workflowInstanceId": "87654321-4321-4321-4321-876543210987",
  "status": "Cancelled",
  "cancelledBy": "manager@example.com",
  "cancelledOn": "2024-09-08T14:15:30.456Z",
  "reason": "Client withdrew application"
}
```

---

## Activity Management

### Complete Activity

**Endpoint**: `POST /workflows/instances/{workflowInstanceId}/activities/{activityId}/complete`

**Description**: Completes a pending activity and resumes workflow execution.

**Request**:
```json
{
  "completedBy": "admin@example.com",
  "input": {
    "Status": "Approved",
    "Comments": "Property details verified. Approved for standard appraisal process.",
    "ApprovalDate": "2024-09-08T14:15:30.456Z",
    "Priority": "Standard",
    "EstimatedDuration": "7 days",
    "RequiresFieldVisit": true,
    "SpecialInstructions": "Schedule field visit within 48 hours"
  },
  "nextAssignmentOverrides": {
    "staff-assignment": {
      "runtimeAssignee": "senior-appraiser@example.com",
      "overrideReason": "Complex property requires senior appraiser expertise"
    },
    "appraisal-work": {
      "runtimeAssignmentStrategies": ["WorkloadBased"],
      "overrideProperties": {
        "maxWorkload": 5,
        "preferredSkills": ["commercial", "high-value"]
      },
      "overrideReason": "Workload balancing for high-priority request"
    }
  }
}
```

**Response**: `200 OK`
```json
{
  "workflowInstanceId": "87654321-4321-4321-4321-876543210987",
  "status": "Suspended",
  "nextActivityId": "staff-assignment",
  "currentAssignee": "admin@example.com",
  "nextAssignee": "manager@example.com",
  "isCompleted": false,
  "updatedVariables": {
    "Status": "Approved",
    "ApprovedBy": "admin@example.com",
    "ApprovalDate": "2024-09-08T14:15:30.456Z",
    "Priority": "Standard"
  }
}
```

### Get Current Activities for User

**Endpoint**: `GET /workflows/activities/current`

**Query Parameters**:
- `userId` (optional): Get activities for specific user (defaults to current user)
- `status` (optional): Filter by activity status (Pending, InProgress, Completed)
- `priority` (optional): Filter by priority (Low, Medium, High, Critical)
- `activityType` (optional): Filter by activity type
- `page`, `pageSize`, `sortBy`, `sortOrder`: Pagination and sorting

**Response**: `200 OK`
```json
{
  "items": [
    {
      "workflowInstanceId": "87654321-4321-4321-4321-876543210987",
      "activityId": "admin-review",
      "activityName": "Admin Review",
      "activityType": "AdminReview",
      "workflowName": "Property Appraisal AR-2024-001",
      "status": "Pending",
      "priority": "High",
      "assignedTo": "admin@example.com",
      "startedOn": "2024-09-08T10:30:15.234Z",
      "dueDate": "2024-09-09T10:30:15.234Z",
      "timeRemaining": "22:45:30",
      "correlationId": "correlation-12345",
      "contextVariables": {
        "RequestId": "AR-2024-001",
        "PropertyType": "Residential",
        "ClientName": "John Smith"
      },
      "requiredActions": [
        "Review property details",
        "Verify client information",
        "Approve or reject request"
      ],
      "attachments": [
        {
          "name": "property_details.pdf",
          "url": "/attachments/property_details.pdf",
          "type": "application/pdf",
          "size": 2048576
        }
      ]
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  }
}
```

### Get Activity History

**Endpoint**: `GET /workflows/instances/{workflowInstanceId}/activities/history`

**Response**: `200 OK`
```json
{
  "workflowInstanceId": "87654321-4321-4321-4321-876543210987",
  "activities": [
    {
      "activityId": "start",
      "activityName": "Start Process",
      "activityType": "StartActivity",
      "status": "Completed",
      "startedOn": "2024-09-08T10:30:15.123Z",
      "completedOn": "2024-09-08T10:30:15.234Z",
      "duration": "00:00:00.111",
      "assignedTo": null,
      "completedBy": "System",
      "inputData": {
        "RequestId": "AR-2024-001",
        "InitiatedBy": "user@example.com"
      },
      "outputData": {
        "WorkflowStarted": true,
        "NextActivity": "admin-review"
      },
      "comments": null
    },
    {
      "activityId": "admin-review",
      "activityName": "Admin Review",
      "activityType": "AdminReview",
      "status": "Completed",
      "startedOn": "2024-09-08T10:30:15.234Z",
      "completedOn": "2024-09-08T14:15:30.456Z",
      "duration": "03:45:15.222",
      "assignedTo": "admin@example.com",
      "completedBy": "admin@example.com",
      "inputData": null,
      "outputData": {
        "Status": "Approved",
        "Comments": "Property details verified",
        "ApprovalDate": "2024-09-08T14:15:30.456Z"
      },
      "comments": "Verified all property details. Client information complete."
    }
  ]
}
```

### Reassign Activity

**Endpoint**: `POST /workflows/instances/{workflowInstanceId}/activities/{activityId}/reassign`

**Request**:
```json
{
  "newAssignee": "senior-admin@example.com",
  "reassignedBy": "manager@example.com",
  "reason": "Original assignee unavailable due to vacation",
  "notifyNewAssignee": true,
  "notifyOriginalAssignee": true,
  "transferNotes": "Please prioritize this high-value property request"
}
```

**Response**: `200 OK`
```json
{
  "workflowInstanceId": "87654321-4321-4321-4321-876543210987",
  "activityId": "admin-review",
  "previousAssignee": "admin@example.com",
  "newAssignee": "senior-admin@example.com",
  "reassignedBy": "manager@example.com",
  "reassignedOn": "2024-09-08T15:30:45.789Z",
  "reason": "Original assignee unavailable due to vacation"
}
```

---

## Workflow Definitions

### Get Workflow Definitions

**Endpoint**: `GET /workflows/definitions`

**Query Parameters**:
- `category` (optional): Filter by category (Appraisal, Review, Administrative)
- `activeOnly` (optional): Only return active definitions (default: false)
- `version` (optional): Specific version number

**Response**: `200 OK`
```json
{
  "definitions": [
    {
      "id": "12345678-1234-1234-1234-123456789012",
      "name": "Property Appraisal Workflow",
      "description": "Complete property appraisal process from submission to final report",
      "version": 3,
      "isActive": true,
      "category": "Appraisal",
      "createdOn": "2024-08-15T09:00:00.000Z",
      "createdBy": "system@example.com",
      "activities": [
        {
          "id": "start",
          "name": "Start Process",
          "type": "StartActivity",
          "description": "Initialize the appraisal workflow",
          "isStartActivity": true,
          "isEndActivity": false,
          "timeoutDuration": null,
          "requiredRoles": [],
          "properties": {}
        },
        {
          "id": "admin-review",
          "name": "Administrative Review",
          "type": "AdminReview",
          "description": "Review and validate the appraisal request",
          "isStartActivity": false,
          "isEndActivity": false,
          "timeoutDuration": "1.00:00:00",
          "requiredRoles": ["admin", "manager"],
          "properties": {
            "allowDelegation": true,
            "requireComments": true,
            "maxProcessingTime": "24:00:00"
          }
        }
      ],
      "transitions": [
        {
          "id": "start-to-admin",
          "from": "start",
          "to": "admin-review",
          "condition": null,
          "type": "Normal"
        },
        {
          "id": "admin-to-assignment",
          "from": "admin-review",
          "to": "staff-assignment",
          "condition": "Status == 'Approved'",
          "type": "Conditional"
        }
      ],
      "variables": {
        "RequestId": "",
        "PropertyType": "",
        "Status": "Pending",
        "Priority": "Medium"
      }
    }
  ]
}
```

### Get Workflow Definition Details

**Endpoint**: `GET /workflows/definitions/{definitionId}`

**Response**: `200 OK`
```json
{
  "id": "12345678-1234-1234-1234-123456789012",
  "name": "Property Appraisal Workflow",
  "description": "Complete property appraisal process from submission to final report",
  "version": 3,
  "isActive": true,
  "category": "Appraisal",
  "schema": {
    "id": "appraisal-workflow",
    "name": "Property Appraisal Workflow",
    "description": "Complete property appraisal process",
    "category": "Appraisal",
    "activities": [...],
    "transitions": [...],
    "variables": {...},
    "metadata": {
      "author": "Workflow Designer",
      "createdDate": "2024-08-15T09:00:00.000Z",
      "version": "3.0",
      "tags": ["appraisal", "property", "review"]
    }
  },
  "statistics": {
    "totalInstances": 1247,
    "activeInstances": 89,
    "completedInstances": 1089,
    "failedInstances": 15,
    "cancelledInstances": 54,
    "averageCompletionTime": "4.12:30:00",
    "successRate": 0.94
  }
}
```

### Create Workflow Definition

**Endpoint**: `POST /workflows/definitions`

**Request**:
```json
{
  "name": "Custom Appraisal Workflow",
  "description": "Customized workflow for special property types",
  "category": "Appraisal",
  "schema": {
    "id": "custom-appraisal-workflow",
    "name": "Custom Appraisal Workflow",
    "description": "Customized workflow for special property types",
    "category": "Appraisal",
    "activities": [
      {
        "id": "start",
        "name": "Start Process",
        "type": "StartActivity",
        "description": "Initialize the workflow",
        "isStartActivity": true,
        "properties": {}
      },
      {
        "id": "special-review",
        "name": "Special Property Review",
        "type": "TaskActivity",
        "description": "Review special property characteristics",
        "requiredRoles": ["specialist"],
        "timeoutDuration": "2.00:00:00",
        "properties": {
          "requirePhotos": true,
          "requireSketchPlan": true,
          "specialistRequired": true
        }
      },
      {
        "id": "end",
        "name": "End Process",
        "type": "EndActivity",
        "description": "Complete the workflow",
        "isEndActivity": true
      }
    ],
    "transitions": [
      {
        "id": "start-to-review",
        "from": "start",
        "to": "special-review"
      },
      {
        "id": "review-to-end",
        "from": "special-review",
        "to": "end"
      }
    ],
    "variables": {
      "PropertyType": "Special",
      "RequiresSpecialist": true,
      "Status": "Pending"
    }
  }
}
```

**Response**: `201 Created`
```json
{
  "id": "98765432-8765-4321-8765-432187654321",
  "name": "Custom Appraisal Workflow",
  "version": 1,
  "isActive": true,
  "createdOn": "2024-09-08T16:45:20.123Z",
  "createdBy": "designer@example.com"
}
```

### Get Activity Types

**Endpoint**: `GET /workflows/activity-types`

**Response**: `200 OK`
```json
{
  "activityTypes": [
    {
      "type": "TaskActivity",
      "name": "Human Task",
      "description": "Activity requiring human interaction",
      "category": "Human",
      "icon": "task",
      "color": "#3b82f6",
      "properties": [
        {
          "name": "assignee",
          "displayName": "Assigned To",
          "type": "string",
          "required": false,
          "description": "User or group to assign this task to"
        },
        {
          "name": "dueDate",
          "displayName": "Due Date",
          "type": "datetime",
          "required": false,
          "description": "When this task should be completed"
        },
        {
          "name": "priority",
          "displayName": "Priority",
          "type": "string",
          "required": false,
          "options": ["Low", "Medium", "High", "Critical"],
          "defaultValue": "Medium"
        }
      ]
    },
    {
      "type": "IfElseActivity",
      "name": "Conditional Decision",
      "description": "Route workflow based on conditions",
      "category": "Logic",
      "icon": "decision",
      "color": "#f59e0b",
      "properties": [
        {
          "name": "condition",
          "displayName": "Condition",
          "type": "string",
          "required": true,
          "description": "Expression to evaluate (e.g., Status == 'Approved')"
        },
        {
          "name": "trueBranch",
          "displayName": "True Branch",
          "type": "string",
          "required": true,
          "description": "Activity ID to execute if condition is true"
        },
        {
          "name": "falseBranch",
          "displayName": "False Branch", 
          "type": "string",
          "required": true,
          "description": "Activity ID to execute if condition is false"
        }
      ]
    }
  ]
}
```

---

## Monitoring and Status

### Get Workflow Metrics

**Endpoint**: `GET /workflows/metrics`

**Query Parameters**:
- `timeRange` (optional): last24hours, last7days, last30days, last90days
- `category` (optional): Filter by workflow category
- `status` (optional): Filter by workflow status

**Response**: `200 OK`
```json
{
  "timeRange": "last30days",
  "totalWorkflows": 2543,
  "activeWorkflows": 234,
  "completedWorkflows": 2145,
  "failedWorkflows": 89,
  "cancelledWorkflows": 75,
  "averageCompletionTime": "3.14:22:15",
  "successRate": 0.96,
  "throughput": {
    "workflowsStartedPerDay": 84.2,
    "workflowsCompletedPerDay": 81.7,
    "peakDailyVolume": 127,
    "averageDailyVolume": 82.4
  },
  "categoryBreakdown": {
    "Appraisal": {
      "total": 1829,
      "active": 167,
      "completed": 1534,
      "failed": 64,
      "cancelled": 64,
      "successRate": 0.95
    },
    "Review": {
      "total": 458,
      "active": 42,
      "completed": 389,
      "failed": 15,
      "cancelled": 12,
      "successRate": 0.97
    }
  },
  "activityMetrics": {
    "admin-review": {
      "totalExecutions": 2543,
      "averageDuration": "02:15:30",
      "successRate": 0.98,
      "timeoutRate": 0.01
    },
    "staff-assignment": {
      "totalExecutions": 2387,
      "averageDuration": "00:45:12",
      "successRate": 0.99,
      "timeoutRate": 0.005
    }
  }
}
```

### Get System Health

**Endpoint**: `GET /workflows/health`

**Response**: `200 OK`
```json
{
  "status": "Healthy",
  "timestamp": "2024-09-08T16:45:20.123Z",
  "components": {
    "workflowEngine": {
      "status": "Healthy",
      "responseTime": "12ms",
      "lastChecked": "2024-09-08T16:45:15.000Z"
    },
    "database": {
      "status": "Healthy",
      "responseTime": "8ms",
      "connectionPoolSize": 45,
      "activeConnections": 12,
      "lastChecked": "2024-09-08T16:45:15.000Z"
    },
    "outboxDispatcher": {
      "status": "Healthy",
      "pendingEvents": 3,
      "processingRate": "15 events/minute",
      "lastProcessed": "2024-09-08T16:44:30.000Z",
      "lastChecked": "2024-09-08T16:45:15.000Z"
    },
    "workflowTimer": {
      "status": "Healthy",
      "activeTimers": 89,
      "dueTimers": 2,
      "lastProcessed": "2024-09-08T16:44:00.000Z",
      "lastChecked": "2024-09-08T16:45:15.000Z"
    },
    "cleanupService": {
      "status": "Healthy",
      "lastCleanup": "2024-09-08T02:00:00.000Z",
      "itemsCleanedUp": 45,
      "nextScheduledRun": "2024-09-09T02:00:00.000Z"
    }
  },
  "performanceMetrics": {
    "averageResponseTime": "45ms",
    "requestsPerSecond": 12.4,
    "errorRate": 0.001,
    "memoryUsage": "67%",
    "cpuUsage": "23%"
  }
}
```

### Get Resilience Metrics

**Endpoint**: `GET /workflows/resilience/metrics`

**Response**: `200 OK`
```json
{
  "totalRetries": 156,
  "circuitBreakerTrips": 2,
  "timeoutOccurrences": 23,
  "rateLimitExceeded": 0,
  "serviceMetrics": {
    "database": {
      "serviceKey": "database",
      "requestCount": 15672,
      "successCount": 15634,
      "failureCount": 38,
      "successRate": 0.9976,
      "circuitOpen": false,
      "lastFailure": "2024-09-08T14:22:15.000Z"
    },
    "external-valuation-service": {
      "serviceKey": "external-valuation-service",
      "requestCount": 234,
      "successCount": 226,
      "failureCount": 8,
      "successRate": 0.9658,
      "circuitOpen": false,
      "lastFailure": "2024-09-08T13:45:30.000Z"
    },
    "notification-service": {
      "serviceKey": "notification-service",
      "requestCount": 1245,
      "successCount": 1243,
      "failureCount": 2,
      "successRate": 0.9984,
      "circuitOpen": false,
      "lastFailure": "2024-09-07T16:30:45.000Z"
    }
  }
}
```

---

## Error Handling

### Error Response Format

All API errors follow a consistent format:

```json
{
  "type": "https://api.example.com/problems/workflow-not-found",
  "title": "Workflow instance not found",
  "status": 404,
  "detail": "The workflow instance with ID '87654321-4321-4321-4321-876543210987' was not found.",
  "instance": "/workflows/instances/87654321-4321-4321-4321-876543210987",
  "timestamp": "2024-09-08T16:45:20.123Z",
  "correlationId": "correlation-12345",
  "errors": {
    "workflowInstanceId": ["The workflow instance was not found or you don't have permission to access it."]
  }
}
```

### Common HTTP Status Codes

| Status Code | Description | Common Causes |
|-------------|-------------|---------------|
| `200 OK` | Request successful | Normal operation |
| `201 Created` | Resource created successfully | Workflow definition created |
| `400 Bad Request` | Invalid request format | Missing required fields, invalid data |
| `401 Unauthorized` | Authentication required | Missing or invalid JWT token |
| `403 Forbidden` | Access denied | Insufficient permissions |
| `404 Not Found` | Resource not found | Workflow instance or definition doesn't exist |
| `409 Conflict` | Concurrent modification | Optimistic concurrency conflict |
| `422 Unprocessable Entity` | Validation failed | Business rule validation error |
| `429 Too Many Requests` | Rate limit exceeded | Too many workflow starts |
| `500 Internal Server Error` | Server error | Unexpected system error |
| `503 Service Unavailable` | Service temporarily unavailable | Circuit breaker open |

### Validation Error Example

**Response**: `422 Unprocessable Entity`
```json
{
  "type": "https://api.example.com/problems/validation-failed",
  "title": "Request validation failed",
  "status": 422,
  "detail": "The request contains validation errors.",
  "instance": "/workflows/instances/start",
  "timestamp": "2024-09-08T16:45:20.123Z",
  "correlationId": "correlation-12345",
  "errors": {
    "workflowDefinitionId": ["The WorkflowDefinitionId field is required."],
    "instanceName": ["The InstanceName field must be between 1 and 100 characters."],
    "initialVariables.PropertyType": ["PropertyType must be one of: Residential, Commercial, Industrial."]
  }
}
```

### Concurrency Conflict Example

**Response**: `409 Conflict`
```json
{
  "type": "https://api.example.com/problems/concurrency-conflict",
  "title": "Concurrency conflict detected",
  "status": 409,
  "detail": "The workflow instance has been modified by another process. Please refresh and try again.",
  "instance": "/workflows/instances/87654321-4321-4321-4321-876543210987/activities/admin-review/complete",
  "timestamp": "2024-09-08T16:45:20.123Z",
  "correlationId": "correlation-12345",
  "retryable": true,
  "retryAfter": 2
}
```

---

## SDK Examples

### C# Client Example

```csharp
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class WorkflowApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public WorkflowApiClient(HttpClient httpClient, string baseUrl, string bearerToken)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task<StartWorkflowResponse> StartWorkflowAsync(StartWorkflowRequest request)
    {
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/workflows/instances/start", content);
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<StartWorkflowResponse>(responseJson, _jsonOptions);
    }

    public async Task<CompleteActivityResponse> CompleteActivityAsync(
        Guid workflowInstanceId, 
        string activityId, 
        CompleteActivityRequest request)
    {
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        var url = $"/api/workflows/instances/{workflowInstanceId}/activities/{activityId}/complete";
        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CompleteActivityResponse>(responseJson, _jsonOptions);
    }
}

// Usage example
var client = new WorkflowApiClient(httpClient, "https://api.example.com", bearerToken);

var startRequest = new StartWorkflowRequest
{
    WorkflowDefinitionId = Guid.Parse("12345678-1234-1234-1234-123456789012"),
    InstanceName = "Property Appraisal AR-2024-001",
    StartedBy = "user@example.com",
    InitialVariables = new Dictionary<string, object>
    {
        ["RequestId"] = "AR-2024-001",
        ["PropertyType"] = "Residential"
    }
};

var startResponse = await client.StartWorkflowAsync(startRequest);
Console.WriteLine($"Started workflow: {startResponse.WorkflowInstanceId}");
```

### JavaScript/TypeScript Example

```typescript
interface StartWorkflowRequest {
  workflowDefinitionId: string;
  instanceName: string;
  startedBy: string;
  initialVariables?: Record<string, any>;
  correlationId?: string;
  assignmentOverrides?: Record<string, AssignmentOverrideRequest>;
}

interface StartWorkflowResponse {
  workflowInstanceId: string;
  instanceName: string;
  status: string;
  nextActivityId: string;
  nextAssignee?: string;
  startedOn: string;
}

class WorkflowApiClient {
  constructor(
    private baseUrl: string,
    private bearerToken: string
  ) {}

  private async request<T>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<T> {
    const url = `${this.baseUrl}${endpoint}`;
    const headers = {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${this.bearerToken}`,
      ...options.headers,
    };

    const response = await fetch(url, { ...options, headers });
    
    if (!response.ok) {
      const error = await response.json();
      throw new Error(`API Error: ${error.title} - ${error.detail}`);
    }

    return response.json();
  }

  async startWorkflow(request: StartWorkflowRequest): Promise<StartWorkflowResponse> {
    return this.request<StartWorkflowResponse>('/api/workflows/instances/start', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async completeActivity(
    workflowInstanceId: string,
    activityId: string,
    request: CompleteActivityRequest
  ): Promise<CompleteActivityResponse> {
    const endpoint = `/api/workflows/instances/${workflowInstanceId}/activities/${activityId}/complete`;
    return this.request<CompleteActivityResponse>(endpoint, {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async getCurrentActivities(userId?: string): Promise<CurrentActivitiesResponse> {
    const params = userId ? `?userId=${encodeURIComponent(userId)}` : '';
    return this.request<CurrentActivitiesResponse>(`/api/workflows/activities/current${params}`);
  }
}

// Usage example
const client = new WorkflowApiClient('https://api.example.com', 'your-jwt-token');

const startRequest: StartWorkflowRequest = {
  workflowDefinitionId: '12345678-1234-1234-1234-123456789012',
  instanceName: 'Property Appraisal AR-2024-001',
  startedBy: 'user@example.com',
  initialVariables: {
    RequestId: 'AR-2024-001',
    PropertyType: 'Residential',
    Priority: 'High'
  },
  correlationId: 'correlation-12345'
};

try {
  const response = await client.startWorkflow(startRequest);
  console.log('Workflow started:', response.workflowInstanceId);
} catch (error) {
  console.error('Failed to start workflow:', error.message);
}
```

### Python Example

```python
import requests
import json
from typing import Dict, Any, Optional
from dataclasses import dataclass

@dataclass
class StartWorkflowRequest:
    workflow_definition_id: str
    instance_name: str
    started_by: str
    initial_variables: Optional[Dict[str, Any]] = None
    correlation_id: Optional[str] = None
    assignment_overrides: Optional[Dict[str, Dict[str, Any]]] = None

class WorkflowApiClient:
    def __init__(self, base_url: str, bearer_token: str):
        self.base_url = base_url.rstrip('/')
        self.session = requests.Session()
        self.session.headers.update({
            'Authorization': f'Bearer {bearer_token}',
            'Content-Type': 'application/json'
        })

    def start_workflow(self, request: StartWorkflowRequest) -> Dict[str, Any]:
        url = f'{self.base_url}/api/workflows/instances/start'
        
        payload = {
            'workflowDefinitionId': request.workflow_definition_id,
            'instanceName': request.instance_name,
            'startedBy': request.started_by
        }
        
        if request.initial_variables:
            payload['initialVariables'] = request.initial_variables
        if request.correlation_id:
            payload['correlationId'] = request.correlation_id
        if request.assignment_overrides:
            payload['assignmentOverrides'] = request.assignment_overrides
        
        response = self.session.post(url, json=payload)
        response.raise_for_status()
        return response.json()

    def complete_activity(
        self, 
        workflow_instance_id: str, 
        activity_id: str, 
        completed_by: str,
        input_data: Dict[str, Any],
        next_assignment_overrides: Optional[Dict[str, Dict[str, Any]]] = None
    ) -> Dict[str, Any]:
        url = f'{self.base_url}/api/workflows/instances/{workflow_instance_id}/activities/{activity_id}/complete'
        
        payload = {
            'completedBy': completed_by,
            'input': input_data
        }
        
        if next_assignment_overrides:
            payload['nextAssignmentOverrides'] = next_assignment_overrides
        
        response = self.session.post(url, json=payload)
        response.raise_for_status()
        return response.json()

# Usage example
client = WorkflowApiClient('https://api.example.com', 'your-jwt-token')

start_request = StartWorkflowRequest(
    workflow_definition_id='12345678-1234-1234-1234-123456789012',
    instance_name='Property Appraisal AR-2024-001',
    started_by='user@example.com',
    initial_variables={
        'RequestId': 'AR-2024-001',
        'PropertyType': 'Residential',
        'Priority': 'High'
    },
    correlation_id='correlation-12345'
)

try:
    response = client.start_workflow(start_request)
    print(f"Workflow started: {response['workflowInstanceId']}")
except requests.exceptions.RequestException as e:
    print(f"Failed to start workflow: {e}")
```

---

## Testing and Development

### Using Postman Collection

A comprehensive Postman collection is available for testing all API endpoints. Import the collection and set up environment variables:

**Environment Variables**:
```json
{
  "baseUrl": "https://api.example.com",
  "bearerToken": "your-jwt-token-here",
  "workflowDefinitionId": "12345678-1234-1234-1234-123456789012",
  "workflowInstanceId": "87654321-4321-4321-4321-876543210987",
  "correlationId": "correlation-12345"
}
```

### Sample Test Scenarios

1. **Happy Path Test**:
   - Start workflow
   - Complete admin review (approved)
   - Complete staff assignment
   - Complete appraisal work
   - Verify workflow completion

2. **Rejection Path Test**:
   - Start workflow
   - Complete admin review (rejected)
   - Verify workflow completion with rejected status

3. **Timeout Test**:
   - Start workflow
   - Wait for activity timeout
   - Verify workflow status and error handling

4. **Concurrent Modification Test**:
   - Start workflow
   - Attempt concurrent activity completion
   - Verify concurrency conflict handling

This comprehensive API reference provides all the information needed to integrate with and use the Workflow API effectively.