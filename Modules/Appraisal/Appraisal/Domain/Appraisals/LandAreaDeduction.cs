namespace Appraisal.Domain.Appraisals;

/// <summary>
/// One reason a slice of a land property's registered area cannot be appraised — an encroaching
/// structure, a strip used by someone else, a public waterway, and so on. A property can carry
/// several; their areas add up to the deduction applied before pricing.
/// </summary>
/// <remarks>
/// <para>
/// The bank already words the policy this way in the seeded <c>Limitation</c> parameter group,
/// code 03: "ถูกรุกล้ำ / ใช้เพื่อบุคคลอื่น / ตัดเนื้อที่ประเมินเนื่องจากสาเหตุอื่น" — encroachment is one
/// reason to cut appraised area, not the only one, which is why this entity is named for the
/// deduction rather than for encroachment.
/// </para>
/// <para>
/// There is deliberately NO direction column (encroached-upon vs encroaching-onto-others): both
/// cost the property usable area, both are deducted, and <see cref="ReasonCode"/> plus
/// <see cref="Remark"/> already say which happened.
/// </para>
/// <para>
/// This never touches a title deed's own area. <see cref="LandTitle.Area"/> is the registered legal
/// fact and stays untouched — see the note on <see cref="LandTitle.ApplyCorrection"/>.
/// </para>
/// </remarks>
public class LandAreaDeduction : Entity<Guid>
{
    public Guid LandAppraisalDetailId { get; private set; }

    /// <summary>Code from the <c>LandAreaDeductionReason</c> parameter group ('01'-'06', '99' = other).</summary>
    public string ReasonCode { get; private set; } = default!;

    /// <summary>Free text for the "other reason" code; null for every other code.</summary>
    public string? ReasonOther { get; private set; }

    /// <summary>Area lost to this reason, in square wa. Same unit the rest of the land maths uses.</summary>
    public decimal? AreaInSqWa { get; private set; }

    public string? Remark { get; private set; }

    private LandAreaDeduction()
    {
        // For EF Core
    }

    public static LandAreaDeduction Create(Guid landAppraisalDetailId, string reasonCode)
    {
        return new LandAreaDeduction
        {
            LandAppraisalDetailId = landAppraisalDetailId,
            ReasonCode = reasonCode
        };
    }

    public void Update(string? reasonOther, decimal? areaInSqWa, string? remark)
    {
        // A deduction only ever removes area. A negative one would ADD priceable land beyond the
        // deed, and pricing multiplies that inflated area by the rate.
        if (areaInSqWa < 0m)
            throw new DomainException("A land area deduction cannot be negative.");

        ReasonOther = reasonOther;
        AreaInSqWa = areaInSqWa;
        Remark = remark;
    }

    /// <summary>Reachable only here — <see cref="ReasonCode"/> is required, so <see cref="Update"/> leaves it alone.</summary>
    public void ChangeReason(string reasonCode)
    {
        ReasonCode = reasonCode;
    }
}
