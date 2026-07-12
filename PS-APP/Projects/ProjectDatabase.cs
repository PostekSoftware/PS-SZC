using Microsoft.Data.Sqlite;

namespace PS.APP.Projects;

public sealed record ProjectDocumentEntry(
    string Id,
    string Name,
    string RelativePath,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt);

internal sealed class ProjectDatabase : IDisposable
{
    private readonly string _databasePath;
    private SqliteConnection? _connection;

    private ProjectDatabase(string databasePath) => _databasePath = databasePath;

    private SqliteConnection Connection => _connection ??= SqliteConnectionHelper.OpenConnection(_databasePath);

    public static ProjectDatabase Open(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var database = new ProjectDatabase(databasePath);
        database.InitializeSchema();
        return database;
    }

    public void SetMetadata(string key, string value)
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO metadata (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    public string? GetMetadata(string key)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "SELECT value FROM metadata WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    public void InsertDocument(ProjectDocumentEntry entry)
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO documents (id, name, relative_path, created_at, modified_at)
            VALUES ($id, $name, $relativePath, $createdAt, $modifiedAt);
            """;
        AddDocumentParameters(command, entry);
        command.ExecuteNonQuery();
    }

    public void UpdateDocument(ProjectDocumentEntry entry)
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            """
            UPDATE documents
            SET name = $name, relative_path = $relativePath, modified_at = $modifiedAt
            WHERE id = $id;
            """;
        AddDocumentParameters(command, entry);
        command.ExecuteNonQuery();
    }

    public void DeleteDocument(string id)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "DELETE FROM documents WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public ProjectDocumentEntry? GetDocument(string id)
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, relative_path, created_at, modified_at
            FROM documents
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadDocument(reader) : null;
    }

    public IReadOnlyList<ProjectDocumentEntry> GetDocuments()
    {
        var documents = new List<ProjectDocumentEntry>();
        using var command = Connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, relative_path, created_at, modified_at
            FROM documents
            ORDER BY name;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
            documents.Add(ReadDocument(reader));
        return documents;
    }

    public void InsertCustomDatabase(ProjectCustomDatabaseEntry entry)
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO custom_databases (id, name, relative_path, created_at, modified_at)
            VALUES ($id, $name, $relativePath, $createdAt, $modifiedAt);
            """;
        AddCustomDatabaseParameters(command, entry);
        command.ExecuteNonQuery();
    }

    public void UpdateCustomDatabase(ProjectCustomDatabaseEntry entry)
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            """
            UPDATE custom_databases
            SET name = $name, relative_path = $relativePath, modified_at = $modifiedAt
            WHERE id = $id;
            """;
        AddCustomDatabaseParameters(command, entry);
        command.ExecuteNonQuery();
    }

    public void DeleteCustomDatabase(string id)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "DELETE FROM custom_databases WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public ProjectCustomDatabaseEntry? GetCustomDatabase(string id)
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, relative_path, created_at, modified_at
            FROM custom_databases
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadCustomDatabase(reader) : null;
    }

    public ProjectCustomDatabaseEntry? GetCustomDatabaseByName(string name)
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, relative_path, created_at, modified_at
            FROM custom_databases
            WHERE name = $name;
            """;
        command.Parameters.AddWithValue("$name", name);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadCustomDatabase(reader) : null;
    }

    public IReadOnlyList<ProjectCustomDatabaseEntry> GetCustomDatabases()
    {
        var databases = new List<ProjectCustomDatabaseEntry>();
        using var command = Connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, relative_path, created_at, modified_at
            FROM custom_databases
            ORDER BY name;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
            databases.Add(ReadCustomDatabase(reader));
        return databases;
    }

    public void Dispose() => ReleaseConnectionForPackaging();

    internal void ReleaseConnectionForPackaging() =>
        SqliteConnectionHelper.ReleaseConnection(ref _connection);

    internal void ReopenAfterPackaging() => _ = Connection;

    private void InitializeSchema()
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS metadata (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS documents (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                relative_path TEXT NOT NULL UNIQUE,
                created_at TEXT NOT NULL,
                modified_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS custom_databases (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                relative_path TEXT NOT NULL UNIQUE,
                created_at TEXT NOT NULL,
                modified_at TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private static void AddDocumentParameters(SqliteCommand command, ProjectDocumentEntry entry)
    {
        command.Parameters.AddWithValue("$id", entry.Id);
        command.Parameters.AddWithValue("$name", entry.Name);
        command.Parameters.AddWithValue("$relativePath", entry.RelativePath);
        command.Parameters.AddWithValue("$createdAt", entry.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$modifiedAt", entry.ModifiedAt.ToString("O"));
    }

    private static ProjectDocumentEntry ReadDocument(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            DateTimeOffset.Parse(reader.GetString(3)),
            DateTimeOffset.Parse(reader.GetString(4)));

    private static void AddCustomDatabaseParameters(
        SqliteCommand command,
        ProjectCustomDatabaseEntry entry)
    {
        command.Parameters.AddWithValue("$id", entry.Id);
        command.Parameters.AddWithValue("$name", entry.Name);
        command.Parameters.AddWithValue("$relativePath", entry.RelativePath);
        command.Parameters.AddWithValue("$createdAt", entry.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$modifiedAt", entry.ModifiedAt.ToString("O"));
    }

    private static ProjectCustomDatabaseEntry ReadCustomDatabase(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            DateTimeOffset.Parse(reader.GetString(3)),
            DateTimeOffset.Parse(reader.GetString(4)));
}
