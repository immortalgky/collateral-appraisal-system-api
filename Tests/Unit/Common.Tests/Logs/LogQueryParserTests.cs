using Common.Application.Features.Logs;
using FluentAssertions;

namespace Common.Tests.Logs;

public class LogQueryParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_BlankQuery_ReturnsNoWhereClause(string? query)
    {
        var result = LogQueryParser.Parse(query);
        result.WhereClause.Should().BeNull();
    }

    [Fact]
    public void Parse_BareWord_SearchesMessageAndException()
    {
        var result = LogQueryParser.Parse("timeout");

        result.WhereClause.Should().Be("(Message LIKE @q0 ESCAPE '\\' OR Exception LIKE @q0 ESCAPE '\\')");
        result.Parameters.Get<string>("q0").Should().Be("%timeout%");
    }

    [Fact]
    public void Parse_NegatedWord_FoldsThroughCaseInsteadOfPlainNot()
    {
        // Plain NOT(x) would still be UNKNOWN (not TRUE) when x is UNKNOWN — e.g. a NULL Message
        // and NULL Exception — silently dropping rows a negated search should include.
        var result = LogQueryParser.Parse("-timeout");

        result.WhereClause.Should().Be(
            "(CASE WHEN (Message LIKE @q0 ESCAPE '\\' OR Exception LIKE @q0 ESCAPE '\\') THEN 1 ELSE 0 END) = 0");
        result.Parameters.Get<string>("q0").Should().Be("%timeout%");
    }

    [Theory]
    [InlineData("-user:someone", "(CASE WHEN UserName = @q0 THEN 1 ELSE 0 END) = 0")]
    [InlineData("-path:/api/v1/requests",
        "(CASE WHEN COALESCE(RequestPath, JSON_VALUE(Properties,'$.Properties.RequestPath')) LIKE @q0 ESCAPE '\\' THEN 1 ELSE 0 END) = 0")]
    [InlineData("-source:WebhookDispatchConsumer",
        "(CASE WHEN COALESCE(SourceContext, JSON_VALUE(Properties,'$.Properties.SourceContext')) LIKE @q0 ESCAPE '\\' THEN 1 ELSE 0 END) = 0")]
    public void Parse_NegatedKeyClauses_AlsoFoldThroughCase(string query, string expectedWhereClause)
    {
        LogQueryParser.Parse(query).WhereClause.Should().Be(expectedWhereClause);
    }

    [Fact]
    public void Parse_QuotedPhrase_KeepsSpacesInOneTerm()
    {
        var result = LogQueryParser.Parse("\"connection refused\"");

        result.Parameters.Get<string>("q0").Should().Be("%connection refused%");
    }

    [Fact]
    public void Parse_MultipleTerms_AreAndedTogether()
    {
        var result = LogQueryParser.Parse("timeout -retry");

        result.WhereClause.Should().Contain(" AND ");
        result.Parameters.Get<string>("q0").Should().Be("%timeout%");
        result.Parameters.Get<string>("q1").Should().Be("%retry%");
    }

    [Fact]
    public void Parse_AppraisalKeyWithGuid_MatchesAppraisalIdColumn()
    {
        var guid = Guid.NewGuid().ToString();
        var result = LogQueryParser.Parse($"appraisal:{guid}");

        result.WhereClause.Should().Be("AppraisalId = @q0");
        result.Parameters.Get<string>("q0").Should().Be(guid);
    }

    [Fact]
    public void Parse_AppraisalKeyWithNumber_LooksUpAppraisalNumber()
    {
        var result = LogQueryParser.Parse("appraisal:APR-2026-0001");

        result.WhereClause.Should().Contain("SELECT CAST(Id AS nvarchar(64)) FROM appraisal.Appraisals WHERE AppraisalNumber = @q0");
        result.Parameters.Get<string>("q0").Should().Be("APR-2026-0001");
    }

    [Theory]
    [InlineData("request", "RequestId")]
    [InlineData("corr", "CorrelationId")]
    [InlineData("user", "UserName")]
    [InlineData("level", "Level")]
    public void Parse_KnownEqualsKey_MatchesTheRightColumn(string key, string column)
    {
        var result = LogQueryParser.Parse($"{key}:someone");

        result.WhereClause.Should().Be($"{column} = @q0");
        result.Parameters.Get<string>("q0").Should().Be("someone");
    }

    [Fact]
    public void Parse_PathKey_UsesContainsWithJsonFallback()
    {
        var result = LogQueryParser.Parse("path:/api/v1/requests");

        result.WhereClause.Should()
            .Be("COALESCE(RequestPath, JSON_VALUE(Properties,'$.Properties.RequestPath')) LIKE @q0 ESCAPE '\\'");
        result.Parameters.Get<string>("q0").Should().Be("%/api/v1/requests%");
    }

    [Fact]
    public void Parse_SourceKey_UsesContainsWithJsonFallback()
    {
        var result = LogQueryParser.Parse("source:WebhookDispatchConsumer");

        result.WhereClause.Should()
            .Be("COALESCE(SourceContext, JSON_VALUE(Properties,'$.Properties.SourceContext')) LIKE @q0 ESCAPE '\\'");
    }

    [Fact]
    public void Parse_BareGuid_OrsAcrossEveryIdColumn()
    {
        var guid = Guid.NewGuid().ToString();
        var result = LogQueryParser.Parse(guid);

        result.WhereClause.Should().Be(
            $"(CorrelationId = @q0 OR EntityId = @q0 OR AppraisalId = @q0 OR RequestId = @q0 OR WorkflowInstanceId = @q0 OR CollateralId = @q0 OR DocumentId = @q0)");
        result.Parameters.Get<string>("q0").Should().Be(guid);
    }

    [Fact]
    public void Parse_UnknownKey_IsTreatedAsPlainFreeTextIncludingTheColon()
    {
        var result = LogQueryParser.Parse("foo:bar");

        result.WhereClause.Should().Be("(Message LIKE @q0 ESCAPE '\\' OR Exception LIKE @q0 ESCAPE '\\')");
        result.Parameters.Get<string>("q0").Should().Be("%foo:bar%");
    }

    [Fact]
    public void Parse_FreeText_EscapesLikeWildcards()
    {
        var result = LogQueryParser.Parse("100%_done");

        result.Parameters.Get<string>("q0").Should().Be("%100\\%\\_done%");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("timeout", true)]
    [InlineData("\"connection refused\"", true)]
    [InlineData("foo:bar", true)] // unknown key falls through to free text
    [InlineData("path:/api/v1/requests", true)]
    [InlineData("source:WebhookDispatchConsumer", true)]
    public void Parse_ResidualPredicateTerms_SetHasResidualPredicateTrue(string? query, bool expected)
    {
        LogQueryParser.Parse(query).HasResidualPredicate.Should().Be(expected);
    }

    [Theory]
    [InlineData("request:someone")]
    [InlineData("corr:someone")]
    [InlineData("user:someone")]
    [InlineData("level:Error")]
    public void Parse_SargableEqualsKeys_DoNotSetHasResidualPredicate(string query)
    {
        LogQueryParser.Parse(query).HasResidualPredicate.Should().BeFalse();
    }

    [Fact]
    public void Parse_AppraisalKeyEitherForm_DoesNotSetHasResidualPredicate()
    {
        LogQueryParser.Parse($"appraisal:{Guid.NewGuid()}").HasResidualPredicate.Should().BeFalse();
        LogQueryParser.Parse("appraisal:APR-2026-0001").HasResidualPredicate.Should().BeFalse();
    }

    [Fact]
    public void Parse_BareGuid_DoesNotSetHasResidualPredicate()
    {
        LogQueryParser.Parse(Guid.NewGuid().ToString()).HasResidualPredicate.Should().BeFalse();
    }

    [Fact]
    public void Parse_LevelKey_SetsHasLevelKeyTrue()
    {
        LogQueryParser.Parse("level:Error").HasLevelKey.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("timeout")]
    [InlineData("request:someone")]
    [InlineData("corr:someone")]
    [InlineData("user:someone")]
    public void Parse_NoLevelKey_DoesNotSetHasLevelKey(string? query)
    {
        LogQueryParser.Parse(query).HasLevelKey.Should().BeFalse();
    }
}
