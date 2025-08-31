# Real-time Notifications Implementation for MassTransit Saga Workflow

## Overview

This implementation provides real-time frontend notifications for your MassTransit Saga-based collateral appraisal workflow. The solution addresses your need for the frontend to know about task progress, assignments, and workflow transitions in real-time.

## Key Features

### 1. SignalR Hub for Real-time Communication
- **NotificationHub** (`/Modules/Notification/Notification/Notification/Hubs/NotificationHub.cs`)
- Automatically groups users by their identity for targeted notifications
- Handles connection/disconnection events
- Available at endpoint: `/notificationHub`

### 2. Comprehensive Notification System
- **INotificationService** with multiple notification types:
  - Task assigned notifications
  - Task completed notifications
  - Workflow progress updates
  - System notifications

### 3. Event-Driven Integration
- **Event Consumers** automatically listen to your existing saga events:
  - `TaskAssignedNotificationEventHandler` - Notifies users when tasks are assigned
  - `TaskCompletedNotificationEventHandler` - Broadcasts task completion
  - `TransitionCompletedNotificationEventHandler` - Updates workflow progress

### 4. RESTful API Endpoints
- `GET /api/notifications/{userId}` - Get user notifications
- `GET /api/notifications/{userId}/unread` - Get unread notifications
- `PATCH /api/notifications/{notificationId}/read` - Mark notification as read
- `PATCH /api/notifications/users/{userId}/read-all` - Mark all as read
- `GET /api/workflow/{requestId}/status` - Get workflow status

## Architecture Flow

```
Frontend User Submits Task 
    ↓
TaskCompleted Event Published
    ↓
AppraisalStateMachine Processes Event
    ↓
State Transition + AssignmentRequested Event
    ↓
TaskAssigned Event Published
    ↓
Notification Consumers Triggered
    ↓
SignalR Notifications Sent to Frontend
```

## Frontend Integration

### 1. SignalR Connection
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/notificationHub")
    .build();

// Listen for notifications
connection.on("ReceiveNotification", (notification) => {
    // Handle task assigned notification
    displayNotification(notification);
});

connection.on("ReceiveGroupNotification", (notification) => {
    // Handle workflow progress updates
    updateWorkflowProgress(notification);
});
```

### 2. API Integration
```javascript
// Get user notifications
const notifications = await fetch(`/api/notifications/${userId}`);

// Get workflow status
const workflowStatus = await fetch(`/api/workflow/${requestId}/status`);

// Mark as read
await fetch(`/api/notifications/${notificationId}/read`, { method: 'PATCH' });
```

## Notification Types

### TaskAssignedNotification
- Sent when a task is assigned to a user
- Contains: task name, assignee, current state, request ID
- Real-time delivery to assigned user

### TaskCompletedNotification
- Sent when a task is completed (approved/returned)
- Contains: action taken, next state, completed by user
- Broadcast to relevant stakeholders

### WorkflowProgressNotification
- Sent on workflow state transitions
- Contains: current state, next assignee, workflow steps
- Shows complete workflow progress

## Configuration

### CORS Setup
CORS is configured to support SignalR with credentials:
```csharp
policy.WithOrigins("https://localhost:3000", "https://localhost:7111")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials(); // Required for SignalR
```

### SignalR Configuration
```csharp
services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
```

## Benefits

### For Your Frontend Users:
1. **Immediate Notifications** - No need to refresh or poll for updates
2. **Clear Workflow Visibility** - See exactly where requests are in the approval process  
3. **Next Action Awareness** - Know who needs to act next and what action is required
4. **Progress Tracking** - Visual workflow progress with completed/current/pending states

### For Your Application:
1. **Event-Driven Architecture** - Seamlessly integrates with existing MassTransit saga
2. **Scalable Real-time Communication** - SignalR handles connection management
3. **Flexible Notification System** - Easy to extend with new notification types
4. **RESTful API Fallback** - APIs available for polling scenarios

## Usage Example

When a user completes an Admin task with "Proceed" action:

1. **TaskCompleted** event triggers saga transition to **AppraisalStaff** state
2. **AssignmentRequested** event finds next available appraiser  
3. **TaskAssigned** event assigns task to selected appraiser
4. **Real-time notifications** sent:
   - Assigned appraiser gets "New Task Assigned" notification
   - Original submitter gets workflow progress update
   - Any watchers get workflow status change

## Next Steps

1. **Frontend Implementation**: Connect SignalR client and implement notification UI
2. **Database Persistence**: Implement UserNotification table for notification history
3. **User Preferences**: Add notification preferences (email, push, etc.)
4. **Testing**: Add unit tests for notification service

## Security Considerations

- Authentication required for all endpoints
- User-specific notification delivery
- Group-based access control through SignalR
- CORS properly configured for production domains

This implementation transforms your saga workflow from a backend-only process into a fully interactive, real-time user experience.