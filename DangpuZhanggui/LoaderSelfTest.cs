using System.IO;
using Dangpu.Services;

namespace Dangpu;

/// <summary>--loadertest：导出当前加载器，再装进一个临时游戏目录，验证安装/版本识别/启停。</summary>
public static class LoaderSelfTest
{
    public static string Run(GameInfo realGame)
    {
        var log = new List<string>();
        void Check(string name, bool ok, string detail = "")
            => log.Add($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  — " + detail : "")}");

        var temp = Path.Combine(Path.GetTempPath(), "dangpu-loadertest-" + Guid.NewGuid().ToString("N")[..6]);
        var fake = Path.Combine(temp, "game");
        try
        {
            Directory.CreateDirectory(temp);
            var real = LoaderService.GetStatus(realGame);
            Check("识别当前加载器", real.Installed, $"{real.Version} / {(real.Disabled ? "已停用" : "已启用")}");

            if (!real.Installed) return string.Join(Environment.NewLine, log);

            var zip = Path.Combine(temp, "loader.zip");
            LoaderService.ExportLoader(realGame, zip);
            Check("导出加载器", File.Exists(zip), $"{new FileInfo(zip).Length / 1024.0 / 1024.0:0.0} MB");

            Directory.CreateDirectory(fake);
            File.WriteAllBytes(Path.Combine(fake, "Probably Stolen.exe"), new byte[] { 0x4D, 0x5A });
            Directory.CreateDirectory(Path.Combine(fake, "UserData"));
            if (File.Exists(realGame.LoaderCfgPath))
                File.Copy(realGame.LoaderCfgPath, Path.Combine(fake, "UserData", "Loader.cfg"), true);

            var fakeGame = new GameInfo
            {
                GameDir = fake,
                ExePath = Path.Combine(fake, "Probably Stolen.exe"),
                GameType = "Il2cpp",
                GameArch = "x64"
            };

            Check("空目录默认为未安装", !LoaderService.GetStatus(fakeGame).Installed);

            LoaderService.InstallFromZip(zip, fakeGame, real.Version);
            var after = LoaderService.GetStatus(fakeGame);
            Check("从 zip 安装", after.Installed, after.Summary);
            Check("版本识别一致", after.Version == real.Version, $"{after.Version} vs {real.Version}");

            if (File.Exists(fakeGame.LoaderCfgPath))
            {
                var (ok1, msg1) = LoaderService.SetEnabled(fakeGame, false);
                Check("停用加载器", ok1 && LoaderService.GetStatus(fakeGame).Disabled, msg1);
                var (ok2, msg2) = LoaderService.SetEnabled(fakeGame, true);
                Check("重新启用加载器", ok2 && !LoaderService.GetStatus(fakeGame).Disabled, msg2);
            }
            else
            {
                Check("Loader.cfg 启停", false, "真实环境里没有 Loader.cfg，跳过");
            }
        }
        catch (Exception ex)
        {
            Check("加载器自检", false, ex.Message);
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }

        return string.Join(Environment.NewLine, log);
    }
}
