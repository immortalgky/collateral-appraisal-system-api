namespace Shared.Identity;

public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user's unique identifier from the "sub" claim.
    /// Returns null if user is not authenticated.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Gets the current user's username from the "name" claim.
    /// Returns null if user is not authenticated.
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// Gets the current user's bank code (e.g. "P5229") from the "preferred_username" claim
    /// (= AspNetUsers.UserName). This is the canonical user identifier used for actor/audit
    /// fields throughout the domain — prefer this over <see cref="UserId"/> (Guid) for stamping
    /// who performed an action. Returns null if not authenticated.
    /// </summary>
    string? UserCode { get; }

    /// <summary>
    /// Gets whether the current request has an authenticated user.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Checks if the current user has a specific permission.
    /// </summary>
    /// <param name="permission">The permission code to check (e.g., "request:write")</param>
    /// <returns>True if the user has the permission, false otherwise</returns>
    bool HasPermission(string permission);

    /// <summary>
    /// Checks if the current user has any of the specified permissions.
    /// </summary>
    /// <param name="permissions">The permission codes to check</param>
    /// <returns>True if the user has at least one of the permissions, false otherwise</returns>
    bool HasAnyPermission(params string[] permissions);

    /// <summary>
    /// Checks if the current user has all of the specified permissions.
    /// </summary>
    /// <param name="permissions">The permission codes to check</param>
    /// <returns>True if the user has all of the permissions, false otherwise</returns>
    bool HasAllPermissions(params string[] permissions);

    /// <summary>
    /// Gets all permissions for the current user from the "permissions" claim.
    /// </summary>
    IReadOnlyList<string> Permissions { get; }

    /// <summary>
    /// Gets all roles for the current user from the "roles" claim.
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Gets the current user's company identifier from the "company_id" claim.
    /// Returns null if the user has no company or is not authenticated.
    /// </summary>
    Guid? CompanyId { get; }

    /// <summary>
    /// True when the current user belongs to an external appraisal company
    /// (i.e. has a company_id claim). Bank-internal users have no CompanyId.
    /// </summary>
    bool IsExternal { get; }

    /// <summary>
    /// Checks if the current user has a specific role.
    /// </summary>
    /// <param name="role">The role name to check</param>
    /// <returns>True if the user has the role, false otherwise</returns>
    bool IsInRole(string role);
}