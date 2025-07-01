# Developer Quick Reference Guide
## Collateral Appraisal System API

### **🚀 Quick Start Commands**

```bash
# Setup environment (first time)
git clone <repository-url>
cd collateral-appraisal-system-api
docker-compose up -d
dotnet restore
dotnet run --project Bootstrapper/Api

# Daily development
docker-compose up -d    # Start infrastructure
dotnet run --project Bootstrapper/Api    # Start API
```

**API Base URL**: `https://localhost:7111`

---

### **📁 Project Structure**

```
├── Bootstrapper/Api/           # Main API entry point
├── Modules/
│   ├── Request/               # Core business logic (CRUD operations)
│   ├── Auth/                  # Authentication (OAuth2/OpenID)
│   ├── Notification/          # Event processing
│   └── Collateral/            # Asset management
├── Shared/                    # DDD infrastructure, CQRS contracts
└── docs/                      # Documentation
```

---

### **🛠️ Infrastructure Services**

| Service | Port | Purpose | Management URL |
|---------|------|---------|----------------|
| SQL Server | 1433 | Primary database | N/A |
| Redis | 6379 | Distributed cache | N/A |
| RabbitMQ | 5672/15672 | Message broker | http://localhost:15672 |
| Seq | 5341 | Structured logging | http://localhost:5341 |


---

### **🔧 Common Development Tasks**

#### **Adding a New Feature**
1. **Create Command/Query**:
   ```csharp
   // In Modules/{Module}/Features/{FeatureName}/
   public record MyCommand(string Data) : ICommand<Result<MyResponse>>;
   ```

2. **Create Handler**:
   ```csharp
   public class MyCommandHandler : IRequestHandler<MyCommand, Result<MyResponse>>
   {
       public async Task<Result<MyResponse>> Handle(MyCommand request, 
           CancellationToken cancellationToken)
       {
           // Implementation
       }
   }
   ```

3. **Add Validation**:
   ```csharp
   public class MyCommandValidator : AbstractValidator<MyCommand>
   {
       public MyCommandValidator()
       {
           RuleFor(x => x.Data).NotEmpty();
       }
   }
   ```

4. **Register Endpoint**:
   ```csharp
   // Carter Endpoint
   app.MapPost("/my-endpoint", async (MyCommand command, IMediator mediator) =>
       await mediator.Send(command));
   ```

#### **Adding a New Entity**
```csharp
public class MyEntity : Entity<MyId>
{
    public string Name { get; private set; }
    
    public MyEntity(MyId id, string name)
    {
        Name = name;
    }
    
    public void UpdateName(string newName)
    {
        Name = newName;
    }
}
```


#### **Adding a Value Object**
```csharp
public record MyValueObject
{
    public string Property { get; init; }
    
    public static Result<MyValueObject> Create(string property)
    {
        if (string.IsNullOrWhiteSpace(property))
            return Result.Failure<MyValueObject>("Property cannot be empty");
            
        return new MyValueObject { Property = property };
    }
}
```

#### **Adding Domain Events**
```csharp
// 1. Create event
public record MyDomainEvent(string Data) : IDomainEvent;

// 2. Raise in aggregate
public void DoSomething()
{
    // Business logic
    RaiseDomainEvent(new MyDomainEvent("data"));
}

// 3. Handle event
public class MyDomainEventHandler : IDomainEventHandler<MyDomainEvent>
{
    public async Task Handle(MyDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // Handle event
    }
}
```

---

### **🐛 Debugging & Troubleshooting**

#### **Common Issues**

**API Won't Start**:
```bash
# Check if port is in use
netstat -an | grep 7111

# Clean and rebuild
dotnet clean && dotnet restore && dotnet build
```

**Database Connection Errors**:
```bash
# Check SQL Server container
docker logs collateral-sql-server

# Test connection
docker exec -it collateral-sql-server sqlcmd -S localhost -U sa -P "YourPassword123!" -Q "SELECT 1"
```

**Redis Connection Issues**:
```bash
# Check Redis container
docker logs collateral-redis

# Test connection
docker exec -it collateral-redis redis-cli ping
```

**RabbitMQ Not Working**:
```bash
# Check RabbitMQ container
docker logs collateral-rabbitmq

# Access management console
open http://localhost:15672
```

