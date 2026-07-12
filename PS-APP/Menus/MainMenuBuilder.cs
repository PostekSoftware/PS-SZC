namespace PS.APP.Menus;

public sealed class MainMenuBuilder
{
    private readonly List<MenuBarMenu> _menus = [];

    public bool HasMenus => _menus.Count > 0;

    internal IReadOnlyList<MenuBarMenu> Menus => _menus;

    public MenuBarMenu AddMenu(string label)
    {
        var menu = new MenuBarMenu(label);
        _menus.Add(menu);
        return menu;
    }
}

public sealed class MenuBarMenu
{
    internal MenuBarMenu(string label) => Label = label;

    public string Label { get; }

    internal List<MenuNode> Nodes { get; } = [];

    public void AddItem(
        string label,
        Action action,
        string? shortcut = null,
        bool enabled = true,
        bool isChecked = false)
    {
        Nodes.Add(new MenuActionNode(label, action, shortcut, enabled, isChecked));
    }

    public void AddSeparator() => Nodes.Add(MenuSeparatorNode.Instance);

    public MenuBarMenu AddSubMenu(string label, Action<MenuBarMenu> configure)
    {
        var subMenu = new MenuBarMenu(label);
        configure(subMenu);
        Nodes.Add(new MenuSubMenuNode(subMenu));
        return subMenu;
    }
}

internal abstract class MenuNode;

internal sealed class MenuActionNode(
    string label,
    Action action,
    string? shortcut,
    bool enabled,
    bool isChecked) : MenuNode
{
    public string Label { get; } = label;

    public Action Action { get; } = action;

    public string? Shortcut { get; } = shortcut;

    public bool Enabled { get; } = enabled;

    public bool IsChecked { get; } = isChecked;
}

internal sealed class MenuSubMenuNode(MenuBarMenu menu) : MenuNode
{
    public MenuBarMenu Menu { get; } = menu;
}

internal sealed class MenuSeparatorNode : MenuNode
{
    public static MenuSeparatorNode Instance { get; } = new();

    private MenuSeparatorNode()
    {
    }
}
