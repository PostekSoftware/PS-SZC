namespace PS.APP.Windows;

public sealed class AppWindow
{
    internal AppWindow(WindowRuntime runtime) => Runtime = runtime;

    internal WindowRuntime Runtime { get; }

    public Form Form => Runtime.Form;

    public bool IsMain => Runtime.IsMain;

    public bool IsOpen => Runtime.IsOpen;

    public void Close() => Runtime.RequestClose();

    public void SetTitle(string title) => Application.Instance.SetWindowTitle(Runtime, title);
}
