# Testing Real-time Notifications System

## 1. Manual Testing with Browser

### Step 1: Start the Application
```bash
cd /Users/gky/Developer/collateral-appraisal-system-api
dotnet run --project Bootstrapper/Api/Api.csproj
```

### Step 2: Test SignalR Connection in Browser Console

Open browser developer console at `https://localhost:7111` and run:

```javascript
// Connect to SignalR hub
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:7111/notificationHub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

// Set up event listeners
connection.on("ReceiveNotification", function (notification) {
    console.log("📧 Personal Notification:", notification);
});

connection.on("ReceiveGroupNotification", function (notification) {
    console.log("📢 Group Notification:", notification);
});

// Start connection
connection.start().then(function () {
    console.log("✅ SignalR Connected successfully!");
    
    // Join user group (replace with actual user ID)
    connection.invoke("JoinGroup", "User_testuser");
    
}).catch(function (err) {
    console.error("❌ SignalR Connection failed:", err);
});
```

## 2. Testing API Endpoints with cURL

### Get User Notifications
```bash
curl -X GET "https://localhost:7111/api/notifications/testuser" \
     -H "accept: application/json" \
     -k
```

### Get Workflow Status
```bash
curl -X GET "https://localhost:7111/api/workflow/1/status" \
     -H "accept: application/json" \
     -k
```

### Mark Notification as Read
```bash
curl -X PATCH "https://localhost:7111/api/notifications/123e4567-e89b-12d3-a456-426614174000/read" \
     -H "accept: application/json" \
     -k
```

## 3. Testing with Postman

### Collection Setup
Create a Postman collection with these requests:

**Environment Variables:**
- `baseUrl`: `https://localhost:7111`
- `userId`: `testuser`
- `requestId`: `1`

**Requests:**
1. **GET Notifications** - `{{baseUrl}}/api/notifications/{{userId}}`
2. **GET Workflow Status** - `{{baseUrl}}/api/workflow/{{requestId}}/status`
3. **PATCH Mark as Read** - `{{baseUrl}}/api/notifications/{notificationId}/read`

## 4. Integration Testing with MassTransit

### Test Event Publishing
Create a test endpoint to simulate workflow events:

```csharp
// Add this to test the notification system
[HttpPost("/test/simulate-task-completion")]
public async Task<IResult> SimulateTaskCompletion([FromServices] IBus bus)
{
    var taskCompleted = new TaskCompleted
    {
        CorrelationId = Guid.NewGuid(),
        TaskName = "Admin",
        ActionTaken = "P" // Proceed
    };

    await bus.Publish(taskCompleted);
    return Results.Ok("Task completion event published");
}

[HttpPost("/test/simulate-task-assignment")]
public async Task<IResult> SimulateTaskAssignment([FromServices] IBus bus)
{
    var taskAssigned = new TaskAssigned
    {
        CorrelationId = Guid.NewGuid(),
        TaskName = "AppraisalStaff",
        AssignedTo = "testuser",
        AssignedType = "U"
    };

    await bus.Publish(taskAssigned);
    return Results.Ok("Task assignment event published");
}
```

### Test Commands
```bash
# Simulate task completion
curl -X POST "https://localhost:7111/test/simulate-task-completion" -k

# Simulate task assignment  
curl -X POST "https://localhost:7111/test/simulate-task-assignment" -k
```

## 5. End-to-End Workflow Testing

### Complete Workflow Test Scenario:

1. **Start Application & Connect SignalR**
2. **Submit Request** (triggers workflow)
3. **Complete Admin Task** → Should see notifications for:
   - Task completion
   - Workflow transition
   - Next assignment
4. **Complete AppraisalStaff Task** → Should see progression
5. **Complete AppraisalChecker Task** → Should see progression  
6. **Complete AppraisalVerifier Task** → Should see workflow completion

