namespace Appraisal.Domain.Projects;

/// <summary>
/// Tracks a CSV upload batch for project units (replaces CondoUnitUpload + VillageUnitUpload).
/// FK is ProjectId (not AppraisalId).
/// </summary>
public class ProjectUnitUpload : Entity<Guid>
{
    public Guid ProjectId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public DateTime UploadedAt { get; private set; }
    public bool IsUsed { get; private set; }
    public Guid? DocumentId { get; private set; }

    /// <summary>
    /// True when the batch was created by the system rather than uploaded by a person — today only
    /// the reappraisal seed, which copies the inventory out of the collateral master. Upload History
    /// shows it, so it has to be able to say so instead of presenting a file name nobody uploaded.
    /// </summary>
    public bool IsSystemGenerated { get; private set; }

    /// <summary>
    /// What this batch did to the project, recorded when it ran.
    ///
    /// The re-match endpoint has always returned these four numbers and the screen has always shown
    /// them in a toast that disappears. A day later nobody can say what a given file changed — which
    /// is exactly the question asked when a price looks wrong. Storing them turns Upload History from
    /// a list of file names into a record of what happened.
    ///
    /// <see cref="AddedUnits"/> counts every path (a seed and a first-round replace both add their
    /// whole set); the other three are re-match only and stay null elsewhere, so "did not apply"
    /// stays distinguishable from "applied to nothing".
    /// </summary>
    public int AddedUnits { get; private set; }

    public int? MatchedUnsoldUnits { get; private set; }
    public int? AutoSoldUnits { get; private set; }
    public int? UpdatedUnits { get; private set; }


    private ProjectUnitUpload()
    {
    }

    public static ProjectUnitUpload Create(
        Guid projectId, string fileName, Guid? documentId = null, bool isSystemGenerated = false)
    {
        return new ProjectUnitUpload
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            FileName = fileName,
            UploadedAt = DateTime.Now,
            IsUsed = false,
            DocumentId = documentId,
            IsSystemGenerated = isSystemGenerated
        };
    }

    /// <summary>Records how many units the batch brought in — seed, replace or append alike.</summary>
    public void RecordAdded(int addedUnits) => AddedUnits = addedUnits;

    /// <summary>Records the full re-match outcome, including the units it appended.</summary>
    public void RecordRematchOutcome(int matchedUnsold, int autoSold, int added, int updated)
    {
        MatchedUnsoldUnits = matchedUnsold;
        AutoSoldUnits = autoSold;
        AddedUnits = added;
        UpdatedUnits = updated;
    }

    public void MarkAsUsed()
    {
        IsUsed = true;
    }

    public void MarkAsUnused()
    {
        IsUsed = false;
    }
}
