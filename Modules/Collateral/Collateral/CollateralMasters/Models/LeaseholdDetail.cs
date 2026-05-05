namespace Collateral.CollateralMasters.Models;

public class LeaseholdDetail
{
    public Guid CollateralMasterId { get; private set; }

    // Dedup key
    public string LeaseRegistrationNo { get; private set; } = null!;
    public Guid UnderlyingMasterId { get; private set; }
    public string Lessor { get; private set; } = null!;
    public string Lessee { get; private set; } = null!;
    public DateOnly LeaseTermStart { get; private set; }

    // Last-known
    public DateOnly? LeaseTermEnd { get; private set; }
    public int? LeaseTermMonths { get; private set; }
    public decimal? AnnualRent { get; private set; }
    public string? LeasePurpose { get; private set; }

    // Appraisal summary (owned)
    public AppraisalSummary AppraisalSummary { get; private set; } = null!;

    // Synced from CollateralMaster for filtered unique index support
    public bool IsDeleted { get; private set; }

    private LeaseholdDetail() { }

    internal LeaseholdDetail(
        Guid collateralMasterId,
        string leaseRegistrationNo,
        Guid underlyingMasterId,
        string lessor,
        string lessee,
        DateOnly leaseTermStart,
        bool isDeleted)
    {
        CollateralMasterId = collateralMasterId;
        LeaseRegistrationNo = leaseRegistrationNo;
        UnderlyingMasterId = underlyingMasterId;
        Lessor = lessor;
        Lessee = lessee;
        LeaseTermStart = leaseTermStart;
        AppraisalSummary = new AppraisalSummary(null, null, null, null);
        IsDeleted = isDeleted;
    }

    public void UpdateLastKnown(
        DateOnly? leaseTermEnd,
        int? leaseTermMonths,
        decimal? annualRent,
        string? leasePurpose)
    {
        LeaseTermEnd = leaseTermEnd;
        LeaseTermMonths = leaseTermMonths;
        AnnualRent = annualRent;
        LeasePurpose = leasePurpose;
    }

    public void UpdateAppraisalSummary(
        Guid appraisalId,
        string appraisalNumber,
        DateTime appraisedDate,
        decimal appraisedValue)
    {
        AppraisalSummary.Update(appraisalId, appraisalNumber, appraisedDate, appraisedValue);
    }

    internal void SetIsDeleted(bool isDeleted) => IsDeleted = isDeleted;

    internal void ApplyAdminEdit(LeaseholdAdminEdit edit, System.Collections.Generic.Dictionary<string, object?> diff)
    {
        if (edit.LeaseRegistrationNo is not null && edit.LeaseRegistrationNo != LeaseRegistrationNo)
        {
            diff["Leasehold.LeaseRegistrationNo"] = new { from = LeaseRegistrationNo, to = edit.LeaseRegistrationNo };
            LeaseRegistrationNo = edit.LeaseRegistrationNo;
        }
        if (edit.Lessor is not null && edit.Lessor != Lessor)
        {
            diff["Leasehold.Lessor"] = new { from = Lessor, to = edit.Lessor };
            Lessor = edit.Lessor;
        }
        if (edit.Lessee is not null && edit.Lessee != Lessee)
        {
            diff["Leasehold.Lessee"] = new { from = Lessee, to = edit.Lessee };
            Lessee = edit.Lessee;
        }
        if (edit.LeaseTermStart is not null && edit.LeaseTermStart != LeaseTermStart)
        {
            diff["Leasehold.LeaseTermStart"] = new { from = LeaseTermStart, to = edit.LeaseTermStart };
            LeaseTermStart = edit.LeaseTermStart.Value;
        }
        if (edit.LeaseTermEnd is not null && edit.LeaseTermEnd != LeaseTermEnd)
        {
            diff["Leasehold.LeaseTermEnd"] = new { from = LeaseTermEnd, to = edit.LeaseTermEnd };
            LeaseTermEnd = edit.LeaseTermEnd;
        }
        if (edit.LeaseTermMonths is not null && edit.LeaseTermMonths != LeaseTermMonths)
        {
            diff["Leasehold.LeaseTermMonths"] = new { from = LeaseTermMonths, to = edit.LeaseTermMonths };
            LeaseTermMonths = edit.LeaseTermMonths;
        }
        if (edit.AnnualRent is not null && edit.AnnualRent != AnnualRent)
        {
            diff["Leasehold.AnnualRent"] = new { from = AnnualRent, to = edit.AnnualRent };
            AnnualRent = edit.AnnualRent;
        }
        if (edit.LeasePurpose is not null && edit.LeasePurpose != LeasePurpose)
        {
            diff["Leasehold.LeasePurpose"] = new { from = LeasePurpose, to = edit.LeasePurpose };
            LeasePurpose = edit.LeasePurpose;
        }
    }
}
