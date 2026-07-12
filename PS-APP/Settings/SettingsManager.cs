using PS.APP.Localization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PS.APP.Settings;

public sealed class SettingsManager
{
    private readonly Dictionary<string, ISetting> _settings = [];
    private readonly string _filePath;

    public SettingsManager(string filePath = "settings.json")
    {
        _filePath = filePath;
    }

    public bool IsDirty { get; private set; }

    public bool AutoSaveOnExit { get; set; } = true;

    public IReadOnlyCollection<ISetting> Settings => _settings.Values;

    public Setting<bool> AddBool(string key, bool defaultValue, LocalizedString label, string category = "")
    {
        var setting = new Setting<bool>(key, defaultValue, label, category);
        Register(setting);
        return setting;
    }

    public Setting<int> AddInt(string key, int defaultValue, LocalizedString label, string category = "",
        int? min = null, int? max = null)
    {
        var setting = new Setting<int>(key, defaultValue, label, category)
        {
            IntMin = min,
            IntMax = max
        };
        Register(setting);
        return setting;
    }

    public Setting<float> AddFloat(string key, float defaultValue, LocalizedString label, string category = "",
        float? min = null, float? max = null)
    {
        var setting = new Setting<float>(key, defaultValue, label, category)
        {
            FloatMin = min,
            FloatMax = max
        };
        Register(setting);
        return setting;
    }

    public Setting<string> AddString(string key, string defaultValue, LocalizedString label, string category = "")
    {
        var setting = new Setting<string>(key, defaultValue, label, category);
        Register(setting);
        return setting;
    }

    public Setting<string> AddChoice(string key, string defaultValue, LocalizedString label, string[] choices,
        string category = "")
    {
        var setting = new Setting<string>(key, defaultValue, label, category) { Choices = choices };
        Register(setting);
        return setting;
    }

    public Setting<T> AddEnum<T>(string key, T defaultValue, LocalizedString label, string category = "")
        where T : struct, Enum
    {
        var setting = new Setting<T>(key, defaultValue, label, category);
        Register(setting);
        return setting;
    }

    public T Get<T>(string key)
    {
        if (!_settings.TryGetValue(key, out var setting))
            throw new KeyNotFoundException($"Setting '{key}' is not registered.");

        return (T)setting.GetValue()!;
    }

    public bool TryGet<T>(string key, out T value)
    {
        if (_settings.TryGetValue(key, out var setting) && setting.GetValue() is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            IsDirty = false;
            return;
        }

        var json = File.ReadAllText(_filePath);
        var root = JsonNode.Parse(json) as JsonObject;
        if (root == null)
            return;

        foreach (var (key, node) in root)
        {
            if (!_settings.TryGetValue(key, out var setting) || node == null)
                continue;

            setting.SetValue(ReadNode(node, setting.GetValue()?.GetType() ?? typeof(object)));
        }

        IsDirty = false;
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var root = new JsonObject();
        foreach (var setting in _settings.Values.OrderBy(x => x.Key))
        {
            var node = ToJsonNode(setting.GetValue());
            if (node != null)
                root[setting.Key] = node;
        }

        File.WriteAllText(_filePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        IsDirty = false;
    }

    private static JsonNode? ToJsonNode(object? value) => value switch
    {
        null => null,
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        float f => JsonValue.Create(f),
        double d => JsonValue.Create(d),
        string s => JsonValue.Create(s),
        Enum e => JsonValue.Create(e.ToString()),
        _ => JsonSerializer.SerializeToNode(value)
    };

    public void ResetAll()
    {
        foreach (var setting in _settings.Values)
            setting.Reset();
        IsDirty = true;
    }

    internal void MarkDirty() => IsDirty = true;

    internal IEnumerable<IGrouping<string, ISetting>> GetByCategory() =>
        _settings.Values
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Key)
            .GroupBy(x => x.Category);

    private void Register(ISetting setting)
    {
        if (!_settings.TryAdd(setting.Key, setting))
            throw new InvalidOperationException($"Setting '{setting.Key}' is already registered.");
    }

    private static object? ReadNode(JsonNode node, Type type)
    {
        if (type == typeof(bool) && node.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
            return node.GetValue<bool>();
        if (type == typeof(int) && node.GetValueKind() == JsonValueKind.Number)
            return node.GetValue<int>();
        if (type == typeof(float) && node.GetValueKind() == JsonValueKind.Number)
            return node.GetValue<float>();
        if (type == typeof(string) && node.GetValueKind() == JsonValueKind.String)
            return node.GetValue<string>();
        if (type.IsEnum && node.GetValueKind() == JsonValueKind.String)
            return Enum.Parse(type, node.GetValue<string>()!);

        return node.Deserialize(type);
    }
}
