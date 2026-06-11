namespace Auth.Infrastructure.Seed;

/// <summary>
/// Seed blueprint for activity-scoped menu overrides. Overrides only RESTRICT —
/// they hide an appraisal-scope item (IsVisible=false) or force it read-only
/// (CanEdit=false) while a user is on the given activity. They never grant: a tab
/// is only visible/editable if the user's role already permits it. So only seed
/// rows that take a right away; omitting a (activity, menu) pair (or a no-op
/// IsVisible=true/CanEdit=true row) leaves the item at its plain role-based state.
/// </summary>
public static class ActivityMenuOverrideSeedData
{
    public record Override(string ActivityId, string MenuItemKey, bool IsVisible, bool CanEdit);

    public static List<Override> GetSeed() => new()
    {
        // Activity: appraisal-initiation (role: RequestMaker)
        // Request maker fills in the initiation form. Role grants all section tabs;
        // here we only trim: 360 is read-only, administration & summary are hidden.
        new("appraisal-initiation", "appraisal.360",            IsVisible: true,  CanEdit: false),
        new("appraisal-initiation", "appraisal.administration", IsVisible: false, CanEdit: false),
        new("appraisal-initiation", "appraisal.summary",        IsVisible: false, CanEdit: false),

        // Activity: provide-additional-documents (role: RequestMaker)
        // Follow-up task — only documents stay editable; request & 360 are read-only,
        // everything else is hidden.
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
