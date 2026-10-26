using Request.Application.Services;
using Request.Contracts.Requests.Dtos;
using Request.Domain.Requests;

namespace Request.Tests.Request.Services;

/// <summary>
/// How a draft save carries a stored legacy AS400 book ("99A…"): the form never sends the number back, so it
/// signals "cleared" by sending no value and no date.
/// </summary>
public class LegacyPriorBookTests
{
    private static readonly RequestDetail Stored = RequestDetail.Create(new RequestDetailData(
        false, null, null, null, null, null, null, "99A0001234", 1m, new DateTime(2026, 1, 1)));

    private static RequestDetailDto Incoming(string? number, decimal? value = null, DateTime? date = null) =>
        new(false, null, null, null, null, null, null, number, value, date);

    [Fact]
    public void Sending_the_stored_number_with_an_empty_value_and_date_keeps_the_book()
    {
        var kept = LegacyPriorBook.ForUpdate("03", Incoming("99A0001234"), Stored);

        Assert.Equal(("99A0001234", 1m, new DateTime(2026, 1, 1)), kept);
    }

    [Fact]
    public void Sending_nothing_clears_the_book()
    {
        var cleared = LegacyPriorBook.ForUpdate("03", Incoming(null), Stored);

        Assert.Equal((null, null, null), cleared);
    }

    [Fact]
    public void Sending_the_value_and_date_back_keeps_the_book()
    {
        var kept = LegacyPriorBook.ForUpdate("03", Incoming(null, 1m, new DateTime(2026, 1, 1)), Stored);

        Assert.Equal("99A0001234", kept.Number);
    }
}
