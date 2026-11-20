using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using Appraisal.Infrastructure.Converters;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Domain;

/// <summary>LandCheckMethodType (ตรวจสอบจาก) is a multi-select stored as a JSON array, but the column still holds bare codes and a few junk values.</summary>
public class TolerantCodeListConverterTests
{
    private static readonly TolerantCodeListConverter Converter = new();

    private static List<string>? FromDb(string? raw) => (List<string>?)Converter.ConvertFromProvider(raw);
    private static string? ToDb(List<string>? list) => (string?)Converter.ConvertToProvider(list);

    [Theory]
    [InlineData("01", new[] { "01" })]              // a bare code, as rows written before the multi-select hold
    [InlineData(" 02 ", new[] { "02" })]
    [InlineData("plot", new[] { "plot" })]          // junk the column already holds is kept, not thrown on
    [InlineData("Rawang", new[] { "Rawang" })]
    [InlineData("[\"01\",\"99\"]", new[] { "01", "99" })]
    [InlineData("[]", new string[0])]
    [InlineData("[not json", new[] { "[not json" })]
    public void Reads_a_bare_value_or_an_array(string raw, string[] expected)
        => Assert.Equal(expected, FromDb(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_reads_as_null(string? raw)
        => Assert.Null(FromDb(raw));

    [Fact]
    public void Always_writes_an_array_and_round_trips()
    {
        Assert.Equal("[\"01\",\"99\"]", ToDb(["01", "99"]));
        Assert.Null(ToDb(null));
        Assert.Equal(["01", "99"], FromDb(ToDb(["01", "99"])));
        Assert.Equal(["01"], FromDb(ToDb(FromDb("01")))); // a bare code read, then saved, is an array from then on
    }

    [Fact]
    public void Land_and_block_land_keep_the_check_methods_through_Update_and_CopyFrom()
    {
        var land = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now).AddLandProperty().LandDetail!;
        var project = ProjectLand.Create(Guid.NewGuid());

        land.Update(landCheckMethodType: ["01", "99"], landCheckMethodTypeOther: "ตรวจจากภาพถ่ายดาวเทียม");
        project.Update(landCheckMethodType: ["02"]);

        foreach (var l in new[] { land, LandAppraisalDetail.CopyFrom(land, Guid.NewGuid()) })
        {
            Assert.Equal(["01", "99"], l.LandCheckMethodType);
            Assert.Equal("ตรวจจากภาพถ่ายดาวเทียม", l.LandCheckMethodTypeOther);
        }

        Assert.NotSame(land.LandCheckMethodType, LandAppraisalDetail.CopyFrom(land, Guid.NewGuid()).LandCheckMethodType);
        Assert.Equal(["02"], project.LandCheckMethodType);
    }
}
