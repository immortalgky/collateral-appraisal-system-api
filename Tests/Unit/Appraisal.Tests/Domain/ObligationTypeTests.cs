using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Appraisal.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Obligation is a multi-select (ObligationType, stored as a JSON array in nvarchar(500)) with its own
/// MortgageDetails; ObligationDetails now means "other obligations". Lease agreements gain two yes/no answers.
/// </summary>
public class ObligationTypeTests
{
    private static AppraisalProperty NewProperty(Func<AppraisalAggregate, AppraisalProperty> add)
        => add(AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now));

    [Fact]
    public void Land_Update_and_CopyFrom_keep_the_obligation_list_in_a_new_list()
    {
        var land = NewProperty(a => a.AddLandProperty()).LandDetail!;

        land.Update(obligationType: ["02", "03"], mortgageDetails: "Bank A", obligationDetails: "30y lease");
        var copy = LandAppraisalDetail.CopyFrom(land, Guid.NewGuid());

        foreach (var l in new[] { land, copy })
        {
            Assert.Equal(["02", "03"], l.ObligationType);
            Assert.Equal("Bank A", l.MortgageDetails);
            Assert.Equal("30y lease", l.ObligationDetails);
        }

        Assert.NotSame(land.ObligationType, copy.ObligationType);
    }

    [Fact]
    public void Building_Update_and_CopyFrom_keep_the_obligation_list_in_a_new_list()
    {
        var building = NewProperty(a => a.AddBuildingProperty()).BuildingDetail!;

        building.Update(obligationType: ["02"], mortgageDetails: "Bank B");
        var copy = BuildingAppraisalDetail.CopyFrom(building, Guid.NewGuid());

        Assert.Equal(["02"], copy.ObligationType);
        Assert.Equal("Bank B", copy.MortgageDetails);
        Assert.NotSame(building.ObligationType, copy.ObligationType);
    }

    [Fact]
    public void Condo_Update_and_CopyFrom_keep_the_obligation_list_in_a_new_list()
    {
        var condo = NewProperty(a => a.AddCondoProperty()).CondoDetail!;

        condo.Update(obligationType: ["99"], obligationDetails: "Other");
        var copy = CondoAppraisalDetail.CopyFrom(condo, Guid.NewGuid());

        Assert.Equal(["99"], copy.ObligationType);
        Assert.Equal("Other", copy.ObligationDetails);
        Assert.Null(copy.MortgageDetails);
        Assert.NotSame(condo.ObligationType, copy.ObligationType);
    }

    [Fact]
    public void Condo_Update_and_CopyFrom_keep_IsRentedOut()
    {
        var condo = NewProperty(a => a.AddCondoProperty()).CondoDetail!;

        condo.Update(isRentedOut: true);
        Assert.True(CondoAppraisalDetail.CopyFrom(condo, Guid.NewGuid()).IsRentedOut);

        condo.Update(isRentedOut: false);
        Assert.False(CondoAppraisalDetail.CopyFrom(condo, Guid.NewGuid()).IsRentedOut);
    }

    [Fact]
    public void Lease_agreement_Update_sets_the_two_answers_and_keeps_them_when_not_resent()
    {
        var lease = LeaseAgreementDetail.Create(Guid.NewGuid());

        lease.Update(isLeaseRegistered: true, isLeaseholdPledgeable: false);
        lease.Update(lesseeName: "someone"); // a patch that does not mention them

        Assert.True(lease.IsLeaseRegistered);
        Assert.False(lease.IsLeaseholdPledgeable);

        var copy = LeaseAgreementDetail.CopyFrom(lease, Guid.NewGuid());
        Assert.True(copy.IsLeaseRegistered);
        Assert.False(copy.IsLeaseholdPledgeable);
    }

    [Theory]
    [InlineData(typeof(LandAppraisalDetail))]
    [InlineData(typeof(BuildingAppraisalDetail))]
    [InlineData(typeof(CondoAppraisalDetail))]
    [InlineData(typeof(ProjectLand))]
    [InlineData(typeof(ProjectTower))]
    public void ObligationType_is_a_json_array_in_nvarchar_500_and_round_trips(Type entity)
    {
        using var db = new AppraisalDbContext(
            new DbContextOptionsBuilder<AppraisalDbContext>().UseSqlServer("Server=.;Database=x").Options);
        var property = db.Model.FindEntityType(entity)!.FindProperty("ObligationType")!;
        var converter = property.GetValueConverter()!;

        Assert.Equal("nvarchar(500)", property.GetColumnType());
        Assert.Equal("[\"02\",\"03\"]", converter.ConvertToProvider(new List<string> { "02", "03" }));
        Assert.Equal(["02", "03"], (List<string>)converter.ConvertFromProvider("[\"02\",\"03\"]")!);
        Assert.Null(converter.ConvertFromProvider(" "));
        Assert.Equal(["01"], (List<string>)converter.ConvertFromProvider("01")!); // a bare legacy code reads as a one-item list, never throws
    }
}
