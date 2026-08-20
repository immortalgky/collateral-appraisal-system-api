namespace Auth.Domain;

/// <summary>
/// The organisational scope a role, group or team belongs to.
///
/// <c>Bank</c> = the host bank's own staff (<c>auth.AspNetUsers.CompanyId IS NULL</c>);
/// <c>Company</c> = users belonging to an external appraisal company.
///
/// A user's own scope is derived from CompanyId, and the admin UI then only offers roles,
/// groups and teams whose Scope matches it exactly (see UserDetailPanel's
/// <c>filter(r =&gt; r.scope === userScope)</c>). Any other value — NULL, empty, or a typo —
/// silently disappears from every picker, so these strings are load-bearing and must not be
/// re-spelled at call sites.
/// </summary>
public static class AuthScopes
{
    public const string Bank = "Bank";
    public const string Company = "Company";

    public static readonly string[] All = [Bank, Company];

    public static bool IsValid(string? scope) => scope is not null && All.Contains(scope);
}
