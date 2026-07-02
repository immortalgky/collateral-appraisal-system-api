namespace Auth.Domain.Organization;

/// <summary>
/// Officer reference data sourced from the AS400 core system (Silverlake file SSOFFR —
/// Officer Parameter File). Keyed by the natural AS400 officer code; refreshed by one-time
/// seed and future periodic sync (upsert-by-code, <see cref="LastSyncedAt"/>).
/// Financial fields (currency, approval limits) are intentionally not stored — org-structure only.
/// </summary>
public class Officer
{
    public string OfficerCode { get; private set; } = default!;   // SSOOFF
    public string? BranchNumber { get; private set; }             // SSOBRN
    public string? OfficerId { get; private set; }                // SSOIDN (likely = AspNetUsers.UserName)
    public string? Name { get; private set; }                     // SSONAM
    public string? ShortName { get; private set; }                // SSOSNA
    public string? CostCenterCode { get; private set; }           // SSONTH (8-digit; see CostCenter mismatch note)
    public string? DepartmentCode { get; private set; }           // SSDDEPT (logical ref -> Department.Code)
    public bool IsActive { get; private set; } = true;
    public DateTime? LastSyncedAt { get; private set; }

    private Officer() { }

    public static Officer Create(
        string officerCode,
        string? branchNumber = null,
        string? officerId = null,
        string? name = null,
        string? shortName = null,
        string? costCenterCode = null,
        string? departmentCode = null)
    {
        return new Officer
        {
            OfficerCode = officerCode,
            BranchNumber = branchNumber,
            OfficerId = officerId,
            Name = name,
            ShortName = shortName,
            CostCenterCode = costCenterCode,
            DepartmentCode = departmentCode,
            IsActive = true
        };
    }

    public void Update(
        string? branchNumber,
        string? officerId,
        string? name,
        string? shortName,
        string? costCenterCode,
        string? departmentCode,
        bool isActive)
    {
        BranchNumber = branchNumber;
        OfficerId = officerId;
        Name = name;
        ShortName = shortName;
        CostCenterCode = costCenterCode;
        DepartmentCode = departmentCode;
        IsActive = isActive;
    }

    public void MarkSynced(DateTime syncedAt) => LastSyncedAt = syncedAt;
}
