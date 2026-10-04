using FluentAssertions;
using Integration.Application.Features.OutboxMessages;
using Shared.Messaging.Services;

namespace Integration.Tests;

/// <summary>
/// <c>failureClass</c> is classified from the error prefix AND what the row's EventType resolves to now. The
/// old delivery code wrote "Disallowed type:" for an unresolvable type too (it merged the two cases), so a
/// legacy row carrying that prefix must not be labelled <c>Disallowed</c> — the permanent don't-resend class —
/// unless its type really does resolve to a disallowed type.
/// </summary>
public class OutboxFailureClassTests
{
    private const string Failed = "Failed";

    [Fact]
    public void Classify_DisallowedPrefix_TypeReallyDisallowed_IsDisallowed() =>
        OutboxFailureClass.Classify(Failed, "Disallowed type: System.Guid", TypeResolution.Disallowed)
            .Should().Be(OutboxFailureClass.Disallowed);

    [Fact]
    public void Classify_LegacyDisallowedPrefix_TypeUnresolvable_IsUnresolvable() =>
        OutboxFailureClass.Classify(Failed, "Disallowed type: Some.Old.Event, Nowhere", TypeResolution.Unresolvable)
            .Should().Be(OutboxFailureClass.Unresolvable);

    /// <summary>The type now resolves to an allowed type (deployed since): Unresolvable, so the client reads
    /// <c>typeResolvable == true</c> next to it and shows "type now resolves" — a resend can help.</summary>
    [Fact]
    public void Classify_LegacyDisallowedPrefix_TypeNowResolves_IsUnresolvable() =>
        OutboxFailureClass.Classify(Failed, "Disallowed type: Some.Event, Asm", TypeResolution.Resolved)
            .Should().Be(OutboxFailureClass.Unresolvable);

    [Theory]
    [InlineData(TypeResolution.Resolved)]
    [InlineData(TypeResolution.Unresolvable)]
    [InlineData(TypeResolution.Disallowed)]
    public void Classify_UnresolvablePrefix_IsUnresolvableWhateverTheTypeResolvesToNow(TypeResolution resolution) =>
        OutboxFailureClass.Classify(Failed, "Unresolvable type: Some.Event, Asm", resolution)
            .Should().Be(OutboxFailureClass.Unresolvable);

    [Theory]
    [InlineData("Deserialization failed: bad json")]
    [InlineData("Deserialization returned null")]
    public void Classify_DeserializationPrefixes_AreDeserialization(string error) =>
        OutboxFailureClass.Classify(Failed, error, TypeResolution.Resolved)
            .Should().Be(OutboxFailureClass.Deserialization);

    [Fact]
    public void Classify_OtherError_IsNull() =>
        OutboxFailureClass.Classify(Failed, "System.Net.Http.HttpRequestException: refused", TypeResolution.Resolved)
            .Should().BeNull();

    [Theory]
    [InlineData("Pending")]
    [InlineData("Processed")]
    [InlineData("Processing")]
    public void Classify_NonFailedRow_IsNull(string status) =>
        OutboxFailureClass.Classify(status, "Disallowed type: X", TypeResolution.Disallowed).Should().BeNull();

    [Fact]
    public void Classify_NullError_IsNull() =>
        OutboxFailureClass.Classify(Failed, null, TypeResolution.Resolved).Should().BeNull();
}
