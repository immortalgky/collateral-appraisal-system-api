namespace Auth.Domain.Organization;

/// <summary>
/// Department reference data sourced from the AS400 core system (Silverlake file LP4PAR4 —
/// Department Code Parameters). Keyed by the natural AS400 department code; refreshed by
/// one-time seed and future periodic sync (upsert-by-code, <see cref="LastSyncedAt"/>).
/// </summary>
public class Department
{
    public string Code { get; private set; } = default!;          // LP4COD
    public string? DivisionCode { get; private set; }             // LP4DVC
    public string? Description { get; private set; }              // LP4DSC
    public bool IsActive { get; private set; } = true;
    public DateTime? LastSyncedAt { get; private set; }

    private Department() { }

    public static Department Create(string code, string? divisionCode = null, string? description = null)
    {
        return new Department
        {
            Code = code,
            DivisionCode = divisionCode,
            Description = description,
            IsActive = true
        };
    }

    public void Update(string? divisionCode, string? description, bool isActive)
    {
        DivisionCode = divisionCode;
        Description = description;
        IsActive = isActive;
    }

    public void MarkSynced(DateTime syncedAt) => LastSyncedAt = syncedAt;
}
