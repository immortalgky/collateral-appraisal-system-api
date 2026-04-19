namespace Auth.Infrastructure.Seed;

/// <summary>
/// Seed blueprint for activity-scoped menu overrides. Rows here declare, for a given
/// activity (e.g. "appraisal-initiation"), which appraisal-scope menu items are
/// visible and which are editable. When /auth/me/menu is called with ?activityId=..,
/// these overrides become authoritative for the appraisal tree — bypassing the role
/// ViewPermissionCode / EditPermissionCode check. Only seed rows that diverge from
/// plain role-based behavior; omitting a (activity, menu) pair falls back to roles.
/// </summary>
public static class ActivityMenuOverrideSeedData
{
    public record Override(string ActivityId, string MenuItemKey, bool IsVisible, bool CanEdit);

    public static List<Override> GetSeed() => new()
    {
        // Activity: appraisal-initiation (role: RequestMaker)
        // Request maker fills in the initiation form — request info, appointment, property, documents.
        new("appraisal-initiation", "appraisal.request",        IsVisible: true,  CanEdit: true),
        new("appraisal-initiation", "appraisal.appointment",    IsVisible: true,  CanEdit: true),
        new("appraisal-initiation", "appraisal.property",       IsVisible: true,  CanEdit: true),
        new("appraisal-initiation", "appraisal.block-condo",    IsVisible: true,  CanEdit: true),
        new("appraisal-initiation", "appraisal.block-village",  IsVisible: true,  CanEdit: true),
        new("appraisal-initiation", "appraisal.property-pma",   IsVisible: true,  CanEdit: true),
        new("appraisal-initiation", "appraisal.documents",      IsVisible: true,  CanEdit: true),
        new("appraisal-initiation", "appraisal.360",            IsVisible: true,  CanEdit: false),
        new("appraisal-initiation", "appraisal.administration", IsVisible: false, CanEdit: false),
        new("appraisal-initiation", "appraisal.summary",        IsVisible: false, CanEdit: false),

        // Activity: provide-additional-documents (role: RequestMaker)
        // Follow-up task — only documents are editable; request info stays visible read-only.
        new("provide-additional-documents", "appraisal.documents",      IsVisible: true,  CanEdit: true),
        new("provide-additional-documents", "appraisal.request",        IsVisible: true,  CanEdit: false),
        new("provide-additional-documents", "appraisal.360",            IsVisible: true,  CanEdit: false),
        new("provide-additional-documents", "appraisal.appointment",    IsVisible: false, CanEdit: false),
        new("provide-additional-documents", "appraisal.property",       IsVisible: false, CanEdit: false),
        new("provide-additional-documents", "appraisal.block-condo",    IsVisible: false, CanEdit: false),
        new("provide-additional-documents", "appraisal.block-village",  IsVisible: false, CanEdit: false),
        new("provide-additional-documents", "appraisal.property-pma",   IsVisible: false, CanEdit: false),
        new("provide-additional-documents", "appraisal.administration", IsVisible: false, CanEdit: false),
        new("provide-additional-documents", "appraisal.summary",        IsVisible: false, CanEdit: false),
    };
}
