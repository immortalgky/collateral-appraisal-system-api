namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Tracks which schedule cells the user manually overrode. Null = use computed value.
/// </summary>
public class RentalScheduleOverride : Entity<Guid>
{
    public Guid RentalInfoId { get; private set; }
    public int Year { get; private set; }
    public decimal? UpFront { get; private set; }
    public decimal? ContractRentalFee { get; private set; }

    private RentalScheduleOverride() { }

    public static RentalScheduleOverride Create(
        Guid rentalInfoId, int year, decimal? upFront, decimal? contractRentalFee)
    {
        return new RentalScheduleOverride
        {
            RentalInfoId = rentalInfoId,
            Year = year,
            UpFront = upFront,
            ContractRentalFee = contractRentalFee
        };
    }
}
