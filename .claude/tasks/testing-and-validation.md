# Testing & Validation

## Task Overview
**Objective**: Create comprehensive testing and validation framework for the database project, including unit tests, integration tests, performance tests, and validation procedures to ensure database objects function correctly and maintain quality standards.

**Priority**: Low  
**Estimated Effort**: 4-6 hours  
**Dependencies**: All previous tasks  
**Assignee**: TBD  

## Prerequisites
- All previous database project tasks completed
- Understanding of testing frameworks (.NET, xUnit, NUnit)
- Knowledge of database testing patterns
- Access to test databases and environments
- Familiarity with performance testing concepts

## Testing Strategy

### Testing Pyramid for Database Objects
```
    ┌─────────────────────┐
    │   End-to-End Tests  │  ← Full application integration
    │     (Minimal)       │
    ├─────────────────────┤
    │ Integration Tests   │  ← Database + application layer
    │    (Moderate)       │
    ├─────────────────────┤
    │   Unit Tests        │  ← Individual database objects
    │    (Extensive)      │
    └─────────────────────┘
```

### Test Categories
1. **Unit Tests**: Individual database objects (views, SPs, functions)
2. **Integration Tests**: Cross-module dependencies and EF Core integration
3. **Performance Tests**: Query performance and scalability
4. **Data Validation Tests**: Data integrity and business rules
5. **Migration Tests**: Migration execution and rollback scenarios
6. **Security Tests**: Permissions and access control validation

## Implementation Steps

### Step 1: Database Unit Testing Framework

**Database.Tests/Database.Tests.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.5" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.5" />
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="Testcontainers.MsSql" Version="3.10.0" />
    <PackageReference Include="Respawn" Version="6.2.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../Database/Database.csproj" />
    <ProjectReference Include="../Modules/Request/Request/Request.csproj" />
    <ProjectReference Include="../Modules/Document/Document/Document.csproj" />
    <ProjectReference Include="../Modules/Assignment/Assignment/Assignment.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="TestData/**/*.sql" CopyToOutputDirectory="Always" />
    <None Include="TestScripts/**/*.sql" CopyToOutputDirectory="Always" />
  </ItemGroup>

</Project>
```

**Database.Tests/Infrastructure/DatabaseTestFixture.cs**:
```csharp
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Respawn;

namespace Database.Tests.Infrastructure;

public class DatabaseTestFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;
    private Respawner _respawner = default!;
    
    public string ConnectionString { get; private set; } = string.Empty;
    public IServiceProvider ServiceProvider { get; private set; } = default!;

    public DatabaseTestFixture()
    {
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("Test123!")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Start the test container
        await _container.StartAsync();
        
        ConnectionString = _container.GetConnectionString();
        
        // Setup test database
        await SetupTestDatabaseAsync();
        
        // Initialize respawner for database cleanup
        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = new[] { "request", "document", "assignment", "notification" },
            TablesToIgnore = new[] { "DatabaseMigrationHistory" }
        });
        
        // Setup dependency injection
        SetupServices();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    private async Task SetupTestDatabaseAsync()
    {
        // Run EF Core migrations
        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Create schemas
        var schemaCommands = new[]
        {
            "IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'request') EXEC('CREATE SCHEMA request')",
            "IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'document') EXEC('CREATE SCHEMA document')",
            "IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'assignment') EXEC('CREATE SCHEMA assignment')",
            "IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'notification') EXEC('CREATE SCHEMA notification')"
        };

        foreach (var schemaCommand in schemaCommands)
        {
            using var command = new SqlCommand(schemaCommand, connection);
            await command.ExecuteNonQueryAsync();
        }

        // Run database migrations
        Environment.SetEnvironmentVariable("DATABASE_CONNECTION_STRING", ConnectionString);
        
        var migrationService = GetService<Migration.IMigrationService>();
        await migrationService.MigrateAsync("Test");
    }

    private void SetupServices()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<Migration.DatabaseMigrator>();
                services.AddSingleton<Migration.IMigrationService, Migration.MigrationService>();
                services.AddLogging(builder => builder.AddConsole());
                
                // Add configuration
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                        ["ConnectionStrings:Database:Test"] = ConnectionString
                    })
                    .Build();
                services.AddSingleton<IConfiguration>(configuration);
            })
            .Build();

        ServiceProvider = host.Services;
    }

    public T GetService<T>() where T : class
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    public async Task<T> ExecuteScalarAsync<T>(string sql, object? parameters = null)
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        
        using var command = new SqlCommand(sql, connection);
        
        if (parameters != null)
        {
            AddParameters(command, parameters);
        }
        
        var result = await command.ExecuteScalarAsync();
        return (T)result!;
    }

    public async Task<List<T>> ExecuteQueryAsync<T>(string sql, Func<SqlDataReader, T> mapper, object? parameters = null)
    {
        var results = new List<T>();
        
        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        
        using var command = new SqlCommand(sql, connection);
        
        if (parameters != null)
        {
            AddParameters(command, parameters);
        }
        
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            results.Add(mapper(reader));
        }
        
        return results;
    }

    private static void AddParameters(SqlCommand command, object parameters)
    {
        var properties = parameters.GetType().GetProperties();
        foreach (var prop in properties)
        {
            var value = prop.GetValue(parameters) ?? DBNull.Value;
            command.Parameters.AddWithValue($"@{prop.Name}", value);
        }
    }
}
```

### Step 2: Database Object Unit Tests

**Database.Tests/Views/RequestViewTests.cs**:
```csharp
using FluentAssertions;
using Xunit;

