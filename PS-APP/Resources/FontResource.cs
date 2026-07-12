using Hexa.NET.ImGui;
using PS.APP;

namespace PS.APP.Resources;

public sealed class FontResource
{
    private ImGuiContextPtr _cachedContext;
    private ImFontPtr? _cachedPointer;

    internal FontResource(FontData data) => Data = data;

    internal FontData Data { get; }

    public FontResource Clone(int size) =>
        new(new FontData(Data.Metadata, size, Data.Fallback?.WithSize(size)));

    public ImFontPtr? GetPointer()
    {
        var context = ImGui.GetCurrentContext();
        if (_cachedPointer != null && _cachedContext == context)
            return _cachedPointer;

        _cachedContext = context;
        _cachedPointer = FontFactory.GetPointer(Data);
        return _cachedPointer;
    }
}

internal sealed record FontMetaData(string Name, string Path, bool IsTemporary = false);

internal sealed record FontData(FontMetaData Metadata, int Size, FontData? Fallback = null)
{
    public FontData WithSize(int size) =>
        new(Metadata, size, Fallback?.WithSize(size));
}
