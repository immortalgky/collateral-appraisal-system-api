using Shared.Exceptions;

namespace Shared.DDD;

/// <summary>
/// A validation utility for domain rules that collects validation errors
/// and provides a fluent interface for validation checks.
/// </summary>
public class RuleCheck
{
    private readonly List<string> _errors = new();

    /// <summary>
    /// Gets the collection of validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();

    /// <summary>
    /// Indicates whether this validation is valid (has no errors).
    /// </summary>
    public bool IsValid => !_errors.Any();

    /// <summary>
    /// Indicates whether this validation is invalid (has errors).
    /// </summary>
    public bool IsInvalid => _errors.Any();

    /// <summary>
    /// Adds an error message to the validation.
    /// </summary>
    /// <param name="error">The error message to add.</param>
    /// <returns>This RuleCheck instance for method chaining.</returns>
    public RuleCheck AddError(string error)
    {
        if (!string.IsNullOrWhiteSpace(error)) _errors.Add(error);

        return this;
    }

    /// <summary>
    /// Conditionally adds an error message based on a condition.
    /// </summary>
    /// <param name="condition">If true, the error message will be added.</param>
    /// <param name="error">The error message to add if the condition is true.</param>
    /// <returns>This RuleCheck instance for method chaining.</returns>
    public RuleCheck AddErrorIf(bool condition, string error)
    {
        if (condition) AddError(error);

        return this;
    }

    /// <summary>
    /// Adds multiple error messages to the validation.
    /// </summary>
    /// <param name="errors">The collection of error messages to add.</param>
    /// <returns>This RuleCheck instance for method chaining.</returns>
    public RuleCheck AddErrors(IEnumerable<string> errors)
    {
        _errors.AddRange(errors.Where(e => !string.IsNullOrWhiteSpace(e)));
        return this;
    }

    /// <summary>
    /// Combines errors from another RuleCheck instance.
    /// </summary>
    /// <param name="other">The other RuleCheck to combine errors from.</param>
    /// <returns>This RuleCheck instance for method chaining.</returns>
    public RuleCheck Combine(RuleCheck other)
    {
        if (other != null) AddErrors(other.Errors);

        return this;
    }

    /// <summary>
    /// Throws a DomainException if this validation is invalid.
    /// </summary>
    /// <exception cref="DomainException">Thrown if IsInvalid is true.</exception>
    public void ThrowIfInvalid()
    {
        if (IsInvalid) throw new DomainException(string.Join("; ", _errors));
    }

    /// <summary>
    /// Creates a new valid RuleCheck instance.
    /// </summary>
    /// <returns>A new RuleCheck instance with no errors.</returns>
    public static RuleCheck Valid()
    {
        return new RuleCheck();
    }

    /// <summary>
    /// Creates a new invalid RuleCheck instance with a single error.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A new RuleCheck instance with the specified error.</returns>
    public static RuleCheck Invalid(string error)
    {
        return new RuleCheck().AddError(error);
    }

    /// <summary>
    /// Creates a new invalid RuleCheck instance with multiple errors.
    /// </summary>
    /// <param name="errors">The collection of error messages.</param>
    /// <returns>A new RuleCheck instance with the specified errors.</returns>
    public static RuleCheck Invalid(IEnumerable<string> errors)
    {
        return new RuleCheck().AddErrors(errors);
    }
}