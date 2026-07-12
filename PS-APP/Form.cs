using Hexa.NET.ImGui;
using PS.APP.Localization;
using PS.APP.Menus;
using PS.APP.Resources;
using PS.APP.Windows;
using System.Numerics;

namespace PS.APP;

public abstract class Form
{
    private static int _nextId;

    public int Id { get; } = Interlocked.Increment(ref _nextId);

    public LocalizedString Title { get; set; } = string.Empty;

    public Vector2 Size { get; set; } = new(1280, 720);

    public int Width => (int)Size.X;

    public int Height => (int)Size.Y;

    public Vector2 Padding { get; set; } = Vector2.Zero;

    public FontResource DefaultFont { get; set; } = FontFactory.GetDefault();

    public bool ShowMenuBar { get; set; } = true;

    internal AppWindow? Window { get; set; }

    public bool IsMainWindow => Window?.IsMain ?? false;

    public event EventHandler? Load;

    public event EventHandler? Resized;

    public event EventHandler? Closed;

    public abstract void Draw();

    protected virtual void BuildMainMenu(MainMenuBuilder menu)
    {
    }

    internal void Update(WindowRuntime runtime)
    {
        Application.Instance.SetCurrentWindow(runtime);

        if (ShowMenuBar)
        {
            var menuBuilder = new MainMenuBuilder();
            BuildMainMenu(menuBuilder);
            MainMenuRenderer.Draw(menuBuilder);
        }

        var io = ImGui.GetIO();
        var topOffset = ShowMenuBar && MainMenuRenderer.LastHeight > 0f ? MainMenuRenderer.LastHeight : 0f;
        var contentSize = new Vector2(io.DisplaySize.X, Math.Max(0f, io.DisplaySize.Y - topOffset));

        ImGui.SetNextWindowPos(new Vector2(0, topOffset), ImGuiCond.Always);
        ImGui.SetNextWindowSize(contentSize, ImGuiCond.Always);
        ImGui.Begin($"##MainWindow{Id}",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove);

        var style = ImGui.GetStyle();
        style.WindowRounding = 0;
        style.WindowBorderSize = 0;
        style.FrameBorderSize = 0;

        Application.Instance.SetWindowTitle(runtime, Title);

        var fontPtr = DefaultFont.GetPointer();
        if (fontPtr != null)
            ImGui.PushFont(fontPtr.Value, DefaultFont.Data.Size);

        if (Padding != Vector2.Zero)
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Padding);

        Draw();

        if (Padding != Vector2.Zero)
            ImGui.PopStyleVar();

        if (fontPtr != null)
            ImGui.PopFont();

        ImGui.End();
    }

    public virtual bool TryRequestClose() => true;

    protected void Close()
    {
        if (Window == null || Window.IsMain)
        {
            if (TryRequestClose())
                Application.Instance.RequestExit();
        }
        else
            Window.Close();
    }

    protected static void OpenSettings()
    {
        if (Application.Instance.CanOpenSettings)
            Application.Instance.OpenSettings();
    }

    internal void OnLoad() => Load?.Invoke(this, EventArgs.Empty);

    internal void OnResized() => Resized?.Invoke(this, EventArgs.Empty);

    internal void OnClosed() => Closed?.Invoke(this, EventArgs.Empty);
}
