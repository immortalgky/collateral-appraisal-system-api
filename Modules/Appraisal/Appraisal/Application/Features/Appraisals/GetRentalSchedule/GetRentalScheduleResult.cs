namespace Appraisal.Application.Features.Appraisals.GetRentalSchedule;

public record GetRentalScheduleResult(List<RentalScheduleRow> Rows);

public record RentalScheduleRow(
    int Year,
    DateTime ContractStart,
    DateTime ContractEnd,
    decimal UpFront,
    decimal ContractRentalFee,
    decimal TotalAmount,
    decimal ContractRentalFeeGrowthRatePercent
);
