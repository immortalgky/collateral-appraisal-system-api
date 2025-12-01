using System;

namespace Request.Requests.ValueObjects;

public class SoftDelete
{
    public bool IsDeleted { get; }
    public DateTime? DeletedOn { get; }
    public string? DeletedBy { get; }

    private SoftDelete()
    {
        //EF Core
    }

    private SoftDelete(bool isDeleted, DateTime? deletedOn, string? deletedBy)
    {
        IsDeleted = isDeleted;
        DeletedOn = deletedOn;
        DeletedBy = deletedBy;
    }

    public static SoftDelete Create(bool isDeleted, DateTime? deletedOn, string? deletedBy)
    {
        return new SoftDelete(isDeleted, deletedOn, deletedBy);
    }
}
