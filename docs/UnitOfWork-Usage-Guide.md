# UnitOfWork Pattern Usage Guide

## Overview

The UnitOfWork pattern is fully implemented in this codebase but currently unused. This guide demonstrates how to leverage the existing `IUnitOfWork` implementation in `Shared/Data/` for coordinated data operations across multiple repositories.

## Current Implementation

### Interface: `Shared/Data/IUnitOfWork.cs`
```csharp
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    IRepository<T, TId> Repository<T, TId>() where T : class, IEntity<TId>;
    IReadRepository<T, TId> ReadRepository<T, TId>() where T : class, IEntity<TId>;
}
```

### Implementation: `Shared/Data/UnitOfWork.cs`
- Manages DbContext and transactions
- Provides repository caching
- Coordinates save operations across multiple repositories

## Why Use UnitOfWork?

### Current Pattern (What You Have Now)
```csharp
// Example from AddRequestCommentCommandHandler.cs
public class AddRequestCommentCommandHandler(
    IRequestRepository requestRepository,
    IRequestCommentRepository requestCommentRepository)
{
    public async Task<Result> Handle(AddRequestCommentCommand command, CancellationToken cancellationToken)
    {
        // Two separate SaveChanges calls - no coordination
        await requestCommentRepository.AddAsync(comment, cancellationToken);
        await requestCommentRepository.SaveChangesAsync(cancellationToken);
        // What if this fails? Previous save is already committed!
    }
}
```

### UnitOfWork Pattern (Better Coordination)
```csharp
public class AddRequestCommentCommandHandler(IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(AddRequestCommentCommand command, CancellationToken cancellationToken)
    {
        var requestRepo = unitOfWork.Repository<Request, Guid>();
        var commentRepo = unitOfWork.Repository<RequestComment, Guid>();
        
        // All operations in same transaction
        var request = await requestRepo.GetByIdAsync(command.RequestId);
        var comment = new RequestComment(command.RequestId, command.Content);
        
        await commentRepo.AddAsync(comment);
        request.UpdateLastActivity(); // Business logic
        
        // Single coordination point - all or nothing
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

## Integration Steps

### Step 1: Register UnitOfWork in DI Container

Add to your module registration (e.g., in `RequestModule.cs`):

```csharp
public static IServiceCollection AddRequestModule(this IServiceCollection services, IConfiguration configuration)
{
    // Existing registrations...
    
    // Add UnitOfWork registration
    services.AddScoped<IUnitOfWork, UnitOfWork>();
    
    return services;
}
```

### Step 2: Update Service Constructors

**Before:**
```csharp
public class MyHandler(
    IRequestRepository requestRepository,
    IRequestCommentRepository commentRepository)
