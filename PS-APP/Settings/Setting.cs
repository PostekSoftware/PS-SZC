using Hexa.NET.ImGui;
using PS.APP.Localization;
using System.Numerics;

namespace PS.APP.Settings;

public sealed class Setting<T> : ISetting
{
    public Setting(string key, T defaultValue, LocalizedString label, string category = "")
    {
        Key = key;
        DefaultValue = defaultValue;
        Value = defaultValue;
        Label = label;
        Category = category;
    }

    public string Key { get; }

    public LocalizedString Label { get; }

    public string Category { get; }

    public T DefaultValue { get; }

    public T Value { get; set; }

    public float? FloatMin { get; init; }

    public float? FloatMax { get; init; }

    public int? IntMin { get; init; }

    public int? IntMax { get; init; }

    public string[]? Choices { get; init; }

    public void Reset() => Value = DefaultValue;

    public object? GetValue() => Value;

    public void SetValue(object? value)
    {
        if (value is T typed)
            Value = typed;
        else if (value != null)
            Value = (T)Convert.ChangeType(value, typeof(T));
    }

    public void DrawEditor()
    {
        ImGui.PushID(Key);

        if (typeof(T) == typeof(bool))
            DrawBool();
        else if (typeof(T) == typeof(int))
            DrawInt();
        else if (typeof(T) == typeof(float))
            DrawFloat();
        else if (typeof(T) == typeof(string))
            DrawString();
        else if (typeof(T).IsEnum)
            DrawEnum();
        else
            ImGui.TextDisabled($"{Label}: unsupported type");

        ImGui.PopID();
    }

    private void DrawBool()
    {
        var value = Convert.ToBoolean(Value);
        ImGui.Checkbox(Label, ref value);
        Value = (T)(object)value;
    }

    private void DrawInt()
    {
        var value = Convert.ToInt32(Value);
        if (IntMin.HasValue && IntMax.HasValue)
            ImGui.SliderInt(Label, ref value, IntMin.Value, IntMax.Value);
        else if (IntMin.HasValue)
            ImGui.InputInt(Label, ref value, 1, 10);
        else
            ImGui.InputInt(Label, ref value);
        Value = (T)(object)value;
    }

    private void DrawFloat()
    {
        var value = Convert.ToSingle(Value);
        if (FloatMin.HasValue && FloatMax.HasValue)
            ImGui.SliderFloat(Label, ref value, FloatMin.Value, FloatMax.Value);
        else
            ImGui.InputFloat(Label, ref value, 0.1f, 1f);
        Value = (T)(object)value;
    }

    private void DrawString()
    {
        if (Choices is { Length: > 0 })
        {
            var current = Value?.ToString() ?? string.Empty;
            if (ImGui.BeginCombo(Label, current))
            {
                foreach (var choice in Choices)
                {
                    if (ImGui.Selectable(choice, choice == current))
                        Value = (T)(object)choice;
                }
                ImGui.EndCombo();
            }
        }
        else
        {
            var buffer = Value?.ToString() ?? string.Empty;
            if (ImGui.InputText(Label, ref buffer, 256))
                Value = (T)(object)buffer;
        }
    }

    private void DrawEnum()
    {
        var enumType = typeof(T);
        var values = Enum.GetValues(enumType);
        var current = Value?.ToString() ?? string.Empty;

        if (ImGui.BeginCombo(Label, current))
        {
            foreach (var item in values)
            {
                var name = item.ToString() ?? string.Empty;
                if (ImGui.Selectable(name, name == current))
                    Value = (T)item;
            }
            ImGui.EndCombo();
        }
    }
}
