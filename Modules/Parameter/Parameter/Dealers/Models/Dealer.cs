namespace Parameter.Dealers.Models;

/// <summary>
/// Dealer lookup — code-named reference for the request "Dealer Code" dropdown.
/// </summary>
public class Dealer
{
    public string DealerCode { get; private set; } = null!;
    public string DealerName { get; private set; } = null!;

    private Dealer()
    {
        // For EF Core
    }
}
