using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PS.APP.Projects.Ef;

namespace PS.APP.Projects;

public static class ProjectManager
{
    public static ProjectFile Create(string projectName) =>
        ProjectFile.CreateNew(projectName);

    public static ProjectFile Open(string filePath) =>
        ProjectFile.OpenPackage(filePath);

    public static bool IsProjectFile(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ProjectPackageFormat.Extension, StringComparison.OrdinalIgnoreCase);

    public static string FileExtension => ProjectPackageFormat.Extension;
}

public sealed class ProjectFile : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Dictionary<string, ProjectJsonDocument> _loadedDocuments = [];
    private readonly Dictionary<string, ProjectCustomDatabase> _openCustomDatabases = [];
    private readonly object _sync = new();
    private bool _disposed;
    private DateTimeOffset? _savedToDiskModifiedAt;

    private ProjectFile(string workspacePath, ProjectDatabase database, string name, string? filePath)
    {
        WorkspacePath = workspacePath;
        Database = database;
        Name = name;
        FilePath = filePath;
        CreatedAt = DateTimeOffset.Parse(
            database.GetMetadata("createdAt") ?? DateTimeOffset.UtcNow.ToString("O"));
        ModifiedAt = DateTimeOffset.Parse(
            database.GetMetadata("modifiedAt") ?? CreatedAt.ToString("O"));

        if (filePath != null)
            _savedToDiskModifiedAt = ModifiedAt;
    }

    public string Name { get; private set; }

    public string? FilePath { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset ModifiedAt { get; private set; }

    public bool IsDirty
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FilePath))
                return ModifiedAt > CreatedAt;

            return _savedToDiskModifiedAt == null || ModifiedAt > _savedToDiskModifiedAt;
        }
    }

    internal string WorkspacePath { get; }

    internal ProjectDatabase Database { get; }

    public static ProjectFile CreateNew(string projectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        var workspacePath = CreateWorkspaceDirectory();
        var database = ProjectDatabase.Open(Path.Combine(workspacePath, ProjectPackageFormat.DatabaseRelativePath));
        var now = DateTimeOffset.UtcNow;

        database.SetMetadata("projectName", projectName);
        database.SetMetadata("createdAt", now.ToString("O"));
        database.SetMetadata("modifiedAt", now.ToString("O"));
        database.SetMetadata("formatVersion", ProjectPackageFormat.FormatVersion.ToString());

        var project = new ProjectFile(workspacePath, database, projectName, null);
        project.WriteManifest();
        return project;
    }

    public static ProjectFile OpenPackage(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Project file not found: {filePath}");

        var workspacePath = CreateWorkspaceDirectory();
        ProjectPackageIO.ExtractPackage(filePath, workspacePath);

        var manifestPath = Path.Combine(workspacePath, ProjectPackageFormat.ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new InvalidDataException("Project package is missing manifest.json.");

        var manifest = JsonSerializer.Deserialize<ProjectManifest>(
                           File.ReadAllText(manifestPath), JsonOptions)
                       ?? throw new InvalidDataException("Project manifest could not be parsed.");

        if (manifest.FormatVersion != ProjectPackageFormat.FormatVersion)
        {
            throw new NotSupportedException(
                $"Project format version {manifest.FormatVersion} is not supported.");
        }

        var databasePath = Path.Combine(workspacePath, ProjectPackageFormat.DatabaseRelativePath);
        if (!File.Exists(databasePath))
            throw new InvalidDataException("Project package is missing database/project.db.");

        var database = ProjectDatabase.Open(databasePath);
        var projectName = database.GetMetadata("projectName") ?? manifest.ProjectName;
        return new ProjectFile(workspacePath, database, projectName, Path.GetFullPath(filePath));
    }

    public ProjectJsonDocument CreateJsonDocument(string name, JsonNode? content = null)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_sync)
        {
            if (Database.GetDocuments().Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A document named '{name}' already exists.");

            var now = DateTimeOffset.UtcNow;
            var id = Guid.NewGuid().ToString("N");
            var entry = new ProjectDocumentEntry(
                id,
                name,
                ProjectPackageFormat.DocumentRelativePath(id),
                now,
                now);

            Database.InsertDocument(entry);

            var document = new ProjectJsonDocument(this, entry, content ?? new JsonObject());
            WriteJsonDocumentFile(document);
            _loadedDocuments[id] = document;
            TouchModified(now);
            return document;
        }
    }

    public ProjectJsonDocument GetJsonDocument(string id)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (_sync)
        {
            if (_loadedDocuments.TryGetValue(id, out var cached))
                return cached;

            var entry = Database.GetDocument(id)
                        ?? throw new KeyNotFoundException($"Document '{id}' was not found.");

            var root = ReadJsonDocumentFile(entry.RelativePath);
            var document = new ProjectJsonDocument(this, entry, root);
            _loadedDocuments[id] = document;
            return document;
        }
    }

    public bool TryGetJsonDocument(string id, out ProjectJsonDocument? document)
    {
        document = null;
        try
        {
            document = GetJsonDocument(id);
            return true;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    public IReadOnlyList<ProjectDocumentEntry> GetJsonDocuments()
    {
        ThrowIfDisposed();
        lock (_sync)
            return Database.GetDocuments();
    }

    public void DeleteJsonDocument(string id)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (_sync)
        {
            var entry = Database.GetDocument(id)
                        ?? throw new KeyNotFoundException($"Document '{id}' was not found.");

            Database.DeleteDocument(id);
            _loadedDocuments.Remove(id);

            var absolutePath = Path.Combine(WorkspacePath, entry.RelativePath);
            if (File.Exists(absolutePath))
                File.Delete(absolutePath);

            TouchModified();
        }
    }

    public ProjectCustomDatabase CreateCustomDatabase(
        string name,
        Action<ProjectCustomDatabase>? configure = null)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_sync)
        {
            if (Database.GetCustomDatabaseByName(name) != null)
                throw new InvalidOperationException($"A custom database named '{name}' already exists.");

            var now = DateTimeOffset.UtcNow;
            var id = Guid.NewGuid().ToString("N");
            var entry = new ProjectCustomDatabaseEntry(
                id,
                name,
                ProjectPackageFormat.CustomDatabaseRelativePath(name),
                now,
                now);

            var absolutePath = Path.Combine(WorkspacePath, entry.RelativePath);
            var database = ProjectCustomDatabase.Create(absolutePath, entry, this, configure);

            Database.InsertCustomDatabase(entry);
            _openCustomDatabases[name] = database;
            TouchModified(now);
            return database;
        }
    }

    public ProjectCustomDatabase GetCustomDatabase(string name)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_sync)
        {
            if (_openCustomDatabases.TryGetValue(name, out var cached))
                return cached;

            var entry = Database.GetCustomDatabaseByName(name)
                        ?? throw new KeyNotFoundException($"Custom database '{name}' was not found.");

            var absolutePath = Path.Combine(WorkspacePath, entry.RelativePath);
            var database = ProjectCustomDatabase.Open(absolutePath, entry, this);
            _openCustomDatabases[name] = database;
            return database;
        }
    }

    public bool TryGetCustomDatabase(string name, out ProjectCustomDatabase? database)
    {
        database = null;
        try
        {
            database = GetCustomDatabase(name);
            return true;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    public IReadOnlyList<ProjectCustomDatabaseEntry> GetCustomDatabases()
    {
        ThrowIfDisposed();
        lock (_sync)
            return Database.GetCustomDatabases();
    }

    public bool HasCustomDatabase(string name)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_sync)
            return Database.GetCustomDatabaseByName(name) != null;
    }

    public string GetCustomDatabaseFilePath(string name)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_sync)
            return GetCustomDatabaseFilePathCore(name);
    }

    public TContext CreateEfDatabase<TContext>(
        string name,
        Func<DbContextOptions<TContext>, TContext> contextFactory,
        Action<TContext>? configure = null,
        bool ensureCreated = true)
        where TContext : DbContext
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(contextFactory);

        lock (_sync)
        {
            if (Database.GetCustomDatabaseByName(name) == null)
                CreateCustomDatabase(name);

            var context = OpenEfContextCore(name, contextFactory);
            if (ensureCreated)
                context.Database.EnsureCreated();

            configure?.Invoke(context);
            SaveEfChanges(context, name);
            return context;
        }
    }

    public TContext OpenEfContext<TContext>(
        string name,
        Func<DbContextOptions<TContext>, TContext> contextFactory)
        where TContext : DbContext
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(contextFactory);

        lock (_sync)
            return OpenEfContextCore(name, contextFactory);
    }

    public int SaveEfChanges<TContext>(TContext context, string databaseName)
        where TContext : DbContext
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        lock (_sync)
        {
            var changes = context.SaveChanges();
            if (changes > 0)
                TouchCustomDatabaseModified(databaseName);
            return changes;
        }
    }

    public void DeleteCustomDatabase(string name)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_sync)
        {
            var entry = Database.GetCustomDatabaseByName(name)
                        ?? throw new KeyNotFoundException($"Custom database '{name}' was not found.");

            if (_openCustomDatabases.Remove(name, out var openDatabase))
                openDatabase.Dispose();

            Database.DeleteCustomDatabase(entry.Id);

            var absolutePath = Path.Combine(WorkspacePath, entry.RelativePath);
            if (File.Exists(absolutePath))
                File.Delete(absolutePath);

            TouchModified();
        }
    }

    public void Save()
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(FilePath))
            throw new InvalidOperationException("Project has no file path. Use SaveAs first.");

        SaveAs(FilePath);
    }

    public void SaveAs(string filePath)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!filePath.EndsWith(ProjectPackageFormat.Extension, StringComparison.OrdinalIgnoreCase))
            filePath += ProjectPackageFormat.Extension;

        lock (_sync)
        {
            foreach (var document in _loadedDocuments.Values)
                WriteJsonDocumentFile(document);

            TouchModified();
            WriteManifest();
            ProjectPackageIO.CreatePackage(WorkspacePath, filePath);
            FilePath = Path.GetFullPath(filePath);
            _savedToDiskModifiedAt = ModifiedAt;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _loadedDocuments.Clear();

        foreach (var database in _openCustomDatabases.Values)
            database.Dispose();
        _openCustomDatabases.Clear();

        Database.Dispose();

        if (Directory.Exists(WorkspacePath))
            Directory.Delete(WorkspacePath, recursive: true);

        _disposed = true;
    }

    internal void SaveJsonDocument(ProjectJsonDocument document)
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            var updatedEntry = document.Entry with { ModifiedAt = now };
            Database.UpdateDocument(updatedEntry);
            document.Entry = updatedEntry;
            WriteJsonDocumentFile(document);
            _loadedDocuments[document.Id] = document;
            TouchModified(now);
        }
    }

    internal void UpdateCustomDatabaseEntry(ProjectCustomDatabaseEntry entry)
    {
        lock (_sync)
        {
            Database.UpdateCustomDatabase(entry);
            TouchModified(entry.ModifiedAt);
        }
    }

    internal void TouchCustomDatabaseModified(string name)
    {
        var entry = Database.GetCustomDatabaseByName(name)
                    ?? throw new KeyNotFoundException($"Custom database '{name}' was not found.");

        var updated = entry with { ModifiedAt = DateTimeOffset.UtcNow };
        Database.UpdateCustomDatabase(updated);
        TouchModified(updated.ModifiedAt);
    }

    private TContext OpenEfContextCore<TContext>(
        string name,
        Func<DbContextOptions<TContext>, TContext> contextFactory)
        where TContext : DbContext
    {
        var path = GetCustomDatabaseFilePathCore(name);
        return contextFactory(ProjectEfCore.BuildOptions<TContext>(path));
    }

    private string GetCustomDatabaseFilePathCore(string name)
    {
        var entry = Database.GetCustomDatabaseByName(name)
                    ?? throw new KeyNotFoundException($"Custom database '{name}' was not found.");

        return Path.Combine(WorkspacePath, entry.RelativePath);
    }

    private void WriteManifest()
    {
        var manifest = new ProjectManifest
        {
            FormatVersion = ProjectPackageFormat.FormatVersion,
            ProjectName = Name,
            CreatedAt = CreatedAt,
            ModifiedAt = ModifiedAt,
            Documents = Database.GetDocuments()
                .Select(x => new ProjectManifestDocument
                {
                    Id = x.Id,
                    Name = x.Name,
                    RelativePath = x.RelativePath
                })
                .ToList(),
            CustomDatabases = Database.GetCustomDatabases()
                .Select(x => new ProjectManifestCustomDatabase
                {
                    Id = x.Id,
                    Name = x.Name,
                    RelativePath = x.RelativePath
                })
                .ToList()
        };

        var manifestPath = Path.Combine(WorkspacePath, ProjectPackageFormat.ManifestFileName);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
    }

    private void WriteJsonDocumentFile(ProjectJsonDocument document)
    {
        var absolutePath = Path.Combine(WorkspacePath, document.Entry.RelativePath);
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(absolutePath, document.Root.ToJsonString(JsonOptions));
    }

    private JsonNode ReadJsonDocumentFile(string relativePath)
    {
        var absolutePath = Path.Combine(WorkspacePath, relativePath);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException($"Document file not found in project package: {relativePath}");

        return JsonNode.Parse(File.ReadAllText(absolutePath))
               ?? new JsonObject();
    }

    private void TouchModified(DateTimeOffset? modifiedAt = null)
    {
        ModifiedAt = modifiedAt ?? DateTimeOffset.UtcNow;
        Database.SetMetadata("modifiedAt", ModifiedAt.ToString("O"));
        Database.SetMetadata("projectName", Name);
    }

    private static string CreateWorkspaceDirectory()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), "ps-app-projects", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(Path.Combine(workspacePath, ProjectPackageFormat.DocumentsDirectory));
        Directory.CreateDirectory(Path.Combine(workspacePath, ProjectPackageFormat.DatabaseDirectory));
        Directory.CreateDirectory(Path.Combine(workspacePath, ProjectPackageFormat.CustomDatabaseDirectory));
        return workspacePath;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class ProjectManifest
    {
        public int FormatVersion { get; set; }

        public string ProjectName { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset ModifiedAt { get; set; }

        public List<ProjectManifestDocument> Documents { get; set; } = [];

        public List<ProjectManifestCustomDatabase> CustomDatabases { get; set; } = [];
    }

    private sealed class ProjectManifestDocument
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string RelativePath { get; set; } = string.Empty;
    }

    private sealed class ProjectManifestCustomDatabase
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string RelativePath { get; set; } = string.Empty;
    }
}

internal static class ProjectPackageIO
{
    public static void CreatePackage(string workspacePath, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(filePath))
            File.Delete(filePath);

        ZipFile.CreateFromDirectory(workspacePath, filePath, CompressionLevel.Optimal, includeBaseDirectory: false);
    }

    public static void ExtractPackage(string filePath, string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        Directory.CreateDirectory(workspacePath);

        using var archive = ZipFile.OpenRead(filePath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var destinationPath = Path.GetFullPath(Path.Combine(workspacePath, entry.FullName));
            var fullWorkspacePath = Path.GetFullPath(workspacePath);
            if (!destinationPath.StartsWith(fullWorkspacePath + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && destinationPath != fullWorkspacePath)
            {
                throw new InvalidDataException($"Unsafe path detected in project package: {entry.FullName}");
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }
}
