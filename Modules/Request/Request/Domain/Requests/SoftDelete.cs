namespace Request.Domain.Requests;

public class SoftDelete : ValueObject
{
    public bool IsDeleted { get; }
    public DateTime? DeletedAt { get; }
    public string? DeletedBy { get; }

    private SoftDelete()
    {
        //EF Core
    }

    private SoftDelete(bool isDeleted, DateTime? deletedAt, string? deletedBy)
    {
        IsDeleted = isDeleted;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
    }

    public static SoftDelete NotDeleted => new(false, null, null);

    public static SoftDelete Create(bool isDeleted, DateTime? deletedAt, string? deletedBy)
    {
        return new SoftDelete(isDeleted, deletedAt, deletedBy);
    }

    public SoftDelete Delete(string deletedBy, DateTime deletedAt)
    {
        return new SoftDelete(true, deletedAt, deletedBy);
    }

    public SoftDelete Restore()
    {
        return NotDeleted;
    }
}