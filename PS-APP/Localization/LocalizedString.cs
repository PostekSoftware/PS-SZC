namespace PS.APP.Localization;

public struct LocalizedString : IEquatable<LocalizedString>
{
    private readonly string? _id;
    private readonly Func<object>[] _args;
    private readonly string? _fixedText;
    private string? _locale;
    private string? _localizedText;

    public bool IsEmpty => string.IsNullOrEmpty(_fixedText) && string.IsNullOrEmpty(_id);

    private LocalizedString(string? id, string? fixedText, Func<object>[] args)
    {
        _id = id;
        _fixedText = fixedText;
        _args = args;
    }

    public static LocalizedString FromId(string localizationId) =>
        new(localizationId, null, []);

    public static LocalizedString FromId(string localizationId, params Func<object>[] args) =>
        new(localizationId, null, args);

    public static LocalizedString FromText(string? fixedText) =>
        new(null, fixedText ?? string.Empty, []);

    public override string ToString()
    {
        if (_fixedText != null)
            return _fixedText;

        var localizer = Application.Instance?.Localizer;
        if (localizer == null || _id == null)
            return string.Empty;

        if (localizer.CurrentLocale == _locale && _args.Length <= 0 && _localizedText != null)
            return _localizedText;

        _locale = localizer.CurrentLocale;
        var args = _args.Select(x => x()).ToArray();
        return _localizedText = localizer.Localize(_id, args);
    }

    public bool Equals(LocalizedString other) =>
        _fixedText != null
            ? _fixedText == other._fixedText
            : other._fixedText == null && _id == other._id;

    public override bool Equals(object? obj) => obj is LocalizedString other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_id, _fixedText);

    public static implicit operator LocalizedString(string? s) => FromText(s);

    public static implicit operator string(LocalizedString s) => s.ToString();
}
