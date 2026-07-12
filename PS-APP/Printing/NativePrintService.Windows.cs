using System.Diagnostics;

namespace PS.APP.Printing;

public static partial class NativePrintService
{
    private static PrintPdfResult PrintPdfOnWindows(string pdfPath)
    {
        var workingDirectory = Path.GetDirectoryName(pdfPath);
        var defaultPrinter = TryGetDefaultPrinterName();

        if (TryWindowsShellPrint(pdfPath, workingDirectory, defaultPrinter))
            return new PrintPdfResult(true);

        foreach (var readerPath in GetAdobeReaderPaths())
        {
            if (TryStartWindowsProcess(readerPath, ["/p", "/h", pdfPath], workingDirectory))
                return new PrintPdfResult(true);

            if (defaultPrinter != null &&
                TryStartWindowsProcess(readerPath, ["/t", pdfPath, defaultPrinter], workingDirectory))
            {
                return new PrintPdfResult(true);
            }
        }

        foreach (var sumatraPath in GetSumatraPdfPaths())
        {
            if (TryStartWindowsProcess(sumatraPath, ["-print-to-default", "-silent", pdfPath], workingDirectory))
                return new PrintPdfResult(true);
        }

        foreach (var edgePath in GetEdgePaths())
        {
            if (TryStartWindowsProcess(edgePath, [pdfPath], workingDirectory))
            {
                return new PrintPdfResult(
                    true,
                    "Report.PrintOpenedForManualPrint");
            }
        }

        throw new InvalidOperationException(
            "No PDF print handler is installed on this system. Install Microsoft Edge or Adobe Acrobat Reader, or export the report as PDF and print it manually.");
    }

    private static bool TryWindowsShellPrint(string pdfPath, string? workingDirectory, string? printerName)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = pdfPath,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
            };

            if (!string.IsNullOrWhiteSpace(printerName))
            {
                startInfo.Verb = "printto";
                startInfo.Arguments = $"\"{printerName}\"";
            }
            else
            {
                startInfo.Verb = "print";
            }

            using var process = Process.Start(startInfo);
            return process != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string? TryGetDefaultPrinterName()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"(Get-CimInstance Win32_Printer | Where-Object { $_.Default -eq $true } | Select-Object -First 1 -ExpandProperty Name)\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
                return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IEnumerable<string> GetAdobeReaderPaths()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Adobe", "Acrobat Reader DC", "Reader", "AcroRd32.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Adobe", "Acrobat Reader DC", "Reader", "AcroRd32.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Adobe", "Acrobat Reader", "Reader", "AcroRd32.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Adobe", "Acrobat Reader", "Reader", "AcroRd32.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                yield return candidate;
        }
    }

    private static IEnumerable<string> GetSumatraPdfPaths()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "SumatraPDF.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SumatraPDF", "SumatraPDF.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "SumatraPDF", "SumatraPDF.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                yield return candidate;
        }
    }

    private static IEnumerable<string> GetEdgePaths()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft", "Edge", "Application", "msedge.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                yield return candidate;
        }
    }

    private static bool TryStartWindowsProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
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
