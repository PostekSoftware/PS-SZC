namespace PS.APP.Projects;

public sealed record ProjectCustomDatabaseEntry(
    string Id,
    string Name,
    string RelativePath,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt);
