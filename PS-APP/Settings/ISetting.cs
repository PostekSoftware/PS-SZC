using PS.APP.Localization;

namespace PS.APP.Settings;

public interface ISetting
{
    string Key { get; }

    LocalizedString Label { get; }

    string Category { get; }

    void Reset();

    void DrawEditor();

    object? GetValue();

    void SetValue(object? value);
}
