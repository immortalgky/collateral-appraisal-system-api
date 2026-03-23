using System.Data;
using Dapper;
using FluentAssertions;
using NSubstitute;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;
using Workflow.Tasks.Features.GetMyTasks;
using Workflow.Tasks.Features.GetTasks;
using Xunit;

namespace Workflow.Tests.Tasks.Features.GetMyTasks;

public class GetMyTasksQueryHandlerTests
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDbConnection _dbConnection;

    public GetMyTasksQueryHandlerTests()
    {
        _connectionFactory = Substitute.For<ISqlConnectionFactory>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _dbConnection = Substitute.For<IDbConnection>();

        _connectionFactory.GetOpenConnection().Returns(_dbConnection);
    }

    [Fact]
    public void Constructor_ShouldAcceptDependencies()
    {
        // Act
        var handler = new GetMyTasksQueryHandler(_connectionFactory, _currentUserService);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldUseCurrentUserServiceUsername()
    {
        // Arrange
        _currentUserService.Username.Returns("john.doe");
        var handler = new GetMyTasksQueryHandler(_connectionFactory, _currentUserService);
        var query = new GetMyTasksQuery(new PaginationRequest(0, 10));

        // Act & Assert — the handler will call GetOpenConnection() and execute SQL via Dapper.
        // Since we're mocking IDbConnection, the Dapper call will fail with a connection-related
        // exception (not a real DB), but we can verify the handler accessed Username.
        try
        {
            await handler.Handle(query, CancellationToken.None);
        }
        catch
        {
            // Expected — mock IDbConnection doesn't support Dapper operations
        }

        // Verify it accessed the current user's username
        _ = _currentUserService.Received(1).Username;
        _connectionFactory.Received(1).GetOpenConnection();
    }

    [Fact]
    public void GetMyTasksFilterRequest_ShouldNotHaveAssigneeUserId()
    {
        // Verify the filter request doesn't expose AssigneeUserId
        var filter = new GetMyTasksFilterRequest("Active", "High", "Appraisal");

        filter.Status.Should().Be("Active");
        filter.Priority.Should().Be("High");
        filter.TaskName.Should().Be("Appraisal");

        // GetMyTasksFilterRequest should only have Status, Priority, TaskName — no AssigneeUserId
        var properties = typeof(GetMyTasksFilterRequest).GetProperties();
        properties.Should().NotContain(p => p.Name == "AssigneeUserId");
    }

    [Fact]
    public void GetMyTasksQuery_ShouldNotExposeAssigneeUserId()
    {
        // The query and its filter should never accept an assignee user ID from outside
        var filter = new GetMyTasksFilterRequest();
        var query = new GetMyTasksQuery(new PaginationRequest(0, 10), filter);

        query.Filter.Should().NotBeNull();

        // Contrast with GetTasksFilterRequest which HAS AssigneeUserId
        var myFilterProps = typeof(GetMyTasksFilterRequest).GetProperties().Select(p => p.Name).ToList();
        var tasksFilterProps = typeof(GetTasksFilterRequest).GetProperties().Select(p => p.Name).ToList();

        myFilterProps.Should().NotContain("AssigneeUserId");
        tasksFilterProps.Should().Contain("AssigneeUserId");
    }

    [Fact]
    public void GetMyTasksResult_ShouldWrapPaginatedTaskDto()
    {
        // Verify the result type reuses the shared TaskDto
        var items = new List<TaskDto>
        {
            new() { Id = Guid.NewGuid(), TaskType = "Appraisal", Status = "Active" }
        };
        var paginated = new PaginatedResult<TaskDto>(items, 1, 0, 10);
        var result = new GetMyTasksResult(paginated);

        result.Result.Should().NotBeNull();
        result.Result.Items.Should().HaveCount(1);
        result.Result.Count.Should().Be(1);
    }

    [Fact]
    public void GetMyTasksResponse_ShouldWrapPaginatedTaskDto()
    {
        var items = new List<TaskDto>();
        var paginated = new PaginatedResult<TaskDto>(items, 0, 0, 10);
        var response = new GetMyTasksResponse(paginated);

        response.Result.Should().NotBeNull();
        response.Result.Items.Should().BeEmpty();
    }
}
