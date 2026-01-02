---
name: Testing Task
about: For unit tests, integration tests, and test infrastructure
title: '[TEST] '
labels: testing, quality
assignees: ''
---

## 🧪 Test Overview

**Task ID**: <!-- e.g., TEST-001 -->
**Test Type**: <!-- Unit / Integration / Load / Security -->
**Sprint**: <!-- Sprint 1, 2, 3, or 4 -->
**Estimated Time**: <!-- e.g., 5 hours -->
**Priority**: <!-- Critical, High, Medium, Low -->

## 📝 Description

<!-- Brief description of what needs to be tested -->

## ✅ Task Checklist

<!-- List all test scenarios with time estimates -->
- [ ] **Setup test infrastructure** - Configure test environment (Xh)
- [ ] **Write test cases** - Implement test scenarios (Xh)
- [ ] **Test happy paths** - All positive scenarios (Xh)
- [ ] **Test edge cases** - Boundary conditions (Xh)
- [ ] **Test error handling** - Negative scenarios (Xh)
- [ ] **Verify coverage** - Ensure ≥80% coverage (Xh)

**Total Estimated Time**: X hours

## 🎯 Acceptance Criteria

<!-- Clear definition of done -->
- [ ] All test scenarios implemented
- [ ] All tests passing
- [ ] Code coverage ≥80%
- [ ] No flaky tests
- [ ] Test documentation complete
- [ ] CI/CD integration working

## 🔗 Dependencies

<!-- Issues that must be completed before this one -->
- Depends on: #issue-number (implementation must be complete)
- Blocks: #issue-number

## 💡 Testing Strategy

### Unit Tests

**What to test:**
- Domain logic
- Validators
- Command/Query handlers
- Value objects
- Business rules

**Example:**
```csharp
public class RequestAggregateTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateRequest()
    {
        // Arrange
        var requestDetail = RequestDetail.Create(
            loanAmount: 1000000m,
            propertyType: PropertyType.LandAndBuilding
        );

        // Act
        var request = Request.Create(requestDetail, userId: Guid.NewGuid());

        // Assert
        request.Should().NotBeNull();
        request.RequestNumber.Should().NotBeNullOrEmpty();
        request.Status.Should().Be(RequestStatus.Draft);
        request.Detail.Should().Be(requestDetail);
    }

    [Fact]
    public void Submit_WhenInDraftStatus_ShouldChangeStatusToSubmitted()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        request.Submit();

        // Assert
        request.Status.Should().Be(RequestStatus.Submitted);
    }

    [Fact]
    public void Submit_WhenAlreadySubmitted_ShouldThrowException()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Submit();

        // Act
        Action act = () => request.Submit();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot submit request that is already submitted");
    }

    [Theory]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    [InlineData(RequestStatus.Cancelled)]
    public void Submit_WhenInInvalidStatus_ShouldThrowException(RequestStatus status)
    {
        // Arrange
        var request = CreateRequestWithStatus(status);

        // Act
        Action act = () => request.Submit();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    private Request CreateValidRequest()
    {
        var detail = RequestDetail.Create(1000000m, PropertyType.LandAndBuilding);
        return Request.Create(detail, Guid.NewGuid());
    }
}
```

### Integration Tests

**What to test:**
- API endpoints (end-to-end)
- Database operations
- Event publishing/consuming
- External service integration

