using System.Numerics;

namespace PS.APP.Windows;

public sealed class AppWindowOptions
{
    public static AppWindowOptions Default { get; } = new();

    public Vector2? Size { get; init; }

    public Vector2? Position { get; init; }

    public bool ShowMenuBar { get; init; }

    public bool Resizable { get; init; } = true;
}
