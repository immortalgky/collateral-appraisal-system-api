using FluentAssertions;
using Shared.Messaging.Events;
using Shared.Messaging.Services;

namespace Shared.Tests.Messaging;

/// <summary>Unit tests for the shared type-resolution helper, independent of the delivery
/// service that consumes it. Same cases as the base outbox fix's
/// IntegrationEventNamespaceTests, using this codebase's <see cref="AssignmentSlaRecalculatedIntegrationEvent"/>
/// as the sample event.</summary>
public class IntegrationEventNamespaceTests
{
    [Fact]
    public void TryResolve_TypeInAllowedNamespace_ReturnsResolved()
    {
        var resolution = IntegrationEventNamespace.TryResolve(
            typeof(AssignmentSlaRecalculatedIntegrationEvent).AssemblyQualifiedName!, out var type);

        resolution.Should().Be(TypeResolution.Resolved);
        type.Should().Be(typeof(AssignmentSlaRecalculatedIntegrationEvent));
    }

    [Fact]
    public void TryResolve_TypeNameThatDoesNotExist_ReturnsUnresolvable()
    {
        var resolution = IntegrationEventNamespace.TryResolve(
            "Some.Missing.Type, SomeMissingAssembly", out var type);

        resolution.Should().Be(TypeResolution.Unresolvable);
        type.Should().BeNull();
    }

    [Fact]
    public void TryResolve_TypeOutsideAllowedNamespace_ReturnsDisallowed()
    {
        var resolution = IntegrationEventNamespace.TryResolve(
            typeof(string).AssemblyQualifiedName!, out var type);

        resolution.Should().Be(TypeResolution.Disallowed);
        type.Should().BeNull();
    }

    /// <summary>
    /// A malformed assembly-qualified name doesn't just return null from Type.GetType — it can
    /// throw (e.g. FileLoadException for an invalid assembly name) even with throwOnError: false.
    /// Verified empirically against .NET 9: Type.GetType("Foo, Bar, Baz, Qux, Quux", false) throws.
    /// That must be caught and treated the same as any other unresolvable type, not escape.
    /// </summary>
    [Fact]
    public void TryResolve_MalformedNameThatThrows_ReturnsUnresolvable()
    {
        var resolution = IntegrationEventNamespace.TryResolve("Foo, Bar, Baz, Qux, Quux", out var type);

        resolution.Should().Be(TypeResolution.Unresolvable);
        type.Should().BeNull();
    }
}