**Example:**
```csharp
public class RequestEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public RequestEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateRequest_WithValidData_ShouldReturn201Created()
    {
        // Arrange
        var request = new CreateRequestRequest(
            LoanAmount: 1000000m,
            PropertyType: "LandAndBuilding"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/requests", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateRequestResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRequest_WithValidId_ShouldReturn200Ok()
    {
        // Arrange
        var requestId = await CreateTestRequest();

        // Act
        var response = await _client.GetAsync($"/api/requests/{requestId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RequestResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(requestId);
    }

    [Fact]
    public async Task SubmitRequest_WhenValid_ShouldReturn200AndPublishEvent()
    {
        // Arrange
        var requestId = await CreateTestRequest();

        // Act
        var response = await _client.PostAsync($"/api/requests/{requestId}/submit", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify event was published (using test harness)
        var published = await GetPublishedEvents<RequestCreatedEvent>();
        published.Should().ContainSingle(e => e.RequestId == requestId);
    }

    [Fact]
    public async Task CreateRequest_WithInvalidData_ShouldReturn400BadRequest()
    {
        // Arrange
        var request = new CreateRequestRequest(
            LoanAmount: -1000m, // Invalid: negative amount
            PropertyType: "InvalidType"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/requests", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> CreateTestRequest()
    {
        var request = new CreateRequestRequest(1000000m, "LandAndBuilding");
        var response = await _client.PostAsJsonAsync("/api/requests", request);
        var result = await response.Content.ReadFromJsonAsync<CreateRequestResponse>();
        return result!.Id;
    }
}
```

### Validator Tests

**Example:**
```csharp
public class CreateRequestCommandValidatorTests
{
    private readonly CreateRequestCommandValidator _validator;

    public CreateRequestCommandValidatorTests()
    {
        _validator = new CreateRequestCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        // Arrange
        var command = new CreateRequestCommand(
            LoanAmount: 1000000m,
            PropertyType: "LandAndBuilding"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public void Validate_WithInvalidLoanAmount_ShouldFail(decimal amount)
    {
        // Arrange
        var command = new CreateRequestCommand(amount, "LandAndBuilding");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LoanAmount");
    }
}
```

### Repository Tests (Integration)

**Example:**
```csharp
public class RequestRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly RequestRepository _repository;
    private readonly RequestDbContext _context;

    [Fact]
    public async Task AddAsync_ShouldPersistRequest()
    {
        // Arrange
        var request = CreateTestRequest();

        // Act
        await _repository.AddAsync(request);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _repository.GetByIdAsync(request.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(request.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(invalidId);

        // Assert
        result.Should().BeNull();
    }
}
```

## 🧪 Test Coverage Goals

| Component | Target Coverage |
|-----------|----------------|
| Domain Entities | ≥90% |
| Command Handlers | ≥85% |
| Query Handlers | ≥85% |
| Validators | 100% |
| API Endpoints | ≥80% |
| Overall | ≥80% |

## 🔍 Test Scenarios

<!-- List all test scenarios to cover -->

### Happy Path Scenarios
1. [ ] Scenario 1 description
2. [ ] Scenario 2 description
3. [ ] Scenario 3 description

### Edge Cases
1. [ ] Edge case 1
2. [ ] Edge case 2
3. [ ] Edge case 3

### Error Handling
1. [ ] Error scenario 1
2. [ ] Error scenario 2
3. [ ] Error scenario 3

## 🛠️ Test Infrastructure

### Setup Required
```csharp
// Database fixture for integration tests
public class DatabaseFixture : IDisposable
{
    public RequestDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RequestDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new RequestDbContext(options);
    }

    public void Dispose()
    {
        // Cleanup
    }
}

// WebApplicationFactory for API tests
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real services with test doubles
            services.RemoveAll<DbContext>();
            services.AddDbContext<RequestDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDb");
            });
        });
    }
}
```

## 📊 Test Results

<!-- To be filled after test execution -->

- [ ] All tests passing
- [ ] Coverage report generated
- [ ] No test warnings
- [ ] Performance acceptable (test execution time)

## 📚 References

- Testing framework: xUnit
- Assertion library: FluentAssertions
- Mocking library: NSubstitute
- Test data: AutoFixture / Bogus

## 📌 Notes

<!-- Any additional context, warnings, or considerations -->
- **Flaky Tests**: Ensure tests are deterministic and repeatable
- **Test Data**: Use meaningful test data that reflects real scenarios
- **Isolation**: Each test should be independent
- **Performance**: Keep tests fast (<100ms for unit tests)

---

**Epic**: #epic-issue-number
**Milestone**: Sprint X - [Phase Name]
**Assignee Suggestion**: QA Lead / Backend Developer X
**Module Under Test**: [Module Name]