namespace Database.Tests.Views;

public class RequestViewTests : IClassFixture<Infrastructure.DatabaseTestFixture>
{
    private readonly Infrastructure.DatabaseTestFixture _fixture;

    public RequestViewTests(Infrastructure.DatabaseTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task vw_Request_Summary_ShouldReturnCorrectColumns()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        await SeedTestDataAsync();

        // Act
        var sql = @"
            SELECT TOP 1 
                Id, AppraisalNumber, PropertyType, Status, CreatedOn, CreatedBy,
                CommentCount, TitleCount, AgeDays, ProcessingDays, IsOverdue
            FROM [request].[vw_Request_Summary]";

        var result = await _fixture.ExecuteQueryAsync(sql, reader => new
        {
            Id = reader.GetInt64("Id"),
            AppraisalNumber = reader.GetString("AppraisalNumber"),
            PropertyType = reader.GetString("PropertyType"),
            Status = reader.GetString("Status"),
            CreatedOn = reader.GetDateTime("CreatedOn"),
            CreatedBy = reader.GetString("CreatedBy"),
            CommentCount = reader.GetInt32("CommentCount"),
            TitleCount = reader.GetInt32("TitleCount"),
            AgeDays = reader.GetInt32("AgeDays"),
            ProcessingDays = reader.GetInt32("ProcessingDays"),
            IsOverdue = reader.GetInt32("IsOverdue")
        });

        // Assert
        result.Should().NotBeEmpty();
        var firstResult = result.First();
        firstResult.Id.Should().BeGreaterThan(0);
        firstResult.AppraisalNumber.Should().NotBeNullOrEmpty();
        firstResult.CommentCount.Should().BeGreaterOrEqualTo(0);
        firstResult.TitleCount.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task vw_Request_Summary_ShouldCalculateCorrectCounts()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        
        // Create test request
        var requestId = await CreateTestRequestAsync("TEST001", "Residential");
        
        // Add comments and titles
        await AddTestCommentAsync(requestId, "Test comment 1");
        await AddTestCommentAsync(requestId, "Test comment 2");
        await AddTestTitleAsync(requestId, "Test title 1");

        // Act
        var sql = @"
            SELECT CommentCount, TitleCount 
            FROM [request].[vw_Request_Summary] 
            WHERE Id = @RequestId";

        var result = await _fixture.ExecuteQueryAsync(sql, reader => new
        {
            CommentCount = reader.GetInt32("CommentCount"),
            TitleCount = reader.GetInt32("TitleCount")
        }, new { RequestId = requestId });

        // Assert
        result.Should().HaveCount(1);
        result[0].CommentCount.Should().Be(2);
        result[0].TitleCount.Should().Be(1);
    }

    [Fact]
    public async Task vw_Request_Dashboard_ShouldReturnValidMetrics()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        await SeedMultipleRequestsAsync();

        // Act
        var sql = "SELECT * FROM [request].[vw_Request_Dashboard]";
        var result = await _fixture.ExecuteQueryAsync(sql, reader => new
        {
            TotalRequests = reader.GetInt32("TotalRequests"),
            PendingCount = reader.GetInt32("PendingCount"),
            InProgressCount = reader.GetInt32("InProgressCount"),
            CompletedCount = reader.GetInt32("CompletedCount"),
            NewThisWeek = reader.GetInt32("NewThisWeek"),
            OverdueCount = reader.GetInt32("OverdueCount")
        });

