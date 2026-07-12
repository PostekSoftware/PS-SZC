using Microsoft.EntityFrameworkCore;

namespace PS.APP.Projects.Ef;

public static class ProjectEfCore
{
    public static DbContextOptions<TContext> BuildOptions<TContext>(string databaseFilePath)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseFilePath);

        return new DbContextOptionsBuilder<TContext>()
            .UseSqlite(SqliteConnectionHelper.BuildConnectionString(databaseFilePath))
            .Options;
    }
}
