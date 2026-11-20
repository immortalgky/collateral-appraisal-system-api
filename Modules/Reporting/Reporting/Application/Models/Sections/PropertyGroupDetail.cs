namespace Reporting.Application.Models.Sections;

/// <summary>
/// One property group in the appraisal-book detail pages (group-major layout).
/// Holds every land and building detail belonging to the group, so the template
/// renders "กลุ่มที่ N → its land(s) → its building(s)" together.
///
/// Source: appraisal.PropertyGroups + PropertyGroupItems. Properties not assigned
/// to any group are bucketed under <see cref="GroupNumber"/> 0.
/// </summary>
public sealed class PropertyGroupDetail
{
    /// <summary>กลุ่มที่ — PropertyGroups.GroupNumber (0 = ungrouped fallback).</summary>
    public int GroupNumber { get; init; }

    /// <summary>Group label — PropertyGroups.GroupName (null when ungrouped).</summary>
    public string? GroupName { get; init; }

    /// <summary>Land detail(s) in this group (L / LB / lease-land properties).</summary>
    public IReadOnlyList<LandSection> Lands { get; init; } = [];

    /// <summary>Building detail(s) in this group (B / LB / lease-building properties).</summary>
    public IReadOnlyList<BuildingSection> Buildings { get; init; } = [];

    /// <summary>Buildings proper — development-only properties print under ส่วนพัฒนา instead.</summary>
    public IReadOnlyList<BuildingSection> RealBuildings => Buildings.Where(b => !b.IsDevelopmentOnly).ToList();

    /// <summary>ส่วนพัฒนา items of every building in the group.</summary>
    public IReadOnlyList<DevelopmentItem> DevelopmentItems => Buildings.SelectMany(b => b.DevelopmentItems).ToList();

    /// <summary>Up to 3 photos (one per development-only property) for the ส่วนพัฒนา page.</summary>
    public IReadOnlyList<string> DevelopmentPhotos =>
        Buildings.Where(b => b.IsDevelopmentOnly && b.PhotoSrc != null).Select(b => b.PhotoSrc!).Take(3).ToList();
}
