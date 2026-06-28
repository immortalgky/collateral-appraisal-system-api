using FluentAssertions;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;

namespace Reporting.Tests;

/// <summary>
/// Unit tests for the pure mapping in <see cref="ReportFilterResolver.BuildCriteria"/> — the
/// code->description resolution + filtering logic, without touching the database.
/// </summary>
public class ReportFilterResolverTests
{
    private static readonly IReadOnlyDictionary<(string Group, string Code), string> Descriptions =
        new Dictionary<(string, string), string>
        {
            [("FeePaymentMethod", "04")] = "Partial Payment",
            [("FeePaymentMethod", "05")] = "Full Payment",
            [("BankingSegment", "R")] = "Retail",
        };

    [Fact]
    public void Resolves_a_single_code_to_its_description()
    {
        var result = ReportFilterResolver.BuildCriteria(
            [new FilterField("Pay Type", "05", "FeePaymentMethod")], Descriptions);

        result.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FilterCriterion("Pay Type", "Full Payment"));
    }

    [Fact]
    public void Resolves_comma_separated_codes_and_joins_descriptions()
    {
        var result = ReportFilterResolver.BuildCriteria(
            [new FilterField("Pay Type", "04,05", "FeePaymentMethod")], Descriptions);

        result.Single().Value.Should().Be("Partial Payment, Full Payment");
    }

    [Fact]
    public void Falls_back_to_the_raw_code_when_unmapped()
    {
        var result = ReportFilterResolver.BuildCriteria(
            [new FilterField("Pay Type", "99", "FeePaymentMethod")], Descriptions);

        result.Single().Value.Should().Be("99");
    }

    [Fact]
    public void Skips_fields_with_a_blank_value()
    {
        var result = ReportFilterResolver.BuildCriteria(
        [
            new FilterField("Pay Type", null, "FeePaymentMethod"),
            new FilterField("Status", "   "),
            new FilterField("Appraisal No.", "AP-1"),
        ], Descriptions);

        result.Should().ContainSingle().Which.Label.Should().Be("Appraisal No.");
    }

    [Fact]
    public void Passes_through_free_text_and_dates_unchanged()
    {
        var result = ReportFilterResolver.BuildCriteria(
        [
            new FilterField("Created From", "2026-01-01"),
            new FilterField("Customer Name", "  ACME  "),
        ], Descriptions);

        result.Should().BeEquivalentTo(new[]
        {
            new FilterCriterion("Created From", "2026-01-01"),
            new FilterCriterion("Customer Name", "ACME"),
        }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Resolves_codes_case_insensitively_matching_the_SQL_collation()
    {
        // Production loads descriptions into a KeyComparer-backed dictionary; a filter value whose
        // casing differs from the seeded code must still resolve (the grid filter is CI too).
        var ci = new Dictionary<(string, string), string>(ReportFilterResolver.KeyComparer)
        {
            [("BankingSegment", "R")] = "Retail",
        };

        var result = ReportFilterResolver.BuildCriteria(
            [new FilterField("Retail/IBG", "r", "bankingsegment")], ci);

        result.Single().Value.Should().Be("Retail");
    }

    [Fact]
    public void Preserves_declared_order()
    {
        var result = ReportFilterResolver.BuildCriteria(
        [
            new FilterField("Retail/IBG", "R", "BankingSegment"),
            new FilterField("Pay Type", "05", "FeePaymentMethod"),
        ], Descriptions);

        result.Select(c => c.Label).Should().ContainInOrder("Retail/IBG", "Pay Type");
        result.Select(c => c.Value).Should().ContainInOrder("Retail", "Full Payment");
    }
}
