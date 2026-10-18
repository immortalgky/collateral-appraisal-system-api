using Appraisal.Application.Features.Appraisals.Shared;
using Dapper;

namespace Appraisal.Tests.Application.Appraisals;

/// <summary>
/// Appraisal numbers are {yy}{running:D6}; people type the tail. A digits-only term must therefore
/// match anywhere in the number, while any other term keeps the prefix/glob rules.
/// </summary>
public class AppraisalSearchPredicateNumericTermTests
{
    private static (string Sql, DynamicParameters P) Build(string term) =>
        AppraisalSearchPredicate.Build(term, "documents")!.Value;

    [Theory]
    [InlineData("105454", "%105454%")]
    [InlineData(" 5454 ", "%5454%")]
    [InlineData("๑๐๕๔๕๔", "%105454%")]        // Thai digits
    [InlineData("１０５４５４", "%105454%")]   // full-width digits
    public void Digits_only_term_is_a_substring_match(string term, string expected)
    {
        var (_, p) = Build(term);

        Assert.Equal(expected, p.Get<string>("NumberPattern"));
    }

    [Theory]
    [InlineData("REQ-105", "REQ-105%")]
    [InlineData("69a105", "69a105%")]
    [InlineData("*105", "%105")]          // explicit glob is untouched
    public void Non_numeric_term_stays_prefix_or_glob(string term, string expected)
    {
        var (_, p) = Build(term);

        Assert.Equal(expected, p.Get<string>("NumberPattern"));
    }

    [Fact]
    public void Only_the_appraisal_number_arm_uses_the_number_pattern()
    {
        var (sql, p) = Build("105454");

        Assert.Equal(1, sql.Split("@NumberPattern").Length - 1);
        Assert.Equal("105454%", p.Get<string>("SearchPattern"));   // every other arm: prefix, as before
    }

    [Fact]
    public void Minimum_length_still_applies()
    {
        Assert.Null(AppraisalSearchPredicate.Build("63", "documents"));
    }
}
