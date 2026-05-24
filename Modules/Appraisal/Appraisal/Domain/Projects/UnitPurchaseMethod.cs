namespace Appraisal.Domain.Projects;

/// <summary>
/// How the buyer is financing the purchase of a project unit.
/// Stored as int in the database.
/// </summary>
public enum UnitPurchaseMethod
{
    Cash = 1,
    Loan = 2
}
