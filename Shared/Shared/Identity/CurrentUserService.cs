using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Shared.Identity;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var subClaim = User?.FindFirst("sub")?.Value;
            return Guid.TryParse(subClaim, out var userId) ? userId : null;
        }
    }

    public string? Username => User?.FindFirst("name")?.Value;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Permissions
    {
        get
        {
            if (User == null) return [];

            return User.FindAll("permissions")
                .Select(c => c.Value)
                .ToList()
                .AsReadOnly();
        }
    }

    public IReadOnlyList<string> Roles
    {
        get
        {
            if (User == null) return [];

            return User.FindAll("roles")
                .Select(c => c.Value)
                .ToList()
                .AsReadOnly();
        }
    }

    public bool HasPermission(string permission)
    {
        return !string.IsNullOrWhiteSpace(permission) &&
               Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasAnyPermission(params string[] permissions)
    {
        if (permissions == null || permissions.Length == 0) return false;
        var userPermissions = Permissions;
        return permissions.Any(p => userPermissions.Contains(p, StringComparer.OrdinalIgnoreCase));
    }

    public bool HasAllPermissions(params string[] permissions)
    {
        if (permissions == null || permissions.Length == 0) return true;
        var userPermissions = Permissions;
        return permissions.All(p => userPermissions.Contains(p, StringComparer.OrdinalIgnoreCase));
    }


    public bool IsInRole(string role)
    {
        return !string.IsNullOrWhiteSpace(role) && Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}