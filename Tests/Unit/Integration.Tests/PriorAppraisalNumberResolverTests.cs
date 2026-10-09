using FluentAssertions;
using Integration.Application.Services;
using NSubstitute;
using Request.Contracts.Requests.Dtos;
using Shared.Exceptions;

namespace Integration.Tests;

/// <summary>
/// LOS sends the prior appraisal by number. It becomes PrevAppraisalId for every purpose; an unknown or
/// not-Completed number is a 400 naming it; a 99A legacy number stays a number.
/// </summary>
public class PriorAppraisalNumberResolverTests
{
    private static RequestDetailDto Detail(Guid? id = null, string? number = null) =>
        new(false, null, id, null, null, null, null, number);

    private static IAppraisalLookupService Lookup(PriorAppraisalRef? result)
    {
        var lookup = Substitute.For<IAppraisalLookupService>();
        lookup.ResolvePriorAppraisalByNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(result);
        return lookup;
    }

    [Fact]
    public async Task Number_of_a_completed_appraisal_becomes_the_id()
    {
        var priorId = Guid.NewGuid();

        var resolved = await PriorAppraisalNumberResolver.ResolveAsync(
            Lookup(new PriorAppraisalRef(priorId, "Completed")), "02", Detail(number: " 69000001 "), TestContext.Current.CancellationToken);

        resolved!.PrevAppraisalId.Should().Be(priorId);
        PriorAppraisalNumberResolver.LegacyNumber(resolved).Should().BeNull();
    }

    [Fact]
    public async Task Unknown_number_is_rejected_naming_it()
    {
        var act = () => PriorAppraisalNumberResolver.ResolveAsync(Lookup(null), "02", Detail(number: "69000001"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestException>()).WithMessage("*69000001*");
    }

    [Fact]
    public async Task Number_of_an_appraisal_that_is_not_completed_is_rejected_naming_it()
    {
        var act = () => PriorAppraisalNumberResolver.ResolveAsync(
            Lookup(new PriorAppraisalRef(Guid.NewGuid(), "InProgress")), "02", Detail(number: "69000001"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestException>()).WithMessage("*69000001*");
    }

    [Fact]
    public async Task Legacy_99A_number_is_kept_as_a_number_without_a_lookup()
    {
        var lookup = Lookup(null);

        var resolved = await PriorAppraisalNumberResolver.ResolveAsync(lookup, "02", Detail(number: "99A0001234"), TestContext.Current.CancellationToken);

        resolved!.PrevAppraisalId.Should().BeNull();
        PriorAppraisalNumberResolver.LegacyNumber(resolved).Should().Be("99A0001234");
        await lookup.DidNotReceive().ResolvePriorAppraisalByNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Explicit_id_wins_over_a_number()
    {
        var id = Guid.NewGuid();
        var lookup = Lookup(null);

        var resolved = await PriorAppraisalNumberResolver.ResolveAsync(lookup, "02", Detail(id, "69000001"), TestContext.Current.CancellationToken);

        resolved!.PrevAppraisalId.Should().Be(id);
        await lookup.DidNotReceive().ResolvePriorAppraisalByNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_number_and_no_id_is_left_alone()
    {
        var detail = Detail();

        (await PriorAppraisalNumberResolver.ResolveAsync(Lookup(null), "02", detail, TestContext.Current.CancellationToken)).Should().BeSameAs(detail);
        (await PriorAppraisalNumberResolver.ResolveAsync(Lookup(null), "02", null, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task An_empty_guid_counts_as_no_id_so_the_number_is_resolved()
    {
        var priorId = Guid.NewGuid();

        var resolved = await PriorAppraisalNumberResolver.ResolveAsync(
            Lookup(new PriorAppraisalRef(priorId, "Completed")), "02", Detail(Guid.Empty, "69000001"),
            TestContext.Current.CancellationToken);

        resolved!.PrevAppraisalId.Should().Be(priorId);
    }

    [Fact]
    public async Task Purpose_07_is_not_looked_up_so_the_guard_error_wins()
    {
        var lookup = Lookup(null);
        var detail = Detail(number: "69000001");

        var resolved = await PriorAppraisalNumberResolver.ResolveAsync(
            lookup, "07", detail, TestContext.Current.CancellationToken);

        resolved.Should().BeSameAs(detail);
        await lookup.DidNotReceive().ResolvePriorAppraisalByNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_raw_B_prefixed_legacy_number_is_kept_normalised_without_a_lookup()
    {
        var lookup = Lookup(null);

        var resolved = await PriorAppraisalNumberResolver.ResolveAsync(
            lookup, "02", Detail(number: " b99a0001234 "), TestContext.Current.CancellationToken);

        PriorAppraisalNumberResolver.LegacyNumber(resolved).Should().Be("99A0001234");
        resolved!.PrevAppraisalNumber.Should().Be("99A0001234");
        await lookup.DidNotReceive().ResolvePriorAppraisalByNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_lookup_reads_the_number_the_way_the_reappraisal_flow_does()
    {
        var priorId = Guid.NewGuid();
        var lookup = Lookup(new PriorAppraisalRef(priorId, "Completed"));

        var resolved = await PriorAppraisalNumberResolver.ResolveAsync(
            lookup, "02", Detail(number: " b62a00645 "), TestContext.Current.CancellationToken);

        resolved!.PrevAppraisalId.Should().Be(priorId);
        await lookup.Received(1).ResolvePriorAppraisalByNumberAsync("62A00645", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("62A00645")]
    [InlineData(" b62a00645 ")]
    public async Task The_recorded_book_of_a_reappraisal_draft_is_kept_as_a_number_without_a_lookup(string sent)
    {
        var lookup = Lookup(null);

        var resolved = await PriorAppraisalNumberResolver.ResolveAsync(
            lookup, "03", Detail(number: sent), TestContext.Current.CancellationToken, recordedBookNumber: "62A00645");

        resolved!.PrevAppraisalId.Should().BeNull();
        resolved.PrevAppraisalNumber.Should().Be("62A00645");
        await lookup.DidNotReceive().ResolvePriorAppraisalByNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_number_that_is_not_the_recorded_book_is_still_looked_up()
    {
        var act = () => PriorAppraisalNumberResolver.ResolveAsync(
            Lookup(null), "03", Detail(number: "62A99999"), TestContext.Current.CancellationToken, recordedBookNumber: "62A00645");

        (await act.Should().ThrowAsync<BadRequestException>()).WithMessage("*62A99999*");
    }
}
