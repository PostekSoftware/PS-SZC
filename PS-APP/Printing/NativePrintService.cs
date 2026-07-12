using System.Diagnostics;

namespace PS.APP.Printing;

public static class NativePrintService
{
    public static void PrintPdf(string pdfPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

        if (!File.Exists(pdfPath))
            throw new FileNotFoundException("The PDF file to print was not found.", pdfPath);

        var fullPath = Path.GetFullPath(pdfPath);

        if (OperatingSystem.IsWindows())
            PrintOnWindows(fullPath);
        else if (OperatingSystem.IsMacOS())
            PrintOnMacOs(fullPath);
        else if (OperatingSystem.IsLinux())
            PrintOnLinux(fullPath);
        else
            throw new PlatformNotSupportedException("Printing is not supported on this platform.");
    }

    public static string CreateTempPdfPath(string suggestedFileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "PS-SZC", "print");
        Directory.CreateDirectory(directory);

        var baseName = Path.GetFileNameWithoutExtension(suggestedFileName);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "report";

        foreach (var invalid in Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(invalid, '_');

        return Path.Combine(directory, $"{baseName}-{Guid.NewGuid():N}.pdf");
    }

    private static void PrintOnWindows(string pdfPath)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = pdfPath,
            Verb = "print",
            UseShellExecute = true,
            CreateNoWindow = true
        });

        if (process == null)
            throw new InvalidOperationException("Could not start the system print dialog.");
    }

    private static void PrintOnMacOs(string pdfPath)
    {
        if (TryPrintOnMacOsWithPreview(pdfPath))
            return;

        throw new InvalidOperationException("Could not open the system print dialog.");
    }

    private static bool TryPrintOnMacOsWithPreview(string pdfPath)
    {
        var escapedPath = pdfPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var script = $"""
            tell application "Preview"
                activate
                set docRef to open POSIX file "{escapedPath}"
                print docRef with print dialog
            end tell
            """;

        return TryStartProcess("osascript", ["-e", script]);
    }

    private static void PrintOnLinux(string pdfPath)
    {
        if (TryStartProcess("lp", ["-o", "fit-to-page", pdfPath]))
            return;

        if (TryStartProcess("lpr", ["-o", "fit-to-page", pdfPath]))
            return;

        throw new InvalidOperationException("Could not open the system print dialog.");
    }

    private static bool TryStartProcess(string fileName, IReadOnlyList<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            return process != null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
