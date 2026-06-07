namespace Appraisal.Application.Features.Project.PreviewBlockReappraisalUnits;

/// <summary>
/// Dry-run preview: parses the Excel and classifies each working-copy unit into one of four
/// mutually exclusive status buckets without writing anything to the database.
/// </summary>
public record PreviewBlockReappraisalUnitsCommand(
    Guid AppraisalId,
    Stream FileStream,
    string FileName
) : ICommand<PreviewBlockReappraisalUnitsResult>;
