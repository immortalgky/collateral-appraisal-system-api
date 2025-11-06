namespace Request.Requests.ValueObjects;

public class Deletion : ValueObject
{
    public bool IsDeleted { get; }
    public DateTime? DeletedOn { get; }
    public long? DeletedBy { get; }

    private Deletion(bool isDeleted, DateTime? deletedOn, long? deletedBy)
    {
        IsDeleted = isDeleted;
        DeletedOn = deletedOn;
        DeletedBy = deletedBy;
    }

    public static Deletion NotDeleted() => new Deletion(false, null, null);
    public static Deletion Deleted(long id) => new Deletion(true, DateTime.UtcNow, id);
}
