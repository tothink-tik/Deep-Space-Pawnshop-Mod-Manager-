using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Dangpu.Models;

namespace Dangpu.Services;

public sealed class InstallOutcome
{
    public List<string> Installed { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Skipped { get; } = new();
}

public static class ModScanner
{
    public const string DisabledSuffix = ".disabled";

    public static bool IsDllLike(string path) =>
        path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".dll" + DisabledSuffix, StringComparison.OrdinalIgnoreCase);

    public static bool IsEnabledDll(string path) =>
        path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

    public static List<ModItem> Scan(string modsDir)
    {
        var list = new List<ModItem>();
        if (!Directory.Exists(modsDir)) return list;

        try
        {
            foreach (var file in Directory.GetFiles(modsDir))
            {
                var name = Path.GetFileName(file);
                if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    list.Add(BuildFileItem(file, true));
                else if (name.EndsWith(".dll" + DisabledSuffix, StringComparison.OrdinalIgnoreCase))
                    list.Add(BuildFileItem(file, false));
            }
        }
        catch { }

        try
        {
            foreach (var dir in Directory.GetDirectories(modsDir))
            {
                var dlls = FindDlls(dir);
                if (dlls.Count == 0) continue;
                list.Add(BuildFolderItem(dir, dlls));
            }
        }
        catch { }

        PostProcess(list);
        return list.OrderBy(i => i.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static List<string> FindDlls(string dir)
    {
        try
        {
            return Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                .Where(IsDllLike)
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static ModItem BuildFileItem(string path, bool enabled)
    {
        var fi = new FileInfo(path);
        var item = new ModItem
        {
            Path = path,
            FileName = fi.Name,
            Enabled = enabled,
            Size = fi.Length,
            Modified = fi.LastWriteTime
        };
        ApplyMetadata(item, path);
        item.DllCount = 1;
        item.DllNames.Add(fi.Name);
        AddBaseIssues(item);
        return item;
    }

    private static ModItem BuildFolderItem(string dir, List<string> dlls)
    {
        var enabledDlls = dlls.Where(IsEnabledDll).ToList();
        var primary = enabledDlls.OrderByDescending(SafeLength).FirstOrDefault()
                      ?? dlls.OrderByDescending(SafeLength).First();

        var item = new ModItem
        {
            Path = dir,
            IsFolder = true,
            FileName = Path.GetFileName(dir),
            Enabled = enabledDlls.Count > 0,
            Size = dlls.Sum(SafeLength),
            Modified = Directory.GetLastWriteTime(dir),
            HasManifest = File.Exists(Path.Combine(dir, "manifest.json"))
        };
        ApplyMetadata(item, primary);
        item.DllCount = dlls.Count;
        foreach (var d in dlls) item.DllNames.Add(Path.GetFileName(d));
        AddBaseIssues(item);
        return item;
    }

    private static long SafeLength(string p)
    {
        try { return new FileInfo(p).Length; } catch { return 0; }
    }

    private static void ApplyMetadata(ModItem item, string dll)
    {
        var info = AssemblyMetadataReader.Read(dll);
        if (info == null) return;
        item.AssemblyName = info.AssemblyName;
        item.AssemblyVersion = info.AssemblyVersion;
        item.MelonName = info.MelonName;
        item.MelonVersion = info.MelonVersion;
        item.Author = info.Author;
        item.DownloadLink = info.DownloadLink;
        item.GameName = info.GameName;
        item.GameDeveloper = info.GameDeveloper;
        item.HasMelonInfo = info.HasMelonInfo;
        item.HasMelonGame = info.HasMelonGame;
        item.References.AddRange(info.References);
        item.Dependencies.AddRange(info.Dependencies);
        item.TileBrush = ModItem.TileFor(item.AssemblyName);
    }

    private static void AddBaseIssues(ModItem item)
    {
        if (!item.HasMelonInfo)
        {
            item.Issues.Add(new ModIssue
            {
                Level = IssueLevel.Error,
                Text = "没有 [assembly: MelonInfo] 声明，MelonLoader 会在预扫描阶段静默跳过它，日志里连报错都不会有。"
            });
        }

        if (item.IsFolder && !item.HasManifest)
        {
            item.Issues.Add(new ModIssue
            {
                Level = IssueLevel.Error,
                Text = "这个子文件夹里没有 manifest.json，MelonLoader 不会把它当成 Melon 子文件夹加载。"
            });
        }

        if (item.HasMelonGame && !string.IsNullOrWhiteSpace(item.GameName) &&
            !item.GameName!.Contains("Probably Stolen", StringComparison.OrdinalIgnoreCase))
        {
            item.Issues.Add(new ModIssue
            {
                Level = IssueLevel.Warning,
                Text = $"MelonGame 声明的是「{item.GameName}」，不是 Probably Stolen，加载时可能被判定为不兼容。"
            });
        }

        if (!item.HasMelonInfo && string.IsNullOrWhiteSpace(item.AssemblyName))
        {
            item.Issues.Add(new ModIssue
            {
                Level = IssueLevel.Error,
                Text = "读不出程序集信息，这个文件可能不是 .NET 程序集（或者是原生 dll，应放到 UserLibs）。"
            });
        }

        bool looksMono = item.References.Any(r => r.Equals("Assembly-CSharp", StringComparison.OrdinalIgnoreCase))
                         && !item.References.Any(r => r.Contains("Il2Cpp", StringComparison.OrdinalIgnoreCase));
        if (looksMono)
        {
            item.Issues.Add(new ModIssue
            {
                Level = IssueLevel.Warning,
                Text = "引用了 Assembly-CSharp 且没有 Il2CppInterop，看起来是 Mono 版构建；当前游戏是 IL2CPP，可能加载失败。"
            });
        }

        if (!string.IsNullOrWhiteSpace(item.AssemblyName) && !item.IsFolder)
        {
            var fileBase = Path.GetFileNameWithoutExtension(
                item.FileName.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase)
                    ? item.FileName[..^DisabledSuffix.Length]
                    : item.FileName);
            if (!string.Equals(fileBase, item.AssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                item.Issues.Add(new ModIssue
                {
                    Level = IssueLevel.Info,
                    Text = $"文件名（{fileBase}.dll）和程序集名（{item.AssemblyName}）不同。加载器按文件加载，不影响运行，但排查冲突时要按程序集名认人。"
                });
            }
        }
    }

    private static void PostProcess(List<ModItem> list)
    {
        var byAsm = list.Where(i => !string.IsNullOrWhiteSpace(i.AssemblyName))
                        .GroupBy(i => i.AssemblyName, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byAsm)
        {
            var alive = group.Where(g => g.Enabled).ToList();
            if (alive.Count > 1)
            {
                var names = string.Join("、", alive.Select(a => a.FileName));
                alive[0].Issues.Add(new ModIssue
                {
                    Level = IssueLevel.Warning,
                    Text = $"{names} 是同一个程序集（{group.Key}），同时启用多半会重复注册补丁。"
                });
            }
        }

        var installedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in list)
        {
            if (!string.IsNullOrWhiteSpace(item.AssemblyName)) installedNames.Add(item.AssemblyName);
            installedNames.Add(item.FileName.Replace(DisabledSuffix, "", StringComparison.OrdinalIgnoreCase));
            installedNames.Add(Path.GetFileNameWithoutExtension(item.FileName.Replace(DisabledSuffix, "", StringComparison.OrdinalIgnoreCase)));
            foreach (var d in item.DllNames)
            {
                installedNames.Add(d);
                installedNames.Add(Path.GetFileNameWithoutExtension(d.Replace(DisabledSuffix, "", StringComparison.OrdinalIgnoreCase)));
            }
        }

        foreach (var item in list)
        {
            foreach (var dep in item.Dependencies.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (installedNames.Contains(dep)) continue;
                item.Issues.Add(new ModIssue
                {
                    Level = IssueLevel.Warning,
                    Text = $"声明了前置依赖「{dep}」，但 Mods 里没有找到同名模组。"
                });
            }
        }
    }

    // ---------------- 启用 / 停用 ----------------

    public static (bool ok, string message) SetEnabled(ModItem item, bool enable)
    {
        try
        {
            if (!item.IsFolder)
            {
                return enable ? EnableFile(item.Path) : DisableFile(item.Path);
            }

            var dlls = FindDlls(item.Path);
            int changed = 0;
            var errors = new List<string>();
            foreach (var dll in dlls)
            {
                var result = enable ? EnableFile(dll) : DisableFile(dll);
                if (result.ok) changed++;
                else errors.Add(result.message);
            }

            if (errors.Count == 0)
                return (true, $"{(enable ? "启用" : "停用")}了 {changed} 个 dll");

            return (false, errors[0]);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static (bool ok, string message) EnableFile(string path)
    {
        if (!path.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase))
            return (true, "已经是启用状态");

        var target = path[..^DisabledSuffix.Length];
        if (File.Exists(target))
            return (false, $"无法启用：{Path.GetFileName(target)} 已经存在");

        File.Move(path, target);
        return (true, $"已启用 {Path.GetFileName(target)}");
    }

    private static (bool ok, string message) DisableFile(string path)
    {
        if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return (true, "已经停用");

        var target = path + DisabledSuffix;
        if (File.Exists(target))
            return (false, $"无法停用：{Path.GetFileName(target)} 已经存在");

        File.Move(path, target);
        return (true, $"已停用 {Path.GetFileName(path)}");
    }

    // ---------------- 安装 ----------------

    /// <summary>可以传文件，也可以传文件夹；文件夹会递归找 dll 和 zip。</summary>
    public static InstallOutcome InstallFromPaths(IEnumerable<string> paths, string modsDir)
    {
        var modsRoot = Path.GetFullPath(modsDir).TrimEnd(Path.DirectorySeparatorChar);
        var gameRoot = Path.GetDirectoryName(modsRoot) ?? "";
        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;

        bool Blocked(string file) =>
            IsUnder(file, modsRoot) ||
            IsUnder(file, Path.Combine(gameRoot, "MelonLoader")) ||
            IsUnder(file, Path.Combine(gameRoot, "Probably Stolen_Data")) ||
            IsUnder(file, Path.Combine(gameRoot, "UserLibs"));

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                if (IsInstallable(path) && !Blocked(path) && seen.Add(Path.GetFullPath(path))) files.Add(path);
                else skipped++;
                continue;
            }

            if (!Directory.Exists(path))
            {
                skipped++;
                continue;
            }

            IEnumerable<string> found;
            try
            {
                found = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            }
            catch
            {
                skipped++;
                continue;
            }

            foreach (var file in found)
            {
                if (!IsInstallable(file)) continue;
                if (Blocked(file))
                {
                    skipped++;
                    continue;
                }
                if (seen.Add(Path.GetFullPath(file))) files.Add(file);
            }
        }

        var outcome = Install(files, modsDir);
        if (files.Count == 0)
            outcome.Warnings.Add(skipped > 0
                ? "没找到可导入的 dll 或 zip：只认这两种，7z/rar 请先解压；已经在 Mods 里或游戏/加载器目录里的文件会跳过。"
                : "这个文件夹里没有 dll 或 zip。");

        return outcome;
    }

    private static bool IsInstallable(string file) =>
        file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
        file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string file, string dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return false;
        var root = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(file).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    public static InstallOutcome Install(IEnumerable<string> files, string modsDir)
    {
        var outcome = new InstallOutcome();
        Directory.CreateDirectory(modsDir);

        foreach (var file in files)
        {
            try
            {
                if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    var dest = Path.Combine(modsDir, Path.GetFileName(file));
                    CopyWithBackup(file, dest);
                    outcome.Installed.Add(Path.GetFileName(dest));
                }
                else if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    InstallZip(file, modsDir, outcome);
                }
                else
                {
                    outcome.Skipped.Add(Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                outcome.Warnings.Add($"{Path.GetFileName(file)}：{ex.Message}");
            }
        }

        foreach (var dll in outcome.Installed.Where(x => x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            var installed = Path.Combine(modsDir, dll);
            if (!File.Exists(installed)) continue;

            var info = AssemblyMetadataReader.Read(installed);
            if (info == null || !info.HasMelonInfo)
                outcome.Warnings.Add($"{dll} 没有 MelonInfo，装进去也不会被加载。");
        }

        return outcome;
    }

    private static void InstallZip(string zipPath, string modsDir, InstallOutcome outcome)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "dangpu-install-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, tmp, true);
            var manifest = Directory.GetFiles(tmp, "manifest.json", SearchOption.AllDirectories).FirstOrDefault();
            if (manifest != null)
            {
                var root = Path.GetDirectoryName(manifest)!;
                var dest = Path.Combine(modsDir, Path.GetFileNameWithoutExtension(zipPath));
                CopyTree(root, dest);
                outcome.Installed.Add(Path.GetFileName(dest) + "（Melon 子文件夹）");
                return;
            }

            var dlls = Directory.GetFiles(tmp, "*.dll", SearchOption.AllDirectories);
            if (dlls.Length == 0)
            {
                outcome.Warnings.Add($"{Path.GetFileName(zipPath)} 里没有 dll，已跳过。");
                return;
            }

            foreach (var dll in dlls)
            {
                var dest = Path.Combine(modsDir, Path.GetFileName(dll));
                CopyWithBackup(dll, dest);
                outcome.Installed.Add(Path.GetFileName(dest));
            }

            var others = Directory.GetFiles(tmp, "*", SearchOption.AllDirectories).Length - dlls.Length;
            if (others > 0)
                outcome.Warnings.Add($"{Path.GetFileName(zipPath)} 里还有 {others} 个非 dll 文件没有展开（本作 mod 基本都是单 dll，可忽略）。");
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    private static void CopyTree(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(dest, Path.GetRelativePath(src, dir)));
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(dest, Path.GetRelativePath(src, file)), true);
    }

    private static void CopyWithBackup(string src, string dest)
    {
        if (File.Exists(dest))
        {
            var backup = dest + ".old-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            File.Move(dest, backup);
        }
        File.Copy(src, dest, true);
    }

    // ---------------- 删除到回收站 ----------------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    public static bool Recycle(string path, out string error)
    {
        error = "";
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            error = "路径不存在";
            return false;
        }

        var op = new SHFILEOPSTRUCT
        {
            wFunc = 3, // FO_DELETE
            pFrom = path + "\0",
            fFlags = 0x40 | 0x04 | 0x10, // 允许撤销 + 静默 + 不确认
            hwnd = IntPtr.Zero
        };

        var code = SHFileOperation(ref op);
        if (code != 0)
        {
            error = "移入回收站失败（错误码 " + code + "）";
            return false;
        }
        return true;
    }
}
