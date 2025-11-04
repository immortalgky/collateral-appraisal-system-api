---
name: Module Implementation
about: For implementing modules (Auth, Request, Document, Appraisal, Collateral)
title: '[MODULE] '
labels: module, implementation
assignees: ''
---

## 📦 Module Information

**Module**: <!-- Auth / Request / Document / Appraisal / Collateral -->
**Task ID**: <!-- e.g., AUTH-001, REQ-002, DOC-003 -->
**Sprint**: <!-- Sprint 1, 2, 3, or 4 -->
**Estimated Time**: <!-- e.g., 6 hours -->
**Priority**: <!-- Critical, High, Medium, Low -->

## 📝 Description

<!-- Brief description of what needs to be implemented in this module -->

## ✅ Task Checklist

<!-- List all subtasks with time estimates -->
- [ ] **Create entity/aggregate** - Define domain model (Xh)
- [ ] **Add value objects** - Create value objects (Xh)
- [ ] **Implement business logic** - Add domain behaviors (Xh)
- [ ] **Create DbContext** - Set up EF Core configuration (Xh)
- [ ] **Create migrations** - Generate and apply migrations (Xh)
- [ ] **Create commands** - Implement CQRS commands (Xh)
- [ ] **Create queries** - Implement CQRS queries (Xh)
- [ ] **Add validation** - FluentValidation rules (Xh)
- [ ] **Create API endpoints** - Carter endpoints (Xh)
- [ ] **Write tests** - Unit and integration tests (Xh)

**Total Estimated Time**: X hours (~X days)

## 🎯 Acceptance Criteria

<!-- Clear definition of done -->
- [ ] All entities created with proper relationships
- [ ] DbContext configured with indexes and conventions
- [ ] Migrations applied successfully
- [ ] All commands have validators
- [ ] All queries return correct data
- [ ] API endpoints functional and tested
- [ ] Unit tests: ≥80% coverage
- [ ] Integration tests: All happy paths covered
- [ ] Documentation updated (XML comments)
- [ ] Code reviewed and approved

## 🔗 Dependencies

<!-- Issues that must be completed before this one -->
- Depends on: #issue-number
- Blocks: #issue-number

## 💡 Implementation Guide

### 1. Entity/Aggregate Structure

```csharp
// Example entity structure
public class YourAggregate : AggregateRoot<Guid>
{
    public string AggregateNumber { get; private set; } = default!;
    public YourStatus Status { get; private set; }

    // Value Objects
    public YourDetail Detail { get; private set; } = default!;

    // Collections
    private readonly List<YourChild> _children = new();
    public IReadOnlyCollection<YourChild> Children => _children.AsReadOnly();

    // Factory method
    public static YourAggregate Create(/* parameters */)
    {
        var aggregate = new YourAggregate
        {
            Id = Guid.NewGuid(),
            AggregateNumber = GenerateNumber(),
            Status = YourStatus.Pending,
            // Initialize other fields
        };

        // Raise domain event
        aggregate.AddDomainEvent(new YourCreatedEvent(aggregate.Id));

        return aggregate;
    }

    // Business methods
    public void UpdateStatus(YourStatus newStatus)
    {
        if (!CanTransitionTo(newStatus))
            throw new InvalidOperationException($"Cannot transition from {Status} to {newStatus}");

        Status = newStatus;
        AddDomainEvent(new YourStatusChangedEvent(Id, newStatus));
    }

    private bool CanTransitionTo(YourStatus newStatus)
    {
        // Implement state machine logic
        return true;
    }
}
```

### 2. DbContext Configuration

```csharp
public class YourModuleDbContext : DbContext
{
    public DbSet<YourAggregate> YourAggregates => Set<YourAggregate>();

    public YourModuleDbContext(DbContextOptions<YourModuleDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(YourModuleDbContext).Assembly);

        // Apply global conventions
        modelBuilder.ApplyGlobalConventions();
    }
}
```

### 3. Entity Configuration

```csharp
public class YourAggregateConfiguration : IEntityTypeConfiguration<YourAggregate>
{
    public void Configure(EntityTypeBuilder<YourAggregate> builder)
    {
        builder.ToTable("YourAggregates", "yourschema");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AggregateNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.AggregateNumber)
            .IsUnique();

        // Value object
        builder.OwnsOne(x => x.Detail, detail =>
        {
            detail.Property(d => d.Property1).HasMaxLength(200);
        });

        // Collection
        builder.HasMany(x => x.Children)
            .WithOne()
            .HasForeignKey("YourAggregateId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### 4. Command Example

```csharp
public record CreateYourCommand(
    string Property1,
    string Property2
) : IRequest<Result<Guid>>;

