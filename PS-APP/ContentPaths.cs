namespace PS.APP;

public static class ContentPaths
{
    public const string AppRoot = "Content";
    public const string LibraryRoot = "LibraryContent";

    public static string App(string relativePath) =>
        Path.Combine(AppContext.BaseDirectory, AppRoot, NormalizeRelativePath(relativePath));

    public static string Library(string relativePath) =>
        Path.Combine(AppContext.BaseDirectory, LibraryRoot, NormalizeRelativePath(relativePath));

    public static string Resolve(string relativePath)
    {
        var appPath = App(relativePath);
        if (File.Exists(appPath))
            return appPath;

        var libraryPath = Library(relativePath);
        if (File.Exists(libraryPath))
            return libraryPath;

        return appPath;
    }

    public static bool Exists(string relativePath) =>
        File.Exists(App(relativePath)) || File.Exists(Library(relativePath));

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/');
}
