using Appraisal.Domain.Appraisals;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Domain;

/// <summary>The condo's free-text "ข้อจำกัดอื่นๆ ที่พบ" survives Update and a re-appraisal copy, as on Land.</summary>
public class CondoOtherLegalLimitationsTests
{
    [Fact]
    public void Update_and_CopyFrom_keep_the_other_legal_limitations()
    {
        var condo = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now)
            .AddCondoProperty().CondoDetail!;

        condo.Update(otherLegalLimitations: "Shared easement over the car park");

        var copy = CondoAppraisalDetail.CopyFrom(condo, Guid.NewGuid());

        Assert.Equal("Shared easement over the car park", condo.OtherLegalLimitations);
        Assert.Equal("Shared easement over the car park", copy.OtherLegalLimitations);
    }
}