public class CreateYourCommandHandler : IRequestHandler<CreateYourCommand, Result<Guid>>
{
    private readonly IYourRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public async Task<Result<Guid>> Handle(CreateYourCommand request, CancellationToken cancellationToken)
    {
        // Create aggregate
        var aggregate = YourAggregate.Create(request.Property1, request.Property2);

        // Add to repository
        await _repository.AddAsync(aggregate, cancellationToken);

        // Commit
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(aggregate.Id);
    }
}

public class CreateYourCommandValidator : AbstractValidator<CreateYourCommand>
{
    public CreateYourCommandValidator()
    {
        RuleFor(x => x.Property1)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Property2)
            .NotEmpty();
    }
}
```

### 5. Query Example

```csharp
public record GetYourByIdQuery(Guid Id) : IRequest<Result<YourResponse>>;

public class GetYourByIdQueryHandler : IRequestHandler<GetYourByIdQuery, Result<YourResponse>>
{
    private readonly IYourRepository _repository;

    public async Task<Result<YourResponse>> Handle(GetYourByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
            return Result.Failure<YourResponse>(YourErrors.NotFound);

        var response = new YourResponse(
            entity.Id,
            entity.AggregateNumber,
            entity.Status.ToString()
        );

        return Result.Success(response);
    }
}
```

### 6. Carter Endpoint

```csharp
public class CreateYourEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/yours", async (
            CreateYourRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateYourCommand(
                request.Property1,
                request.Property2
            );

            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/yours/{result.Value}", new { id = result.Value })
                : Results.BadRequest(result.Error);
        })
        .WithName("CreateYour")
        .WithTags("Yours")
        .RequireAuthorization()
        .Produces<Guid>(StatusCodes.Status201Created)
        .Produces<Error>(StatusCodes.Status400BadRequest);
    }
}
```

## 🧪 Testing Requirements

### Unit Tests
```csharp
public class YourAggregateTests
{
    [Fact]
    public void Create_ShouldGenerateNumber()
    {
        // Arrange & Act
        var aggregate = YourAggregate.Create("property1", "property2");

        // Assert
        aggregate.AggregateNumber.Should().NotBeNullOrEmpty();
        aggregate.Status.Should().Be(YourStatus.Pending);
    }

    [Fact]
    public void UpdateStatus_WithValidTransition_ShouldUpdateStatus()
    {
        // Arrange
        var aggregate = YourAggregate.Create("property1", "property2");

        // Act
        aggregate.UpdateStatus(YourStatus.Approved);

        // Assert
        aggregate.Status.Should().Be(YourStatus.Approved);
    }
}
```

### Integration Tests
```csharp
public class YourEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task CreateYour_WithValidRequest_ShouldReturnCreated()
    {
        // Arrange
        var request = new CreateYourRequest("property1", "property2");

        // Act
        var response = await _client.PostAsJsonAsync("/api/yours", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

## 📊 Database Schema

<!-- Include SQL schema or EF Core migration preview -->
```sql
CREATE TABLE yourschema.YourAggregates (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    AggregateNumber NVARCHAR(50) UNIQUE NOT NULL,
    Status NVARCHAR(50) NOT NULL,
    -- Add other fields
    CreatedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    UpdatedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy UNIQUEIDENTIFIER NOT NULL,
    RowVersion ROWVERSION NOT NULL
);

CREATE INDEX IX_YourAggregate_Status ON yourschema.YourAggregates(Status);
```

## 📚 References

- Module documentation: `docs/data-model/XX-module-name.md`
- WBS reference: Task [MODULE-XXX] in `docs/data-model/20-implementation-wbs.md`
- Design patterns: [Link to pattern documentation]

## 📌 Notes

<!-- Any additional context, warnings, or considerations -->
- **Performance**: Consider adding caching for frequently accessed data
- **Security**: Ensure proper authorization checks
- **Events**: Remember to raise domain events for important state changes

---

**Epic**: #epic-issue-number
**Milestone**: Sprint X - [Phase Name]
**Assignee Suggestion**: Backend Developer X
**Related Modules**: List any related modules
