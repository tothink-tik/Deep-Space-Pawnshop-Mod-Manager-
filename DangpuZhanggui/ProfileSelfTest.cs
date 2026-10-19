using System.IO;
using Dangpu.Services;

namespace Dangpu;

/// <summary>--profiletest：在临时目录里验证方案保存/应用/严格应用和列表导出。</summary>
public static class ProfileSelfTest
{
    public static string Run(GameInfo realGame)
    {
        var log = new List<string>();
        void Check(string name, bool ok, string detail = "")
            => log.Add($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  — " + detail : "")}");

        var temp = Path.Combine(Path.GetTempPath(), "dangpu-profiletest-" + Guid.NewGuid().ToString("N")[..6]);
        var gameDir = Path.Combine(temp, "game");
        var modsDir = Path.Combine(gameDir, "Mods");
        ModProfile? saved = null;

        try
        {
            Directory.CreateDirectory(modsDir);
            File.WriteAllBytes(Path.Combine(gameDir, "Probably Stolen.exe"), new byte[] { 0x4D, 0x5A });

            var source = Path.Combine(realGame.ModsDir, "RealAILearningModule.dll");
            if (!File.Exists(source))
            {
                Check("准备样本", false, "真实 Mods 里没有可复制的 dll");
                return string.Join(Environment.NewLine, log);
            }

            foreach (var name in new[] { "A.dll", "B.dll", "C.dll" })
                File.Copy(source, Path.Combine(modsDir, name));

            var game = new GameInfo
            {
                GameDir = gameDir,
                ExePath = Path.Combine(gameDir, "Probably Stolen.exe"),
                Version = "0.46D",
                GameType = "Il2cpp",
                GameArch = "x64"
            };

            var initial = ModScanner.Scan(modsDir);
            Check("准备三个模组", initial.Count == 3, string.Join("、", initial.Select(m => m.FileName)));

            var b = initial.First(m => m.FileBaseName == "B");
            ModScanner.SetEnabled(b, false);
            var snapshotSource = ModScanner.Scan(modsDir);

            saved = ProfileService.CreateSnapshot(snapshotSource, "自检方案", "0.46D");
            ProfileService.Save(saved);
            Check("保存方案", File.Exists(saved.FilePath), $"{saved.Name} · {saved.SummaryText}");

            // 改乱状态：A 关掉，B 打开
            var now = ModScanner.Scan(modsDir);
            ModScanner.SetEnabled(now.First(m => m.FileBaseName == "A"), false);
            ModScanner.SetEnabled(now.First(m => m.FileBaseName == "B"), true);

            var current = ModScanner.Scan(modsDir);
            var (diff, missing) = ProfileService.Compare(saved, current);
            Check("对比差异", diff == 2 && missing == 0, $"{diff} 处不同，{missing} 个缺失");

            var result = ProfileService.Apply(saved, current, false);
            var after = ModScanner.Scan(modsDir);
            var okStates = after.First(m => m.FileBaseName == "A").Enabled
                           && !after.First(m => m.FileBaseName == "B").Enabled
                           && after.First(m => m.FileBaseName == "C").Enabled;
            Check("应用方案", okStates && result.Changed == 2, $"改了 {result.Changed} 个");

            // 新装一个 D，严格应用时应当被停用
            File.Copy(source, Path.Combine(modsDir, "D.dll"));
            var withD = ModScanner.Scan(modsDir);
            var strict = ProfileService.Apply(saved, withD, true);
            var dState = ModScanner.Scan(modsDir).First(m => m.FileBaseName == "D");
            Check("严格应用停用未记录模组", !dState.Enabled && strict.StrictDisabled == 1, $"额外停用 {strict.StrictDisabled} 个");

            // 导出
            var csv = Path.Combine(temp, "list.csv");
            ModListExporter.Export(ModScanner.Scan(modsDir), csv, game);
            var csvText = File.ReadAllText(csv);
            Check("导出 CSV", csvText.Contains("中文名") && csvText.Contains("A.dll"), $"{new FileInfo(csv).Length} 字节");

            var md = Path.Combine(temp, "list.md");
            ModListExporter.Export(ModScanner.Scan(modsDir), md, game);
            var mdText = File.ReadAllText(md);
            Check("导出 Markdown", mdText.Contains("深空当铺 模组列表") && mdText.Contains("| 模组 |"),
                $"{new FileInfo(md).Length} 字节");
        }
        catch (Exception ex)
        {
            Check("方案自检", false, ex.Message);
        }
        finally
        {
            if (saved != null) ProfileService.Delete(saved);
            try { Directory.Delete(temp, true); } catch { }
        }

        return string.Join(Environment.NewLine, log);
    }
}
