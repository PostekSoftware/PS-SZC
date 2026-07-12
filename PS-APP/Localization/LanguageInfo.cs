namespace PS.APP.Localization;

public record LanguageInfo(string Locale, string LanguageName, IDictionary<string, string> Entries);
