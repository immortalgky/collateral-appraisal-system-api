using FluentAssertions;
using Reporting.Application.Providers;

namespace Reporting.Tests;

/// <summary>
/// The "อ้างอิง ตรวจครั้งที่" of the construction inspection summary names the PRIOR inspection's round.
/// It must not be derived from this appraisal's (editable) round, and is blank unless the prior is an inspection.
/// </summary>
public class ConstructionInspectionReferenceRoundTests
{
    [Fact]
    public void Prior_that_is_an_appraisal_report_prints_no_reference_round()
    {
        var (reference, current) = AppraisalSummaryConstructionDataProvider.ResolveRounds(
            isPriorProgressive: false, priorStamped: null, stamped: 2, chainCount: 0);

        reference.Should().BeNull();
        current.Should().Be("2");
    }

    [Fact]
    public void Progressive_prior_prints_its_own_stored_round()
    {
        var (reference, current) = AppraisalSummaryConstructionDataProvider.ResolveRounds(
            isPriorProgressive: true, priorStamped: 1, stamped: 5, chainCount: 3);

        reference.Should().Be("1");
        current.Should().Be("5");
    }

    [Fact]
    public void Non_progressive_prior_with_a_stored_round_still_prints_no_reference_round()
    {
        var (reference, current) = AppraisalSummaryConstructionDataProvider.ResolveRounds(
            isPriorProgressive: false, priorStamped: 2, stamped: 3, chainCount: 1);

        reference.Should().BeNull();
        current.Should().Be("3");
    }

    [Fact]
    public void Progressive_prior_without_a_stored_round_falls_back_to_the_chain_count()
    {
        var (reference, current) = AppraisalSummaryConstructionDataProvider.ResolveRounds(
            isPriorProgressive: true, priorStamped: null, stamped: null, chainCount: 2);

        reference.Should().Be("2");
        current.Should().Be("3");
    }

    [Fact]
    public void Chain_count_fallback_ignores_an_edited_current_round()
    {
        var (reference, current) = AppraisalSummaryConstructionDataProvider.ResolveRounds(
            isPriorProgressive: true, priorStamped: null, stamped: 5, chainCount: 1);

        reference.Should().Be("1");
        current.Should().Be("5");
    }

    [Fact]
    public void No_prior_prints_no_reference_round()
    {
        var (reference, current) = AppraisalSummaryConstructionDataProvider.ResolveRounds(
            isPriorProgressive: false, priorStamped: null, stamped: null, chainCount: null);

        reference.Should().BeNull();
        current.Should().Be("1");
    }
}
