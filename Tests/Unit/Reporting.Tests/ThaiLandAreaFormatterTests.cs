using FluentAssertions;
using Reporting.Application.Formatting;

namespace Reporting.Tests;

/// <summary>
/// Unit tests for <see cref="ThaiLandAreaFormatter"/>, focused on the square-wa component keeping its
/// fraction. It used to be cast to int, so a deed recorded as 630.50 sq.wa printed its rai-ngan-wa
/// triple as "1 - 2 - 30" while the absolute total in the very same sentence still read "630.5" —
/// and the triple is the half an appraiser reconciles against the title.
/// </summary>
public class ThaiLandAreaFormatterTests
{
    [Fact]
    public void Fractional_square_wa_survives_normalisation()
    {
        var total = ThaiLandAreaFormatter.NormalizeTotal(sumRai: 1m, sumNgan: 2m, sumSqWa: 30.5m);

        total.Rai.Should().Be(1);
        total.Ngan.Should().Be(2);
        total.Wa.Should().Be(30.5m);
        total.TotalSquareWa.Should().Be(630.5m);
    }

    [Fact]
    public void Both_halves_of_the_sentence_report_the_same_fraction()
    {
        var text = ThaiLandAreaFormatter.FormatTotal(sumRai: 1m, sumNgan: 2m, sumSqWa: 30.5m);

        text.Should().Be("1 - 2 - 30.5 ไร่ หรือ 630.5 ตารางวา");
    }

    [Fact]
    public void Whole_square_wa_prints_without_a_decimal_tail()
    {
        var text = ThaiLandAreaFormatter.FormatTotal(sumRai: 1m, sumNgan: 2m, sumSqWa: 30m);

        text.Should().Be("1 - 2 - 30 ไร่ หรือ 630 ตารางวา");
    }

    [Fact]
    public void Square_wa_beyond_a_hundred_still_carries_into_ngan_and_rai()
    {
        // 250.25 sq.wa = 2 ngan + 50.25 sq.wa; the 2 ngan then joins the 3 given, making 5 → 1 rai 1 ngan.
        var total = ThaiLandAreaFormatter.NormalizeTotal(sumRai: 0m, sumNgan: 3m, sumSqWa: 250.25m);

        total.Rai.Should().Be(1);
        total.Ngan.Should().Be(1);
        total.Wa.Should().Be(50.25m);
        total.TotalSquareWa.Should().Be(550.25m);
    }

    [Fact]
    public void A_remainder_that_rounds_up_to_a_full_ngan_carries_instead_of_printing_100()
    {
        // 99.996 rounds to 100.00 at two places. The carry is computed from the unrounded figure, so
        // without a correction afterwards the square-wa column printed a whole ngan: "0 - 0 - 100".
        var total = ThaiLandAreaFormatter.NormalizeTotal(sumRai: 0m, sumNgan: 0m, sumSqWa: 99.996m);

        total.Rai.Should().Be(0);
        total.Ngan.Should().Be(1);
        total.Wa.Should().Be(0m);
    }

    [Theory]
    // Every component can arrive fractional, and the triple has to reconcile with the total on the
    // same line in each case. These are the shapes two rounds of component-by-component patching got
    // wrong: a fraction in ngan combined with a wa remainder, and a fraction in rai.
    [InlineData(0, 2.5, 60, 0, 3, 10.0, 310.0)]      // used to print 0 - 2 - 110
    [InlineData(1.5, 0, 0, 1, 2, 0.0, 600.0)]        // used to print 1 - 0 - 0, losing 200 sq.wa
    [InlineData(0, 3, 250.25, 1, 1, 50.25, 550.25)]
    [InlineData(0, 0, 99.996, 0, 1, 0.0, 100.0)]
    [InlineData(1, 2, 30.5, 1, 2, 30.5, 630.5)]
    public void The_triple_always_reconciles_with_the_total(
        decimal rai, decimal ngan, decimal sqWa,
        int expectedRai, int expectedNgan, decimal expectedWa, decimal expectedTotal)
    {
        var total = ThaiLandAreaFormatter.NormalizeTotal(rai, ngan, sqWa);

        total.Rai.Should().Be(expectedRai);
        total.Ngan.Should().Be(expectedNgan);
        total.Wa.Should().Be(expectedWa);
        total.TotalSquareWa.Should().Be(expectedTotal);
        // The invariant the components exist to express.
        (total.Rai * 400m + total.Ngan * 100m + total.Wa).Should().Be(total.TotalSquareWa);
    }

    [Fact]
    public void A_fractional_ngan_carries_down_into_square_wa_instead_of_being_truncated()
    {
        // AreaNgan is decimal in the source rows, so half a ngan is real input. Casting it to int
        // dropped 50 sq.wa from the triple while the total on the same line still counted them —
        // the same half-a-sentence mismatch the square-wa fix is about, one component over.
        var total = ThaiLandAreaFormatter.NormalizeTotal(sumRai: 0m, sumNgan: 2.5m, sumSqWa: 0m);

        total.Rai.Should().Be(0);
        total.Ngan.Should().Be(2);
        total.Wa.Should().Be(50m);
        total.TotalSquareWa.Should().Be(250m);
    }

    [Fact]
    public void Zero_input_still_formats_rather_than_blanking()
    {
        // The formatter is pure — suppression is each caller's own policy, not its business.
        ThaiLandAreaFormatter.FormatTotal(0m, 0m, 0m).Should().Be("0 - 0 - 0 ไร่ หรือ 0 ตารางวา");
    }
}
