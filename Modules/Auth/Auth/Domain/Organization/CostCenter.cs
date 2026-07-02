namespace Auth.Domain.Organization;

/// <summary>
/// G/L cost center reference data sourced from the AS400 core system (Silverlake file GLPAR7 —
/// G/L Cost Center Record Format). Keyed by the natural AS400 cost-center code; refreshed by
/// one-time seed and future periodic sync (upsert-by-code, <see cref="LastSyncedAt"/>).
/// AS400 record-date fields (G7DATE/G7DAT6) are intentionally not stored.
/// Note: this master code is 3-digit (G7CNTR) whereas <see cref="Officer.CostCenterCode"/> is
/// 8-digit (SSONTH); the mapping must be reconciled before treating them as the same key.
/// </summary>
public class CostCenter
{
    public string Code { get; private set; } = default!;          // G7CNTR
    public string? Description { get; private set; }              // G7DESC
    public string? Text { get; private set; }                    // G7TEXT
    public bool IsActive { get; private set; } = true;
    public DateTime? LastSyncedAt { get; private set; }

    private CostCenter() { }

    public static CostCenter Create(string code, string? description = null, string? text = null)
    {
        return new CostCenter
        {
            Code = code,
            Description = description,
            Text = text,
            IsActive = true
        };
    }

    public void Update(string? description, string? text, bool isActive)
    {
        Description = description;
        Text = text;
        IsActive = isActive;
    }

    public void MarkSynced(DateTime syncedAt) => LastSyncedAt = syncedAt;
}
