using PdfSharpCore.Fonts;

namespace PS_SZC.Services;

internal sealed class ReportPdfFontResolver : IFontResolver
{
    private const string FaceName = "ReportSans#";
    private static readonly Lazy<byte[]> FontData = new(LoadFontData);

    public string DefaultFontName => "ReportSans";

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(FaceName, isBold, isItalic);

    public byte[] GetFont(string faceName) => FontData.Value;

    private static byte[] LoadFontData()
    {
        foreach (var path in GetCandidateFontPaths())
        {
            if (!File.Exists(path))
                continue;

            return File.ReadAllBytes(path);
        }

        throw new InvalidOperationException(
            "No suitable TrueType font was found for PDF export. Install Arial or DejaVu Sans.");
    }

    private static IEnumerable<string> GetCandidateFontPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "Arial.ttf");
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return "/Library/Fonts/Arial Unicode.ttf";
            yield return "/System/Library/Fonts/Supplemental/Arial.ttf";
        }

        if (OperatingSystem.IsLinux())
        {
            yield return "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
            yield return "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf";
            yield return "/usr/share/fonts/TTF/DejaVuSans.ttf";
        }
    }
}
