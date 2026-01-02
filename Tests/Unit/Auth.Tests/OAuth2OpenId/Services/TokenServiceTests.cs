using NSubstitute;
using OAuth2OpenId.Data.Repository;
using OAuth2OpenId.Domain.Identity.Models;
using OAuth2OpenId.Services;

namespace Auth.Tests.OAuth2OpenId.Services;

public class TokenServiceTests
{
    private static IRoleRepository RoleRepository => Substitute.For<IRoleRepository>();

    private static Permission ReadPermission => new() { PermissionCode = "read" };
    private static UserPermission GrantedReadUserPermission =>
        new() { Permission = ReadPermission, IsGranted = true };
    private static UserPermission UngrantedReadUserPermission =>
        new() { Permission = ReadPermission, IsGranted = false };
    private static RolePermission ReadRolePermission => new() { Permission = ReadPermission };
    private static Permission WritePermission => new() { PermissionCode = "write" };
    private static UserPermission GrantedWriteUserPermission =>
        new() { Permission = WritePermission, IsGranted = true };
    private static UserPermission UngrantedWriteUserPermission =>
        new() { Permission = WritePermission, IsGranted = false };
    private static RolePermission WriteRolePermission => new() { Permission = WritePermission };

    private static string TestRoleName = "TestRole";

    [Fact]
    public async Task GetUserPermissions_UserHasUserPermissions_ShouldGetUserPermissions()
    {
        var userPermissions = new List<UserPermission>()
        {
            GrantedReadUserPermission,
            GrantedWriteUserPermission,
        };

        var jwtPermissions = await TokenService.CalcUserPermissions(
            userPermissions,
            [],
            RoleRepository
        );
        Assert.Contains("read", jwtPermissions);
        Assert.Contains("write", jwtPermissions);
    }

    [Fact]
    public async Task GetUserPermissions_UserHasNotGrantedUserPermissions_ShouldNotGetUserPermissions()
    {
        var userPermissions = new List<UserPermission>()
        {
            UngrantedReadUserPermission,
            UngrantedWriteUserPermission,
        };

        var jwtPermissions = await TokenService.CalcUserPermissions(
            userPermissions,
            [],
            RoleRepository
        );
        Assert.DoesNotContain("read", jwtPermissions);
        Assert.DoesNotContain("write", jwtPermissions);
    }

    [Fact]
    public async Task GetUserPermissions_UserHasRolePermissions_ShouldGetRolePermissions()
    {
        var userPermissions = new List<UserPermission>() { };

        var role = new ApplicationRole { Name = TestRoleName, Permissions = [ReadRolePermission] };

        var roleRepository = RoleRepository;
        roleRepository.GetRoleByName(TestRoleName).Returns(role);

        var jwtPermissions = await TokenService.CalcUserPermissions(
            userPermissions,
            [TestRoleName],
            roleRepository
        );
        Assert.Contains("read", jwtPermissions);
    }

    [Fact]
    public async Task GetUserPermissions_UserHasUserRolePermissions_ShouldGetUserRolePermissions()
    {
        var userPermissions = new List<UserPermission>() { GrantedWriteUserPermission };

        var role = new ApplicationRole { Name = TestRoleName, Permissions = [ReadRolePermission] };

        var roleRepository = RoleRepository;
        roleRepository.GetRoleByName(TestRoleName).Returns(role);

        var jwtPermissions = await TokenService.CalcUserPermissions(
            userPermissions,
            [TestRoleName],
            roleRepository
        );
        Assert.Contains("read", jwtPermissions);
        Assert.Contains("write", jwtPermissions);
    }

    [Fact]
    public async Task GetUserPermissions_UserHasUngrantedRolePermissions_ShouldNotGetRolePermissions()
    {
        var userPermissions = new List<UserPermission>() { UngrantedWriteUserPermission };

        var role = new ApplicationRole { Name = TestRoleName, Permissions = [WriteRolePermission] };

        var roleRepository = RoleRepository;
        roleRepository.GetRoleByName(TestRoleName).Returns(role);

        var jwtPermissions = await TokenService.CalcUserPermissions(
            userPermissions,
            [TestRoleName],
            roleRepository
        );
        Assert.DoesNotContain("write", jwtPermissions);
    }
}
