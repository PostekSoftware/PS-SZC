using Microsoft.Data.Sqlite;
using System.Data;

namespace PS.APP.Projects;

internal static class SqliteConnectionHelper
{
    internal static string BuildConnectionString(string databasePath) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ConnectionString;

    internal static SqliteConnection OpenConnection(string databasePath)
    {
        var connection = new SqliteConnection(BuildConnectionString(databasePath));
        connection.Open();
        return connection;
    }

    internal static void ReleaseConnection(ref SqliteConnection? connection)
    {
        if (connection == null)
            return;

        var activeConnection = connection;
        connection = null;

        if (activeConnection.State == ConnectionState.Open)
        {
            using var command = activeConnection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(FULL);";
            try
            {
                command.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Ignore if WAL mode is not enabled.
            }

            activeConnection.Close();
        }

        SqliteConnection.ClearPool(activeConnection);
        activeConnection.Dispose();
    }

    internal static void ClearAllPools() => SqliteConnection.ClearAllPools();
}
