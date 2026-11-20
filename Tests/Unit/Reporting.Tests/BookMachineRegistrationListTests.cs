using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Providers;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>The letter's เลขทะเบียนเครื่องจักร row: deduplicated, runs of consecutive numbers collapsed, one numbered entry per line.</summary>
public class BookMachineRegistrationListTests
{
    private static IEnumerable<string?> Run(string prefix, int from, int to, int width = 4) => Enumerable.Range(from, to - from + 1).Select(i => (string?)(prefix + i.ToString().PadLeft(width, '0')));

    [Fact]
    public void A_run_of_29_numbers_becomes_one_range()
    {
        MachineRegistrationList.Build(Run("51-414-701-", 1, 29)).Should().Equal("51-414-701-0001 ถึง 0029");
    }

    [Fact]
    public void Mixed_runs_and_singles_keep_their_order()
    {
        var input = Run("51-414-701-", 1, 3).Concat(["AB-77"]).Concat(Run("51-414-702-", 10, 11)).Concat(["51-414-702-0020"]);

        MachineRegistrationList.Build(input).Should().Equal("51-414-701-0001 ถึง 0003", "AB-77", "51-414-702-0010 ถึง 0011", "51-414-702-0020");
    }

    [Fact]
    public void A_gap_in_the_middle_makes_two_ranges()
    {
        var input = Run("M-", 1, 10).Concat(Run("M-", 12, 20));

        MachineRegistrationList.Build(input).Should().Equal("M-0001 ถึง 0010", "M-0012 ถึง 0020");
    }

    [Fact]
    public void Duplicates_blank_and_dash_values_are_dropped_after_trimming()
    {
        MachineRegistrationList.Build(["M-0001", " M-0001 ", "-", "", null, "  ", "M-0002", "M-0002"]).Should().Equal("M-0001 ถึง 0002");
    }

    [Fact]
    public void A_width_change_a_different_prefix_or_a_number_without_digits_ends_the_run()
    {
        MachineRegistrationList.Build(["M-98", "M-99", "M-100"]).Should().Equal("M-98 ถึง 99", "M-100");   // 99 -> 100 is a wider number
        MachineRegistrationList.Build(["M-1", "N-2", "ทะเบียนเก่า", "N-3"]).Should().Equal("M-1", "N-2", "ทะเบียนเก่า", "N-3");
        MachineRegistrationList.Build(["12", "13", "14"]).Should().Equal("12 ถึง 14");                 // a bare number has an empty prefix
    }

    [Fact]
    public void Numbers_out_of_sequence_order_are_not_collapsed()
    {
        MachineRegistrationList.Build(["M-0003", "M-0001", "M-0002"]).Should().Equal("M-0003", "M-0001 ถึง 0002");
    }

    [Fact]
    public void An_overflowing_digit_run_is_kept_as_written()
    {
        MachineRegistrationList.Build(["X-99999999999999999999999", "X-99999999999999999999999a"]).Should().HaveCount(2);
    }

    private static Task<string> LetterAsync(IReadOnlyList<string> entries) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync("{{ include 'partials/book-letter-external' }}", new AppraisalSummaryModel { IsExternal = true, MachineRegistrations = entries }, CancellationToken.None);

    [Fact]
    public async Task The_letter_prints_one_numbered_line_each_and_a_single_entry_has_no_number()
    {
        var many = await LetterAsync(["51-414-701-0001 ถึง 0029", "AB-77", "ZZ-1"]);
        var one = await LetterAsync(["AB-77"]);
        var none = await LetterAsync([]);

        many.Should().Contain("(1) 51-414-701-0001 ถึง 0029</div>").And.Contain("(2) AB-77</div>").And.Contain("(3) ZZ-1</div>");
        one.Should().Contain("<td class=\"kv-value\">AB-77</td>").And.NotContain("(1)");
        none.Should().NotContain("เลขทะเบียนเครื่องจักร");
    }

    // The registrations also sit in the เครื่องจักร block of รายละเอียดทรัพย์สิน on the external summary page.
    private static Task<string> PageAsync(IReadOnlyList<string> entries)
    {
        // 29 registrations collapse into one entry, so the count must come from the machine rows (here 3)
        var m = new AppraisalSummaryModel { IsExternal = true, MachineRegistrations = entries, MachineSection = entries.Count == 0 ? null : new Reporting.Application.Models.Sections.MachineSection { Machines = [new(), new(), new()] } };
        m.PropertyBlocks = ExternalSummaryRows.PropertyBlocks(m);
        return new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync("{{ include 'partials/book-ext-summary' }}", m, CancellationToken.None);
    }

    [Fact]
    public async Task Several_entries_print_one_numbered_line_each_and_a_single_entry_has_no_number()
    {
        var many = await PageAsync(["51-414-701-0001 ถึง 0029", "AB-77", "ZZ-1"]);
        var one = await PageAsync(["AB-77"]);
        var none = await PageAsync([]);

        many.Should().Contain("<b>เครื่องจักร</b> จำนวน 3 รายการ").And.Contain("<div class=\"pl\">(1) 51-414-701-0001 ถึง 0029</div>").And.Contain("<div class=\"pl\">(2) AB-77</div>").And.Contain("<div class=\"pl\">(3) ZZ-1</div>");
        one.Should().Contain("<div class=\"pl\">เลขทะเบียน AB-77</div>").And.NotContain("(1)");
        none.Should().NotContain("<b>เครื่องจักร</b>");
    }
}
