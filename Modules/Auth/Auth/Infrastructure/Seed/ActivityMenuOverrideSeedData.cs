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

    /// <summary>Every appraisal-scope side-nav tab key — the universe a "special activity" hides from.</summary>
    private static readonly string[] AllAppraisalTabKeys =
    {
        "appraisal.360",
        "appraisal.request",
        "appraisal.administration",
        "appraisal.appointment",
        "appraisal.fee-appointment-approval",
        "appraisal.quotation-submit",
        "appraisal.quotation-respond-negotiation",
        "appraisal.quotation-review",
        "appraisal.quotation-pick-winner",
        "appraisal.quotation-finalize",
        "appraisal.property",
        "appraisal.block-condo",
        "appraisal.block-village",
        "appraisal.property-pma",
        "appraisal.documents",
        "appraisal.document-followup",
        "appraisal.summary",
    };

    /// <summary>
    /// A "special" (single-purpose) activity shows ONLY Request Information (read-only) plus the
    /// tab(s) it exists for; every OTHER appraisal tab is hidden — even for roles (e.g. Admin) that
    /// hold the permission. Request stays visible-but-read-only; each key in <paramref name="keepTabKeys"/>
    /// is omitted so it keeps its plain role-based state (visible + editable for the performer).
    /// </summary>
    private static IEnumerable<Override> FocusOnly(string activityId, params string[] keepTabKeys) =>
        AllAppraisalTabKeys
            .Where(k => !keepTabKeys.Contains(k))
            .Select(k => k == "appraisal.request"
                ? new Override(activityId, k, IsVisible: true,  CanEdit: false)   // request read-only
                : new Override(activityId, k, IsVisible: false, CanEdit: false)); // everything else hidden

    public static List<Override> GetSeed()
    {
        var overrides = new List<Override>
        {
            // Activity: appraisal-initiation (role: RequestMaker)
            // Request maker fills in the initiation form. 360 is read-only and administration is hidden;
            // Request Information and Summary & Decision stay editable (Summary is intentionally shown).
            new("appraisal-initiation", "appraisal.360",            IsVisible: true,  CanEdit: false),
            new("appraisal-initiation", "appraisal.administration", IsVisible: false, CanEdit: false),

            // Property Information (PMA) is granted ONLY to IntAppraisalStaff (see AuthDataSeed), so it
            // would otherwise also appear on that role's other activities. Hide it there so the PMA tab
            // is exclusive to int-pma-input; every other role never had the permission to begin with.
            // int-offline-book-keyin only trims PMA (the keyer reproduces the whole external book, so
            // every other appraisal tab stays at its plain role-based state — that is why this activity
            // exists rather than reusing appraisal-book-verification, whose property tabs are restricted).
            new("int-appraisal-execution",     "appraisal.property-pma", IsVisible: false, CanEdit: false),
            new("appraisal-book-verification", "appraisal.property-pma", IsVisible: false, CanEdit: false),
            new("int-offline-book-keyin",      "appraisal.property-pma", IsVisible: false, CanEdit: false),

            // Fee & Appointment Approval is an action tab for the fee-appointment-approval activity only.
            // The roles that hold TASK_FEE_APPOINTMENT_APPROVAL (IntAdmin, IntAppraisalChecker) would
            // otherwise see it on their OTHER activities too, so hide it there — same pattern as PMA above.
            new("appraisal-assignment", "appraisal.fee-appointment-approval", IsVisible: false, CanEdit: false),
            new("int-appraisal-check",  "appraisal.fee-appointment-approval", IsVisible: false, CanEdit: false),

            // Activity: appraisal-book-verification (role: IntAppraisalStaff)
            // A verification step: Document Checklist and Summary & Decision stay editable. Appointment
            // & Fee and Property (+ block variants) are forced read-only (the role can edit them on its
            // execution activities, just not here). 360 / Request / Administration are already view-only
            // for this role; PMA is hidden above.
            new("appraisal-book-verification", "appraisal.appointment",   IsVisible: true, CanEdit: false),
            new("appraisal-book-verification", "appraisal.property",      IsVisible: true, CanEdit: false),
            new("appraisal-book-verification", "appraisal.block-condo",   IsVisible: true, CanEdit: false),
            new("appraisal-book-verification", "appraisal.block-village", IsVisible: true, CanEdit: false),

            // Submit Quotation / Respond to Negotiation are action tabs for their own quotation activity.
            // The roles holding TASK_QUOTATION_SUBMIT/NEGOTIATE (ExtAdmin, ExtAppraisalChecker) would
            // otherwise see them on their appraisal-assignment/check activities, so hide each there.
            new("ext-appraisal-assignment", "appraisal.quotation-submit",              IsVisible: false, CanEdit: false),
            new("ext-appraisal-check",      "appraisal.quotation-submit",              IsVisible: false, CanEdit: false),
            new("ext-appraisal-assignment", "appraisal.quotation-respond-negotiation", IsVisible: false, CanEdit: false),
            new("ext-appraisal-check",      "appraisal.quotation-respond-negotiation", IsVisible: false, CanEdit: false),

            // RequestMaker scoping. Request Information is editable only on appraisal-initiation, so force
            // it read-only on the initiation-check activity. Pick Quotation Winner and Provide Documents
            // are action tabs — hidden on the initiation activities where the maker also lands.
            new("appraisal-initiation-check", "appraisal.request",               IsVisible: true,  CanEdit: false),
            new("appraisal-initiation",       "appraisal.quotation-pick-winner", IsVisible: false, CanEdit: false),
            new("appraisal-initiation-check", "appraisal.quotation-pick-winner", IsVisible: false, CanEdit: false),
            new("appraisal-initiation",       "appraisal.document-followup",     IsVisible: false, CanEdit: false),
            new("appraisal-initiation-check", "appraisal.document-followup",     IsVisible: false, CanEdit: false),
        };

        // ── Special (single-purpose) activities: show ONLY Request Information (read-only) plus the
        // tab(s) the activity exists for; every other appraisal tab is hidden — even for Admin. See
        // FocusOnly. (admin-review-submissions / admin-finalize have no seeded non-admin performer.)
        overrides.AddRange(FocusOnly("ext-collect-submissions",      "appraisal.quotation-submit"));
        overrides.AddRange(FocusOnly("ext-respond-negotiation",      "appraisal.quotation-respond-negotiation"));
        overrides.AddRange(FocusOnly("rm-pick-winner",               "appraisal.quotation-pick-winner"));
        overrides.AddRange(FocusOnly("provide-additional-documents", "appraisal.document-followup"));
        overrides.AddRange(FocusOnly("fee-appointment-approval",     "appraisal.fee-appointment-approval"));
        // int-pma-input (IntAppraisalStaff): key in PMA property values — keep Property Information (PMA)
        // and Summary & Decision editable alongside read-only Request; hide everything else.
        overrides.AddRange(FocusOnly("int-pma-input", "appraisal.property-pma", "appraisal.summary"));

        return overrides;
    }
}
