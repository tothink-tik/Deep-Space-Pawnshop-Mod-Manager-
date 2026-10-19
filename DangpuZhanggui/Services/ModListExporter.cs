using System.IO;
using System.Text;
using Dangpu.Models;

namespace Dangpu.Services;

public static class ModListExporter
{
    private static readonly string[] Header =
    {
        "中文名", "英文名", "文件名", "程序集", "版本", "作者", "状态", "日志", "大小",
        "修改时间", "依赖", "体检问题", "来源", "路径"
    };

    public static void Export(IReadOnlyList<ModItem> mods, string path, GameInfo game)
    {
        var markdown = path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                       || path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);

        if (markdown) ExportMarkdown(mods, path, game);
        else ExportCsv(mods, path);
    }

    private static void ExportCsv(IReadOnlyList<ModItem> mods, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Header.Select(Escape)));

        foreach (var m in mods)
        {
            var cells = new[]
            {
                m.DisplayName, m.EnglishName, m.FileName, m.AssemblyName, m.VersionText, m.Author ?? "",
                m.Enabled ? "已启用" : "已停用", LogText(m), m.SizeText, m.ModifiedText,
                string.Join(" / ", m.Dependencies), string.Join(" / ", m.Issues.Select(i => i.Text)),
                m.CatalogLink ?? "", m.Path
            };
            sb.AppendLine(string.Join(",", cells.Select(Escape)));
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static void ExportMarkdown(IReadOnlyList<ModItem> mods, string path, GameInfo game)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 深空当铺 模组列表");
        sb.AppendLine();
        sb.AppendLine($"- 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- 游戏版本：{(string.IsNullOrWhiteSpace(game.Version) ? "未知" : game.Version)}" +
                      $" · {(string.IsNullOrWhiteSpace(game.Loader) ? "未检测到加载器" : game.Loader)}");
        sb.AppendLine($"- 共 {mods.Count} 个，启用 {mods.Count(m => m.Enabled)} 个，需要留意 {mods.Count(m => m.HasError || m.HasWarning)} 个");
        sb.AppendLine();
        sb.AppendLine("| 模组 | 版本 | 作者 | 状态 | 日志 | 大小 |");
        sb.AppendLine("|---|---|---|---|---|---|");

        foreach (var m in mods.OrderBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            sb.AppendLine($"| {Cell(m.DisplayName)} | {Cell(m.VersionText)} | {Cell(m.Author ?? "")} | " +
                          $"{(m.Enabled ? "已启用" : "已停用")} | {Cell(LogText(m))} | {m.SizeText} |");
        }

        var withIssues = mods.Where(m => m.Issues.Count > 0).ToList();
        if (withIssues.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## 体检");
            foreach (var m in withIssues)
            {
                sb.AppendLine($"- **{m.DisplayName}**");
                foreach (var issue in m.Issues)
                    sb.AppendLine($"  - {issue.Level}：{issue.Text}");
            }
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string LogText(ModItem m) => m.LogState switch
    {
        "loaded" => "日志已加载",
        "failed" => "加载失败",
        "absent" => "日志未见",
        _ => ""
    };

    private static string Escape(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

    private static string Cell(string value) => (value ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
