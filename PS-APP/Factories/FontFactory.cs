using Hexa.NET.ImGui;
using PS.APP.Resources;
using System.Reflection;

namespace PS.APP;

public static class FontFactory
{
    public const string DefaultFontName = "Roboto";
    public const string DefaultFontFile = "Roboto.ttf";
    public const int DefaultSize = 13;

    private static readonly Dictionary<string, FontMetaData> FontCache = [];
    private static readonly Dictionary<ImGuiContextPtr, ContextFontState> Contexts = [];

    static FontFactory() => RegisterDefaultFont();

    public static ImFontPtr? DefaultFontPointer =>
        Contexts.Values.FirstOrDefault()?.DefaultFontPointer;

    public static void RegisterFromFile(string name, string ttfPath)
    {
        if (FontCache.ContainsKey(name) || !File.Exists(ttfPath))
            return;

        FontCache[name] = new FontMetaData(name, ttfPath);
    }

    public static void RegisterFromResource(string name, string resourceName) =>
        RegisterFromResource(name, Assembly.GetCallingAssembly(), resourceName);

    public static void RegisterFromResource(string name, Assembly assembly, string resourceName)
    {
        if (FontCache.ContainsKey(name))
            return;

        using var resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream == null)
            return;

        var tempFile = Path.GetTempFileName();
        using (var tempFileStream = File.OpenWrite(tempFile))
            resourceStream.CopyTo(tempFileStream);

        FontCache[name] = new FontMetaData(name, tempFile, true);
    }

    public static FontResource GetDefault(int size = DefaultSize, FontResource? fallbackFont = null) =>
        Get(DefaultFontName, size, fallbackFont);

    public static FontResource Get(string name, int size, FontResource? fallbackFont = null)
    {
        if (!FontCache.TryGetValue(name, out var metadata))
            throw new InvalidOperationException($"Unregistered font '{name}'.");

        return new FontResource(new FontData(metadata, size, fallbackFont?.Data));
    }

    internal static unsafe void Initialize(ImGuiContextPtr context, ImGuiIOPtr io, float scale)
    {
        RegisterDefaultFont();
        var state = new ContextFontState { Io = io, Scale = scale };
        Contexts[context] = state;

        var defaultFont = GetDefault();
        state.DefaultFontPointer = LoadFont(state, defaultFont.Data);
        if (state.DefaultFontPointer != null)
            io.FontDefault = state.DefaultFontPointer.Value;
    }

    internal static unsafe ImFontPtr? GetPointer(FontData data)
    {
        var context = ImGui.GetCurrentContext();
        if (!Contexts.TryGetValue(context, out var state))
            return Contexts.Values.FirstOrDefault()?.DefaultFontPointer;

        return LoadFont(state, data);
    }

    internal static void ReleaseContext(ImGuiContextPtr context) => Contexts.Remove(context);

    internal static void Dispose()
    {
        foreach (var metadata in FontCache.Values.Where(x => x.IsTemporary))
        {
            if (File.Exists(metadata.Path))
                File.Delete(metadata.Path);
        }

        FontCache.Clear();
        Contexts.Clear();
        RegisterDefaultFont();
    }

    private static void RegisterDefaultFont()
    {
        if (FontCache.ContainsKey(DefaultFontName))
            return;

        var path = ContentPaths.Library(DefaultFontFile);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Default font not found: {path}");

        FontCache[DefaultFontName] = new FontMetaData(DefaultFontName, path);
    }

    private static unsafe ImFontPtr? LoadFont(ContextFontState state, FontData data)
    {
        if (string.IsNullOrEmpty(data.Metadata.Path))
            throw new InvalidOperationException($"Font '{data.Metadata.Name}' has no file path.");

        var key = (data.Metadata.Name, data.Size);
        if (state.FontPointers.TryGetValue(key, out var cached))
            return cached;

        ImFontPtr baseFont = state.Io.Fonts.AddFontFromFileTTF(data.Metadata.Path, data.Size);

        if (data.Fallback != null)
        {
            ImFontConfig* config = ImGui.ImFontConfig();
            config->MergeMode = 1;
            foreach (var fallback in CollectFallbackFonts(data.Fallback).Reverse())
            {
                if (!string.IsNullOrEmpty(fallback.Metadata.Path))
                    _ = state.Io.Fonts.AddFontFromFileTTF(fallback.Metadata.Path, fallback.Size, config);
            }
        }

        state.FontPointers[key] = baseFont;
        return baseFont;
    }

    private static IEnumerable<FontData> CollectFallbackFonts(FontData fontData)
    {
        var fallback = fontData;
        while (fallback != null)
        {
            yield return fallback;
            fallback = fallback.Fallback!;
        }
    }

    private sealed class ContextFontState
    {
        public required ImGuiIOPtr Io { get; init; }

        public required float Scale { get; init; }

        public Dictionary<(string Name, int Size), ImFontPtr> FontPointers { get; } = [];

        public ImFontPtr? DefaultFontPointer { get; set; }
    }
}
