using Dapper;

namespace Auth.Application.Features.Reports;

/// <summary>
/// Shared SQL/parameter builder for the User Access Matrix report.
/// Both the paginated JSON handler and the CSV export handler use this.
/// </summary>
internal static class UserAccessMatrixSqlBuilder
{
    private const string BaseFrom = """
        FROM auth.AspNetUsers u
        LEFT JOIN auth.Companies c ON c.Id = u.CompanyId
        """;

    /// <summary>
    /// Builds the WHERE clause conditions and populates DynamicParameters.
    /// Uses EXISTS subqueries for role/group/team filters so that STRING_AGG
    /// still returns ALL assignments for the user, not just the filtered one.
    /// </summary>
    public static (string Where, DynamicParameters Parameters) BuildFilter(
        string? scope,
        Guid? companyId,
        string? roleName,
        Guid? groupId,
        Guid? teamId,
        bool? isActive,
        string? search)
    {
        var conditions = new List<string>();
        var p = new DynamicParameters();

        // Scope: Bank users have no CompanyId; Company users do
        if (!string.IsNullOrWhiteSpace(scope))
        {
            if (scope.Equals("Bank", StringComparison.OrdinalIgnoreCase))
                conditions.Add("u.CompanyId IS NULL");
            else if (scope.Equals("Company", StringComparison.OrdinalIgnoreCase))
                conditions.Add("u.CompanyId IS NOT NULL");
        }

        if (companyId.HasValue)
        {
            conditions.Add("u.CompanyId = @CompanyId");
            p.Add("CompanyId", companyId.Value);
        }

        if (isActive.HasValue)
        {
            conditions.Add("u.IsActive = @IsActive");
            p.Add("IsActive", isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("""
                (u.UserName LIKE @Search
                 OR u.FirstName LIKE @Search
                 OR u.LastName LIKE @Search
                 OR u.Email LIKE @Search)
                """);
            p.Add("Search", $"%{search}%");
        }

        // Role filter via EXISTS so the STRING_AGG still shows all roles
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            conditions.Add("""
                EXISTS (
                    SELECT 1
                    FROM auth.AspNetUserRoles ur
                    JOIN auth.AspNetRoles r ON r.Id = ur.RoleId
                    WHERE ur.UserId = u.Id AND r.Name = @RoleName
                )
                """);
            p.Add("RoleName", roleName);
        }

        // Group filter via EXISTS
        if (groupId.HasValue)
        {
            conditions.Add("""
                EXISTS (
                    SELECT 1
                    FROM auth.GroupUsers gu
                    JOIN auth.Groups g ON g.Id = gu.GroupId
                    WHERE gu.UserId = u.Id AND g.Id = @GroupId AND g.IsDeleted = 0
                )
                """);
            p.Add("GroupId", groupId.Value);
        }

        // Team filter via EXISTS
        if (teamId.HasValue)
        {
            conditions.Add("""
                EXISTS (
                    SELECT 1
                    FROM auth.TeamMembers tu
                    JOIN auth.Teams t ON t.Id = tu.TeamId
                    WHERE tu.UserId = u.Id AND t.Id = @TeamId
                )
                """);
            p.Add("TeamId", teamId.Value);
        }

        var where = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : string.Empty;

        return (where, p);
    }

    /// <summary>
    /// The SELECT projection columns (no ORDER/OFFSET/FETCH — callers add those).
    /// </summary>
    private const string SelectColumns = """
        SELECT
            u.Id                                                          AS UserId,
            u.UserName,
            u.FirstName + ' ' + u.LastName                               AS FullName,
            u.Email,
            c.Name                                                        AS CompanyName,
            CASE WHEN u.CompanyId IS NULL THEN 'Bank' ELSE 'Company' END AS Scope,
            u.IsActive,
            COALESCE(
                (SELECT STRING_AGG(r.Name, ', ') WITHIN GROUP (ORDER BY r.Name)
                 FROM auth.AspNetUserRoles ur
                 JOIN auth.AspNetRoles r ON r.Id = ur.RoleId
                 WHERE ur.UserId = u.Id),
                '') AS Roles,
            COALESCE(
                (SELECT STRING_AGG(g.Name, ', ') WITHIN GROUP (ORDER BY g.Name)
                 FROM auth.GroupUsers gu
                 JOIN auth.Groups g ON g.Id = gu.GroupId
                 WHERE gu.UserId = u.Id AND g.IsDeleted = 0),
                '') AS Groups,
            COALESCE(
                (SELECT STRING_AGG(t.Name, ', ') WITHIN GROUP (ORDER BY t.Name)
                 FROM auth.TeamMembers tu
                 JOIN auth.Teams t ON t.Id = tu.TeamId
                 WHERE tu.UserId = u.Id),
                '') AS Teams
        """;

    private const string OrderBy = "ORDER BY u.UserName";

    /// <summary>
    /// Returns (countSql, dataSql) for paginated queries.
    /// Offset is 0-based: PageNumber * PageSize.
    /// </summary>
    public static (string CountSql, string DataSql) BuildPaginatedSql(
        string where,
        int pageNumber,
        int pageSize)
    {
        var offset = pageNumber * pageSize;
        var countSql = $"SELECT COUNT(1) {BaseFrom} {where}";
        var dataSql = $"""
            {SelectColumns}
            {BaseFrom}
            {where}
            {OrderBy}
            OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY
            """;
        return (countSql, dataSql);
    }

    /// <summary>
    /// Returns a full (un-paged) data SQL for CSV export.
    /// </summary>
    public static string BuildExportSql(string where)
    {
        return $"""
            {SelectColumns}
            {BaseFrom}
            {where}
            {OrderBy}
            """;
    }
}
