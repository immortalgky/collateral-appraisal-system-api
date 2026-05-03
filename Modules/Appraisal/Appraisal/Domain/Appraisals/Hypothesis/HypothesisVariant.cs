namespace Appraisal.Domain.Appraisals.Hypothesis;

/// <summary>
/// Discriminator for which variant of the Hypothesis/Residual pricing method is in use.
/// </summary>
public enum HypothesisVariant
{
    /// <summary>
    /// Hypothesis for Land and Building (3 tabs: Unit Details, Cost of Building, Summary).
    /// Fields: A01..A10, C01..C82.
    /// </summary>
    LandBuilding = 1,

    /// <summary>
    /// Hypothesis for Condominium (2 tabs: Unit Details, Summary).
    /// Fields: D01..D04, E01..E59.
    /// </summary>
    Condominium = 2
}
