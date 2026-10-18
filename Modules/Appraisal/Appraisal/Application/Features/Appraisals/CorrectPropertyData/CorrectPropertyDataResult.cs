namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

/// <summary>
/// Outcome of a correction. <see cref="ChangedFields"/> are the diff paths that were written to the audit
/// trail (<c>Land.OwnerName</c>, <c>Land.Titles[#1234].Rai</c>, ...).
/// </summary>
public record CorrectPropertyDataResult(int ChangedFieldCount, IReadOnlyList<string> ChangedFields);
