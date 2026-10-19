using System.IO;
using System.Text.RegularExpressions;
using Dangpu.Models;

namespace Dangpu.Services;

/// <summary>
/// 只读 Latest.log 和模组元数据做规则判断，不上传、不改文件。
/// 目的不是给“标准答案”，而是把可疑的模组和证据摆出来。
/// </summary>
public static class DiagnosticService
{
    private static readonly Regex ExceptionRegex = new(@"([A-Za-z0-9_.]+Exception)", RegexOptions.Compiled);
    private static readonly Regex MissingAssemblyRegex = new(@"Could not load file or assembly '([^']+)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<DiagnosticFinding> Analyze(GameInfo game, IReadOnlyList<ModItem> mods, LogSnapshot log, LoaderStatus loader)
    {
        var findings = new List<DiagnosticFinding>();
        var enabled = mods.Where(m => m.Enabled).ToList();

        // 1. 加载器本身
        if (game.Found && !loader.Installed)
        {
            findings.Add(new DiagnosticFinding
            {
                Level = FindingLevel.Critical,
                Title = "没有安装 MelonLoader",
                Detail = "现在 Mods 里的 dll 不会被加载，日志里也不会有模组记录。",
                Suggestion = "去「设置 → 加载器」安装或修复 MelonLoader，然后启动一次游戏。"
            });
            return findings;
        }

        // 2. 加载失败 / 兼容性
        foreach (var failed in log.FailedDlls.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var suspects = ResolveSuspects(failed, mods);
            findings.Add(new DiagnosticFinding
            {
                Level = FindingLevel.Critical,
                Title = $"{failed} 没被加载",
                Detail = "MelonLoader 记录了一次加载失败。模组本身或者它依赖的程序集有问题。",
                Suggestion = "先单独启用它再启动；如果只有它报错，检查版本是否匹配 0.46D。",
                Evidence = FindEvidence(log, failed),
                Suspects = suspects
            });
        }

        // 3. 会被静默丢弃的模组
        var noInfo = enabled.Where(m => !m.HasMelonInfo).ToList();
        if (noInfo.Count > 0)
        {
            findings.Add(new DiagnosticFinding
            {
                Level = FindingLevel.Critical,
                Title = $"{noInfo.Count} 个启用的模组没有 MelonInfo",
                Detail = "这些文件会在预扫描阶段被静默丢掉，日志里连报错都没有：" +
                         string.Join("、", noInfo.Select(m => m.FileName)),
                Suggestion = "换用作者发布的正式版本；自己编译的话补上 [assembly: MelonInfo]。",
                Suspects = noInfo.Select(m => m.DisplayName).ToList()
            });
        }

        // 4. 缺前置
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in mods)
        {
            if (!string.IsNullOrWhiteSpace(m.AssemblyName)) installed.Add(m.AssemblyName);
            installed.Add(m.FileBaseName);
            foreach (var d in m.DllNames)
                installed.Add(Path.GetFileNameWithoutExtension(d.Replace(ModScanner.DisabledSuffix, "", StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var mod in enabled)
        {
            var missing = mod.Dependencies.Where(d => !installed.Contains(d)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (missing.Count == 0) continue;
            findings.Add(new DiagnosticFinding
            {
                Level = FindingLevel.Warning,
                Title = $"{mod.DisplayName} 缺少前置",
                Detail = "它声明了这些依赖，但 Mods 里没找到：" + string.Join("、", missing),
                Suggestion = "把缺少的模组装进 Mods，或者先停用它。",
                Suspects = new List<string> { mod.DisplayName }
            });
        }

        // 5. 重复程序集
        foreach (var group in enabled.Where(m => !string.IsNullOrWhiteSpace(m.AssemblyName))
                     .GroupBy(m => m.AssemblyName, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            var names = group.Select(m => m.FileName).ToList();
            findings.Add(new DiagnosticFinding
            {
                Level = FindingLevel.Warning,
                Title = "同一个程序集启用了两次",
                Detail = string.Join("、", names) + $" 都是 {group.Key}，补丁会被注册两遍。",
                Suggestion = "只保留一个，或者把重复的那个停用。",
                Suspects = group.Select(m => m.DisplayName).ToList()
            });
        }

        // 6. 启用了但日志里没加载
        if (log.Exists && log.LoadedDlls.Count > 0)
        {
            var notLoaded = enabled.Where(m => m.DllNames.Count > 0 && !m.DllNames.Any(d =>
                log.LoadedDlls.Contains(d.Replace(ModScanner.DisabledSuffix, "", StringComparison.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase))).ToList();

            if (notLoaded.Count > 0)
            {
                findings.Add(new DiagnosticFinding
                {
                    Level = FindingLevel.Warning,
                    Title = $"{notLoaded.Count} 个启用的模组没有出现在日志里",
                    Detail = "本次日志加载了 " + log.LoadedDlls.Count + " 个模组，这些不在里面：" +
                             string.Join("、", notLoaded.Select(m => m.FileName)),
                    Suggestion = "可能是刚装还没重启游戏；如果重启后仍然没有，检查 MelonInfo 和加载器版本。",
                    Suspects = notLoaded.Select(m => m.DisplayName).ToList()
                });
            }
        }
        else if (log.Exists && enabled.Count > 0 && log.LoadedDlls.Count == 0)
        {
            findings.Add(new DiagnosticFinding
            {
                Level = FindingLevel.Warning,
                Title = "日志里没有任何模组被加载",
                Detail = "加载器在，但一个 Melon Assembly 都没读到。",
                Suggestion = "确认 Mods 路径正确、加载器没有被停用，然后重新启动一次。"
            });
        }

        // 7. 异常聚合
        var exceptionGroups = new Dictionary<string, (int count, string sample, HashSet<string> suspects)>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in log.Lines.Where(l => l.Level == LogLevel.Error))
        {
            if (line.Text.Contains("Failed to load Melon Assembly", StringComparison.OrdinalIgnoreCase)) continue;

            var match = ExceptionRegex.Match(line.Text);
            if (!match.Success) continue;

            var type = match.Groups[1].Value;
            if (!exceptionGroups.TryGetValue(type, out var entry))
                entry = (0, line.Text, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            entry.count++;
            foreach (var suspect in ResolveSuspects(line.Text, mods)) entry.suspects.Add(suspect);
            entry.sample = line.Text;
            exceptionGroups[type] = entry;
        }

        foreach (var (type, entry) in exceptionGroups)
        {
            var simple = type.Contains('.') ? type[(type.LastIndexOf('.') + 1)..] : type;
            var severe = simple is "TypeLoadException" or "MissingMethodException" or "BadImageFormatException" or "OutOfMemoryException";
            findings.Add(new DiagnosticFinding
            {
                Level = severe ? FindingLevel.Critical : FindingLevel.Warning,
                Title = FriendlyExceptionName(simple),
                Detail = $"日志里出现 {entry.count} 次。最后一次：" + Trim(entry.sample, 180),
                Suggestion = severe
                    ? "多半是模组和游戏版本对不上；先停用可疑模组，再逐个启用确认。"
                    : "先看可疑模组；如果只在某个动作后出现，把它的补丁和最近装的模组一起排查。",
                Evidence = entry.sample,
                Suspects = entry.suspects.ToList(),
                Count = entry.count
            });
        }

        // 8. Harmony 冲突痕迹
        var harmonyLines = log.Lines.Where(l =>
            l.Text.Contains("already patched", StringComparison.OrdinalIgnoreCase) ||
            l.Text.Contains("patch conflict", StringComparison.OrdinalIgnoreCase) ||
            l.Text.Contains("HarmonyException", StringComparison.OrdinalIgnoreCase) ||
            l.Text.Contains("was already patched", StringComparison.OrdinalIgnoreCase)).ToList();

        if (harmonyLines.Count > 0)
        {
            var suspects = harmonyLines.SelectMany(l => ResolveSuspects(l.Text, mods)).Distinct().ToList();
            findings.Add(new DiagnosticFinding
            {
                Level = FindingLevel.Critical,
                Title = "检测到 Harmony 补丁冲突",
                Detail = $"日志里有 {harmonyLines.Count} 行补丁重复/冲突的痕迹。",
                Suggestion = "两个模组改了同一个方法。先停用其中一个；如果知道是谁，优先保留作者明确说明兼容的那个。",
                Evidence = harmonyLines[0].Text,
                Suspects = suspects,
                Count = harmonyLines.Count
            });
        }

        // 9. trampoline 刷屏
        var trampolines = log.Lines.Where(l => l.Text.Contains("native->managed trampoline", StringComparison.OrdinalIgnoreCase)).ToList();
        if (trampolines.Count > 20)
        {
            findings.Add(new DiagnosticFinding
            {
                Level = FindingLevel.Warning,
                Title = "异常在 Il2Cpp 桥接层反复刷屏",
                Detail = $"日志里有 {trampolines.Count} 行 trampoline 报错。这通常是某个模组的补丁在每次调用时都抛异常。",
                Suggestion = "它会拖慢游戏。结合上面的异常条目找到那个模组，先停用它。",
                Evidence = trampolines[0].Text,
                Count = trampolines.Count
            });
        }

        // 10. 缺程序集
        var missingAsm = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in log.Lines)
        {
            var m = MissingAssemblyRegex.Match(line.Text);
            if (m.Success) missingAsm.Add(m.Groups[1].Value);
        }

        foreach (var asm in missingAsm)
        {
            var suspects = enabled.Where(m => m.Dependencies.Contains(asm, StringComparer.OrdinalIgnoreCase) ||
                                              m.References.Contains(asm, StringComparer.OrdinalIgnoreCase))
                                  .Select(m => m.DisplayName).ToList();
            findings.Add(new DiagnosticFinding
            {
                Level = FindingLevel.Critical,
                Title = $"缺少程序集 {asm}",
                Detail = "有模组在运行时要加载它，但没找到。",
                Suggestion = "把对应的前置模组装进 Mods；或者停用引用了它的模组。",
                Suspects = suspects
            });
        }

        return findings
            .OrderBy(f => f.Level)
            .ThenByDescending(f => f.Count)
            .ToList();
    }

    private static string FriendlyExceptionName(string type) => type switch
    {
        "TypeLoadException" => "类型加载失败（TypeLoadException）",
        "MissingMethodException" => "找不到方法（MissingMethodException）",
        "BadImageFormatException" => "程序集格式不对（BadImageFormatException）",
        "NullReferenceException" => "空引用异常（NullReferenceException）",
        "OutOfMemoryException" => "内存不够（OutOfMemoryException）",
        "InvalidOperationException" => "无效操作（InvalidOperationException）",
        "ArgumentException" => "参数异常（ArgumentException）",
        "TargetInvocationException" => "反射调用里抛了异常",
        _ => type
    };

    private static List<string> ResolveSuspects(string text, IReadOnlyList<ModItem> mods)
    {
        var hits = new List<string>();
        foreach (var mod in mods)
        {
            if (string.IsNullOrWhiteSpace(mod.AssemblyName)) continue;
            if (text.Contains(mod.AssemblyName, StringComparison.OrdinalIgnoreCase) ||
                text.Contains(mod.FileBaseName, StringComparison.OrdinalIgnoreCase))
                hits.Add(mod.DisplayName);
        }
        return hits.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList();
    }

    private static string FindEvidence(LogSnapshot log, string keyword)
    {
        var line = log.Lines.FirstOrDefault(l => l.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        return line?.Text ?? "";
    }

    private static string Trim(string text, int max)
    {
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length <= max ? text : text[..max].TrimEnd() + "…";
    }
}
