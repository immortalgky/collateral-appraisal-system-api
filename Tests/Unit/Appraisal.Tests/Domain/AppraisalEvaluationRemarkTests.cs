using Appraisal.Domain.Evaluations;

namespace Appraisal.Tests.Domain;

/// <summary>Per-criterion remarks: saved on Create and Update, trimmed, capped at 500 characters.</summary>
public class AppraisalEvaluationRemarkTests
{
    private static readonly DateTime Now = new(2026, 1, 1);

    private static AppraisalEvaluation NewPending(string? c1Remark = null, string? c5Remark = null) =>
        AppraisalEvaluation.Create(
            Guid.NewGuid(), "A-1", "Pending", null,
            3, 3, false, null, 3, 3, 3, null, null,
            c1Remark, null, null, null, c5Remark,
            Now);

    private static void UpdateRemarks(AppraisalEvaluation e, string? c1 = null, string? c2 = null, string? c3 = null) =>
        e.Update(3, 3, false, null, 3, 3, 3, null, null,
            c1, c2, c3, null, null,
            "Pending", null, Now);

    [Fact]
    public void Create_saves_remarks()
    {
        var e = NewPending("book", "coord");

        Assert.Equal("book", e.Criteria1Remark);
        Assert.Null(e.Criteria2Remark);
        Assert.Equal("coord", e.Criteria5Remark);
    }

    [Fact]
    public void Update_replaces_remarks()
    {
        var e = NewPending("old");

        UpdateRemarks(e, c1: null, c2: "late");

        Assert.Null(e.Criteria1Remark);
        Assert.Equal("late", e.Criteria2Remark);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_remark_becomes_null(string blank)
    {
        Assert.Null(NewPending(blank).Criteria1Remark);

        var e = NewPending("old");
        UpdateRemarks(e, c1: blank);
        Assert.Null(e.Criteria1Remark);
    }

    [Fact]
    public void Remark_is_trimmed()
    {
        Assert.Equal("abc", NewPending("  abc  ").Criteria1Remark);

        var e = NewPending();
        UpdateRemarks(e, c1: "  abc  ");
        Assert.Equal("abc", e.Criteria1Remark);
    }

    [Fact]
    public void Remark_of_500_chars_is_accepted_and_501_throws()
    {
        Assert.Equal(500, NewPending(new string('x', 500)).Criteria1Remark!.Length);

        Assert.Throws<ArgumentException>(() => NewPending(new string('x', 501)));

        var e = NewPending();
        Assert.Throws<ArgumentException>(() => UpdateRemarks(e, c3: new string('x', 501)));
    }

    [Fact]
    public void Remark_of_500_chars_with_surrounding_spaces_is_accepted_trimmed()
    {
        var padded = "  " + new string('x', 500) + "  ";

        Assert.Equal(500, NewPending(padded).Criteria1Remark!.Length);

        var e = NewPending();
        UpdateRemarks(e, c1: padded);
        Assert.Equal(500, e.Criteria1Remark!.Length);
    }
}
