namespace Appraisal.Application.Features.Quotations.GetMyDraftsForAssembly;

/// <summary>
/// Returns a rich list of the calling admin's Draft quotations for the entry-modal picker.
/// The picker itself shows every draft with its BankingSegment set instead of pre-filtering.
/// </summary>
public record GetMyDraftsForAssemblyQuery : IQuery<GetMyDraftsForAssemblyResult>;
