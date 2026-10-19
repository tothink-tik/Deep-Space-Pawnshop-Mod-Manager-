using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace Dangpu.Services;

public enum ConfigKind { Bool, Int, Float, String, Inline, Raw }

public sealed class ConfigEntry : ObservableObject
{
    public ConfigSection Section { get; set; } = null!;
    public string Key { get; set; } = "";
    public string Comment { get; set; } = "";
    public int LineIndex { get; set; }
    public int ValueStart { get; set; }
    public ConfigKind Kind { get; set; }
    public string RawValue { get; set; } = "";

    private string _value = "";
    public string Value
    {
        get => _value;
        set
        {
            if (!Set(ref _value, value)) return;
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(BoolValue));
            OnPropertyChanged(nameof(KindHint));
        }
    }

    public bool IsDirty => !string.Equals(Value, RawValue, StringComparison.Ordinal);

    public bool IsEditable => Kind != ConfigKind.Inline;

    public bool IsBool => IsEditable && Kind == ConfigKind.Bool;
    public bool IsText => IsEditable && Kind != ConfigKind.Bool;
    public bool IsInline => !IsEditable;

    public bool BoolValue
    {
        get => string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase);
        set => Value = value ? "true" : "false";
    }

    public string KindHint => Kind switch
    {
        ConfigKind.Bool => "开关",
        ConfigKind.Int => "整数",
        ConfigKind.Float => "小数",
        ConfigKind.String => "文本",
        ConfigKind.Inline => "内联表 · 只读",
        _ => ""
    };

    public string DisplayValue => Value;
}

public sealed class ConfigSection : ObservableObject
{
    public string Name { get; set; } = "";
    public ObservableCollection<ConfigEntry> Entries { get; } = new();
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "未分组" : Name;
    public string CountText => Entries.Count.ToString();
}

public sealed class ConfigDocument : ObservableObject
{
    private readonly List<string> _lines = new();
    public string Path { get; private set; } = "";
    public ObservableCollection<ConfigSection> Sections { get; } = new();
    public ObservableCollection<ConfigEntry> AllEntries { get; } = new();
    public bool Exists { get; private set; }
    public string LoadError { get; private set; } = "";

    public int EditableCount => AllEntries.Count(e => e.IsEditable);

    public bool IsDirty => AllEntries.Any(e => e.IsDirty);

    public static ConfigDocument Load(string path)
    {
        var doc = new ConfigDocument { Path = path };
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            doc.LoadError = "没有找到 MelonPreferences.cfg";
            return doc;
        }

        doc.Exists = true;
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            doc.LoadError = ex.Message;
            return doc;
        }

        var section = new ConfigSection { Name = "" };
        doc.Sections.Add(section);
        doc._lines.AddRange(lines);

        var pendingComments = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']') && trimmed.Length > 2)
            {
                var name = trimmed[1..^1];
                section = doc.Sections.FirstOrDefault(s => s.Name == name) ?? new ConfigSection { Name = name };
                if (!doc.Sections.Contains(section)) doc.Sections.Add(section);
                pendingComments.Clear();
                continue;
            }

            if (trimmed.Length == 0)
            {
                pendingComments.Clear();
                continue;
            }

            if (trimmed.StartsWith('#'))
            {
                pendingComments.Add(trimmed.TrimStart('#').Trim());
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                pendingComments.Clear();
                continue;
            }

            var key = line[..eq].Trim();
            var valueStart = eq + 1;
            while (valueStart < line.Length && line[valueStart] == ' ') valueStart++;
            var value = line[valueStart..].TrimEnd();

            var entry = new ConfigEntry
            {
                Section = section,
                Key = key,
                Comment = pendingComments.Count == 0 ? "" : string.Join(" ", pendingComments.Take(2)),
                LineIndex = i,
                ValueStart = valueStart,
                RawValue = value,
                Value = value,
                Kind = DetectKind(value)
            };

            section.Entries.Add(entry);
            doc.AllEntries.Add(entry);
            entry.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ConfigEntry.IsDirty))
                    doc.OnPropertyChanged(nameof(IsDirty));
            };
            pendingComments.Clear();
        }

        return doc;
    }

    private static ConfigKind DetectKind(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return ConfigKind.Raw;
        if (v.StartsWith('{') || v.StartsWith('[')) return ConfigKind.Inline;
        if (v.StartsWith('"')) return ConfigKind.String;
        if (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("false", StringComparison.OrdinalIgnoreCase)) return ConfigKind.Bool;
        if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return ConfigKind.Int;
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return ConfigKind.Float;
        return ConfigKind.Raw;
    }

    public (bool ok, string message) Save()
    {
        if (!Exists) return (false, "配置文件不存在");
        if (!IsDirty) return (true, "没有改动");

        try
        {
            foreach (var entry in AllEntries.Where(e => e.IsDirty && e.IsEditable))
            {
                if (entry.Kind == ConfigKind.Int &&
                    !long.TryParse(entry.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    return (false, $"{entry.Key} 只能填整数，现在是「{entry.Value}」");

                if (entry.Kind == ConfigKind.Float &&
                    !double.TryParse(entry.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    return (false, $"{entry.Key} 只能填数字，现在是「{entry.Value}」");

                var current = _lines[entry.LineIndex];
                var eq = current.IndexOf('=');
                if (eq <= 0 || current[..eq].Trim() != entry.Key)
                    return (false, "配置文件在外部被改过了，点「还原」重新读一遍再改");
            }

            var backup = Path + ".bak-dangpu-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(Path, backup, true);

            foreach (var entry in AllEntries.Where(e => e.IsDirty && e.IsEditable))
            {
                var line = _lines[entry.LineIndex];
                var prefix = line[..entry.ValueStart];
                _lines[entry.LineIndex] = prefix + Format(entry);
            }

            File.WriteAllLines(Path, _lines, new UTF8Encoding(false));

            foreach (var entry in AllEntries)
            {
                entry.RawValue = entry.Value;
                entry.Notify(nameof(ConfigEntry.IsDirty));
            }

            OnPropertyChanged(nameof(IsDirty));
            return (true, $"已保存，旧文件备份为 {System.IO.Path.GetFileName(backup)}");
        }
        catch (Exception ex)
        {
            return (false, "保存失败：" + ex.Message);
        }
    }

    private static string Format(ConfigEntry e)
    {
        var v = e.Value.Trim();
        return e.Kind switch
        {
            ConfigKind.Bool => v.Equals("true", StringComparison.OrdinalIgnoreCase) ? "true" : "false",
            ConfigKind.String => "\"" + v.Trim('"').Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
            _ => v
        };
    }

    public void Reload()
    {
        var fresh = Load(Path);
        Sections.Clear();
        AllEntries.Clear();
        _lines.Clear();
        foreach (var s in fresh.Sections) Sections.Add(s);
        foreach (var e in fresh.AllEntries) AllEntries.Add(e);
        _lines.AddRange(File.Exists(Path) ? File.ReadAllLines(Path, Encoding.UTF8) : Array.Empty<string>());
        OnPropertyChanged(nameof(IsDirty));
    }
}
