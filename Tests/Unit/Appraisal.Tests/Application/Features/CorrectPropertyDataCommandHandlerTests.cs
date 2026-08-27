using Appraisal.Application.Features.Appraisals.CorrectPropertyData;
using Appraisal.Domain.Appraisals;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Handler-level rules for <see cref="CorrectPropertyDataCommandHandler"/>.
///
/// Two of them exist to stop this endpoint from becoming a general-purpose write path:
///   * it only accepts CLOSED appraisals, so it can never be used to sidestep the validation that
///     governs in-flight work;
///   * it rejects a payload that changes nothing, so the audit trail never fills with rows saying
///     "someone corrected this and nothing happened".
///
/// The 403 case is not covered here: authorization is the endpoint's "appraisal.data-correction"
/// policy, which never reaches the handler.
/// </summary>
public class CorrectPropertyDataCommandHandlerTests
{
    private readonly IAppraisalRepository _repository = Substitute.For<IAppraisalRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private CorrectPropertyDataCommandHandler CreateHandler() => new(_repository, _currentUser);

    private static LandCorrection EmptyLand() => new();

    private static PropertyCorrectionData LandOnly(LandCorrection land) =>
        new(null, land, null, null, null, null, null, null, null);

    /// <summary>Builds a persisted-looking appraisal with one land property carrying an owner.</summary>
    private static (AppraisalAggregate Appraisal, Guid PropertyId) BuildAppraisal()
    {
        var appraisal = AppraisalAggregate.Create(
            requestId: Guid.NewGuid(),
            appraisalType: "New",
            priority: "Normal",
            now: new DateTime(2026, 1, 1));

        var property = appraisal.AddLandProperty();
        property.Id = Guid.NewGuid();
        property.LandDetail!.Update(ownerName: "Owner A");
        appraisal.ClearDomainEvents();

        return (appraisal, property.Id);
    }

    /// <summary>
    /// Drives the aggregate to Completed, the only status the correction path accepts. Uses the
    /// workflow sync entry point so the test does not depend on the committee-approval sequence.
    /// </summary>
    private static void MarkCompleted(AppraisalAggregate appraisal) =>
        appraisal.SyncStatusFromWorkflow(AppraisalStatus.Completed);

    private void GivenRepositoryReturns(AppraisalAggregate appraisal) =>
        _repository
            .GetByIdWithPropertiesAsync(appraisal.Id, Arg.Any<CancellationToken>())
            .Returns(appraisal);

    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_AppraisalNotFound_Throws()
    {
        _repository
            .GetByIdWithPropertiesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((AppraisalAggregate?)null);

        var command = new CorrectPropertyDataCommand(
            Guid.NewGuid(), Guid.NewGuid(), "reason", LandOnly(EmptyLand()));

        await Assert.ThrowsAsync<Appraisal.Domain.Appraisals.Exceptions.AppraisalNotFoundException>(
            () => CreateHandler().Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_AppraisalStillInProgress_ThrowsConflict()
    {
        var (appraisal, propertyId) = BuildAppraisal();
        // Freshly created appraisals are Pending — anything that is not Completed must be refused,
        // so the correction path cannot bypass workflow validation.
        GivenRepositoryReturns(appraisal);

        var command = new CorrectPropertyDataCommand(
            appraisal.Id, propertyId, "reason", LandOnly(EmptyLand() with { OwnerName = "Owner B" }));

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateHandler().Handle(command, TestContext.Current.CancellationToken));

        // Assert on the code, not the prose — the message is free to change.
        Assert.Equal("APPRAISAL_NOT_COMPLETED", exception.Code);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancelledAppraisal_IsRejected()
    {
        var (appraisal, propertyId) = BuildAppraisal();
        appraisal.Cancel("EMP999", new DateTime(2026, 2, 1), "customer withdrew");
        appraisal.ClearDomainEvents();
        GivenRepositoryReturns(appraisal);
        _currentUser.UserCode.Returns("EMP001");

        var command = new CorrectPropertyDataCommand(
            appraisal.Id, propertyId, "wrong owner", LandOnly(EmptyLand() with { OwnerName = "Owner B" }));

        // Cancelled work is abandoned — correcting it serves no purpose, so it stays read-only.
        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateHandler().Handle(command, TestContext.Current.CancellationToken));

        Assert.Equal("APPRAISAL_NOT_COMPLETED", exception.Code);
        Assert.Equal("Owner A", appraisal.Properties.Single().LandDetail!.OwnerName);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NothingChanged_ThrowsBadRequestAndDoesNotSave()
    {
        var (appraisal, propertyId) = BuildAppraisal();
        MarkCompleted(appraisal);
        appraisal.ClearDomainEvents();
        GivenRepositoryReturns(appraisal);
        _currentUser.UserCode.Returns("EMP001");

        var command = new CorrectPropertyDataCommand(
            appraisal.Id, propertyId, "reason", LandOnly(EmptyLand() with { OwnerName = "Owner A" }));

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateHandler().Handle(command, TestContext.Current.CancellationToken));

        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StampsUserCodeAsTheActor()
    {
        var (appraisal, propertyId) = BuildAppraisal();
        MarkCompleted(appraisal);
        appraisal.ClearDomainEvents();
        GivenRepositoryReturns(appraisal);
        _currentUser.UserCode.Returns("EMP042");
        _currentUser.Username.Returns("someone.else");

        var command = new CorrectPropertyDataCommand(
            appraisal.Id, propertyId, "wrong owner", LandOnly(EmptyLand() with { OwnerName = "Owner B" }));

        await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        var raised = Assert.Single(appraisal.DomainEvents);
        var corrected = Assert.IsType<Appraisal.Domain.Appraisals.Events.AppraisalPropertyCorrectedEvent>(raised);
        // UserCode is the canonical actor identifier across this codebase, not Username.
        Assert.Equal("EMP042", corrected.By);
    }

    [Fact]
    public async Task Handle_FallsBackToUsernameWhenUserCodeIsMissing()
    {
        var (appraisal, propertyId) = BuildAppraisal();
        MarkCompleted(appraisal);
        appraisal.ClearDomainEvents();
        GivenRepositoryReturns(appraisal);
        _currentUser.UserCode.Returns((string?)null);
        _currentUser.Username.Returns("fallback.user");

        var command = new CorrectPropertyDataCommand(
            appraisal.Id, propertyId, "wrong owner", LandOnly(EmptyLand() with { OwnerName = "Owner B" }));

        await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        var corrected = Assert.IsType<Appraisal.Domain.Appraisals.Events.AppraisalPropertyCorrectedEvent>(
            Assert.Single(appraisal.DomainEvents));
        Assert.Equal("fallback.user", corrected.By);
    }

    [Fact]
    public async Task Handle_ReturnsTheSameDiffThatWasAudited()
    {
        var (appraisal, propertyId) = BuildAppraisal();
        MarkCompleted(appraisal);
        appraisal.ClearDomainEvents();
        GivenRepositoryReturns(appraisal);
        _currentUser.UserCode.Returns("EMP001");

        var command = new CorrectPropertyDataCommand(
            appraisal.Id, propertyId, "wrong owner", LandOnly(EmptyLand() with { OwnerName = "Owner B" }));

        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        var corrected = Assert.IsType<Appraisal.Domain.Appraisals.Events.AppraisalPropertyCorrectedEvent>(
            Assert.Single(appraisal.DomainEvents));

        Assert.Equal(corrected.ChangedFields, result.ChangedFields);
        Assert.Equal(appraisal.Id, result.AppraisalId);
        Assert.Equal(propertyId, result.PropertyId);
    }
}
