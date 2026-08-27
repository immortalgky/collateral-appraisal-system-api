namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Applies one admin field correction and records it in a field-level diff, in a single call.
///
/// Semantics (the whole point of this helper — see <see cref="PropertyCorrectionData"/>):
///   * <c>incoming == null</c>  → the caller did not touch this field. Leave it alone.
///   * <c>incoming == ""</c>    → the caller explicitly cleared a string field. Store null.
///   * value equal to current   → no-op; nothing is written to the diff.
///
/// This is deliberately NOT delegated to the detail VOs' existing <c>Update(...)</c> methods:
/// <see cref="LandAppraisalDetail"/>, <see cref="BuildingAppraisalDetail"/> and
/// <see cref="CondoAppraisalDetail"/> overwrite every property unconditionally, so any field the
/// caller did not supply would be wiped to null. That data-loss trap is already documented on
/// their <c>UpdatePmaFields</c> methods.
///
/// Diff keys are dotted paths scoped by the detail they belong to — "Land.OwnerName",
/// "Condo.RoomNumber", "Land.Title[{titleId}].TitleNumber" — and each value is
/// <c>new { from, to }</c>, matching the JSON shape already produced by the Collateral module's
/// CollateralMasterAuditLog so both audit trails read the same way.
/// </summary>
internal static class CorrectionDiff
{
    /// <summary>
    /// String fields — the overwhelming majority of correctable fields. Declared non-generic so it
    /// can never be ambiguous with the value-type overloads below.
    /// </summary>
    public static void Apply(
        string key,
        string? current,
        string? incoming,
        Action<string?> set,
        Dictionary<string, object?> diff)
    {
        if (incoming is null) return;

        // "" is the explicit "clear this field" signal; anything else is stored as sent.
        var next = incoming.Length == 0 ? null : incoming;

        if (string.Equals(current, next, StringComparison.Ordinal)) return;

        diff[key] = new { from = current, to = next };
        set(next);
    }

    /// <summary>
    /// Nullable value-type fields: <c>bool?</c>, <c>int?</c>, <c>decimal?</c>, <c>DateTime?</c>.
    /// There is no "clear" signal here — null means unchanged, so a nullable flag cannot be reset
    /// to null through this path (documented limitation).
    /// </summary>
    public static void Apply<T>(
        string key,
        T? current,
        T? incoming,
        Action<T?> set,
        Dictionary<string, object?> diff)
        where T : struct
    {
        if (incoming is null) return;
        if (Nullable.Equals(current, incoming)) return;

        diff[key] = new { from = current, to = incoming };
        set(incoming);
    }

    /// <summary>
    /// Multi-select fields, stored as <c>List&lt;string&gt;?</c> (roof types, utilities, land-use
    /// codes and so on). Order is not meaningful to the domain but is preserved as sent; an empty
    /// list is an explicit "none selected" and clears the field, mirroring "" on a string.
    /// </summary>
    public static void ApplyList(
        string key,
        List<string>? current,
        IReadOnlyList<string>? incoming,
        Action<List<string>?> set,
        Dictionary<string, object?> diff)
    {
        if (incoming is null) return;

        var next = incoming.Count == 0 ? null : incoming.ToList();

        if (current is null && next is null) return;
        if (current is not null && next is not null && current.SequenceEqual(next)) return;

        diff[key] = new { from = current, to = next };
        set(next);
    }

    /// <summary>
    /// Non-nullable value-type fields. Needed because <c>IsOwnerVerified</c> is <c>bool?</c> on the
    /// land/building/condo details but a non-nullable <c>bool</c> on machinery, vehicle and vessel.
    /// The incoming value is still nullable so "not supplied" stays expressible.
    /// </summary>
    public static void ApplyRequired<T>(
        string key,
        T current,
        T? incoming,
        Action<T> set,
        Dictionary<string, object?> diff)
        where T : struct
    {
        if (incoming is null) return;
        if (EqualityComparer<T>.Default.Equals(current, incoming.Value)) return;

        diff[key] = new { from = current, to = incoming.Value };
        set(incoming.Value);
    }
}
