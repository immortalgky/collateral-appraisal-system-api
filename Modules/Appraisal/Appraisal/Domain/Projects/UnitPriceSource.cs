namespace Appraisal.Domain.Projects;

/// <summary>
/// Where a project's unit prices come from. Stored as its name ("Calculated" / "Imported").
/// </summary>
public enum UnitPriceSource
{
    /// <summary>Computed by <see cref="Project.CalculateUnitPrices"/> from the pricing assumptions.</summary>
    Calculated = 0,

    /// <summary>Uploaded from an Excel price list prepared outside CAS.</summary>
    Imported = 1
}
