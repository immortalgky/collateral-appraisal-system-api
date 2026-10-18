using Request.Application.Services;
using Request.Contracts.Requests.Dtos;

namespace Request.Tests.Request.Services;

/// <summary>An Integration resubmit replaces the request data: only a 99A number sent with no id is a legacy book.</summary>
public class LegacyPriorBookResubmitTests
{
    private static RequestDetailDto Detail(Guid? id = null, string? number = null, decimal? value = null) =>
        new(false, null, id, null, null, null, null, number, value, null);

    [Fact]
    public void Sent_99A_number_is_the_legacy_book_with_its_value()
    {
        var book = LegacyPriorBook.ForResubmit(Detail(number: " 99A0001234 ", value: 5m));

        Assert.Equal(("99A0001234", (decimal?)5m, (DateTime?)null), book);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("69000001")]
    public void Anything_else_is_no_legacy_book(string? number) =>
        Assert.Equal((null, null, null), LegacyPriorBook.ForResubmit(Detail(number: number)));

    [Fact]
    public void An_id_wins_over_a_99A_number() =>
        Assert.Equal((null, null, null), LegacyPriorBook.ForResubmit(Detail(Guid.NewGuid(), "99A0001234")));

    [Fact]
    public void Update_to_purpose_07_drops_a_legacy_book_even_when_it_has_no_value_or_date()
    {
        var stored = Domain.Requests.RequestDetail.Create(new Domain.Requests.RequestDetailData(
            false, null, null, null, null, null, null, "99A0001234", null, null));

        Assert.Equal((null, null, null), LegacyPriorBook.ForUpdate("07", Detail(), stored));
        // Any other purpose still keeps what is stored: the form sends back the same nulls whether or not it was cleared.
        Assert.Equal("99A0001234", LegacyPriorBook.ForUpdate("03", Detail(), stored).Number);
    }

    [Fact]
    public void An_empty_guid_does_not_hide_a_legacy_number() =>
        Assert.Equal("99A0001234", LegacyPriorBook.ForResubmit(Detail(Guid.Empty, "99A0001234")).Number);

    [Theory]
    [InlineData("99A0001234", true)]
    [InlineData("  99a0001234 ", true)]
    [InlineData("B99A0001234", true)] // AS400's raw form, 'B' dropped as CAS stores it
    [InlineData("b99a0001234", true)]
    [InlineData("62A00645", false)]
    [InlineData("B62A00645", false)]
    [InlineData("69000001", false)]
    [InlineData("B", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Legacy_books_are_recognised_in_the_forms_the_reappraisal_flow_uses(string? number, bool legacy) =>
        Assert.Equal(legacy, Domain.Requests.Request.IsLegacyPriorBook(number));

    [Theory]
    [InlineData("B62A00645", "62A00645")]
    [InlineData(" b99a1 ", "99A1")]
    [InlineData("B", "B")]
    [InlineData("69000001", "69000001")]
    public void Book_numbers_are_normalised_like_Collateral_As400AppraisalNumber(string raw, string expected) =>
        Assert.Equal(expected, Domain.Requests.Request.NormalizeBookNumber(raw));

    [Fact]
    public void A_raw_B_prefixed_legacy_number_is_stored_normalised() =>
        Assert.Equal("99A0001234", LegacyPriorBook.ForResubmit(Detail(number: "B99A0001234")).Number);
}
