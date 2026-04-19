using Shared.DDD;
using Shared.Exceptions;

namespace Auth.Domain.Menu;

public class ActivityMenuOverride : Entity<Guid>
{
    public string ActivityId { get; private set; } = default!;
    public Guid MenuItemId { get; private set; }
    public bool IsVisible { get; private set; }
    public bool CanEdit { get; private set; }

    private ActivityMenuOverride() { }

    public static ActivityMenuOverride Create(
        string activityId,
        Guid menuItemId,
        bool isVisible,
        bool canEdit)
    {
        if (string.IsNullOrWhiteSpace(activityId))
            throw new DomainException("ActivityId is required");
        if (menuItemId == Guid.Empty)
            throw new DomainException("MenuItemId is required");

        return new ActivityMenuOverride
        {
            Id = Guid.CreateVersion7(),
            ActivityId = activityId.Trim(),
            MenuItemId = menuItemId,
            IsVisible = isVisible,
            CanEdit = canEdit,
        };
    }

    public void Update(bool isVisible, bool canEdit)
    {
        IsVisible = isVisible;
        CanEdit = canEdit;
    }
}
