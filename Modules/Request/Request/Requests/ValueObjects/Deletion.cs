namespace Request.Requests.ValueObjects;

public class Deletion : ValueObject
{
    public bool IsDeleted { get; }
    public DateTime? DeletedOn { get; }
    public string? DeletedBy { get; }

    private Deletion(bool isDeleted, DateTime? deletedOn, string? deletedBy)
    {
        IsDeleted = isDeleted;
        DeletedOn = deletedOn;
        DeletedBy = deletedBy;
    }

    public static Deletion NotDeleted() => new Deletion(false, null, null);
    public static Deletion Deleted(string id) => new Deletion(true, DateTime.Now, id);
}
