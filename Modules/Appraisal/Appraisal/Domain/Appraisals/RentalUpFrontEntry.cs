namespace Appraisal.Domain.Appraisals;

/// <summary>
/// An up-front payment entry within a RentalInfo.
/// </summary>
public class RentalUpFrontEntry : Entity<Guid>
{
    public Guid RentalInfoId { get; private set; }
    public DateTime AtYear { get; private set; }
    public decimal UpFrontAmount { get; private set; }

    private RentalUpFrontEntry()
    {
    }

    public static RentalUpFrontEntry Create(
        Guid rentalInfoId,
        DateTime atYear,
        decimal upFrontAmount)
    {
        return new RentalUpFrontEntry
        {
            RentalInfoId = rentalInfoId,
            AtYear = atYear,
            UpFrontAmount = upFrontAmount
        };
    }
}
