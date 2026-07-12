namespace PS.APP.Projects;

internal static class ProjectPackageFormat
{
    public const string Extension = ".psszc";
    public const int FormatVersion = 1;

    public const string ManifestFileName = "manifest.json";
    public const string DatabaseDirectory = "database";
    public const string DatabaseFileName = "project.db";
    public const string CustomDatabaseDirectory = "database/custom";
    public const string DocumentsDirectory = "documents";

    public static string DatabaseRelativePath => Path.Combine(DatabaseDirectory, DatabaseFileName);

    public static string CustomDatabaseRelativePath(string databaseName) =>
        Path.Combine(CustomDatabaseDirectory, $"{SanitizeDatabaseName(databaseName)}.db");

    public static string DocumentRelativePath(string documentId) =>
        Path.Combine(DocumentsDirectory, $"{documentId}.json");

    public static string SanitizeDatabaseName(string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        var sanitized = new string(databaseName
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_')
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ArgumentException("Database name is invalid.", nameof(databaseName));

        if (sanitized.Equals(DatabaseFileName, StringComparison.OrdinalIgnoreCase)
            || sanitized.Equals("project", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Database name is reserved.", nameof(databaseName));
        }

        return sanitized;
    }
}
