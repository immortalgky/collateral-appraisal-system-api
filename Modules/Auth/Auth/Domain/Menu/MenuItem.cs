using Shared.DDD;
using Shared.Exceptions;

namespace Auth.Domain.Menu;

public class MenuItem : Entity<Guid>
{
    public string ItemKey { get; private set; } = default!;

    /// <summary>
    /// Menu scope (Main | Appraisal). Set once at <see cref="Create"/> time and never mutated.
    /// Reparenting across scopes is forbidden by <c>CreateMenuItemCommandHandler</c> and
    /// <c>UpdateMenuItemCommandHandler</c> (parent must be same scope). Do not add a setter
    /// path that mutates this without reviewing the entire tree for scope coherence.
    /// </summary>
    public MenuScope Scope { get; private set; }
    public Guid? ParentId { get; private set; }
    public string? Path { get; private set; }
    public MenuIcon Icon { get; private set; } = default!;
    public string? IconColor { get; private set; }
    public int SortOrder { get; private set; }
    public string ViewPermissionCode { get; private set; } = default!;
    public string? EditPermissionCode { get; private set; }
    public bool IsSystem { get; private set; }

    private readonly List<MenuItemTranslation> _translations = new();
    public IReadOnlyCollection<MenuItemTranslation> Translations => _translations.AsReadOnly();

    private MenuItem() { }

    public static MenuItem Create(
        string itemKey,
        MenuScope scope,
        Guid? parentId,
        string? path,
        MenuIcon icon,
        string? iconColor,
        int sortOrder,
        string viewPermissionCode,
        string? editPermissionCode,
        IEnumerable<MenuItemTranslation> translations,
        bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            throw new DomainException("ItemKey is required");
        if (string.IsNullOrWhiteSpace(viewPermissionCode))
            throw new DomainException("ViewPermissionCode is required");

        var item = new MenuItem
        {
            Id = Guid.CreateVersion7(),
            ItemKey = itemKey.Trim(),
            Scope = scope,
            ParentId = parentId,
            Path = string.IsNullOrWhiteSpace(path) ? null : path.Trim(),
            Icon = icon ?? throw new DomainException("Icon is required"),
            IconColor = string.IsNullOrWhiteSpace(iconColor) ? null : iconColor.Trim(),
            SortOrder = sortOrder,
            ViewPermissionCode = viewPermissionCode.Trim(),
            EditPermissionCode = string.IsNullOrWhiteSpace(editPermissionCode) ? null : editPermissionCode.Trim(),
            IsSystem = isSystem
        };

        item.ReplaceTranslations(translations);

        if (item._translations.Count == 0)
            throw new DomainException("At least one translation is required");

        return item;
    }

    public void Update(
        string? path,
        MenuIcon icon,
        string? iconColor,
        int sortOrder,
        string viewPermissionCode,
        string? editPermissionCode)
    {
        if (string.IsNullOrWhiteSpace(viewPermissionCode))
            throw new DomainException("ViewPermissionCode is required");

        Path = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        Icon = icon ?? throw new DomainException("Icon is required");
        IconColor = string.IsNullOrWhiteSpace(iconColor) ? null : iconColor.Trim();
        SortOrder = sortOrder;
        ViewPermissionCode = viewPermissionCode.Trim();
        EditPermissionCode = string.IsNullOrWhiteSpace(editPermissionCode) ? null : editPermissionCode.Trim();
    }

    public void Reparent(Guid? parentId)
    {
        ParentId = parentId;
    }

    public void ReplaceTranslations(IEnumerable<MenuItemTranslation> translations)
    {
        _translations.Clear();
        if (translations is null) return;
        foreach (var t in translations)
        {
            if (t is null) continue;
            _translations.Add(t);
        }
    }

    public void EnsureDeletable()
    {
        if (IsSystem)
            throw new DomainException($"Menu item '{ItemKey}' is a system item and cannot be deleted.");
    }
}
