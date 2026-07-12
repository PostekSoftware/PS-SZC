using PS.APP.Projects;

namespace PS_SZC.Startup;

internal static class StartupProjectPath
{
    public static string? TryParse(string[] args)
    {
        foreach (var rawArg in args)
        {
            if (string.IsNullOrWhiteSpace(rawArg))
                continue;

            var arg = rawArg.Trim().Trim('"');
            if (arg.Length == 0 || arg[0] == '-')
                continue;

            var path = NormalizePath(arg);
            if (path == null)
                continue;

            if (!ProjectManager.IsProjectFile(path))
                continue;

            if (!File.Exists(path))
                continue;

            return Path.GetFullPath(path);
        }

        return null;
    }

    private static string? NormalizePath(string arg)
    {
        if (arg.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(arg, UriKind.Absolute, out var uri))
                return null;

            return uri.IsFile ? uri.LocalPath : null;
        }

        return arg;
    }
}
