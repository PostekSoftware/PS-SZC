using System.Text.Json;
using System.Text.Json.Serialization;

namespace PS.APP.Localization;

public sealed class FileLocalizer : ILocalizer
{
    private readonly string _legacyLocaleFilePath;
    private readonly string[] _sourceFilePaths;
    private readonly string _preferencesFilePath;
    private readonly string _undefinedValue;
    private Dictionary<string, LanguageInfo> _languages = [];
    private string _defaultLocale = "en";

    public FileLocalizer(string filePath, string undefinedValue = "<undefined>")
        : this(filePath, [filePath], undefinedValue)
    {
    }

    public FileLocalizer(string writeFilePath, IEnumerable<string> sourceFilePaths, string undefinedValue = "<undefined>")
    {
        _legacyLocaleFilePath = writeFilePath;
        _sourceFilePaths = sourceFilePaths.ToArray();
        _preferencesFilePath = AppPaths.UserDataFile("user-settings.json");
        _undefinedValue = undefinedValue;
        Load();
    }

    public static FileLocalizer CreateMerged(string undefinedValue = "<undefined>") =>
        new(
            ContentPaths.App("localization.json"),
            [
                ContentPaths.Library("localization.json"),
                ContentPaths.App("localization.json")
            ],
            undefinedValue);

    public string? CurrentLocale { get; private set; }

    public IList<string> GetLocales() => _languages.Keys.ToArray();

    public string GetLanguageName(string locale) =>
        _languages.TryGetValue(locale, out var language) ? language.LanguageName : _undefinedValue;

    public void ChangeLocale(string locale, bool persist = true)
    {
        if (!_languages.ContainsKey(locale))
            return;

        CurrentLocale = locale;
        if (persist)
            SaveCurrentLocale();
    }

    public bool TryLocalize(string localizationId, out string localization, params object[] args)
    {
        localization = Localize(localizationId, args);
        return localization != _undefinedValue;
    }

    public string Localize(string localizationId, params object[] args)
    {
        if (CurrentLocale != null &&
            _languages.TryGetValue(CurrentLocale, out var current) &&
            current.Entries.TryGetValue(localizationId, out var value))
            return string.Format(value, args);

        if (_languages.TryGetValue(_defaultLocale, out var fallback) &&
            fallback.Entries.TryGetValue(localizationId, out value))
            return string.Format(value, args);

        return _undefinedValue;
    }

    public void Reload() => Load();

    private void Load()
    {
        _languages = [];
        _defaultLocale = "en";
        CurrentLocale = null;

        var loadedAny = false;
        foreach (var sourcePath in _sourceFilePaths)
        {
            if (!File.Exists(sourcePath))
                continue;

            MergeFile(sourcePath);
            loadedAny = true;
        }

        if (!loadedAny)
            throw new FileNotFoundException($"No localization files were found. Expected one of: {string.Join(", ", _sourceFilePaths)}");

        var savedLocale = LoadSavedLocale();
        if (!string.IsNullOrWhiteSpace(savedLocale) && _languages.ContainsKey(savedLocale))
            CurrentLocale = savedLocale;
        else if (CurrentLocale == null || !_languages.ContainsKey(CurrentLocale))
            CurrentLocale = _languages.ContainsKey(_defaultLocale)
                ? _defaultLocale
                : _languages.Keys.FirstOrDefault();
    }

    private void MergeFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<LocalizationFile>(json, JsonOptions)
                   ?? throw new InvalidOperationException($"Failed to parse localization file: {filePath}");

        if (!string.IsNullOrWhiteSpace(data.DefaultLocale))
            _defaultLocale = data.DefaultLocale;

        foreach (var language in data.Languages)
        {
            if (!_languages.TryGetValue(language.Locale, out var existing))
            {
                _languages[language.Locale] = new LanguageInfo(
                    language.Locale,
                    language.Name,
                    new Dictionary<string, string>(language.Entries));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(language.Name))
                existing = new LanguageInfo(language.Locale, language.Name, existing.Entries);

            foreach (var (key, value) in language.Entries)
                existing.Entries[key] = value;

            _languages[language.Locale] = existing;
        }
    }

    private string? LoadSavedLocale()
    {
        if (File.Exists(_preferencesFilePath))
        {
            var saved = ReadLocaleFromPreferences(_preferencesFilePath);
            if (!string.IsNullOrWhiteSpace(saved))
                return saved;
        }

        if (!File.Exists(_legacyLocaleFilePath))
            return null;

        var legacyLocale = ReadLocaleFromLocalizationFile(_legacyLocaleFilePath);
        if (string.IsNullOrWhiteSpace(legacyLocale))
            return null;

        SaveLocalePreference(legacyLocale);
        return legacyLocale;
    }

    private static string? ReadLocaleFromPreferences(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<UserPreferences>(json, JsonOptions);
            return string.IsNullOrWhiteSpace(data?.CurrentLocale) ? null : data.CurrentLocale;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadLocaleFromLocalizationFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<LocalizationFile>(json, JsonOptions);
            return string.IsNullOrWhiteSpace(data?.CurrentLocale) ? null : data.CurrentLocale;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void SaveCurrentLocale()
    {
        if (CurrentLocale == null)
            return;

        SaveLocalePreference(CurrentLocale);
    }

    private void SaveLocalePreference(string locale)
    {
        var directory = Path.GetDirectoryName(_preferencesFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var data = new UserPreferences { CurrentLocale = locale };
        File.WriteAllText(_preferencesFilePath, JsonSerializer.Serialize(data, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class LocalizationFile
    {
        public string DefaultLocale { get; set; } = "en";

        public string? CurrentLocale { get; set; }

        public List<LanguageDefinition> Languages { get; set; } = [];
    }

    private sealed class LanguageDefinition
    {
        public string Locale { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public Dictionary<string, string> Entries { get; set; } = [];
    }

    private sealed class UserPreferences
    {
        public string? CurrentLocale { get; set; }
    }
}
