namespace Appraisal.Application.Features.Quotations.GetMyDraftsForAssembly;

/// <summary>
/// Returns a rich list of the calling admin's Draft quotations for the entry-modal picker.
/// Optionally filtered by BankingSegment.
/// </summary>
public record GetMyDraftsForAssemblyQuery(
    string? BankingSegment = null
) : IQuery<GetMyDraftsForAssemblyResult>;
