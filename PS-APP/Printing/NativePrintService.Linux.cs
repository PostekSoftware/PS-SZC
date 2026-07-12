using System.Diagnostics;

namespace PS.APP.Printing;

public static partial class NativePrintService
{
    private static void PrintPdfOnLinux(string pdfPath)
    {
        if (TryStartLinuxProcess("lp", ["-o", "fit-to-page", pdfPath]))
            return;

        if (TryStartLinuxProcess("lpr", ["-o", "fit-to-page", pdfPath]))
            return;

        throw new InvalidOperationException("Could not open the system print dialog.");
    }

    private static bool TryStartLinuxProcess(string fileName, IReadOnlyList<string> arguments)
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
