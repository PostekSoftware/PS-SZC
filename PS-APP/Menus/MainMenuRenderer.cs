using Hexa.NET.ImGui;

namespace PS.APP.Menus;

public static class MainMenuRenderer
{
    public static float LastHeight { get; private set; }

    public static bool Draw(MainMenuBuilder builder)
    {
        LastHeight = 0;

        if (!builder.HasMenus)
            return false;

        if (!ImGui.BeginMainMenuBar())
            return false;

        foreach (var menu in builder.Menus)
        {
            if (!ImGui.BeginMenu(menu.Label))
                continue;

            DrawNodes(menu.Nodes);
            ImGui.EndMenu();
        }

        LastHeight = ImGui.GetWindowSize().Y;
        ImGui.EndMainMenuBar();
        return true;
    }

    private static void DrawNodes(IReadOnlyList<MenuNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case MenuActionNode action:
                    DrawAction(action);
                    break;
                case MenuSeparatorNode:
                    ImGui.Separator();
                    break;
                case MenuSubMenuNode subMenu:
                    if (ImGui.BeginMenu(subMenu.Menu.Label))
                    {
                        DrawNodes(subMenu.Menu.Nodes);
                        ImGui.EndMenu();
                    }

                    break;
            }
        }
    }

    private static void DrawAction(MenuActionNode action)
    {
        if (!action.Enabled)
            ImGui.BeginDisabled();

        var clicked = ImGui.MenuItem(action.Label, action.Shortcut, action.IsChecked, action.Enabled);
        if (clicked && action.Enabled)
            action.Action();

        if (!action.Enabled)
            ImGui.EndDisabled();
    }
}
