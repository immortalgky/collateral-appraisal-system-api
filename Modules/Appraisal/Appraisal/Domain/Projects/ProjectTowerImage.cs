namespace Appraisal.Domain.Projects;

/// <summary>
/// Photos attached to a project tower.
/// Child entity of ProjectTower (accessed via the Project aggregate root).
/// References gallery photos via GalleryPhotoId (from AppraisalGallery).
/// </summary>
public class ProjectTowerImage : Entity<Guid>
{
    public Guid ProjectTowerId { get; private set; }
    public Guid GalleryPhotoId { get; private set; }
    public int DisplaySequence { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public bool IsThumbnail { get; private set; }

    private ProjectTowerImage() { }

    internal static ProjectTowerImage Create(
        Guid projectTowerId,
        int displaySequence,
        Guid galleryPhotoId,
        string? title = null,
        string? description = null)
    {
        if (galleryPhotoId == Guid.Empty)
            throw new ArgumentException("GalleryPhotoId cannot be empty", nameof(galleryPhotoId));

        return new ProjectTowerImage
        {
            ProjectTowerId = projectTowerId,
            DisplaySequence = displaySequence,
            GalleryPhotoId = galleryPhotoId,
            Title = title,
            Description = description,
            IsThumbnail = false
        };
    }

    internal void UpdateDetails(string? title, string? description)
    {
        Title = title;
        Description = description;
    }

    internal void UpdateSequence(int newSequence)
    {
        DisplaySequence = newSequence;
    }

    internal void SetAsThumbnail()
    {
        IsThumbnail = true;
    }

    internal void UnsetAsThumbnail()
    {
        IsThumbnail = false;
    }
}
