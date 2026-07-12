using Hexa.NET.ImGui;
using PS.APP.Localization;
using PS.APP.Menus;
using System.Numerics;

namespace PS.APP.Settings;

public sealed class SettingsForm : Form
{
    private readonly SettingsManager _settings;
    private readonly Dictionary<string, object?> _snapshot;
    private readonly string? _initialLocale;

    public SettingsForm(SettingsManager settings)
    {
        _settings = settings;
        _snapshot = settings.Settings.ToDictionary(x => x.Key, x => x.GetValue());
        _initialLocale = Application.Instance.Localizer?.CurrentLocale;
        Title = LocalizedString.FromId("Settings.Title");
        Padding = new Vector2(24, 24);
    }

    public override void Draw()
    {
        ImGui.Text(LocalizedString.FromId("Settings.Title"));
        ImGui.Spacing();

        var footerHeight = ImGui.GetFrameHeightWithSpacing() * 2;
        if (ImGui.BeginChild("SettingsContent", new Vector2(0, -footerHeight)))
        {
            DrawSettingsList();
            DrawLocaleSettingIfAvailable();
            ImGui.EndChild();
        }

        ImGui.Separator();
        DrawButtons();
    }

    protected override void BuildMainMenu(MainMenuBuilder menu)
    {
        var file = menu.AddMenu("File");
        file.AddItem(LocalizedString.FromId("Settings.Save").ToString(), SaveAndClose, "Ctrl+S");
        file.AddItem(LocalizedString.FromId("Settings.Cancel").ToString(), CloseWithoutSaving, "Esc");
        file.AddSeparator();
        file.AddItem(LocalizedString.FromId("Settings.Reset").ToString(), ResetSettings);
    }

    private void SaveAndClose()
    {
        _settings.Save();
        Application.Instance.ReturnToMainForm();
    }

    private void ResetSettings()
    {
        _settings.ResetAll();
        _settings.MarkDirty();
    }

    private void DrawSettingsList()
    {
        foreach (var group in _settings.GetByCategory())
        {
            if (!string.IsNullOrEmpty(group.Key))
                ImGui.SeparatorText(LocalizedString.FromId(group.Key));

            foreach (var setting in group)
                setting.DrawEditor();
        }
    }

    private static void DrawLocaleSettingIfAvailable()
    {
        var localizer = Application.Instance.Localizer;
        if (localizer == null)
            return;

        ImGui.SeparatorText(LocalizedString.FromId("Settings.Appearance"));

        var current = localizer.CurrentLocale ?? string.Empty;
        if (ImGui.BeginCombo(LocalizedString.FromId("App.Language"), localizer.GetLanguageName(current)))
        {
            foreach (var locale in localizer.GetLocales())
            {
                if (ImGui.Selectable(localizer.GetLanguageName(locale), locale == current))
                    localizer.ChangeLocale(locale);
            }

            ImGui.EndCombo();
        }
    }

    private void DrawButtons()
    {
        if (ImGui.Button(LocalizedString.FromId("Settings.Save")))
            SaveAndClose();

        ImGui.SameLine();

        if (ImGui.Button(LocalizedString.FromId("Settings.Cancel")))
            CloseWithoutSaving();

        ImGui.SameLine();

        if (ImGui.Button(LocalizedString.FromId("Settings.Reset")))
            ResetSettings();
    }

    private void CloseWithoutSaving()
    {
        foreach (var (key, value) in _snapshot)
        {
            if (_settings.Settings.FirstOrDefault(x => x.Key == key) is { } setting)
                setting.SetValue(value);
        }

        if (!string.IsNullOrWhiteSpace(_initialLocale))
            Application.Instance.Localizer?.ChangeLocale(_initialLocale, persist: false);

        Application.Instance.ReturnToMainForm();
    }
}