### Test Script Example:
```bash
#!/bin/bash

echo "🚀 Starting E2E Notification Test"

# 1. Submit initial request
echo "📝 Submitting request..."
REQUEST_ID=$(curl -s -X POST "https://localhost:7111/api/assignment/kickstart" \
  -H "Content-Type: application/json" \
  -d '{"requestId": 123}' \
  -k | jq -r '.requestId')

echo "✅ Request submitted: $REQUEST_ID"

# 2. Complete Admin task
echo "⚖️  Completing Admin task..."
curl -s -X POST "https://localhost:7111/api/assignment/complete" \
  -H "Content-Type: application/json" \
  -d '{"correlationId": "'$CORRELATION_ID'", "taskName": "Admin", "actionTaken": "P"}' \
  -k

echo "✅ Admin task completed"

# 3. Check workflow status
echo "📊 Checking workflow status..."
curl -s -X GET "https://localhost:7111/api/workflow/$REQUEST_ID/status" \
  -H "accept: application/json" \
  -k | jq '.'
```

## 6. Unit Testing

### Test the NotificationService
```csharp
[Test]
public async Task SendTaskAssignedNotificationAsync_ShouldSendNotification()
{
    // Arrange
    var mockHubContext = Mock.Of<IHubContext<NotificationHub>>();
    var mockLogger = Mock.Of<ILogger<NotificationService>>();
    var mockDateTimeProvider = Mock.Of<IDateTimeProvider>();
    
    var service = new NotificationService(mockHubContext, mockLogger, mockDateTimeProvider);
    
    var notification = new TaskAssignedNotificationDto(
        Guid.NewGuid(),
        "Admin",
        "testuser",
        "U",
        123,
        "Admin",
        DateTime.UtcNow
    );

    // Act
    await service.SendTaskAssignedNotificationAsync(notification);

    // Assert
    // Verify SignalR hub was called
    Mock.Get(mockHubContext.Clients.Group("User_testuser"))
        .Verify(x => x.SendAsync("ReceiveNotification", 
            It.IsAny<NotificationDto>(), 
            default), Times.Once);
}
```

## 7. Load Testing SignalR

### Using Artillery.js
```yaml
# artillery-signalr-test.yml
config:
  target: 'https://localhost:7111'
  phases:
    - duration: 30
      arrivalRate: 10
  socketio:
    transports: ['websocket']

scenarios:
  - name: 'SignalR Connection Test'
    weight: 100
    engine: socketio
    socketio:
      path: '/notificationHub'
    flow:
      - emit:
          channel: 'JoinGroup'
          data: 'User_{{ $uuid }}'
      - think: 5
```

Run with: `artillery run artillery-signalr-test.yml`

## 8. Monitoring & Debugging

### Enable Detailed Logging
Add to `appsettings.Development.json`:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "Microsoft.AspNetCore.SignalR": "Debug",
        "Notification": "Debug",
        "Assignment": "Debug"
      }
    }
  }
}
```

### SignalR Connection Monitoring
Check browser Network tab for WebSocket connections:
- Look for `ws://localhost:7111/notificationHub`
- Monitor message flow in WebSocket frames

### Database Monitoring
Query saga state to verify workflow progression:
```sql
SELECT * FROM [saga].[AppraisalSagaState] ORDER BY StartedAt DESC;
```

## 9. Common Issues & Troubleshooting

### SignalR Connection Issues
- **CORS Error**: Check CORS policy includes SignalR origin
- **Authentication**: Ensure user is authenticated for SignalR
- **Firewall**: Check ports 7111 and 7112 are open

### Missing Notifications
- Check MassTransit consumers are registered
- Verify event publishing in saga handlers
- Check SignalR hub context injection

### Event Flow Debugging
Enable MassTransit logging:
```json
{
  "Logging": {
    "LogLevel": {
      "MassTransit": "Debug"
    }
  }
}
```

## 10. Performance Testing

### Benchmark Notification Throughput
```bash
# Send 1000 notifications rapidly
for i in {1..1000}; do
  curl -X POST "https://localhost:7111/test/simulate-task-assignment" -k &
done
wait
```

Monitor:
- SignalR connection count
- Memory usage
- Message delivery latency
- Database connection pool

This comprehensive testing approach ensures your real-time notification system works reliably across all scenarios.