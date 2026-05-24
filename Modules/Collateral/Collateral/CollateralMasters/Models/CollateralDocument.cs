namespace Collateral.CollateralMasters.Models;

/// <summary>
/// Legal evidence document (title deed, lease contract, ownership paper, etc.)
/// attached directly to a CollateralMaster, independent of any appraisal.
/// Audit fields (CreatedAt/CreatedBy/UpdatedAt/UpdatedBy) are stamped automatically
/// by AuditableEntityInterceptor via IEntity.
/// </summary>
public class CollateralDocument : IEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid CollateralMasterId { get; private set; }

    /// <summary>One of <see cref="DocumentTypes"/> constants.</summary>
    public string DocumentType { get; private set; } = null!;

    /// <summary>FK to the Document module's document store.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Original file name captured at attachment time. Immutable post-upload.</summary>
    public string FileName { get; private set; } = null!;

    /// <summary>Optional free-text description. May be updated after attachment.</summary>
    public string? Description { get; private set; }

    /// <summary>False when soft-archived. Row is retained; FK to document store remains valid.</summary>
    public bool IsActive { get; private set; }

    // IEntity audit fields — stamped by AuditableEntityInterceptor.
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedWorkstation { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public string? UpdatedWorkstation { get; set; }

    private CollateralDocument() { }

    internal static CollateralDocument Create(
        Guid collateralMasterId,
        string documentType,
        Guid documentId,
        string fileName,
        string? description)
    {
        return new CollateralDocument
        {
            Id = Guid.CreateVersion7(),
            CollateralMasterId = collateralMasterId,
            DocumentType = documentType,
            DocumentId = documentId,
            FileName = fileName,
            Description = description,
            IsActive = true,
        };
    }

    /// <summary>
    /// Soft-archives the document. The row and DocumentId FK are preserved;
    /// only IsActive flips to false. Idempotent — calling on an already-archived row is a no-op.
    /// </summary>
    internal void Archive() => IsActive = false;
}
