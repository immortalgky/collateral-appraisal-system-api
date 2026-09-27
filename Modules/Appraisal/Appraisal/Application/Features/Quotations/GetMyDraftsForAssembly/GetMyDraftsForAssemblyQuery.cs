namespace Appraisal.Application.Features.Quotations.GetMyDraftsForAssembly;

/// <summary>
/// Returns a rich list of the calling admin's Draft quotations for the entry-modal picker.
/// picker itself now shows every draft with its live SegmentSet instead of pre-filtering.
/// </summary>
public record GetMyDraftsForAssemblyQuery(
    string? BankingSegment = null
) : IQuery<GetMyDraftsForAssemblyResult>;
