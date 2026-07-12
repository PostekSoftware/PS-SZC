using Microsoft.Data.Sqlite;

namespace PS.APP.Projects;

public sealed class ProjectCustomDatabase : IDisposable
{
    private readonly ProjectFile? _project;
    private readonly string _databasePath;
    private SqliteConnection? _connection;
    private bool _disposed;

    private ProjectCustomDatabase(
        string databasePath,
        ProjectCustomDatabaseEntry entry,
        ProjectFile? project)
    {
        _databasePath = databasePath;
        Entry = entry;
        _project = project;
    }

    public string Id => Entry.Id;

    public string Name => Entry.Name;

    internal ProjectCustomDatabaseEntry Entry { get; private set; }

    public SqliteConnection Connection => _connection ??= SqliteConnectionHelper.OpenConnection(_databasePath);

    internal static ProjectCustomDatabase Create(
        string absolutePath,
        ProjectCustomDatabaseEntry entry,
        ProjectFile? project,
        Action<ProjectCustomDatabase>? configure = null)
    {
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var database = new ProjectCustomDatabase(absolutePath, entry, project);
        configure?.Invoke(database);
        return database;
    }

    internal static ProjectCustomDatabase Open(
        string absolutePath,
        ProjectCustomDatabaseEntry entry,
        ProjectFile? project)
    {
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException($"Custom database file not found: {absolutePath}");

        return new ProjectCustomDatabase(absolutePath, entry, project);
    }

    public int ExecuteNonQuery(string sql, params SqliteParameter[] parameters)
    {
        ThrowIfDisposed();
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var affected = command.ExecuteNonQuery();
        TouchModified();
        return affected;
    }

    public object? ExecuteScalar(string sql, params SqliteParameter[] parameters)
    {
        ThrowIfDisposed();
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        return command.ExecuteScalar();
    }

    public SqliteDataReader ExecuteReader(string sql, params SqliteParameter[] parameters)
    {
        ThrowIfDisposed();
        var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        return command.ExecuteReader();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        ReleaseConnectionForPackaging();
        _disposed = true;
    }

    internal void ReleaseConnectionForPackaging()
    {
        ThrowIfDisposed();
        SqliteConnectionHelper.ReleaseConnection(ref _connection);
    }

    internal void ReopenAfterPackaging()
    {
        ThrowIfDisposed();
        _ = Connection;
    }

    internal void TouchModified(DateTimeOffset? modifiedAt = null)
    {
        if (_project == null)
            return;

        var now = modifiedAt ?? DateTimeOffset.UtcNow;
        Entry = Entry with { ModifiedAt = now };
        _project.UpdateCustomDatabaseEntry(Entry);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