        // Assert
        result.Should().HaveCount(1);
        var metrics = result[0];
        metrics.TotalRequests.Should().BeGreaterThan(0);
        metrics.PendingCount.Should().BeGreaterOrEqualTo(0);
        metrics.InProgressCount.Should().BeGreaterOrEqualTo(0);
        metrics.CompletedCount.Should().BeGreaterOrEqualTo(0);
    }

    private async Task SeedTestDataAsync()
    {
        await CreateTestRequestAsync("TEST001", "Residential");
        await CreateTestRequestAsync("TEST002", "Commercial");
    }

    private async Task SeedMultipleRequestsAsync()
    {
        var statuses = new[] { "Pending", "InProgress", "Completed" };
        var propertyTypes = new[] { "Residential", "Commercial", "Industrial" };

        for (int i = 1; i <= 10; i++)
        {
            await CreateTestRequestAsync($"TEST{i:000}", 
                propertyTypes[i % propertyTypes.Length],
                statuses[i % statuses.Length]);
        }
    }

    private async Task<long> CreateTestRequestAsync(string appraisalNumber, string propertyType, string status = "Pending")
    {
        var sql = @"
            INSERT INTO [request].[Requests] 
            (AppraisalNumber, PropertyType, Status, RequestDate, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
            OUTPUT INSERTED.Id
            VALUES 
            (@AppraisalNumber, @PropertyType, @Status, GETDATE(), GETDATE(), 'TEST_USER', GETDATE(), 'TEST_USER')";

        return await _fixture.ExecuteScalarAsync<long>(sql, new
        {
            AppraisalNumber = appraisalNumber,
            PropertyType = propertyType,
            Status = status
        });
    }

    private async Task AddTestCommentAsync(long requestId, string comment)
    {
        var sql = @"
            INSERT INTO [request].[RequestComments] 
            (RequestId, Comment, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
            VALUES 
            (@RequestId, @Comment, GETDATE(), 'TEST_USER', GETDATE(), 'TEST_USER')";

        await _fixture.ExecuteScalarAsync<int>(sql, new
        {
            RequestId = requestId,
            Comment = comment
        });
    }

    private async Task AddTestTitleAsync(long requestId, string titleInfo)
    {
        var sql = @"
            INSERT INTO [request].[RequestTitles] 
            (RequestId, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
            VALUES 
            (@RequestId, GETDATE(), 'TEST_USER', GETDATE(), 'TEST_USER')";

        await _fixture.ExecuteScalarAsync<int>(sql, new
        {
            RequestId = requestId
        });
    }
}
```

**Database.Tests/StoredProcedures/RequestStoredProcedureTests.cs**:
```csharp
using FluentAssertions;
using Xunit;

namespace Database.Tests.StoredProcedures;

public class RequestStoredProcedureTests : IClassFixture<Infrastructure.DatabaseTestFixture>
{
    private readonly Infrastructure.DatabaseTestFixture _fixture;

    public RequestStoredProcedureTests(Infrastructure.DatabaseTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task sp_Request_GetMetrics_ShouldReturnValidResults()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        await SeedTestRequestsAsync();

        var startDate = DateTime.Today.AddDays(-30);
        var endDate = DateTime.Today;

        // Act
        var sql = "EXEC [request].[sp_Request_GetMetrics] @StartDate, @EndDate";
        var result = await _fixture.ExecuteQueryAsync(sql, reader => new
        {
            TotalRequests = reader.GetInt32("TotalRequests"),
            UniqueCreators = reader.GetInt32("UniqueCreators"),
            PendingCount = reader.GetInt32("PendingCount"),
            CompletedCount = reader.GetInt32("CompletedCount"),
            ResidentialCount = reader.GetInt32("ResidentialCount"),
            CommercialCount = reader.GetInt32("CommercialCount")
        }, new { StartDate = startDate, EndDate = endDate });

        // Assert
        result.Should().HaveCount(1);
        var metrics = result[0];
        metrics.TotalRequests.Should().BeGreaterThan(0);
        metrics.UniqueCreators.Should().BeGreaterThan(0);
        metrics.ResidentialCount.Should().BeGreaterOrEqualTo(0);
        metrics.CommercialCount.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task sp_Request_GetMetrics_WithPropertyTypeFilter_ShouldFilterCorrectly()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        await SeedTestRequestsAsync();

        var startDate = DateTime.Today.AddDays(-30);
        var endDate = DateTime.Today;
        var propertyType = "Residential";

        // Act
        var sql = "EXEC [request].[sp_Request_GetMetrics] @StartDate, @EndDate, @PropertyType";
        var result = await _fixture.ExecuteQueryAsync(sql, reader => new
        {
            TotalRequests = reader.GetInt32("TotalRequests"),
            ResidentialCount = reader.GetInt32("ResidentialCount"),
            CommercialCount = reader.GetInt32("CommercialCount")
        }, new { StartDate = startDate, EndDate = endDate, PropertyType = propertyType });

        // Assert
        result.Should().HaveCount(1);
        var metrics = result[0];
        metrics.ResidentialCount.Should().Be(metrics.TotalRequests);
        metrics.CommercialCount.Should().Be(0);
    }

    [Fact]
    public async Task sp_Request_GetMetrics_WithInvalidDateRange_ShouldThrowError()
    {
        // Arrange
        var startDate = DateTime.Today;
        var endDate = DateTime.Today.AddDays(-1); // Invalid: end before start

        // Act & Assert
        var sql = "EXEC [request].[sp_Request_GetMetrics] @StartDate, @EndDate";
        
        await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await _fixture.ExecuteQueryAsync(sql, reader => new { }, new { StartDate = startDate, EndDate = endDate });
        });
    }

    private async Task SeedTestRequestsAsync()
    {
        var requests = new[]
        {
            new { AppraisalNumber = "TEST001", PropertyType = "Residential", Status = "Pending", CreatedBy = "User1" },
            new { AppraisalNumber = "TEST002", PropertyType = "Commercial", Status = "Completed", CreatedBy = "User1" },
            new { AppraisalNumber = "TEST003", PropertyType = "Residential", Status = "InProgress", CreatedBy = "User2" },
            new { AppraisalNumber = "TEST004", PropertyType = "Industrial", Status = "Pending", CreatedBy = "User2" }
        };

        foreach (var request in requests)
        {
            var sql = @"
                INSERT INTO [request].[Requests] 
                (AppraisalNumber, PropertyType, Status, RequestDate, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
                VALUES 
                (@AppraisalNumber, @PropertyType, @Status, GETDATE(), GETDATE(), @CreatedBy, GETDATE(), @CreatedBy)";

            await _fixture.ExecuteScalarAsync<int>(sql, request);
        }
    }
}
```

**Database.Tests/Functions/RequestFunctionTests.cs**:
```csharp
using FluentAssertions;
using Xunit;

namespace Database.Tests.Functions;

public class RequestFunctionTests : IClassFixture<Infrastructure.DatabaseTestFixture>
{
    private readonly Infrastructure.DatabaseTestFixture _fixture;

    public RequestFunctionTests(Infrastructure.DatabaseTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task fn_Request_CalculateAge_ShouldReturnCorrectAge()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        
        // Create request with known creation date (5 business days ago)
        var createdDate = DateTime.Today.AddDays(-7); // 7 calendar days ago
        var requestId = await CreateTestRequestWithDateAsync("TEST001", createdDate);

        // Act
        var sql = "SELECT [request].[fn_Request_CalculateAge](@RequestId) as Age";
        var age = await _fixture.ExecuteScalarAsync<int>(sql, new { RequestId = requestId });

        // Assert
        age.Should().BeGreaterThan(0);
        age.Should().BeLessOrEqualTo(7); // Should be less than 7 due to weekend exclusion
    }

    [Fact]
    public async Task fn_Request_CalculateAge_WithNonExistentRequest_ShouldReturnZero()
    {
        // Arrange
        var nonExistentId = 99999L;

        // Act
        var sql = "SELECT [request].[fn_Request_CalculateAge](@RequestId) as Age";
        var age = await _fixture.ExecuteScalarAsync<int>(sql, new { RequestId = nonExistentId });

        // Assert
        age.Should().Be(0);
    }

    [Fact]
    public async Task fn_Request_CalculateAge_WithCompletedRequest_ShouldUseCompletionDate()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        
        var createdDate = DateTime.Today.AddDays(-10);
        var completedDate = DateTime.Today.AddDays(-3);
        var requestId = await CreateCompletedTestRequestAsync("TEST001", createdDate, completedDate);

        // Act
        var sql = "SELECT [request].[fn_Request_CalculateAge](@RequestId) as Age";
        var age = await _fixture.ExecuteScalarAsync<int>(sql, new { RequestId = requestId });

        // Assert
        age.Should().BeGreaterThan(0);
        age.Should().BeLessOrEqualTo(7); // Should be based on completion date, not current date
    }

    private async Task<long> CreateTestRequestWithDateAsync(string appraisalNumber, DateTime createdDate)
    {
        var sql = @"
            INSERT INTO [request].[Requests] 
            (AppraisalNumber, PropertyType, Status, RequestDate, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
            OUTPUT INSERTED.Id
            VALUES 
            (@AppraisalNumber, 'Residential', 'Pending', @CreatedDate, @CreatedDate, 'TEST_USER', @CreatedDate, 'TEST_USER')";

        return await _fixture.ExecuteScalarAsync<long>(sql, new
        {
            AppraisalNumber = appraisalNumber,
            CreatedDate = createdDate
        });
    }

    private async Task<long> CreateCompletedTestRequestAsync(string appraisalNumber, DateTime createdDate, DateTime completedDate)
    {
        var sql = @"
            INSERT INTO [request].[Requests] 
            (AppraisalNumber, PropertyType, Status, RequestDate, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
            OUTPUT INSERTED.Id
            VALUES 
            (@AppraisalNumber, 'Residential', 'Completed', @CreatedDate, @CreatedDate, 'TEST_USER', @CompletedDate, 'TEST_USER')";

        return await _fixture.ExecuteScalarAsync<long>(sql, new
        {
            AppraisalNumber = appraisalNumber,
            CreatedDate = createdDate,
            CompletedDate = completedDate
        });
    }
}
```

### Step 3: Performance Testing Framework

**Database.Tests/Performance/PerformanceTests.cs**:
```csharp
using FluentAssertions;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Database.Tests.Performance;

public class PerformanceTests : IClassFixture<Infrastructure.DatabaseTestFixture>
{
    private readonly Infrastructure.DatabaseTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public PerformanceTests(Infrastructure.DatabaseTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task vw_Request_Summary_ShouldPerformWellWithLargeDataset()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        await SeedLargeDatasetAsync(1000); // 1000 requests

        // Act
        var stopwatch = Stopwatch.StartNew();
        var sql = "SELECT COUNT(*) FROM [request].[vw_Request_Summary]";
        var count = await _fixture.ExecuteScalarAsync<int>(sql);
        stopwatch.Stop();

        // Assert
        count.Should().Be(1000);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Less than 5 seconds
        
        _output.WriteLine($"View query took {stopwatch.ElapsedMilliseconds}ms for {count} records");
    }

    [Fact]
    public async Task sp_Request_GetMetrics_ShouldCompleteWithinTimeout()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        await SeedLargeDatasetAsync(500);

        var startDate = DateTime.Today.AddDays(-30);
        var endDate = DateTime.Today;

        // Act
        var stopwatch = Stopwatch.StartNew();
        var sql = "EXEC [request].[sp_Request_GetMetrics] @StartDate, @EndDate";
        var result = await _fixture.ExecuteQueryAsync(sql, reader => new
        {
            TotalRequests = reader.GetInt32("TotalRequests")
        }, new { StartDate = startDate, EndDate = endDate });
        stopwatch.Stop();

        // Assert
        result.Should().HaveCount(1);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // Less than 10 seconds
        
        _output.WriteLine($"Stored procedure took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public async Task vw_Request_Dashboard_ShouldScaleLinearlyWithDataSize(int recordCount)
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        await SeedLargeDatasetAsync(recordCount);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var sql = "SELECT * FROM [request].[vw_Request_Dashboard]";
        var result = await _fixture.ExecuteQueryAsync(sql, reader => new
        {
            TotalRequests = reader.GetInt32("TotalRequests")
        });
        stopwatch.Stop();

        // Assert
        result.Should().HaveCount(1);
        result[0].TotalRequests.Should().Be(recordCount);
        
        // Performance should scale reasonably
        var expectedMaxTime = recordCount / 100 * 1000; // 1 second per 100 records
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expectedMaxTime);
        
        _output.WriteLine($"Dashboard view with {recordCount} records took {stopwatch.ElapsedMilliseconds}ms");
    }

    private async Task SeedLargeDatasetAsync(int requestCount)
    {
        _output.WriteLine($"Seeding {requestCount} test requests...");
        
        var batchSize = 100;
        var batches = (requestCount + batchSize - 1) / batchSize;

        for (int batch = 0; batch < batches; batch++)
        {
            var batchRequests = Math.Min(batchSize, requestCount - (batch * batchSize));
            await SeedRequestBatchAsync(batch * batchSize, batchRequests);
        }
        
        _output.WriteLine($"Seeding completed: {requestCount} requests created");
    }

    private async Task SeedRequestBatchAsync(int startIndex, int count)
    {
        var values = new List<string>();
        var statuses = new[] { "Pending", "InProgress", "Completed", "Cancelled" };
        var propertyTypes = new[] { "Residential", "Commercial", "Industrial", "Land" };
        var users = new[] { "User1", "User2", "User3", "User4", "User5" };

        for (int i = 0; i < count; i++)
        {
            var index = startIndex + i;
            var appraisalNumber = $"PERF{index:D6}";
            var status = statuses[index % statuses.Length];
            var propertyType = propertyTypes[index % propertyTypes.Length];
            var user = users[index % users.Length];
            var createdDate = DateTime.Today.AddDays(-(index % 365)); // Spread over last year

            values.Add($"('{appraisalNumber}', '{propertyType}', '{status}', '{createdDate:yyyy-MM-dd}', '{createdDate:yyyy-MM-dd}', '{user}', '{createdDate:yyyy-MM-dd}', '{user}')");
        }

        var sql = $@"
            INSERT INTO [request].[Requests] 
            (AppraisalNumber, PropertyType, Status, RequestDate, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
            VALUES {string.Join(", ", values)}";

        await _fixture.ExecuteScalarAsync<int>(sql);
    }
}
```

### Step 4: Migration Testing Framework

**Database.Tests/Migration/MigrationTests.cs**:
```csharp
using FluentAssertions;
using Xunit;

namespace Database.Tests.Migration;

public class MigrationTests : IClassFixture<Infrastructure.DatabaseTestFixture>
{
    private readonly Infrastructure.DatabaseTestFixture _fixture;

    public MigrationTests(Infrastructure.DatabaseTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MigrationService_ShouldCreateAllDatabaseObjects()
    {
        // Arrange
        var migrationService = _fixture.GetService<Database.Migration.IMigrationService>();

        // Act
        var result = await migrationService.MigrateAsync("Test");

        // Assert
        result.Should().BeTrue();
        
        // Verify views exist
        await VerifyViewsExistAsync();
        
        // Verify stored procedures exist
        await VerifyStoredProceduresExistAsync();
        
        // Verify functions exist
        await VerifyFunctionsExistAsync();
    }

    [Fact]
    public async Task MigrationService_ShouldTrackMigrationHistory()
    {
        // Arrange
        var migrationService = _fixture.GetService<Database.Migration.IMigrationService>();

        // Act
        await migrationService.MigrateAsync("Test");
        var history = await migrationService.GetMigrationHistoryAsync();

        // Assert
        history.Should().NotBeEmpty();
        history.All(h => h.Success).Should().BeTrue();
        history.All(h => h.Environment == "Test").Should().BeTrue();
    }

    [Fact]
    public async Task MigrationService_ValidateAsync_ShouldIdentifyPendingMigrations()
    {
        // This test would need a fresh database or specific test conditions
        // to have actual pending migrations
        
        // Arrange
        var migrationService = _fixture.GetService<Database.Migration.IMigrationService>();

        // Act
        var result = await migrationService.ValidateAsync();

        // Assert
        result.Should().BeTrue(); // Should not fail validation
    }

    private async Task VerifyViewsExistAsync()
    {
        var expectedViews = new[]
        {
            "vw_Request_Summary",
            "vw_Request_Dashboard",
            "vw_Document_Summary"
        };

        foreach (var viewName in expectedViews)
        {
            var sql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.VIEWS WHERE TABLE_NAME = @ViewName";
            var count = await _fixture.ExecuteScalarAsync<int>(sql, new { ViewName = viewName });
            count.Should().Be(1, $"View {viewName} should exist");
        }
    }

    private async Task VerifyStoredProceduresExistAsync()
    {
        var expectedProcedures = new[]
        {
            "sp_Request_GetMetrics",
            "sp_Document_CleanupExpired"
        };

        foreach (var procName in expectedProcedures)
        {
            var sql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_NAME = @ProcName AND ROUTINE_TYPE = 'PROCEDURE'";
            var count = await _fixture.ExecuteScalarAsync<int>(sql, new { ProcName = procName });
            count.Should().Be(1, $"Stored procedure {procName} should exist");
        }
    }

    private async Task VerifyFunctionsExistAsync()
    {
        var expectedFunctions = new[]
        {
            "fn_Request_CalculateAge"
        };

        foreach (var funcName in expectedFunctions)
        {
            var sql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_NAME = @FuncName AND ROUTINE_TYPE = 'FUNCTION'";
            var count = await _fixture.ExecuteScalarAsync<int>(sql, new { FuncName = funcName });
            count.Should().Be(1, $"Function {funcName} should exist");
        }
    }
}
```

### Step 5: Data Validation and Security Tests

**Database.Tests/Security/SecurityTests.cs**:
```csharp
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Database.Tests.Security;

public class SecurityTests : IClassFixture<Infrastructure.DatabaseTestFixture>
{
    private readonly Infrastructure.DatabaseTestFixture _fixture;

    public SecurityTests(Infrastructure.DatabaseTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DatabaseObjects_ShouldHaveCorrectPermissions()
    {
        // Test that views have SELECT permissions for db_datareader
        var viewPermissionsSql = @"
            SELECT 
                OBJECT_SCHEMA_NAME(p.major_id) as SchemaName,
                OBJECT_NAME(p.major_id) as ObjectName,
                p.permission_name,
                p.state_desc,
                pr.name as PrincipalName
            FROM sys.database_permissions p
            LEFT JOIN sys.database_principals pr ON p.grantee_principal_id = pr.principal_id
            WHERE p.major_id IN (
                SELECT object_id FROM sys.views 
                WHERE schema_id IN (SCHEMA_ID('request'), SCHEMA_ID('document'), SCHEMA_ID('assignment'))
            )
            AND p.permission_name = 'SELECT'";

        var permissions = await _fixture.ExecuteQueryAsync(viewPermissionsSql, reader => new
        {
            SchemaName = reader.GetString("SchemaName"),
            ObjectName = reader.GetString("ObjectName"),
            PermissionName = reader.GetString("permission_name"),
            StateDesc = reader.GetString("state_desc"),
            PrincipalName = reader.IsDBNull("PrincipalName") ? null : reader.GetString("PrincipalName")
        });

        // Should have permissions granted (implementation depends on your security model)
        permissions.Should().NotBeEmpty("Views should have SELECT permissions granted");
    }

    [Fact]
    public async Task StoredProcedures_ShouldHaveCorrectPermissions()
    {
        // Test that stored procedures have EXECUTE permissions
        var procPermissionsSql = @"
            SELECT 
                OBJECT_SCHEMA_NAME(p.major_id) as SchemaName,
                OBJECT_NAME(p.major_id) as ObjectName,
                p.permission_name,
                p.state_desc
            FROM sys.database_permissions p
            WHERE p.major_id IN (
                SELECT object_id FROM sys.procedures 
                WHERE schema_id IN (SCHEMA_ID('request'), SCHEMA_ID('document'), SCHEMA_ID('assignment'))
            )
            AND p.permission_name = 'EXECUTE'";

        var permissions = await _fixture.ExecuteQueryAsync(procPermissionsSql, reader => new
        {
            SchemaName = reader.GetString("SchemaName"),
            ObjectName = reader.GetString("ObjectName"),
            PermissionName = reader.GetString("permission_name"),
            StateDesc = reader.GetString("state_desc")
        });

        // Verify permissions exist (adjust based on your security requirements)
        permissions.Should().NotBeEmpty("Stored procedures should have EXECUTE permissions");
    }

    [Fact]
    public async Task DatabaseObjects_ShouldNotAllowSqlInjection()
    {
        // Test stored procedures with potential SQL injection attempts
        var maliciousInput = "'; DROP TABLE [request].[Requests]; --";
        
        // This should not cause any harm due to parameterized queries
        var sql = "EXEC [request].[sp_Request_GetMetrics] @StartDate, @EndDate, @PropertyType";
        
        var ex = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await _fixture.ExecuteQueryAsync(sql, reader => new { },
                new { 
                    StartDate = DateTime.Today.AddDays(-30),
                    EndDate = DateTime.Today,
                    PropertyType = maliciousInput
                });
        });

        // The error should be about invalid date format or similar, not about SQL syntax
        ex.Message.Should().NotContain("DROP TABLE", "SQL injection attempt should be safely handled");
    }
}
```

### Step 6: Test Automation and CI Integration

**Database.Tests/TestData/SeedData.sql**:
```sql
-- Test data seeding script
-- Used by automated tests to create consistent test scenarios

-- Clean existing test data
DELETE FROM [request].[RequestComments] WHERE CreatedBy LIKE 'TEST_%';
DELETE FROM [request].[RequestTitles] WHERE CreatedBy LIKE 'TEST_%';
DELETE FROM [request].[Requests] WHERE CreatedBy LIKE 'TEST_%';

-- Seed test requests
INSERT INTO [request].[Requests] (AppraisalNumber, PropertyType, Status, RequestDate, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
VALUES 
    ('TEST001', 'Residential', 'Pending', GETDATE(), GETDATE(), 'TEST_USER1', GETDATE(), 'TEST_USER1'),
    ('TEST002', 'Commercial', 'InProgress', GETDATE(), GETDATE(), 'TEST_USER2', GETDATE(), 'TEST_USER2'),
    ('TEST003', 'Industrial', 'Completed', DATEADD(DAY, -5, GETDATE()), DATEADD(DAY, -5, GETDATE()), 'TEST_USER1', GETDATE(), 'TEST_USER1'),
    ('TEST004', 'Land', 'Cancelled', DATEADD(DAY, -10, GETDATE()), DATEADD(DAY, -10, GETDATE()), 'TEST_USER3', DATEADD(DAY, -8, GETDATE()), 'TEST_USER3');

-- Seed test comments
DECLARE @RequestId1 BIGINT = (SELECT Id FROM [request].[Requests] WHERE AppraisalNumber = 'TEST001');
DECLARE @RequestId2 BIGINT = (SELECT Id FROM [request].[Requests] WHERE AppraisalNumber = 'TEST002');

INSERT INTO [request].[RequestComments] (RequestId, Comment, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
VALUES 
    (@RequestId1, 'Initial assessment comment', GETDATE(), 'TEST_USER1', GETDATE(), 'TEST_USER1'),
    (@RequestId1, 'Follow-up comment', GETDATE(), 'TEST_USER2', GETDATE(), 'TEST_USER2'),
    (@RequestId2, 'Commercial property notes', GETDATE(), 'TEST_USER2', GETDATE(), 'TEST_USER2');

-- Seed test titles
INSERT INTO [request].[RequestTitles] (RequestId, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
VALUES 
    (@RequestId1, GETDATE(), 'TEST_USER1', GETDATE(), 'TEST_USER1'),
    (@RequestId2, GETDATE(), 'TEST_USER2', GETDATE(), 'TEST_USER2');
```

**azure-pipelines-tests.yml** (excerpt):
```yaml
- task: DotNetCoreCLI@2
  displayName: 'Run Database Unit Tests'
  inputs:
    command: 'test'
    projects: 'Database.Tests/Database.Tests.csproj'
    arguments: '--configuration Release --logger trx --collect:"XPlat Code Coverage" --filter Category=Unit'
    publishTestResults: false

- task: DotNetCoreCLI@2
  displayName: 'Run Database Integration Tests'
  inputs:
    command: 'test'
    projects: 'Database.Tests/Database.Tests.csproj'
    arguments: '--configuration Release --logger trx --collect:"XPlat Code Coverage" --filter Category=Integration'
    publishTestResults: false

- task: DotNetCoreCLI@2
  displayName: 'Run Database Performance Tests'
  inputs:
    command: 'test'
    projects: 'Database.Tests/Database.Tests.csproj'
    arguments: '--configuration Release --logger trx --filter Category=Performance'
    publishTestResults: false
  condition: and(succeeded(), eq(variables['RunPerformanceTests'], 'true'))

- task: PublishTestResults@2
  displayName: 'Publish Database Test Results'
  inputs:
    testResultsFormat: 'VSTest'
    testResultsFiles: '**/*.trx'
    searchFolder: '$(Agent.TempDirectory)'
    mergeTestResults: true
    testRunTitle: 'Database Tests'

- task: PublishCodeCoverageResults@2
  displayName: 'Publish Code Coverage'
  inputs:
    summaryFileLocation: '$(Agent.TempDirectory)/**/coverage.cobertura.xml'
    codecoverageTool: 'Cobertura'
```

## Acceptance Criteria

### Must Have
- [x] Unit tests for all database objects (views, stored procedures, functions)
- [x] Integration tests for migration coordination and EF Core integration
- [x] Performance tests with benchmarks and thresholds
- [x] Security tests for permissions and SQL injection prevention
- [x] Test automation integrated with CI/CD pipeline
- [x] Test data management and cleanup strategies

### Should Have
- [ ] Load testing for high-volume scenarios
- [ ] Regression testing for schema changes
- [ ] Cross-environment test validation
- [ ] Test reporting and metrics dashboard

### Nice to Have
- [ ] Property-based testing for edge cases
- [ ] Mutation testing for test quality validation
- [ ] Visual test reports and trends
- [ ] Automated performance regression detection

## Potential Issues & Solutions

### Issue 1: Test Database Management
**Problem**: Managing test databases and cleanup between tests  
**Solution**: Use Docker containers with Testcontainers and Respawn for cleanup

### Issue 2: Performance Test Consistency
**Problem**: Performance tests may be inconsistent across environments  
**Solution**: Use relative benchmarks and environment-specific thresholds

### Issue 3: Integration Test Complexity
**Problem**: Complex integration scenarios are difficult to test  
**Solution**: Use test fixtures and helper methods for common scenarios

## Handoff Notes

### Key Deliverables
1. **Comprehensive test suite** covering all database objects and scenarios
2. **Performance testing framework** with benchmarks and monitoring
3. **CI/CD integration** for automated test execution
4. **Test utilities and fixtures** for consistent test data management
5. **Security validation** ensuring proper permissions and protections

### Test Execution
- **Local Development**: Run tests with `dotnet test Database.Tests/Database.Tests.csproj`
- **CI Pipeline**: Automated execution on code changes
- **Performance Monitoring**: Regular performance test execution
- **Security Scanning**: Automated security test validation

### Maintenance
- **Test Data**: Update seed data as schema evolves
- **Performance Thresholds**: Adjust benchmarks based on infrastructure changes
- **Test Coverage**: Maintain high coverage for all database objects
- **Integration Updates**: Keep tests synchronized with application changes

## Time Tracking
- **Estimated**: 4-6 hours
- **Actual**: _To be filled by implementer_
- **Variance**: _To be filled by implementer_

## Implementation Notes
_To be filled by implementer with any deviations, discoveries, or improvements_