using Auth.Domain.Auth.Features.Me;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OAuth2OpenId.Data;
using OAuth2OpenId.Data.Repository;
using OAuth2OpenId.Domain.Identity.Models;

namespace Auth.Tests.Auth.Features.Me;

public class MeQueryHandlerTests
{
    private readonly OpenIddictDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRoleRepository _roleRepository;
    private readonly MeQueryHandler _handler;

    public MeQueryHandlerTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<OpenIddictDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new OpenIddictDbContext(options);

        // Setup UserManager mock
        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        // Setup RoleRepository mock
        _roleRepository = Substitute.For<IRoleRepository>();

        _handler = new MeQueryHandler(_dbContext, _userManager, _roleRepository);
    }

    [Fact]
    public async Task Handle_UserWithGrantedPermissions_ShouldReturnUserPermissions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var readPermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "read", Description = "Read permission" };
        var writePermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "write", Description = "Write permission" };
        
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            Permissions = new List<UserPermission>
            {
                new() { Permission = readPermission, IsGranted = true },
                new() { Permission = writePermission, IsGranted = true }
            }
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _userManager.GetRolesAsync(user).Returns(new List<string>());

        var query = new MeQuery(userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(userId, result.Id);
        Assert.Equal("testuser", result.Username);
        Assert.Equal("test@example.com", result.Email);
        Assert.Contains("read", result.Permissions);
        Assert.Contains("write", result.Permissions);
        Assert.Empty(result.Roles);
    }

    [Fact]
    public async Task Handle_UserWithDeniedPermissions_ShouldNotReturnDeniedPermissions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var readPermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "read", Description = "Read permission" };
        var writePermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "write", Description = "Write permission" };
        
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            Permissions = new List<UserPermission>
            {
                new() { Permission = readPermission, IsGranted = false },
                new() { Permission = writePermission, IsGranted = false }
            }
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _userManager.GetRolesAsync(user).Returns(new List<string>());

        var query = new MeQuery(userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("read", result.Permissions);
        Assert.DoesNotContain("write", result.Permissions);
        Assert.Empty(result.Permissions);
    }

    [Fact]
    public async Task Handle_UserWithRolePermissions_ShouldReturnRolePermissions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var readPermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "read", Description = "Read permission" };
        var writePermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "write", Description = "Write permission" };
        
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            Permissions = new List<UserPermission>()
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var role = new ApplicationRole
        {
            Name = "TestRole",
            Permissions = new List<RolePermission>
            {
                new() { Permission = readPermission },
                new() { Permission = writePermission }
            }
        };

        _userManager.GetRolesAsync(user).Returns(new List<string> { "TestRole" });
        _roleRepository.GetRoleByName("TestRole").Returns(role);

        var query = new MeQuery(userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("read", result.Permissions);
        Assert.Contains("write", result.Permissions);
        Assert.Contains("TestRole", result.Roles);
    }

    [Fact]
    public async Task Handle_UserWithDeniedPermissionAndRoleGrant_ShouldNotReturnDeniedPermission()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var readPermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "read", Description = "Read permission" };
        var writePermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "write", Description = "Write permission" };
        
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            Permissions = new List<UserPermission>
            {
                new() { Permission = writePermission, IsGranted = false } // Explicitly deny write
            }
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var role = new ApplicationRole
        {
            Name = "TestRole",
            Permissions = new List<RolePermission>
            {
                new() { Permission = readPermission },
                new() { Permission = writePermission } // Role grants write, but user denies it
            }
        };

        _userManager.GetRolesAsync(user).Returns(new List<string> { "TestRole" });
        _roleRepository.GetRoleByName("TestRole").Returns(role);

        var query = new MeQuery(userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("read", result.Permissions); // Should have read from role
        Assert.DoesNotContain("write", result.Permissions); // Should not have write (explicitly denied)
    }

    [Fact]
    public async Task Handle_UserWithGrantedPermissionAndRolePermission_ShouldReturnBothPermissions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var readPermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "read", Description = "Read permission" };
        var writePermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "write", Description = "Write permission" };
        var deletePermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "delete", Description = "Delete permission" };
        
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            Permissions = new List<UserPermission>
            {
                new() { Permission = deletePermission, IsGranted = true } // User has explicit delete
            }
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var role = new ApplicationRole
        {
            Name = "TestRole",
            Permissions = new List<RolePermission>
            {
                new() { Permission = readPermission },
                new() { Permission = writePermission }
            }
        };

        _userManager.GetRolesAsync(user).Returns(new List<string> { "TestRole" });
        _roleRepository.GetRoleByName("TestRole").Returns(role);

        var query = new MeQuery(userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("read", result.Permissions); // From role
        Assert.Contains("write", result.Permissions); // From role
        Assert.Contains("delete", result.Permissions); // From user explicit grant
        Assert.Equal(3, result.Permissions.Count);
    }

    [Fact]
    public async Task Handle_UserWithMultipleRoles_ShouldReturnAllRolePermissions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var readPermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "read", Description = "Read permission" };
        var writePermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "write", Description = "Write permission" };
        var adminPermission = new Permission { Id = Guid.NewGuid(), PermissionCode = "admin", Description = "Admin permission" };
        
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            Permissions = new List<UserPermission>()
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var role1 = new ApplicationRole
        {
            Name = "Reader",
            Permissions = new List<RolePermission>
            {
                new() { Permission = readPermission }
            }
        };

        var role2 = new ApplicationRole
        {
            Name = "Writer",
            Permissions = new List<RolePermission>
            {
                new() { Permission = writePermission }
            }
        };

        var role3 = new ApplicationRole
        {
            Name = "Admin",
            Permissions = new List<RolePermission>
            {
                new() { Permission = adminPermission }
            }
        };

        _userManager.GetRolesAsync(user).Returns(new List<string> { "Reader", "Writer", "Admin" });
        _roleRepository.GetRoleByName("Reader").Returns(role1);
        _roleRepository.GetRoleByName("Writer").Returns(role2);
        _roleRepository.GetRoleByName("Admin").Returns(role3);

        var query = new MeQuery(userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("read", result.Permissions);
        Assert.Contains("write", result.Permissions);
        Assert.Contains("admin", result.Permissions);
        Assert.Equal(3, result.Roles.Count);
    }

    [Fact]
    public async Task Handle_NonExistentUser_ShouldThrowNotFoundException()
    {
        // Arrange
        var query = new MeQuery(Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<Shared.Exceptions.NotFoundException>(
            async () => await _handler.Handle(query, TestContext.Current.CancellationToken)
        );
    }
}
