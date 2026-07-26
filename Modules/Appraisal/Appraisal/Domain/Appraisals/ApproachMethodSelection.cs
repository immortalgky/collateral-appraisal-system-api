namespace Appraisal.Domain.Appraisals;

/// <summary>
/// One approach's primary-method choice, as supplied to
/// <see cref="PricingAnalysis.ApplySelection"/>. <paramref name="MethodId"/> must belong to
/// <paramref name="ApproachId"/> — the aggregate validates this before mutating anything.
/// </summary>
public readonly record struct ApproachMethodSelection(Guid ApproachId, Guid MethodId);
