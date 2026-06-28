namespace Request.Contracts.Requests.Dtos;

/// <summary>
/// Combined requestor identity + org-data snapshot returned by GET /requests/{id}.
/// Mirrors the shape of the <c>SearchRequestors</c> endpoint projection so the frontend
/// can repopulate the read-only requestor block in edit mode without an extra lookup.
/// <para>
/// <c>EmployeeId</c> is the bank employee code (e.g. P5229) stored in the Requests table.
/// A <c>UserId</c> (Guid) is intentionally absent — the Guid is not persisted on the request row;
/// if the update payload requires a Guid, the frontend can re-resolve it via
/// <c>GET /auth/requestors?search={employeeId}</c> or a dedicated column can be added later.
/// </para>
/// </summary>
public record RequestorDetailDto(
    string EmployeeId,
    string Name,
    string? Email,
    string? ContactNo,
    string? AoCode,
    string? CostCenterCode,
    string? CostCenterDescription,
    string? Department);
