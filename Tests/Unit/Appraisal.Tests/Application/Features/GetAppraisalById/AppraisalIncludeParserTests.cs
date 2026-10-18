using Appraisal.Application.Features.Appraisals.GetAppraisalById;
using Shared.Exceptions;

namespace Appraisal.Tests.Application.Features.GetAppraisalById;

public class AppraisalIncludeParserTests
{
    [Theory]
    [InlineData(null, AppraisalInclude.None)]
    [InlineData("", AppraisalInclude.None)]
    [InlineData("  ", AppraisalInclude.None)]
    [InlineData("request", AppraisalInclude.Request)]
    [InlineData("documents", AppraisalInclude.Documents)]
    [InlineData("request,documents", AppraisalInclude.Request | AppraisalInclude.Documents)]
    [InlineData("documents,request", AppraisalInclude.Request | AppraisalInclude.Documents)]
    [InlineData(" Request , DOCUMENTS ", AppraisalInclude.Request | AppraisalInclude.Documents)]
    [InlineData("request,request", AppraisalInclude.Request)]
    [InlineData("request,,documents,", AppraisalInclude.Request | AppraisalInclude.Documents)]
    public void Parses_the_comma_separated_values_in_any_case(string? value, AppraisalInclude expected) =>
        Assert.Equal(expected, AppraisalIncludeParser.Parse(value));

    [Theory]
    [InlineData("titles")]
    [InlineData("request,titles")]
    [InlineData("request documents")] // not comma separated: one unknown word
    public void An_unknown_value_is_a_400_naming_it(string value)
    {
        var ex = Assert.Throws<BadRequestException>(() => AppraisalIncludeParser.Parse(value));

        Assert.Contains("Unknown include", ex.Message);
    }
}
