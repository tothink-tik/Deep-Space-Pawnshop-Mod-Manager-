using System.IO;
using System.IO.Compression;
using Dangpu.Services;

namespace Dangpu;

/// <summary>--importtest：验证拖文件夹/批量导入，以及不会把 Mods 里的文件重复导入。</summary>
public static class ImportSelfTest
{
    public static string Run(GameInfo realGame)
    {
        var log = new List<string>();
        void Check(string name, bool ok, string detail = "")
            => log.Add($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  — " + detail : "")}");

        var temp = Path.Combine(Path.GetTempPath(), "dangpu-importtest-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var gameDir = Path.Combine(temp, "game");
            var modsDir = Path.Combine(gameDir, "Mods");
            var staging = Path.Combine(temp, "staging");
            Directory.CreateDirectory(modsDir);
            Directory.CreateDirectory(Path.Combine(staging, "nested"));
            File.WriteAllBytes(Path.Combine(gameDir, "Probably Stolen.exe"), new byte[] { 0x4D, 0x5A });

            var source = Path.Combine(realGame.ModsDir, "RealAILearningModule.dll");
            if (!File.Exists(source))
            {
                Check("准备样本", false, "真实 Mods 里没有可复制的 dll");
                return string.Join(Environment.NewLine, log);
            }

            File.Copy(source, Path.Combine(staging, "A.dll"));
            var zipPath = Path.Combine(staging, "nested", "pack.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                zip.CreateEntryFromFile(source, "C.dll");
            File.WriteAllText(Path.Combine(staging, "readme.txt"), "not a mod");

            var outcome = ModScanner.InstallFromPaths(new[] { staging }, modsDir);
            Check("从文件夹导入", outcome.Installed.Count == 2,
                string.Join("、", outcome.Installed));
            Check("dll 直接导入", File.Exists(Path.Combine(modsDir, "A.dll")));
            Check("嵌套 zip 也展开", File.Exists(Path.Combine(modsDir, "C.dll")));
            Check("非模组文件被忽略", !File.Exists(Path.Combine(modsDir, "readme.txt")));

            var again = ModScanner.InstallFromPaths(new[] { modsDir }, modsDir);
            Check("不会把 Mods 自己再导入一遍", again.Installed.Count == 0,
                again.Warnings.FirstOrDefault() ?? "");

            var gameRoot = ModScanner.InstallFromPaths(new[] { gameDir }, modsDir);
            Check("拖整个游戏目录也不乱装", gameRoot.Installed.Count == 0,
                gameRoot.Warnings.FirstOrDefault() ?? "");
        }
        catch (Exception ex)
        {
            Check("导入自检", false, ex.Message);
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }

        return string.Join(Environment.NewLine, log);
    }
}
