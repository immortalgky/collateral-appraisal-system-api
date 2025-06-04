namespace Shared.DDD;

public class Entity<T> : IEntity<T>
{
    public T Id { get; set; }
    public DateTime? CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedBy { get; set; }
}
