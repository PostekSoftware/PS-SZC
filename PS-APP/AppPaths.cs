namespace PS.APP;

public static class AppPaths
{
    public static string UserDataDirectory =>
        OperatingSystem.IsWindows()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PostekSoftware",
                "PS-SZC")
            : AppContext.BaseDirectory;

    public static string UserDataFile(string fileName)
    {
        var path = Path.Combine(UserDataDirectory, fileName);
        if (OperatingSystem.IsWindows())
            MigrateLegacyUserFile(fileName, path);

        return path;
    }

    private static void MigrateLegacyUserFile(string fileName, string newPath)
    {
        try
        {
            if (File.Exists(newPath))
                return;

            var legacyPath = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(legacyPath))
                return;

            var directory = Path.GetDirectoryName(newPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.Copy(legacyPath, newPath);
        }
        catch
        {
            // Keep startup working even if migration fails.
        }
    }
}