```

**After:**
```csharp
public class MyHandler(IUnitOfWork unitOfWork)
```

## Usage Patterns

### Pattern 1: Basic CRUD Operations

```csharp
public class UpdateRequestHandler(IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(UpdateRequestCommand command, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Request, Guid>();
        
        var request = await repository.GetByIdAsync(command.Id);
        if (request == null)
            return Result.NotFound();
            
        request.Update(command.Title, command.Description);
        
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

### Pattern 2: Multi-Repository Operations

```csharp
public class AssignRequestHandler(IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(AssignRequestCommand command, CancellationToken cancellationToken)
    {
        var requestRepo = unitOfWork.Repository<Request, Guid>();
        var assignmentRepo = unitOfWork.Repository<Assignment, Guid>();
        var notificationRepo = unitOfWork.Repository<Notification, Guid>();
        
        // Get entities
        var request = await requestRepo.GetByIdAsync(command.RequestId);
        
        // Business operations
        request.AssignTo(command.AssigneeId);
        var assignment = new Assignment(command.RequestId, command.AssigneeId);
        var notification = new Notification($"Request {request.Id} assigned to you");
        
        // All operations coordinated
        await assignmentRepo.AddAsync(assignment);
        await notificationRepo.AddAsync(notification);
        
        // Single save point - all succeed or all fail
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

### Pattern 3: Transaction Management for Complex Operations

```csharp
public class ComplexWorkflowHandler(IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(ComplexWorkflowCommand command, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var requestRepo = unitOfWork.Repository<Request, Guid>();
            var workflowRepo = unitOfWork.Repository<WorkflowInstance, Guid>();
            var assignmentRepo = unitOfWork.Repository<Assignment, Guid>();
            
            // Complex business logic across multiple aggregates
            var request = await requestRepo.GetByIdAsync(command.RequestId);
            request.StartWorkflow();
            
            var workflow = new WorkflowInstance(request.Id, command.WorkflowDefinition);
            await workflowRepo.AddAsync(workflow);
            
            var assignment = new Assignment(request.Id, workflow.GetNextAssignee());
            await assignmentRepo.AddAsync(assignment);
            
            // Save all changes
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            
            return Result.Success();
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
```

### Pattern 4: Read-Only Operations

```csharp
public class GetRequestWithDetailsHandler(IUnitOfWork unitOfWork)
{
    public async Task<RequestDetailDto> Handle(GetRequestWithDetailsQuery query, CancellationToken cancellationToken)
    {
        var requestRepo = unitOfWork.ReadRepository<Request, Guid>();
        var commentRepo = unitOfWork.ReadRepository<RequestComment, Guid>();
        var assignmentRepo = unitOfWork.ReadRepository<Assignment, Guid>();
        
        // Read-only operations - no SaveChanges needed
        var request = await requestRepo.GetByIdAsync(query.RequestId);
        var comments = await commentRepo.GetByRequestIdAsync(query.RequestId);
        var assignments = await assignmentRepo.GetByRequestIdAsync(query.RequestId);
        
        return new RequestDetailDto
        {
            Request = request,
            Comments = comments,
            Assignments = assignments
        };
    }
}
```

## Real-World Examples from Your Codebase

### Example 1: Converting AddRequestCommentCommandHandler

**Current Implementation:**
```csharp
// File: Modules/Request/Request/RequestComments/Features/AddRequestComment/AddRequestCommentCommandHandler.cs
public class AddRequestCommentCommandHandler(
    IRequestRepository requestRepository,
    IRequestCommentRepository requestCommentRepository) : ICommandHandler<AddRequestCommentCommand>
{
    public async Task<Result> Handle(AddRequestCommentCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null) return Result.NotFound();

        var comment = new RequestComment(command.RequestId, command.Content, command.UserId);
        await requestCommentRepository.AddAsync(comment, cancellationToken);
        await requestCommentRepository.SaveChangesAsync(cancellationToken); // Separate save!

        return Result.Success();
    }
}
```

**UnitOfWork Version:**
```csharp
public class AddRequestCommentCommandHandler(IUnitOfWork unitOfWork) : ICommandHandler<AddRequestCommentCommand>
{
    public async Task<Result> Handle(AddRequestCommentCommand command, CancellationToken cancellationToken)
    {
        var requestRepo = unitOfWork.Repository<Request, Guid>();
        var commentRepo = unitOfWork.Repository<RequestComment, Guid>();
        
        var request = await requestRepo.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null) return Result.NotFound();

        var comment = new RequestComment(command.RequestId, command.Content, command.UserId);
        await commentRepo.AddAsync(comment, cancellationToken);
        
        // Optional: Update request's last activity
        request.UpdateLastActivity();
        
        // Single coordination point
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

### Example 2: Workflow Engine Integration

**Current Pattern in WorkflowEngine.cs:**
```csharp
await _workflowInstanceRepository.AddAsync(workflowInstance, cancellationToken);
await _workflowInstanceRepository.SaveChangesAsync(cancellationToken);
```

**UnitOfWork Version:**
```csharp
public class WorkflowEngine(IUnitOfWork unitOfWork)
{
    public async Task<Result<Guid>> StartWorkflowAsync(StartWorkflowCommand command, CancellationToken cancellationToken)
    {
        var workflowRepo = unitOfWork.Repository<WorkflowInstance, Guid>();
        var requestRepo = unitOfWork.Repository<Request, Guid>();
        var assignmentRepo = unitOfWork.Repository<Assignment, Guid>();
        
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Create workflow instance
            var workflow = new WorkflowInstance(command.RequestId, command.Definition);
            await workflowRepo.AddAsync(workflow, cancellationToken);
            
            // Update request status
            var request = await requestRepo.GetByIdAsync(command.RequestId, cancellationToken);
            request.StartWorkflow(workflow.Id);
            
            // Create initial assignment
            var firstActivity = workflow.GetCurrentActivity();
            if (firstActivity.RequiresAssignment)
            {
                var assignment = new Assignment(command.RequestId, firstActivity.AssigneeId);
                await assignmentRepo.AddAsync(assignment, cancellationToken);
            }
            
            // All operations coordinated
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            
            return Result.Success(workflow.Id);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
```

## Migration Strategy

### Gradual Adoption Approach

1. **Start with New Features**: Use UnitOfWork for new handlers/services
2. **Convert Complex Operations**: Migrate handlers that span multiple repositories
3. **Keep Simple Operations**: Single-repository operations can remain as-is
4. **Test Thoroughly**: Ensure transaction behavior works as expected

### When to Use UnitOfWork vs Current Pattern

**Use UnitOfWork When:**
- Operations span multiple repositories
- Need explicit transaction control
- Complex business logic requires rollback capability
- Coordinating changes across multiple aggregates

**Keep Current Pattern When:**
- Simple single-repository CRUD operations
- Read-only queries
- Performance-critical paths where transaction overhead matters

## Benefits

1. **Data Consistency**: All-or-nothing transaction behavior
2. **Performance**: Single SaveChanges call reduces database roundtrips
3. **Simplified Error Handling**: Single point of failure/rollback
4. **Repository Caching**: Repositories are cached per UnitOfWork instance
5. **Clean Architecture**: Clear separation of concerns

## Best Practices

1. **Always use `using` statements** or ensure proper disposal
2. **Begin transactions explicitly** for complex operations
3. **Keep transaction scope as small as possible**
4. **Handle exceptions properly** with rollback
5. **Use ReadRepository for query-only operations**

## Testing Considerations

```csharp
// Example unit test with UnitOfWork
[Test]
public async Task Handle_ValidRequest_AddsCommentSuccessfully()
{
    // Arrange
    var unitOfWork = new Mock<IUnitOfWork>();
    var requestRepo = new Mock<IRepository<Request, Guid>>();
    var commentRepo = new Mock<IRepository<RequestComment, Guid>>();
    
    unitOfWork.Setup(x => x.Repository<Request, Guid>()).Returns(requestRepo.Object);
    unitOfWork.Setup(x => x.Repository<RequestComment, Guid>()).Returns(commentRepo.Object);
    
    var handler = new AddRequestCommentCommandHandler(unitOfWork.Object);
    
    // Act
    var result = await handler.Handle(command, CancellationToken.None);
    
    // Assert
    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
}
```

## Conclusion

The UnitOfWork pattern provides excellent coordination capabilities for complex operations while maintaining the flexibility to use simpler patterns where appropriate. Your existing implementation is robust and ready to use - just needs integration into your dependency injection and handler patterns.

Start with new features or complex operations, and gradually migrate existing handlers as needed. The pattern works alongside your current repository approach, so adoption can be incremental.