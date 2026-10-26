namespace Appraisal.Application.Features.Appraisals.Shared;

/// <summary>One field that matched, so the UI can say why a row is in the list.</summary>
public record SearchMatch(string Field, string Value);

/// <summary>One row of the arms <see cref="AppraisalSearchPredicate.Build"/> unions.</summary>
internal sealed class SearchMatchRow
{
    public Guid AppraisalId { get; set; }
    public int Rnk { get; set; }
    public string Fld { get; set; } = "";
    public string? Val { get; set; }

    public SearchMatch ToMatch() => new(Fld, Val ?? "");
}
