using System.Diagnostics;

namespace PS.APP.Printing;

public static partial class NativePrintService
{
    private static void PrintPdfOnMacOs(string pdfPath)
    {
        var helperBinary = Path.Combine(AppContext.BaseDirectory, "macos-print-pdf");
        if (File.Exists(helperBinary))
        {
            RunMacOsPrintHelper(helperBinary, [pdfPath]);
            return;
        }

        var helperScript = Path.Combine(AppContext.BaseDirectory, "macos-print-pdf.swift");
        if (File.Exists(helperScript))
        {
            RunMacOsPrintHelper("swift", [helperScript, pdfPath]);
            return;
        }

        throw new InvalidOperationException("The macOS print helper was not found.");
    }

    private static void RunMacOsPrintHelper(string fileName, IReadOnlyList<string> arguments)
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

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the macOS print helper.");

            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new InvalidOperationException("Could not open the system print dialog.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Could not open the system print dialog.", ex);
        }
    }
}
