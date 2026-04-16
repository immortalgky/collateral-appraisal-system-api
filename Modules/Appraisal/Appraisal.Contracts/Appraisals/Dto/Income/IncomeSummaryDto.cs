namespace Appraisal.Contracts.Appraisals.Dto.Income;

/// <summary>
/// Summary block for an income analysis — all fields are year-indexed decimal arrays
/// (length = TotalNumberOfYears, except TerminalRevenue/TotalNet/Discount/PresentValue
/// which have length TotalNumberOfYears - 1, mirroring the calculation service output).
/// </summary>
public record IncomeSummaryDto(
    decimal[] ContractRentalFee,
    decimal[] GrossRevenue,
    decimal[] GrossRevenueProportional,
    decimal[] TerminalRevenue,
    decimal[] TotalNet,
    decimal[] Discount,
    decimal[] PresentValue
);
