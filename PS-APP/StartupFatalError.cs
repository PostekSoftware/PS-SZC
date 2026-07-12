namespace PS.APP;

public static class StartupFatalError
{
    public static void Show(Exception exception)
    {
        var message = exception.ToString();
        Console.Error.WriteLine(message);

        try
        {
            var logPath = Path.Combine(AppPaths.UserDataDirectory, "startup-error.log");
            Directory.CreateDirectory(AppPaths.UserDataDirectory);
            File.WriteAllText(logPath, $"{DateTimeOffset.Now:O}{Environment.NewLine}{message}");
        }
        catch
        {
            // Best effort only.
        }

        if (OperatingSystem.IsWindows())
        {
            MessageBox(IntPtr.Zero, message, "PS-SZC", 0x00000010);
        }
    }

    public static void Show(string message) => Show(new InvalidOperationException(message));

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
