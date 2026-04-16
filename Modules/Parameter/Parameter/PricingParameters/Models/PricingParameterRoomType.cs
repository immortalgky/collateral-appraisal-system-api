namespace Parameter.PricingParameters.Models;

/// <summary>
/// Hotel room type lookup — code-named reference for use in Method01/02/04/07/08 calculations.
/// </summary>
public class PricingParameterRoomType
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int DisplaySeq { get; private set; }

    private PricingParameterRoomType()
    {
        // For EF Core
    }
}
