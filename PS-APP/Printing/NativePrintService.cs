namespace PS.APP.Printing;

public readonly record struct PrintPdfResult(bool Success, string? Message = null);

public static partial class NativePrintService
{
    public static PrintPdfResult PrintPdf(string pdfPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

        if (!File.Exists(pdfPath))
            throw new FileNotFoundException("The PDF file to print was not found.", pdfPath);

        var fullPath = Path.GetFullPath(pdfPath);

        if (OperatingSystem.IsWindows())
            return PrintPdfOnWindows(fullPath);

        if (OperatingSystem.IsMacOS())
        {
            PrintPdfOnMacOs(fullPath);
            return new PrintPdfResult(true);
        }

        if (OperatingSystem.IsLinux())
        {
            PrintPdfOnLinux(fullPath);
            return new PrintPdfResult(true);
        }

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
}
