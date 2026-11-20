namespace Reporting.Application.Models.Sections;

/// <summary>
/// รายละเอียดสัญญาเช่า printed above a Leasehold / Profit Rent calculation: one agreement of the group's
/// properties (LeaseAgreementDetails) with the rental terms recorded for the same property (RentalInfos).
/// </summary>
public sealed class LeaseAgreementBlock
{
    public string? LessorName { get; init; }
    public string? LesseeName { get; init; }
    public string? ContractNo { get; init; }
    public DateTime? LeaseStartDate { get; init; }
    public DateTime? LeaseEndDate { get; init; }

    /// <summary>ระยะเวลาตามสัญญา (ปี).</summary>
    public decimal? LeasePeriodAsContract { get; init; }

    /// <summary>ระยะเวลาเช่าคงเหลือ ณ วันประเมิน (ปี).</summary>
    public decimal? RemainingLeaseAsAppraisalDate { get; init; }

    public decimal? LeaseRentFee { get; init; }
    public decimal? RentAdjust { get; init; }
    public string? Sublease { get; init; }
    public decimal? AdditionalExpenses { get; init; }
    public string? LeaseTerminate { get; init; }
    public string? ContractRenewal { get; init; }
    public string? RentalTermsImpactingPropertyUse { get; init; }
    public string? TerminationOfLease { get; init; }
    public string? Remark { get; init; }

    /// <summary>การจดทะเบียนการเช่า — "จดทะเบียน" / "ไม่จดทะเบียน", "-" when not answered.</summary>
    public string? RegistrationText { get; init; }

    /// <summary>นำสิทธิการเช่าเป็นหลักประกัน — "ได้" / "ไม่ได้", "-" when not answered.</summary>
    public string? PledgeableText { get; init; }

    // RentalInfos of the same property (null when none recorded)
    public int? NumberOfYears { get; init; }
    public DateTime? FirstYearStartDate { get; init; }
    public decimal? ContractRentalFeePerYear { get; init; }
    public decimal? UpFrontTotalAmount { get; init; }

    /// <summary>การปรับค่าเช่า — "ตามช่วงเวลา" / "ทุก N ปี" with the percentage, in Thai.</summary>
    public string? GrowthText { get; init; }
}
