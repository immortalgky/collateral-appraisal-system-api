using Appraisal.Domain.Appraisals;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Domain;

/// <summary>The condo's road-in-front and transportation fields survive Update and a re-appraisal copy, as on Land.</summary>
public class CondoRoadAndTransportationTests
{
    [Fact]
    public void Update_and_CopyFrom_keep_road_and_transportation()
    {
        var condo = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now)
            .AddCondoProperty().CondoDetail!;

        condo.Update(roadPassInFrontOfLand: "Sukhumvit Rd", transportationAccessType: ["01", "99"],
            transportationAccessTypeOther: "Ferry");

        var copy = CondoAppraisalDetail.CopyFrom(condo, Guid.NewGuid());

        foreach (var c in new[] { condo, copy })
        {
            Assert.Equal("Sukhumvit Rd", c.RoadPassInFrontOfLand);
            Assert.Equal(["01", "99"], c.TransportationAccessType);
            Assert.Equal("Ferry", c.TransportationAccessTypeOther);
        }

        Assert.NotSame(condo.TransportationAccessType, copy.TransportationAccessType);
    }
}
