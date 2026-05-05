namespace Appraisal.Domain.Projects;

/// <summary>
/// Photos attached to a project model.
/// Child entity of ProjectModel (accessed via the Project aggregate root).
/// References gallery photos via GalleryPhotoId (from AppraisalGallery).
/// </summary>
public class ProjectModelImage : Entity<Guid>
{
    public Guid ProjectModelId { get; private set; }
    public Guid GalleryPhotoId { get; private set; }
    public int DisplaySequence { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public bool IsThumbnail { get; private set; }

    private ProjectModelImage() { }

    internal static ProjectModelImage Create(
        Guid projectModelId,
        int displaySequence,
        Guid galleryPhotoId,
        string? title = null,
        string? description = null)
    {
        if (galleryPhotoId == Guid.Empty)
            throw new ArgumentException("GalleryPhotoId cannot be empty", nameof(galleryPhotoId));

        return new ProjectModelImage
        {
            ProjectModelId = projectModelId,
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
