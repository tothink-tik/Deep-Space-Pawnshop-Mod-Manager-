using System.IO;
using System.IO.Compression;
using System.Text;
using Dangpu.Models;
using Dangpu.Services;

namespace Dangpu;

/// <summary>
/// --selftest：自己搭一个临时游戏目录，在沙盒里跑启用/停用、安装、备份、配置保存。
/// 绝不碰真实的 Mods 和配置。
/// </summary>
public static class SelfTest
{
    public static string Run(GameInfo realGame)
    {
        var log = new List<string>();
        void Check(string name, bool ok, string detail = "")
            => log.Add($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  — " + detail : "")}");

        var temp = Path.Combine(Path.GetTempPath(), "dangpu-selftest-" + Guid.NewGuid().ToString("N")[..6]);
        BackupItem? backup = null;

        try
        {
            var gameDir = Path.Combine(temp, "game");
            var modsDir = Path.Combine(gameDir, "Mods");
            var userData = Path.Combine(gameDir, "UserData");
            var folderMod = Path.Combine(modsDir, "FolderMod");
            Directory.CreateDirectory(folderMod);
            Directory.CreateDirectory(userData);

            var exe = Path.Combine(gameDir, "Probably Stolen.exe");
            File.WriteAllBytes(exe, new byte[] { 0x4D, 0x5A });

            var sample = Directory.Exists(realGame.ModsDir)
                ? Directory.GetFiles(realGame.ModsDir, "*.dll", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => new FileInfo(f).Length)
                    .FirstOrDefault()
                : null;

            if (sample == null)
            {
                Check("准备样本", false, "真实 Mods 里没有可复制的 dll");
                return string.Join(Environment.NewLine, log);
            }

            File.Copy(sample, Path.Combine(modsDir, "Sample.dll"));
            File.Copy(sample, Path.Combine(folderMod, "Inner.dll"));
            File.WriteAllText(Path.Combine(folderMod, "manifest.json"), "{\"name\":\"FolderMod\",\"version\":\"1.0.0\"}");

            if (File.Exists(realGame.ConfigPath))
                File.Copy(realGame.ConfigPath, Path.Combine(userData, "MelonPreferences.cfg"));
            if (File.Exists(realGame.LoaderCfgPath))
                File.Copy(realGame.LoaderCfgPath, Path.Combine(userData, "Loader.cfg"));

            var game = new GameInfo
            {
                GameDir = gameDir,
                ExePath = exe,
                Version = "0.46D",
                GameType = "Il2cpp",
                GameArch = "x64"
            };

            var mods = ModScanner.Scan(modsDir);
            Check("扫描到模组", mods.Count == 2, string.Join("、", mods.Select(m => m.FileName)));

            var single = mods.FirstOrDefault(m => !m.IsFolder && m.Enabled);
            if (single != null)
            {
                var (ok1, msg1) = ModScanner.SetEnabled(single, false);
                var afterDisable = ModScanner.Scan(modsDir).FirstOrDefault(m => m.Key == single.Key);
                Check("停用单个模组", ok1 && afterDisable is { Enabled: false }, msg1);

                var (ok2, msg2) = afterDisable != null
                    ? ModScanner.SetEnabled(afterDisable, true)
                    : (false, "找不到停用后的条目");
                var afterEnable = ModScanner.Scan(modsDir).FirstOrDefault(m => m.Key == single.Key);
                Check("重新启用单个模组", ok2 && afterEnable is { Enabled: true }, msg2);
            }

            var folder = mods.FirstOrDefault(m => m.IsFolder && m.HasManifest);
            if (folder != null)
            {
                var (ok, msg) = ModScanner.SetEnabled(folder, false);
                var re = ModScanner.Scan(modsDir).FirstOrDefault(m => m.Key == folder.Key);
                Check("停用子文件夹模组", ok && re is { Enabled: false }, msg);
                ModScanner.SetEnabled(re ?? folder, true);
            }

            // 安装 zip
            var zip = Path.Combine(temp, "pack.zip");
            using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
                z.CreateEntryFromFile(sample, "Zipped.dll");

            var outcome = ModScanner.Install(new[] { zip }, modsDir);
            Check("安装 zip", outcome.Installed.Count == 1 && File.Exists(Path.Combine(modsDir, "Zipped.dll")),
                string.Join("、", outcome.Installed));

            // 备份 / 还原
            try
            {
                backup = BackupService.Create(game, "自检");
                var list = BackupService.List();
                Check("创建备份", list.Any(b => b.FullPath == backup.FullPath), backup.Name);
                BackupService.Restore(backup, game);
                Check("还原备份", true, "没有抛异常");
            }
            catch (Exception ex)
            {
                Check("备份/还原", false, ex.Message);
            }

            // 配置保存
            try
            {
                var doc = ConfigDocument.Load(game.ConfigPath);
                var entry = doc.AllEntries.FirstOrDefault(e => e.Kind == ConfigKind.Int);
                if (entry == null)
                {
                    Check("保存配置", true, "沙盒里没有配置项，跳过");
                }
                else
                {
                    var before = entry.Value;
                    entry.Value = (int.Parse(before) + 1).ToString();
                    var (ok, msg) = doc.Save();
                    var reloaded = ConfigDocument.Load(game.ConfigPath).AllEntries
                        .FirstOrDefault(e => e.LineIndex == entry.LineIndex && e.Key == entry.Key);
                    Check("保存配置", ok && reloaded?.Value == entry.Value,
                        $"{entry.Key}: {before} → {reloaded?.Value}；{msg}");

                    var restore = ConfigDocument.Load(game.ConfigPath);
                    var back = restore.AllEntries.FirstOrDefault(x => x.Key == entry.Key && x.LineIndex == entry.LineIndex);
                    if (back != null)
                    {
                        back.Value = before;
                        restore.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                Check("保存配置", false, ex.Message);
            }

            // Nexus 链接解析与简介清洗（不联网）
            try
            {
                var parsed = NexusService.ParseLink("https://www.nexusmods.com/probablystolen/mods/66");
                Check("解析 N 网链接", parsed is { game: "probablystolen", id: 66 }, parsed?.ToString() ?? "null");

                var plain = NexusService.ToPlainText("[b]Hello[/b]<br>world[img]x[/img]<p>next</p>");
                Check("简介去标签",
                    plain.Contains("Hello") && plain.Contains("world") && plain.Contains("next") &&
                    !plain.Contains("[") && !plain.Contains("<"),
                    plain.Replace("\n", " / "));
            }
            catch (Exception ex)
            {
                Check("Nexus 工具函数", false, ex.Message);
            }

            // 诊断规则（合成日志，不联网）
            try
            {
                var snap = new LogSnapshot { Exists = true };
                snap.LoadedDlls.Add("Sample.dll");
                snap.FailedDlls.Add("BrokenMod.dll");
                snap.Lines.Add(new LogLine
                {
                    Time = "00:00:01",
                    Level = LogLevel.Error,
                    Text = "System.TypeLoadException: Could not load type 'Foo' from assembly 'Sample, Version=1.0.0.0'"
                });
                snap.Lines.Add(new LogLine
                {
                    Time = "00:00:02",
                    Level = LogLevel.Error,
                    Text = "[ERROR] Failed to load Melon Assembly from '.\\Mods\\BrokenMod.dll'"
                });

                var loaderStatus = new LoaderStatus
                {
                    GameFound = true,
                    BootstrapExists = true,
                    FolderExists = true,
                    Version = "v0.7.3"
                };

                var findings = DiagnosticService.Analyze(game, mods, snap, loaderStatus);
                Check("诊断识别类型异常", findings.Any(f => f.Title.Contains("类型加载")),
                    string.Join(" / ", findings.Select(f => f.Title)));
                Check("诊断识别加载失败", findings.Any(f => f.Title.Contains("BrokenMod.dll")));
                Check("诊断能指到可疑模组", findings.Any(f => f.Suspects.Count > 0),
                    string.Join(" / ", findings.Where(f => f.Suspects.Count > 0).SelectMany(f => f.Suspects)));
            }
            catch (Exception ex)
            {
                Check("诊断规则", false, ex.Message);
            }

            // 存档快照（在临时目录里跑，不碰真实存档）
            try
            {
                var saveRoot = Path.Combine(temp, "saves");
                Directory.CreateDirectory(saveRoot);
                File.WriteAllText(Path.Combine(saveRoot, "SaveFile.es3"), "pointer");
                File.WriteAllText(Path.Combine(saveRoot, "saves_index.es3"), "index");
                File.WriteAllText(Path.Combine(saveRoot, "save_1.es3"), new string('x', 4096));

                SaveService.RootOverride = saveRoot;
                var snapshot = SaveService.Create("自检");
                var list = SaveService.List();
                Check("创建存档快照", list.Any(x => x.FullPath == snapshot.FullPath), snapshot.FileListText);

                File.WriteAllText(Path.Combine(saveRoot, "save_1.es3"), "changed");
                SaveService.Restore(snapshot);
                Check("还原存档快照", File.ReadAllText(Path.Combine(saveRoot, "save_1.es3")).Length == 4096);

                SaveService.Delete(snapshot);
                Check("删除存档快照", !File.Exists(snapshot.FullPath));
            }
            catch (Exception ex)
            {
                Check("存档快照", false, ex.Message);
            }
            finally
            {
                SaveService.RootOverride = null;
            }

            // 大日志也要能读到版本头（以前只读最后 500 KB，会丢）
            try
            {
                var bigLog = Path.Combine(temp, "big.log");
                var sb = new StringBuilder();
                sb.AppendLine("[00:00:00.001] MelonLoader v0.7.3 Open-Beta");
                sb.AppendLine("[00:00:00.002] Game Version: 0.46D");
                sb.AppendLine("[00:00:00.003] Game Type: Il2cpp");
                while (sb.Length < 700_000) sb.AppendLine("[00:00:01.000] filler line for tail truncation test");
                File.WriteAllText(bigLog, sb.ToString());

                var snap = LogService.Read(bigLog);
                Check("大日志仍能读到版本头",
                    snap.Header.GetValueOrDefault("Game Version") == "0.46D" &&
                    snap.Header.GetValueOrDefault("Game Type") == "Il2cpp",
                    $"{new FileInfo(bigLog).Length / 1024} KB");
            }
            catch (Exception ex)
            {
                Check("大日志版本头", false, ex.Message);
            }
        }
        catch (Exception ex)
        {
            Check("自检", false, ex.Message);
        }
        finally
        {
            if (backup != null) BackupService.Delete(backup);
            try { Directory.Delete(temp, true); } catch { }
        }

        return string.Join(Environment.NewLine, log);
    }
}
