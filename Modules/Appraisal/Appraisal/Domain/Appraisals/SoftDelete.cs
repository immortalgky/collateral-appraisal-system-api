namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value object for soft delete functionality.
/// </summary>
public class SoftDelete : ValueObject
{
    public bool IsDeleted { get; }
    public DateTime? DeletedOn { get; }
    public Guid? DeletedBy { get; }

    private SoftDelete(bool isDeleted, DateTime? deletedOn, Guid? deletedBy)
    {
        IsDeleted = isDeleted;
        DeletedOn = deletedOn;
        DeletedBy = deletedBy;
    }

    public static SoftDelete NotDeleted()
    {
        return new SoftDelete(false, null, null);
    }

    public static SoftDelete Deleted(Guid? deletedBy)
    {
        return new SoftDelete(true, DateTime.UtcNow, deletedBy);
    }

    public override string ToString()
    {
        return IsDeleted ? $"Deleted on {DeletedOn} by {DeletedBy}" : "Active";
    }
}