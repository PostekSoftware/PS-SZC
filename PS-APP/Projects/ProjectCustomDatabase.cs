using Microsoft.Data.Sqlite;

namespace PS.APP.Projects;

public sealed class ProjectCustomDatabase : IDisposable
{
    private readonly ProjectFile? _project;
    private bool _disposed;

    private ProjectCustomDatabase(
        ProjectCustomDatabaseEntry entry,
        SqliteConnection connection,
        ProjectFile? project)
    {
        Entry = entry;
        Connection = connection;
        _project = project;
    }

    public string Id => Entry.Id;

    public string Name => Entry.Name;

    internal ProjectCustomDatabaseEntry Entry { get; private set; }

    public SqliteConnection Connection { get; }

    internal static ProjectCustomDatabase Create(
        string absolutePath,
        ProjectCustomDatabaseEntry entry,
        ProjectFile? project,
        Action<ProjectCustomDatabase>? configure = null)
    {
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var connection = new SqliteConnection($"Data Source={absolutePath}");
        connection.Open();

        var database = new ProjectCustomDatabase(entry, connection, project);
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

        var connection = new SqliteConnection($"Data Source={absolutePath}");
        connection.Open();
        return new ProjectCustomDatabase(entry, connection, project);
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

        Connection.Dispose();
        _disposed = true;
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
