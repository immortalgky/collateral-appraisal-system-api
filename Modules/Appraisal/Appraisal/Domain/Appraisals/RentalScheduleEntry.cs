namespace Appraisal.Domain.Appraisals;

/// <summary>
/// A full schedule row stored for a RentalInfo — the final computed + overridden values.
/// </summary>
public class RentalScheduleEntry : Entity<Guid>
{
    public Guid RentalInfoId { get; private set; }
    public int Year { get; private set; }
    public DateTime ContractStart { get; private set; }
    public DateTime ContractEnd { get; private set; }
    public decimal UpFront { get; private set; }
    public decimal ContractRentalFee { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal ContractRentalFeeGrowthRatePercent { get; private set; }

    private RentalScheduleEntry() { }

    public static RentalScheduleEntry Create(
        Guid rentalInfoId, int year, DateTime contractStart, DateTime contractEnd,
        decimal upFront, decimal contractRentalFee, decimal totalAmount,
        decimal contractRentalFeeGrowthRatePercent)
    {
        return new RentalScheduleEntry
        {
            RentalInfoId = rentalInfoId,
            Year = year,
            ContractStart = contractStart,
            ContractEnd = contractEnd,
            UpFront = upFront,
            ContractRentalFee = contractRentalFee,
            TotalAmount = totalAmount,
            ContractRentalFeeGrowthRatePercent = contractRentalFeeGrowthRatePercent
        };
    }
}
