using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Domain;

/// <summary>The land's สภาพแวดล้อม (Environment parameter codes + other text) survives Update and a re-appraisal copy, as on the condo.</summary>
public class LandEnvironmentTests
{
    [Fact]
    public void Update_and_CopyFrom_keep_environment_type_and_other()
    {
        var land = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now)
            .AddLandProperty().LandDetail!;

        land.Update(environmentType: ["06", "99"], environmentTypeOther: "สวนผลไม้");

        var copy = LandAppraisalDetail.CopyFrom(land, Guid.NewGuid());

        foreach (var l in new[] { land, copy })
        {
            Assert.Equal(["06", "99"], l.EnvironmentType);
            Assert.Equal("สวนผลไม้", l.EnvironmentTypeOther);
        }

        Assert.NotSame(land.EnvironmentType, copy.EnvironmentType);
    }

    [Fact]
    public void The_block_projects_land_keeps_environment_type_and_other()
    {
        var land = ProjectLand.Create(Guid.NewGuid());

        land.Update(environmentType: ["01", "99"], environmentTypeOther: "ริมแม่น้ำ");

        Assert.Equal(["01", "99"], land.EnvironmentType);
        Assert.Equal("ริมแม่น้ำ", land.EnvironmentTypeOther);
    }

    [Fact]
    public void Land_and_condo_keep_the_other_environment_problem_through_Update_and_CopyFrom()
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        var land = appraisal.AddLandProperty().LandDetail!;
        var condo = appraisal.AddCondoProperty().CondoDetail!;

        land.Update(hasOtherEnvironmentProblem: true, otherEnvironmentProblemRemark: "ฝุ่นจากการก่อสร้าง");
        condo.Update(hasOtherEnvironmentProblem: false, otherEnvironmentProblemRemark: null);

        foreach (var l in new[] { land, LandAppraisalDetail.CopyFrom(land, Guid.NewGuid()) })
        {
            Assert.True(l.HasOtherEnvironmentProblem);
            Assert.Equal("ฝุ่นจากการก่อสร้าง", l.OtherEnvironmentProblemRemark);
        }

        foreach (var c in new[] { condo, CondoAppraisalDetail.CopyFrom(condo, Guid.NewGuid()) })
        {
            Assert.False(c.HasOtherEnvironmentProblem);
            Assert.Null(c.OtherEnvironmentProblemRemark);
        }
    }

    [Fact]
    public void Land_and_block_land_keep_the_location_remark()
    {
        var land = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now).AddLandProperty().LandDetail!;
        var project = ProjectLand.Create(Guid.NewGuid());

        land.Update(isLandLocationVerified: false, landLocationRemark: "ตำแหน่งไม่ตรงกับแผนที่");
        project.Update(isLandLocationVerified: false, landLocationRemark: "ตำแหน่งไม่ตรงกับแผนที่");

        foreach (var l in new[] { land, LandAppraisalDetail.CopyFrom(land, Guid.NewGuid()) })
            Assert.Equal("ตำแหน่งไม่ตรงกับแผนที่", l.LandLocationRemark);
        Assert.Equal("ตำแหน่งไม่ตรงกับแผนที่", project.LandLocationRemark);
    }

    [Fact]
    public void Condo_keeps_the_location_remark_through_Update_and_CopyFrom()
    {
        var condo = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now).AddCondoProperty().CondoDetail!;

        condo.Update(locationType: "2", locationRemark: "ห้องอยู่คนละชั้นกับแผนผัง");

        foreach (var c in new[] { condo, CondoAppraisalDetail.CopyFrom(condo, Guid.NewGuid()) })
        {
            Assert.Equal("2", c.LocationType);
            Assert.Equal("ห้องอยู่คนละชั้นกับแผนผัง", c.LocationRemark);
        }
    }

    [Fact]
    public void Land_and_block_land_keep_the_eviction_remark()
    {
        var land = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now).AddLandProperty().LandDetail!;
        var project = ProjectLand.Create(Guid.NewGuid());

        land.Update(evictionType: ["05"], evictionRemark: "แนวท่อผ่านกลางแปลง");
        project.Update(evictionType: ["06"], evictionRemark: "แนวท่อผ่านกลางแปลง");

        foreach (var l in new[] { land, LandAppraisalDetail.CopyFrom(land, Guid.NewGuid()) })
            Assert.Equal("แนวท่อผ่านกลางแปลง", l.EvictionRemark);
        Assert.Equal("แนวท่อผ่านกลางแปลง", project.EvictionRemark);
    }
}
