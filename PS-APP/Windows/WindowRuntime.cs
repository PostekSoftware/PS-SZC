using Hexa.NET.ImGui;
using Hexa.NET.SDL3;

namespace PS.APP.Windows;

internal sealed unsafe class WindowRuntime
{
    public required SDLWindow* SdlWindow { get; init; }

    public required ImGuiContextPtr ImGuiContext { get; init; }

    public required Form Form { get; init; }

    public required AppWindow AppWindow { get; set; }

    public required float Scale { get; init; }

    public bool IsMain { get; init; }

    public Form? ActiveForm { get; set; }

    public bool ShouldClose { get; set; }

    public bool PendingLoad { get; set; } = true;

    public bool IsOpen => !ShouldClose;

    public uint WindowId => SDL.GetWindowID(SdlWindow);

    public void RequestClose() => ShouldClose = true;
}
