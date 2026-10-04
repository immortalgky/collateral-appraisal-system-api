using FluentAssertions;
using Integration.FailedMessages;

namespace Integration.Tests;

public class ExceptionTypeClassifierTests
{
    [Fact]
    public void IsNonTransient_FullNamespaceQualifiedName_MatchesBySimpleName()
    {
        ExceptionTypeClassifier.IsNonTransient("Shared.Exceptions.ConflictException").Should().BeTrue();
        ExceptionTypeClassifier.IsNonTransient("Collateral.CollateralMasters.Exceptions.MissingIdentityKeyException")
            .Should().BeTrue();
    }

    [Fact]
    public void IsNonTransient_BareShortName_StillMatches()
    {
        ExceptionTypeClassifier.IsNonTransient("ConflictException").Should().BeTrue();
    }

    [Fact]
    public void IsNonTransient_NestedTypeWithPlus_UsesTheNestedTypeName()
    {
        // Reflection full names use '+' for a nested type; the nested type's own name is what counts.
        ExceptionTypeClassifier.IsNonTransient("App.Foo+ConflictException").Should().BeTrue();
        ExceptionTypeClassifier.IsNonTransient("Some.Namespace.ConflictException+NestedDetail").Should().BeFalse(
            "the nested type is NestedDetail — an unrelated name nested inside a ConflictException's name");
    }

    [Fact]
    public void IsNonTransient_GenericArityBacktick_TruncatesAtBacktick()
    {
        ExceptionTypeClassifier.IsNonTransient("Some.Namespace.ConflictException`1").Should().BeTrue();
    }

    [Fact]
    public void IsNonTransient_UnrelatedException_ReturnsFalse()
    {
        ExceptionTypeClassifier.IsNonTransient("System.TimeoutException").Should().BeFalse();
    }

    [Theory]
    [InlineData("App.Foo+ConflictException", "ConflictException")]
    [InlineData("App.Outer`1+ConflictException", "ConflictException")]
    [InlineData("App.Foo+Bar+ConflictException", "ConflictException")]
    [InlineData("Some.Namespace.ConflictException`1", "ConflictException")]
    [InlineData(
        "Some.Namespace.ConflictException`1[[System.String, System.Private.CoreLib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]]",
        "ConflictException")]
    [InlineData(
        "Shared.Exceptions.ConflictException, Shared, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
        "ConflictException")]
    [InlineData("App.Foo+ConflictException, App, Version=1.2.3.4, Culture=neutral", "ConflictException")]
    [InlineData("ConflictException", "ConflictException")]
    public void SimpleName_StripsNestingGenericsAndAssemblyQualification(string exceptionType, string expected)
    {
        ExceptionTypeClassifier.SimpleName(exceptionType).Should().Be(expected);
        ExceptionTypeClassifier.IsNonTransient(exceptionType).Should().BeTrue();
    }
}