#### **Useful Debugging Commands**
```bash
# View container status
docker-compose ps

# Follow application logs
dotnet run --project Bootstrapper/Api --verbosity diagnostic

# View infrastructure logs
docker-compose logs -f

# Reset entire environment
docker-compose down -v && docker-compose up -d
```

---

### **📋 Code Patterns**

#### **Result Pattern** (Error Handling)
```csharp
// Success
return Result.Success(data);

// Failure
return Result.Failure<T>("Error message");

// Usage
var result = await handler.Handle(command);
if (result.IsSuccess)
    return Ok(result.Value);
else
    return BadRequest(result.Error);
```

#### **Repository Pattern**
```csharp
public interface IMyRepository
{
    Task<MyEntity?> GetByIdAsync(MyId id);
    Task SaveAsync(MyEntity entity);
}

// Implementation
public class MyRepository : IMyRepository
{
    private readonly MyDbContext _context;
    
    public async Task<MyEntity?> GetByIdAsync(MyId id)
    {
        return await _context.MyEntities.FindAsync(id);
    }
}
```

#### **Caching Decorator**
```csharp
public class CachedMyRepository : IMyRepository
{
    private readonly IMyRepository _repository;
    private readonly IDistributedCache _cache;
    
    public async Task<MyEntity?> GetByIdAsync(MyId id)
    {
        var cacheKey = $"my-entity:{id}";
        var cached = await _cache.GetStringAsync(cacheKey);
        
        if (cached != null)
            return JsonSerializer.Deserialize<MyEntity>(cached);
            
        var entity = await _repository.GetByIdAsync(id);
        if (entity != null)
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(entity));
            
        return entity;
    }
}
```

---

### **🧪 Testing Patterns**

#### **Unit Test Example**
```csharp
[Test]
public async Task CreateRequest_ValidData_ReturnsSuccess()
{
    // Arrange
    var command = new CreateRequestCommand(validRequestDetail);
    var handler = new CreateRequestHandler(mockRepository.Object);
    
    // Act
    var result = await handler.Handle(command, CancellationToken.None);
    
    // Assert
    result.IsSuccess.Should().BeTrue();
    mockRepository.Verify(x => x.SaveAsync(It.IsAny<Request>()), Times.Once);
}
```

#### **Integration Test Setup**
```csharp
[SetUp]
public async Task Setup()
{
    _factory = new WebApplicationFactory<Program>();
    _client = _factory.CreateClient();
    
    // Seed test data
    using var scope = _factory.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<RequestDbContext>();
    await context.Database.EnsureCreatedAsync();
}
```

---

### **⚡ Performance Tips**

#### **Caching Best Practices**
- Use caching for read-heavy operations
- Implement cache invalidation on data changes
- Use appropriate cache expiration times
- Monitor cache hit rates in Seq logs

#### **Database Optimization**
- Use `AsNoTracking()` for read-only queries
- Implement proper indexing on frequently queried fields
- Use projection for large result sets
- Consider pagination for list endpoints

#### **Event Processing**
- Keep event handlers lightweight
- Use async processing for time-consuming operations
- Implement idempotency for event handlers
- Monitor message queue depth

---

### **🔍 Monitoring & Observability**

#### **Seq Queries** (http://localhost:5341)
```sql
-- Find all requests for a specific user
@Properties.UserId = 'user123'

-- Track request processing time
@Level = 'Information' and @MessageTemplate like '%processed in%'

-- Find errors
@Level = 'Error'

-- Track domain events
@Properties.EventType is not null
```

#### **Performance Monitoring**
- **Request timing**: Logged automatically for all endpoints
- **Cache hit rates**: Monitor in Seq logs
- **Event processing**: Track in RabbitMQ management console
- **Database performance**: Monitor connection pools and query timing

---

### **🚨 Production Checklist**

Before deploying:
- [ ] Update connection strings in `appsettings.Production.json`
- [ ] Configure proper logging levels
- [ ] Set up health check monitoring
- [ ] Configure proper authentication settings
- [ ] Set up SSL certificates
- [ ] Configure cache expiration policies
- [ ] Set up database backup strategy
- [ ] Configure message queue clustering (if needed)