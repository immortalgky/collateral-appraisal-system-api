using Appraisal.Contracts.Appraisals;
using MediatR;
using NSubstitute;
using Request.Application.Services;
using Shared.Exceptions;

namespace Request.Tests.Request.Services;

/// <summary>
/// The purpose x prior-appraisal matrix: 02,03,04,05,06,08,09,11,12,13 require one, 07 forbids one,
/// everything else is optional; an id must exist and be Completed; a number with no id is a legacy
/// AS400 book (99A…) and is accepted without a Completed check; any other number alone is free text and
/// satisfies nothing on a purpose that requires a prior.
/// </summary>
public class PriorAppraisalSubmissionGuardTests
{
    private static readonly Guid PriorId = Guid.NewGuid();

    public static TheoryData<string> RequiredPurposes =>
        ["02", "03", "04", "05", "06", "08", "09", "11", "12", "13"];

    public static TheoryData<string> IdRequiredPurposes => ["06", "11", "12"];

    public static TheoryData<string> LegacyAllowedPurposes => ["02", "03", "04", "05", "08", "09", "13"];

    public static TheoryData<string> OptionalPurposes => ["01", "10", "14"];

    private static ISender MediatorReturning(string? status, bool exists = true)
    {
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>())
            .Returns(exists ? new AppraisalReferenceResult("69000001", 1m, null, status) : null);
        return mediator;
    }

    private static Task Run(string purpose, Guid? id, string? number, ISender mediator) =>
        PriorAppraisalSubmissionGuard.EnsureValidAsync(purpose, id, number, mediator, TestContext.Current.CancellationToken);

    [Theory]
    [MemberData(nameof(RequiredPurposes))]
    public async Task Required_purpose_without_prior_is_rejected(string purpose)
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Run(purpose, null, null, MediatorReturning("Completed")));
        Assert.Contains("required", ex.Message);
    }

    [Theory]
    [MemberData(nameof(RequiredPurposes))]
    public async Task Required_purpose_with_completed_id_passes(string purpose) =>
        await Run(purpose, PriorId, "69000001", MediatorReturning("Completed"));

    [Theory]
    [MemberData(nameof(RequiredPurposes))]
    public async Task Required_purpose_with_not_completed_id_is_rejected(string purpose)
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Run(purpose, PriorId, null, MediatorReturning("InProgress")));
        Assert.Contains("completed", ex.Message);
        Assert.Contains("69000001", ex.Message); // the resolved reference's number
    }

    [Theory]
    [MemberData(nameof(RequiredPurposes))]
    public async Task Required_purpose_with_unknown_id_is_rejected(string purpose)
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => Run(purpose, PriorId, null, MediatorReturning(null, exists: false)));
        Assert.Contains("not found", ex.Message);
        Assert.Contains(PriorId.ToString(), ex.Message);
    }

    [Theory]
    [MemberData(nameof(LegacyAllowedPurposes))]
    public async Task Legacy_number_passes_without_a_lookup_where_an_id_is_not_needed(string purpose)
    {
        var mediator = MediatorReturning("InProgress");

        await Run(purpose, null, "99A0001234", mediator);

        await mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(RequiredPurposes))]
    public async Task Required_purpose_with_free_text_number_only_is_rejected_like_none(string purpose)
    {
        var mediator = MediatorReturning("Completed");

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Run(purpose, null, "69000001", mediator));

        Assert.Contains("required", ex.Message);
        await mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("99A0001234")]
    [InlineData(" 99a0001234 ")] // trimmed and upper-cased like the stored form
    [InlineData("B99A0001234")]  // the AS400 block prefix is dropped
    public async Task Required_purpose_with_a_99A_legacy_book_passes(string number)
    {
        await Run("03", null, number, MediatorReturning("InProgress"));
        await Run("09", null, number, MediatorReturning("InProgress"));
    }

    [Theory]
    [InlineData("03", "62A00645", "62A00645")]
    [InlineData("09", "62A00645", "B62A00645")] // AS400's spelling of the same book
    [InlineData("03", " b62a00645 ", "62A00645")]
    public async Task A_periodical_reappraisal_draft_with_a_non_99A_AS400_book_passes(string purpose, string number, string book)
    {
        var mediator = MediatorReturning("InProgress");

        await PriorAppraisalSubmissionGuard.EnsureValidAsync(
            purpose, null, number, mediator, TestContext.Current.CancellationToken, reappraisalBookNumber: book);

        await mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]          // staff cleared the prior: the recorded book no longer counts
    [InlineData("")]
    [InlineData("62A99999")]    // a different number typed over it
    public async Task A_recorded_book_counts_only_while_the_request_still_carries_it(string? number)
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => PriorAppraisalSubmissionGuard.EnsureValidAsync(
            "03", null, number, MediatorReturning("Completed"), TestContext.Current.CancellationToken,
            reappraisalBookNumber: "62A00645"));
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public async Task A_recorded_book_does_not_excuse_the_id_that_06_11_12_need()
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => PriorAppraisalSubmissionGuard.EnsureValidAsync(
            "06", null, "62A00645", MediatorReturning("Completed"), TestContext.Current.CancellationToken,
            reappraisalBookNumber: "62A00645"));
        Assert.Contains("in this system", ex.Message);
    }

    [Fact]
    public async Task Free_text_without_a_recorded_book_is_still_rejected_on_a_required_purpose() =>
        await Assert.ThrowsAsync<BadRequestException>(() => PriorAppraisalSubmissionGuard.EnsureValidAsync(
            "03", null, "62A00645", MediatorReturning("Completed"), TestContext.Current.CancellationToken,
            reappraisalBookNumber: null));

    [Theory]
    [MemberData(nameof(OptionalPurposes))]
    public async Task Optional_purpose_with_free_text_number_only_passes_without_a_lookup(string purpose)
    {
        var mediator = MediatorReturning("InProgress");

        await Run(purpose, null, "whatever the staff typed", mediator);

        await mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(IdRequiredPurposes))]
    public async Task Progressive_and_appeal_with_free_text_number_only_are_rejected_as_missing(string purpose)
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => Run(purpose, null, "69000001", MediatorReturning("Completed")));
        Assert.Contains("required", ex.Message);
    }

    [Theory]
    [MemberData(nameof(IdRequiredPurposes))]
    public async Task Progressive_and_appeal_need_the_id_a_number_alone_is_rejected(string purpose)
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => Run(purpose, null, "99A0001234", MediatorReturning("Completed")));
        Assert.Contains("in this system", ex.Message);
    }

    [Theory]
    [MemberData(nameof(RequiredPurposes))]
    public async Task An_empty_guid_counts_as_no_id(string purpose)
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => Run(purpose, Guid.Empty, null, MediatorReturning("Completed")));
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public async Task A_reference_the_caller_already_resolved_is_not_looked_up_again()
    {
        var mediator = MediatorReturning("InProgress");

        await PriorAppraisalSubmissionGuard.EnsureValidAsync(
            "02", PriorId, null, mediator, TestContext.Current.CancellationToken,
            new AppraisalReferenceResult("69000001", 1m, null, "Completed"));

        await mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Purpose_07_without_prior_passes() =>
        await Run("07", null, null, MediatorReturning("Completed"));

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "69000001")]
    [InlineData(false, "99A0001234")]
    [InlineData(true, "69000001")]
    public async Task Purpose_07_with_any_prior_is_rejected(bool withId, string? number) =>
        await Assert.ThrowsAsync<BadRequestException>(
            () => Run("07", withId ? PriorId : null, number, MediatorReturning("Completed")));

    [Theory]
    [MemberData(nameof(OptionalPurposes))]
    public async Task Optional_purpose_without_prior_passes(string purpose) =>
        await Run(purpose, null, null, MediatorReturning("Completed"));

    [Theory]
    [MemberData(nameof(OptionalPurposes))]
    public async Task Optional_purpose_with_not_completed_id_is_rejected(string purpose) =>
        await Assert.ThrowsAsync<BadRequestException>(
            () => Run(purpose, PriorId, null, MediatorReturning("InProgress")));

    [Theory]
    [MemberData(nameof(OptionalPurposes))]
    public async Task Optional_purpose_with_completed_id_passes(string purpose) =>
        await Run(purpose, PriorId, null, MediatorReturning("Completed"));

    [Fact]
    public async Task Null_purpose_is_ignored() =>
        await PriorAppraisalSubmissionGuard.EnsureValidAsync(null, null, null, MediatorReturning(null), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Not_found_names_the_number_when_known()
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => Run("02", PriorId, "69000009", MediatorReturning(null, exists: false)));
        Assert.Contains("69000009", ex.Message);
    }

    // ── construction inspection (06 / 11): the chain must have room for another round ─────────────────────

    private static ISender MediatorWithChain(ConstructionChainStatusResult? chain)
    {
        var mediator = MediatorReturning("Completed");
        mediator.Send(Arg.Any<GetConstructionChainStatusQuery>(), Arg.Any<CancellationToken>()).Returns(chain);
        return mediator;
    }

    private static ConstructionChainStatusResult Chain(
        string? blockReason,
        ConstructionChainProgress? construction = null,
        LaterInspectionRef? later = null,
        OpenInspectionRequestRef? open = null) =>
        new(PriorId,
            construction ?? new ConstructionChainProgress(ConstructionChainStatuses.InProgress, 40m, null, null, null, null),
            later, open, null, blockReason);

    [Theory]
    [InlineData("06")]
    [InlineData("11")]
    public async Task Construction_inspection_on_a_chain_with_room_passes(string purpose) =>
        await Run(purpose, PriorId, null, MediatorWithChain(Chain(null)));

    [Theory]
    [InlineData("06")]
    [InlineData("11")]
    public async Task A_chain_whose_construction_is_complete_is_rejected_with_the_inspection_that_finished_it(string purpose)
    {
        var done = new ConstructionChainProgress(
            ConstructionChainStatuses.Complete, 100m, Guid.NewGuid(), "69000012", 3, new DateTime(2026, 10, 21, 9, 30, 0));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            Run(purpose, PriorId, null, MediatorWithChain(Chain(ConstructionChainBlockReasons.ConstructionComplete, done))));

        Assert.Equal("Prior appraisal 69000001 is already 100% complete (appraisal 69000012, inspection #3, 21/10/2026).", ex.Message);
        Assert.Equal(ConstructionChainBlockReasons.ConstructionComplete, ex.Code);
    }

    [Fact]
    public async Task When_the_pick_itself_finished_the_building_its_number_is_not_repeated()
    {
        var done = new ConstructionChainProgress(
            ConstructionChainStatuses.Complete, 100m, PriorId, "69000001", 3, new DateTime(2026, 10, 21));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            Run("06", PriorId, null, MediatorWithChain(Chain(ConstructionChainBlockReasons.ConstructionComplete, done))));

        Assert.Equal("Prior appraisal 69000001 is already 100% complete (inspection #3, 21/10/2026).", ex.Message);
    }

    [Fact]
    public async Task A_pick_with_a_later_inspection_is_rejected_naming_it()
    {
        var later = new LaterInspectionRef(Guid.NewGuid(), "69000020", 4, new DateTime(2026, 8, 10));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            Run("06", PriorId, null, MediatorWithChain(Chain(ConstructionChainBlockReasons.LaterInspectionExists, later: later))));

        Assert.Equal("Prior appraisal 69000001 already has a later inspection (69000020).", ex.Message);
        Assert.Equal(ConstructionChainBlockReasons.LaterInspectionExists, ex.Code);
    }

    [Fact]
    public async Task A_chain_with_another_open_inspection_request_is_rejected_naming_it()
    {
        var open = new OpenInspectionRequestRef(Guid.NewGuid(), "REQ-000123-2569");

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            Run("11", PriorId, null, MediatorWithChain(Chain(ConstructionChainBlockReasons.OpenInspectionRequest, open: open))));

        Assert.Equal("Request REQ-000123-2569 is already inspecting this appraisal chain.", ex.Message);
        Assert.Equal(ConstructionChainBlockReasons.OpenInspectionRequest, ex.Code);
    }

    [Fact]
    public async Task The_chain_check_asks_about_the_picked_appraisal()
    {
        var mediator = MediatorWithChain(Chain(null));

        await Run("06", PriorId, null, mediator);

        await mediator.Received(1).Send(
            Arg.Is<GetConstructionChainStatusQuery>(q => q.AppraisalId == PriorId), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("02")]
    [InlineData("03")]
    [InlineData("09")]
    [InlineData("12")]
    [InlineData("01")]
    public async Task Other_purposes_never_consult_the_construction_chain(string purpose)
    {
        var mediator = MediatorWithChain(Chain(ConstructionChainBlockReasons.ConstructionComplete));

        await Run(purpose, PriorId, null, mediator);

        await mediator.DidNotReceive().Send(Arg.Any<GetConstructionChainStatusQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_prior_that_is_not_completed_is_rejected_before_the_chain_is_consulted()
    {
        var mediator = MediatorReturning("InProgress");

        await Assert.ThrowsAsync<BadRequestException>(() => Run("06", PriorId, null, mediator));

        await mediator.DidNotReceive().Send(Arg.Any<GetConstructionChainStatusQuery>(), Arg.Any<CancellationToken>());
    }
}
