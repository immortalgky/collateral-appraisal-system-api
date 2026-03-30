namespace Auth.Domain.Groups;

public class GroupUser
{
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = default!;
    public Guid UserId { get; set; }
}
