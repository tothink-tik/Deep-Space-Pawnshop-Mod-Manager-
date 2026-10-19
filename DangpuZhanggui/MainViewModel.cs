using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Text;
using System.Windows.Media;
using Dangpu.Models;
using Dangpu.Services;
using Dangpu.Views;

namespace Dangpu;

public enum ModFilter { All, Enabled, Disabled, Problem }

public enum StatusKind { Info, Good, Warn, Error }

public enum ModSortMode { Name, Modified, Enabled, Status }

public sealed class IssueRow
{
    public string ModName { get; init; } = "";
    public string Text { get; init; } = "";
    public Brush Dot { get; init; } = Brushes.Gray;
    public Brush Fg { get; init; } = Brushes.DimGray;
    public ModItem? Mod { get; init; }
}

public sealed class NavItem
{
    public string Title { get; init; } = "";
    public Geometry Icon { get; init; } = Geometry.Empty;
}

public sealed class MainViewModel : ObservableObject
{
    public AppSettings Settings { get; }
    public GameInfo Game { get; private set; }
    public Catalog Catalog { get; private set; }

    public ObservableCollection<ModItem> Mods { get; } = new();
    public ICollectionView ModsView { get; }
    public ICollectionView LogView { get; }
    public ObservableCollection<IssueRow> OverviewIssues { get; } = new();
    public ObservableCollection<LogLine> LogLines { get; } = new();
    public ObservableCollection<LogLine> RecentLogLines { get; } = new();
    public ObservableCollection<BackupItem> Backups { get; } = new();
    public ObservableCollection<ProfileItem> Profiles { get; } = new();
    public ObservableCollection<DiagnosticFinding> Findings { get; } = new();
    public ObservableCollection<SaveSnapshot> SaveSnapshots { get; } = new();
    public ObservableCollection<NexusSearchResult> SearchResults { get; } = new();
    public ObservableCollection<NavItem> NavItems { get; } = new();
    public ConfigDocument Config { get; private set; }

    public LogSnapshot Log { get; private set; } = new();

    public MainViewModel()
    {
        Settings = AppSettings.Load();
        Game = GameLocator.Load(Settings.GamePath);
        Catalog = Catalog.Load(Game);
        Config = ConfigDocument.Load(Game.ConfigPath);

        NavItems.Add(new NavItem { Title = "概览", Icon = Geometry.Parse("M3 10.6 L12 3.2 L21 10.6 M5.4 9.4 V20.4 H18.6 V9.4") });
        NavItems.Add(new NavItem { Title = "模组", Icon = Geometry.Parse("M4 4 H10.4 V10.4 H4 Z M13.6 4 H20 V10.4 H13.6 Z M4 13.6 H10.4 V20 H4 Z M13.6 13.6 H20 V20 H13.6 Z") });
        NavItems.Add(new NavItem { Title = "日志", Icon = Geometry.Parse("M6 3.4 H14.4 L18.4 7.4 V20.6 H6 Z M14.2 3.6 V7.6 H18.2 M8.8 11.4 H15.6 M8.8 14.6 H15.6 M8.8 17.8 H13.2") });
        NavItems.Add(new NavItem { Title = "配置", Icon = Geometry.Parse("M4 7 H20 M4 12 H20 M4 17 H20 M9 4.8 V9.2 M15 9.8 V14.2 M7 14.8 V19.2") });
        NavItems.Add(new NavItem { Title = "方案", Icon = Geometry.Parse("M6 3.4 H18 V21 L12 16.6 L6 21 Z") });
        NavItems.Add(new NavItem { Title = "备份", Icon = Geometry.Parse("M3.4 7.6 L12 3.4 L20.6 7.6 L12 11.8 Z M3.4 7.6 V16.4 L12 20.6 V11.8 M20.6 7.6 V16.4 L12 20.6") });
        NavItems.Add(new NavItem { Title = "存档", Icon = Geometry.Parse("M4 4 H17 L20 7 V20 H4 Z M8 4 V9 H16 V4 M8 14 H16 V20 H8 Z") });
        NavItems.Add(new NavItem { Title = "诊断", Icon = Geometry.Parse("M3 12 H7 L9 6 L13 18 L15 12 H21") });
        NavItems.Add(new NavItem { Title = "发现", Icon = Geometry.Parse("M10.5 4 A6.5 6.5 0 1 1 10.45 4 M15.2 15.2 L20.5 20.5") });
        NavItems.Add(new NavItem { Title = "设置", Icon = Geometry.Parse("M12 8.6 A3.4 3.4 0 1 1 11.95 8.6 M12 2.8 V5.2 M12 18.8 V21.2 M2.8 12 H5.2 M18.8 12 H21.2 M5.5 5.5 L7.2 7.2 M16.8 16.8 L18.5 18.5 M18.5 5.5 L16.8 7.2 M7.2 16.8 L5.5 18.5") });

        ModsView = CollectionViewSource.GetDefaultView(Mods);
        ModsView.Filter = FilterMod;

        LogView = CollectionViewSource.GetDefaultView(LogLines);
        LogView.Filter = o =>
        {
            if (o is not LogLine line) return false;
            if (LogOnlyProblems && line.Level is not (LogLevel.Error or LogLevel.Warn)) return false;
            if (!string.IsNullOrWhiteSpace(LogSearch) &&
                !line.Text.Contains(LogSearch.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        };

        ApplySort();

        Rescan(silent: true);
        RefreshLog();
        RefreshLoader();
        RefreshSaves();
        LoadBackground();
        LoadMusic();
    }

    // ---------------- 页面 ----------------

    private int _pageIndex;
    public int PageIndex
    {
        get => _pageIndex;
        set
        {
            if (!Set(ref _pageIndex, value)) return;
            Settings.LastPage = value;
            Settings.Save();
            OnPropertyChanged(nameof(PageTitle));
            if (value == 2) RefreshLog();
            if (value == 4) RefreshProfiles();
            if (value == 5) RefreshBackups();
            if (value == 6) RefreshSaves();
            if (value == 7) RefreshDiagnostics();
        }
    }

    public string PageTitle => _pageIndex switch
    {
        1 => "模组管理",
        2 => "运行日志",
        3 => "模组配置",
        4 => "方案",
        5 => "备份",
        6 => "存档",
        7 => "诊断",
        8 => "发现",
        9 => "设置",
        _ => "概览"
    };

    // ---------------- 游戏 ----------------

    public bool GameFound => Game.Found;
    public bool GameMissing => !Game.Found;
    public string GamePathText => Game.GameDir ?? "未找到游戏目录";
    public string GameVersionText => string.IsNullOrWhiteSpace(Game.Version) ? "版本未知" : Game.Version;
    public string LoaderText => string.IsNullOrWhiteSpace(Game.Loader) ? "未检测到 MelonLoader" : Game.Loader;
    public string GameTypeText => Game.GameType.Equals("Il2cpp", StringComparison.OrdinalIgnoreCase) ? "IL2CPP" : Game.GameType;

    private bool _gameRunning;
    public bool GameRunning
    {
        get => _gameRunning;
        set
        {
            if (!Set(ref _gameRunning, value)) return;
            OnPropertyChanged(nameof(GameStateText));
            OnPropertyChanged(nameof(GameStateBrush));
        }
    }

    public string GameStateText => GameRunning ? "游戏正在运行" : "游戏未运行";
    public Brush GameStateBrush => GameRunning
        ? new SolidColorBrush(Color.FromRgb(0x2E, 0x9E, 0x6B))
        : new SolidColorBrush(Color.FromRgb(0xAE, 0xB6, 0xC4));

    public void SetGamePath(string? path)
    {
        Settings.GamePath = path;
        Settings.Save();
        Game = GameLocator.Load(path);
        Catalog = Catalog.Load(Game);
        Config = ConfigDocument.Load(Game.ConfigPath);
        OnPropertyChanged(nameof(Game));
        OnPropertyChanged(nameof(GameFound));
        OnPropertyChanged(nameof(GameMissing));
        OnPropertyChanged(nameof(GamePathText));
        OnPropertyChanged(nameof(GameVersionText));
        OnPropertyChanged(nameof(LoaderText));
        OnPropertyChanged(nameof(GameTypeText));
        OnPropertyChanged(nameof(Config));
        Rescan();
        RefreshLog();
        RefreshLoader();
        RefreshBackups();
        SetStatus(Game.Found ? "已定位到游戏目录" : "这个目录里没有 Probably Stolen.exe", Game.Found ? StatusKind.Good : StatusKind.Error);
    }

    public void AutoDetectGame()
    {
        var dir = GameLocator.Find(null);
        if (dir == null)
        {
            SetStatus("没有在 Steam 库里找到游戏，请手动指定目录", StatusKind.Warn);
            return;
        }
        SetGamePath(dir);
    }

    public void LaunchGame()
    {
        if (GameRunner.Launch(Game, Settings, out var error))
        {
            SetStatus("已启动游戏，日志会在几秒后开始写入", StatusKind.Good);
            GameRunning = true;
        }
        else
        {
            SetStatus(error, StatusKind.Error);
        }
    }

    public void OpenModsFolder() => GameRunner.OpenInExplorer(Game.ModsDir);
    public void OpenModFolder(ModItem? mod)
    {
        if (mod == null) return;
        GameRunner.OpenInExplorer(mod.Path, !mod.IsFolder);
    }
    public void OpenGameFolder() => GameRunner.OpenInExplorer(Game.GameDir);

    public void OpenModLink()
    {
        var link = SelectedMod?.NexusLink;
        if (string.IsNullOrWhiteSpace(link)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link) { UseShellExecute = true });
        }
        catch { }
    }
    public void OpenConfigFile() => GameRunner.OpenInExplorer(Game.ConfigPath, true);
    public void OpenLogFolder() => GameRunner.OpenInExplorer(Path.GetDirectoryName(Game.LatestLog));

    // ---------------- 模组 ----------------

    public int ModCount => Mods.Count;
    public int EnabledCount => Mods.Count(m => m.Enabled);
    public int DisabledCount => Mods.Count(m => !m.Enabled);
    public int ProblemCount => Mods.Count(m => m.HasError || m.HasWarning);
    public int LoadedInLogCount => Mods.Count(m => m.LogState == "loaded");

    public string EnabledOfTotalText => $"{EnabledCount} / {ModCount} 已启用";

    public string CountSummary => $"{Mods.Count} 个模组 · {EnabledCount} 启用";

    private ModItem? _selectedMod;
    public ModItem? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (!Set(ref _selectedMod, value)) return;
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedSummary));
            LoadNexusForSelected();
        }
    }

    public bool HasSelection => SelectedMod != null;

    public string SelectedSummary => SelectedMod?.CatalogSummary ?? "";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value)) return;
            ModsView.Refresh();
            NotifyCounts();
        }
    }
    private string _searchText = "";

    public ModFilter Filter
    {
        get => _filter;
        set
        {
            if (!Set(ref _filter, value)) return;
            ModsView.Refresh();
            NotifyCounts();
        }
    }
    private ModFilter _filter;

    private bool FilterMod(object o)
    {
        if (o is not ModItem m) return false;
        if (Filter == ModFilter.Enabled && !m.Enabled) return false;
        if (Filter == ModFilter.Disabled && m.Enabled) return false;
        if (Filter == ModFilter.Problem && !(m.HasError || m.HasWarning)) return false;
        if (!string.IsNullOrWhiteSpace(SearchText) && !m.SearchText.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    public void Rescan(bool silent = false)
    {
        var previousKey = SelectedMod?.Key;

        Mods.Clear();
        var manualLinks = NexusLinkStore.Load();

        if (!string.IsNullOrWhiteSpace(Game.ModsDir) && Directory.Exists(Game.ModsDir))
        {
            foreach (var item in ModScanner.Scan(Game.ModsDir))
            {
                if (manualLinks.TryGetValue(item.Key, out var manualLink))
                    item.ManualLink = manualLink;

                var entry = Catalog.Find(item.MelonName, item.AssemblyName, item.FileBaseName);
                if (entry != null)
                {
                    item.CatalogZh = string.IsNullOrWhiteSpace(entry.NameZh) ? null : entry.NameZh;
                    item.CatalogEn = string.IsNullOrWhiteSpace(entry.NameEn) ? null : entry.NameEn;
                    item.CatalogSummary = string.IsNullOrWhiteSpace(entry.SummaryZh) ? entry.SummaryEn : entry.SummaryZh;
                    item.CatalogLink = string.IsNullOrWhiteSpace(entry.Link) ? null : entry.Link;
                }
                if (item.TileBrush == null || string.IsNullOrWhiteSpace(item.AssemblyName))
                    item.TileBrush = ModItem.TileFor(item.DisplayName);
                Mods.Add(item);
            }
        }

        LogService.ApplyLoadStates(Mods, Log);
        ModsView.Refresh();
        SelectedMod = string.IsNullOrWhiteSpace(previousKey)
            ? null
            : Mods.FirstOrDefault(m => m.Key == previousKey);
        NotifyCounts();
        BuildOverviewIssues();

        if (!silent)
            SetStatus($"已刷新，{Mods.Count} 个模组（启用 {EnabledCount}）", StatusKind.Info);
    }

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(ModCount));
        OnPropertyChanged(nameof(EnabledCount));
        OnPropertyChanged(nameof(DisabledCount));
        OnPropertyChanged(nameof(ProblemCount));
        OnPropertyChanged(nameof(LoadedInLogCount));
        OnPropertyChanged(nameof(EnabledOfTotalText));
        OnPropertyChanged(nameof(CountSummary));
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(ShowNoResults));
        OnPropertyChanged(nameof(ShowDropHint));
    }

    public int FilteredCount => ModsView.Cast<object>().Count();

    public bool ShowNoResults => ModCount > 0 && FilteredCount == 0;

    /// <summary>列表还短的时候，在底部留一块虚线投放区。</summary>
    public bool ShowDropHint => ModCount is > 0 and < 4;

    private void BuildOverviewIssues()
    {
        OverviewIssues.Clear();
        foreach (var mod in Mods)
        {
            foreach (var issue in mod.Issues.Where(i => i.Level != IssueLevel.Info))
            {
                OverviewIssues.Add(new IssueRow
                {
                    ModName = mod.DisplayName,
                    Text = issue.Text,
                    Dot = issue.Dot,
                    Fg = issue.Fg,
                    Mod = mod
                });
            }
        }
        OnPropertyChanged(nameof(HealthText));
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(HealthBg));
        OnPropertyChanged(nameof(HealthFg));
    }

    public string HealthText => OverviewIssues.Count == 0
        ? "没有发现明显问题"
        : $"{OverviewIssues.Count} 处需要留意";

    public bool HasIssues => OverviewIssues.Count > 0;

    public Brush HealthBg => HasIssues ? New(0xFD, 0xF4, 0xE4) : New(0xE9, 0xF7, 0xF0);
    public Brush HealthFg => HasIssues ? New(0x9C, 0x60, 0x11) : New(0x1F, 0x7D, 0x54);

    private static SolidColorBrush New(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public void ToggleMod(ModItem mod, bool enable)
    {
        if (mod.Enabled == enable) return;
        var (ok, message) = ModScanner.SetEnabled(mod, enable);
        if (ok)
        {
            Rescan(silent: true);
            SetStatus(message, StatusKind.Good);
        }
        else
        {
            Rescan(silent: true);
            SetStatus(message, StatusKind.Error);
        }
    }

    public void InstallPaths(IEnumerable<string> paths)
    {
        var list = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (list.Count == 0) return;

        if (!Game.Found)
        {
            SetStatus("先指定游戏目录", StatusKind.Error);
            return;
        }

        InstallOutcome outcome;
        try
        {
            outcome = ModScanner.InstallFromPaths(list, Game.ModsDir);
        }
        catch (Exception ex)
        {
            SetStatus("导入失败：" + ex.Message, StatusKind.Error);
            return;
        }

        Rescan(silent: true);

        if (outcome.Installed.Count > 0)
        {
            var first = outcome.Installed[0];
            const string folderSuffix = "（Melon 子文件夹）";
            if (first.EndsWith(folderSuffix, StringComparison.Ordinal))
                first = first[..^folderSuffix.Length];

            SelectedMod = Mods.FirstOrDefault(m => string.Equals(m.FileName, first, StringComparison.OrdinalIgnoreCase))
                          ?? Mods.FirstOrDefault(m => string.Equals(m.FileBaseName,
                              Path.GetFileNameWithoutExtension(first), StringComparison.OrdinalIgnoreCase));

            PageIndex = 1;

            var msg = $"已安装 {outcome.Installed.Count} 个：" + string.Join("、", outcome.Installed.Take(3));
            if (outcome.Installed.Count > 3) msg += " …";
            SetStatus(msg, StatusKind.Good);
        }
        else
        {
            SetStatus(outcome.Warnings.FirstOrDefault() ?? "没有装进任何东西", StatusKind.Warn);
        }

        if (outcome.Warnings.Count > 0)
            MessageDialog.Info("安装提醒", string.Join(Environment.NewLine, outcome.Warnings), DialogTone.Warning);
    }

    public void DeleteMod(ModItem mod)
    {
        var confirmed = MessageDialog.Confirm("删除模组",
            $"把「{mod.DisplayName}」移入回收站？{Environment.NewLine}{Environment.NewLine}{mod.Path}",
            "移入回收站", danger: true);
        if (!confirmed) return;

        if (ModScanner.Recycle(mod.Path, out var error))
        {
            SelectedMod = null;
            Rescan(silent: true);
            SetStatus($"已把 {mod.DisplayName} 移入回收站", StatusKind.Good);
        }
        else
        {
            SetStatus(error, StatusKind.Error);
        }
    }

    // ---------------- 日志 ----------------

    private string _logStamp = "";

    public void RefreshLog(bool force = false)
    {
        if (string.IsNullOrWhiteSpace(Game.LatestLog)) return;
        try
        {
            var fi = new FileInfo(Game.LatestLog);
            var stamp = fi.Exists ? $"{fi.Length}-{fi.LastWriteTimeUtc.Ticks}" : "none";
            if (!force && stamp == _logStamp) return;
            _logStamp = stamp;
        }
        catch { }

        Log = LogService.Read(Game.LatestLog);

        LogLines.Clear();
        // 倒序显示：最新的在最上面，方便一眼看到这次启动的结果
        for (int i = Log.Lines.Count - 1; i >= 0; i--) LogLines.Add(Log.Lines[i]);

        RecentLogLines.Clear();
        string? lastText = null;
        foreach (var line in Log.Lines.Where(l => l.Level != LogLevel.Muted))
        {
            if (line.Text == lastText) continue;
            lastText = line.Text;
            RecentLogLines.Add(line);
        }
        while (RecentLogLines.Count > 7) RecentLogLines.RemoveAt(0);

        LogService.ApplyLoadStates(Mods, Log);

        OnPropertyChanged(nameof(Log));
        OnPropertyChanged(nameof(LogSummary));
        OnPropertyChanged(nameof(LogHeaderText));
        OnPropertyChanged(nameof(HasLog));
        OnPropertyChanged(nameof(LoadedInLogCount));
    }

    public bool HasLog => Log.Exists;

    private string _logSearch = "";
    public string LogSearch
    {
        get => _logSearch;
        set
        {
            if (!Set(ref _logSearch, value)) return;
            LogView.Refresh();
        }
    }

    private bool _logOnlyProblems;
    public bool LogOnlyProblems
    {
        get => _logOnlyProblems;
        set
        {
            if (!Set(ref _logOnlyProblems, value)) return;
            LogView.Refresh();
        }
    }

    public string LogSummary => !Log.Exists
        ? "还没有日志，启动一次游戏就有了"
        : $"日志 {Log.TotalLineCount} 行 · {Log.ErrorCount} 错误 · {Log.WarnCount} 警告 · 已加载 {Log.LoadedDlls.Count} 个模组";

    public string LogHeaderText => Log.Exists
        ? $"{Game.NameEn} {Log.Header.GetValueOrDefault("Game Version", "")} · {Log.Header.GetValueOrDefault("MelonLoader", "")} · {Log.Header.GetValueOrDefault("Game Type", "")}"
        : "";

    // ---------------- 配置 ----------------

    public void ReloadConfig()
    {
        Config = ConfigDocument.Load(Game.ConfigPath);
        OnPropertyChanged(nameof(Config));
    }

    public void SaveConfig()
    {
        var (ok, message) = Config.Save();
        Config.Notify(nameof(ConfigDocument.IsDirty));
        SetStatus(message, ok ? StatusKind.Good : StatusKind.Error);
    }

    // ---------------- 备份 ----------------

    public void RefreshBackups()
    {
        Backups.Clear();
        foreach (var b in BackupService.List()) Backups.Add(b);
        OnPropertyChanged(nameof(BackupCount));
        OnPropertyChanged(nameof(LatestBackupText));
    }

    public int BackupCount => Backups.Count;

    public string LatestBackupText => Backups.Count == 0 ? "还没有备份" : Backups[0].CreatedText;

    public void CreateBackup(string label = "手动")
    {
        try
        {
            var item = BackupService.Create(Game, label);
            RefreshBackups();
            SetStatus($"备份完成：{item.Name}", StatusKind.Good);
        }
        catch (Exception ex)
        {
            SetStatus("备份失败：" + ex.Message, StatusKind.Error);
        }
    }

    public void RestoreBackup(BackupItem item)
    {
        var confirmed = MessageDialog.Confirm("还原备份",
            $"把 {item.CreatedText} 的备份还原到游戏目录？{Environment.NewLine}{Environment.NewLine}" +
            "同名文件会被覆盖；备份里没有的模组不会被删除。还原前会自动再备份一次当前状态。",
            "还原");
        if (!confirmed) return;

        try
        {
            BackupService.Create(Game, "还原前");
            BackupService.Restore(item, Game);
            Rescan(silent: true);
            ReloadConfig();
            RefreshBackups();
            SetStatus("已还原：" + item.CreatedText, StatusKind.Good);
        }
        catch (Exception ex)
        {
            SetStatus("还原失败：" + ex.Message, StatusKind.Error);
        }
    }

    public void DeleteBackup(BackupItem item)
    {
        var confirmed = MessageDialog.Confirm("删除备份",
            $"删除这个备份？{Environment.NewLine}{item.Name}", "删除", danger: true);
        if (!confirmed) return;
        BackupService.Delete(item);
        RefreshBackups();
        SetStatus("备份已删除", StatusKind.Info);
    }

    // ---------------- 加载器 ----------------

    private LoaderStatus _loader = new();

    public LoaderStatus Loader
    {
        get => _loader;
        private set
        {
            if (!Set(ref _loader, value)) return;
            OnPropertyChanged(nameof(LoaderSummary));
            OnPropertyChanged(nameof(LoaderInstalled));
            OnPropertyChanged(nameof(LoaderEnabled));
            OnPropertyChanged(nameof(LoaderToggleText));
            OnPropertyChanged(nameof(LoaderDotBrush));
            OnPropertyChanged(nameof(LoaderCacheText));
            OnPropertyChanged(nameof(LoaderActionText));
        }
    }

    public string LoaderSummary => Loader.Summary;
    public bool LoaderInstalled => Loader.Installed;
    public bool LoaderEnabled => Loader.Installed && !Loader.Disabled;
    public string LoaderToggleText => Loader.Disabled ? "启用加载器" : "停用加载器";
    public string LoaderActionText => Loader.Installed ? "修复 / 重装" : "安装 MelonLoader";

    public Brush LoaderDotBrush => !Loader.Installed
        ? new SolidColorBrush(Color.FromRgb(0xD9, 0x48, 0x3B))
        : Loader.Disabled
            ? new SolidColorBrush(Color.FromRgb(0xD0, 0x84, 0x20))
            : new SolidColorBrush(Color.FromRgb(0x2E, 0x9E, 0x6B));

    public string LoaderCacheText => Loader.CacheExists
        ? $"本地已缓存安装包（{Loader.CacheSize / 1024.0 / 1024.0:0.0} MB），断网也能装"
        : "从 GitHub 官方 release 下载，约 19 MB。下载一次后会缓存，之后可以离线安装";

    public void RefreshLoader() => Loader = LoaderService.GetStatus(Game);

    private bool _loaderBusy;
    public bool LoaderBusy
    {
        get => _loaderBusy;
        private set => Set(ref _loaderBusy, value);
    }

    private double _loaderProgress;
    public double LoaderProgress
    {
        get => _loaderProgress;
        private set => Set(ref _loaderProgress, value);
    }

    private string _loaderProgressText = "";
    public string LoaderProgressText
    {
        get => _loaderProgressText;
        private set => Set(ref _loaderProgressText, value);
    }

    private CancellationTokenSource? _loaderCts;

    public void CancelLoader() => _loaderCts?.Cancel();

    public async Task InstallLoaderAsync(bool useCache)
    {
        if (LoaderBusy) return;
        if (!Game.Found)
        {
            SetStatus("先指定游戏目录", StatusKind.Error);
            return;
        }

        if (GameRunner.IsRunning())
        {
            SetStatus("游戏正在运行，先关掉再装加载器", StatusKind.Error);
            return;
        }

        _loaderCts = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        LoaderBusy = true;
        LoaderProgress = 0;

        try
        {
            string zip;
            string tag = "";

            if (useCache && LoaderService.ExistingZip is { } cached)
            {
                zip = cached;
                LoaderProgressText = "使用本地缓存的安装包";
            }
            else
            {
                LoaderProgressText = "正在查询 MelonLoader 最新版本…";
                var (url, resolvedTag) = await LoaderService.ResolveLatestAsync(Game.GameArch, _loaderCts.Token);
                tag = resolvedTag;
                LoaderProgressText = $"正在下载 {tag}…";
                zip = await LoaderService.DownloadAsync(url, LoaderService.CachedZipPath,
                    new Progress<LoaderProgress>(p =>
                    {
                        LoaderProgress = p.Percent;
                        LoaderProgressText = p.Text;
                    }), _loaderCts.Token);
            }

            LoaderProgressText = "正在解压到游戏目录…";
            LoaderService.InstallFromZip(zip, Game, tag);
            RefreshLoader();
            SetStatus($"MelonLoader 已就位（{Loader.Version}），启动一次游戏生成 IL2CPP 程序集", StatusKind.Good);
        }
        catch (OperationCanceledException)
        {
            SetStatus("已取消安装", StatusKind.Info);
        }
        catch (Exception ex)
        {
            SetStatus("安装失败：" + ex.Message, StatusKind.Error);
            MessageDialog.Info("安装 MelonLoader",
                "自动安装没成功：" + ex.Message + Environment.NewLine + Environment.NewLine +
                "可以试试这两个办法：" + Environment.NewLine +
                "1. 到 MelonLoader 的 GitHub release 页面手动下载 MelonLoader.x64.zip，再用「从 zip 安装」；" + Environment.NewLine +
                "2. 如果只是想把加载器带到别的机器，用「导出当前加载器」。",
                DialogTone.Warning);
        }
        finally
        {
            LoaderBusy = false;
            LoaderProgressText = "";
            _loaderCts?.Dispose();
            _loaderCts = null;
        }
    }

    public void InstallLoaderFromZip(string zipPath)
    {
        if (GameRunner.IsRunning())
        {
            SetStatus("游戏正在运行，先关掉再装加载器", StatusKind.Error);
            return;
        }

        try
        {
            LoaderService.InstallFromZip(zipPath, Game);
            RefreshLoader();
            SetStatus("已从压缩包安装 MelonLoader", StatusKind.Good);
        }
        catch (Exception ex)
        {
            SetStatus("安装失败：" + ex.Message, StatusKind.Error);
        }
    }

    public void ExportLoader(string dest)
    {
        try
        {
            var path = LoaderService.ExportLoader(Game, dest);
            RefreshLoader();
            SetStatus("已导出加载器：" + Path.GetFileName(path), StatusKind.Good);
        }
        catch (Exception ex)
        {
            SetStatus("导出失败：" + ex.Message, StatusKind.Error);
        }
    }

    public void SetLoaderEnabled(bool enabled)
    {
        var (ok, message) = LoaderService.SetEnabled(Game, enabled);
        RefreshLoader();
        SetStatus(message, ok ? StatusKind.Good : StatusKind.Error);
    }

    public void OpenLoaderDownloadPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://github.com/LavaGang/MelonLoader/releases/latest") { UseShellExecute = true });
        }
        catch { }
    }

    // ---------------- 方案 ----------------

    public int ProfileCount => Profiles.Count;

    public void RefreshProfiles()
    {
        Profiles.Clear();
        foreach (var profile in ProfileService.List())
        {
            var (diff, missing) = ProfileService.Compare(profile, Mods);
            Profiles.Add(new ProfileItem
            {
                Data = profile,
                HasDiff = diff > 0 || missing > 0,
                DiffText = diff == 0 && missing == 0
                    ? "与当前一致"
                    : diff == 0
                        ? $"{missing} 个模组已不在"
                        : missing == 0
                            ? $"{diff} 处不同"
                            : $"{diff} 处不同 · {missing} 个已不在"
            });
        }
        OnPropertyChanged(nameof(ProfileCount));
    }

    public void SaveProfile(string name)
    {
        try
        {
            var profile = ProfileService.CreateSnapshot(Mods, name, Game.Version);
            ProfileService.Save(profile);
            RefreshProfiles();
            SetStatus($"已保存方案「{profile.Name}」，{profile.Total} 个模组，{profile.EnabledCount} 个启用", StatusKind.Good);
        }
        catch (Exception ex)
        {
            SetStatus("保存方案失败：" + ex.Message, StatusKind.Error);
        }
    }

    public void ApplyProfile(ProfileItem item, bool strict)
    {
        if (GameRunner.IsRunning())
        {
            SetStatus("游戏正在运行，先关掉再切方案", StatusKind.Error);
            return;
        }

        try
        {
            if (Settings.AutoSaveBeforeProfile && SaveService.Exists)
            {
                try { SaveService.Create("应用方案前"); } catch { }
            }

            var (changed, missing, strictDisabled) = ProfileService.Apply(item.Data, Mods, strict);
            Rescan(silent: true);
            RefreshProfiles();

            var parts = new List<string> { changed == 0 ? "状态本来就一致" : $"改了 {changed} 个" };
            if (strict) parts.Add($"额外停用 {strictDisabled} 个未记录的");
            if (missing > 0) parts.Add($"{missing} 个模组已不在");

            SetStatus($"已应用「{item.Title}」：" + string.Join("，", parts),
                missing > 0 ? StatusKind.Warn : StatusKind.Good);
        }
        catch (Exception ex)
        {
            SetStatus("应用方案失败：" + ex.Message, StatusKind.Error);
        }
    }

    public void DeleteProfile(ProfileItem item)
    {
        ProfileService.Delete(item.Data);
        RefreshProfiles();
        SetStatus($"已删除方案「{item.Title}」", StatusKind.Info);
    }

    public void ExportModList(string path)
    {
        try
        {
            ModListExporter.Export(Mods, path, Game);
            SetStatus("已导出列表：" + Path.GetFileName(path), StatusKind.Good);
        }
        catch (Exception ex)
        {
            SetStatus("导出失败：" + ex.Message, StatusKind.Error);
        }
    }

    // ---------------- Nexus 信息 ----------------

    private CancellationTokenSource? _nexusCts;

    public void LoadNexusForSelected()
    {
        var mod = SelectedMod;
        if (mod == null) return;
        if (mod.NexusLoaded || mod.NexusState == "loading") return;

        var parsed = NexusService.ParseLink(mod.NexusLink);
        if (parsed == null)
        {
            mod.NexusState = "";
            return;
        }

        if (string.IsNullOrWhiteSpace(Settings.NexusApiKey))
        {
            mod.NexusState = "no-key";
            return;
        }

        _nexusCts?.Cancel();
        _nexusCts = new CancellationTokenSource();
        _ = LoadNexusAsync(mod, parsed.Value.game, parsed.Value.id, _nexusCts.Token);
    }

    private async Task LoadNexusAsync(ModItem mod, string game, int modId, CancellationToken ct)
    {
        mod.NexusState = "loading";
        mod.CoverLoading = true;

        try
        {
            var info = await NexusService.GetModInfoAsync(game, modId, Settings.NexusApiKey, ct);
            if (info == null)
            {
                mod.NexusState = "error";
                return;
            }

            mod.NexusAuthor = info.Author;
            mod.NexusDescription = NexusService.ToPlainText(info.Description);
            mod.NexusVersion = info.Version;
            mod.HasUpdate = NexusService.IsNewer(info.Version, string.IsNullOrWhiteSpace(mod.MelonVersion)
                ? mod.AssemblyVersion
                : mod.MelonVersion);

            var coverPath = await NexusService.GetCoverAsync(info, ct);
            if (coverPath != null) mod.Cover = NexusService.LoadCover(coverPath);

            mod.NexusLoaded = true;
            mod.NexusState = "ready";
        }
        catch (OperationCanceledException)
        {
            if (mod.NexusState == "loading") mod.NexusState = "";
        }
        catch (NexusAuthException)
        {
            mod.NexusState = "bad-key";
        }
        catch (NexusRateLimitException)
        {
            mod.NexusState = "rate";
        }
        catch
        {
            mod.NexusState = "error";
        }
        finally
        {
            mod.CoverLoading = false;
        }
    }

    public void ToggleDescription()
    {
        if (SelectedMod is { } mod) mod.DescriptionExpanded = !mod.DescriptionExpanded;
    }

    public void GoToSettings() => PageIndex = 9;

    public string NexusKeyStateText => string.IsNullOrWhiteSpace(Settings.NexusApiKey) ? "未配置" : "已配置";

    public void SaveNexusKey(string key)
    {
        Settings.NexusApiKey = key.Trim();
        Settings.Save();

        foreach (var mod in Mods)
        {
            if (mod.NexusState is "no-key" or "bad-key")
            {
                mod.NexusState = "";
                mod.NexusLoaded = false;
            }
        }

        LoadNexusForSelected();
        OnPropertyChanged(nameof(NexusKeyStateText));
        OnPropertyChanged(nameof(SearchNeedsKey));

        if (SearchState is "no-key" or "bad-key")
        {
            SearchState = "idle";
            NotifySearchState();
        }
        SetStatus(string.IsNullOrWhiteSpace(Settings.NexusApiKey)
            ? "已清除 Nexus API key"
            : "Nexus API key 已保存", StatusKind.Good);
    }

    /// <summary>--nexustest 用，不打印 key。</summary>
    public async Task<string> NexusSelfTestAsync()
    {
        if (string.IsNullOrWhiteSpace(Settings.NexusApiKey)) return "FAIL 没有配置 API key";

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var info = await NexusService.GetModInfoAsync("probablystolen", 66, Settings.NexusApiKey, cts.Token, force: true);
            if (info == null) return "FAIL 接口没有返回数据";

            var cover = await NexusService.GetCoverAsync(info, cts.Token);
            return $"PASS name={info.Name} | author={info.Author} | version={info.Version} | " +
                   $"summary={info.Summary.Length} | desc={info.Description.Length} | cover={(cover != null)}";
        }
        catch (Exception ex)
        {
            return "FAIL " + ex.GetType().Name + ": " + ex.Message;
        }
    }

    public async Task TestNexusKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(Settings.NexusApiKey))
        {
            SetStatus("还没有填 Nexus API key", StatusKind.Warn);
            return;
        }

        SetStatus("正在测试 Nexus API key…", StatusKind.Info);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var info = await NexusService.GetModInfoAsync("probablystolen", 66, Settings.NexusApiKey, cts.Token, force: true);
            if (info == null)
            {
                SetStatus("接口连上了，但没拿到数据", StatusKind.Warn);
                return;
            }

            var cover = await NexusService.GetCoverAsync(info, cts.Token);
            SetStatus($"连接成功：{info.Name} · 作者 {info.Author} · 封面{(cover != null ? "已获取" : "没有")}",
                StatusKind.Good);
        }
        catch (NexusAuthException)
        {
            SetStatus("key 无效或没有权限", StatusKind.Error);
        }
        catch (NexusRateLimitException)
        {
            SetStatus("请求太频繁，过一会儿再试", StatusKind.Warn);
        }
        catch (Exception ex)
        {
            SetStatus("测试失败：" + ex.Message, StatusKind.Error);
        }
    }

    public void OpenNexusKeyPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://www.nexusmods.com/settings/api-keys") { UseShellExecute = true });
        }
        catch { }
    }

    // ---------------- 诊断 ----------------

    public int CriticalCount => Findings.Count(f => f.Level == FindingLevel.Critical);
    public int WarningCount => Findings.Count(f => f.Level == FindingLevel.Warning);
    public bool HasFindings => Findings.Count > 0;
    public string DiagnosisSummary => $"{CriticalCount} 个错误 · {WarningCount} 个警告";
    public string DiagnosisVerdict => !Game.Found
        ? "先指定游戏目录"
        : Findings.Count == 0
            ? "没发现明显的加载或冲突问题"
            : $"发现 {Findings.Count} 处需要留意";

    public void RefreshDiagnostics()
    {
        Findings.Clear();
        if (!Game.Found)
        {
            NotifyDiagnosis();
            return;
        }

        var log = LogService.Read(Game.LatestLog, 2000);
        var loader = LoaderService.GetStatus(Game);
        foreach (var finding in DiagnosticService.Analyze(Game, Mods, log, loader))
            Findings.Add(finding);

        NotifyDiagnosis();
    }

    private void NotifyDiagnosis()
    {
        OnPropertyChanged(nameof(CriticalCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(HasFindings));
        OnPropertyChanged(nameof(DiagnosisSummary));
        OnPropertyChanged(nameof(DiagnosisVerdict));
    }

    public void SelectModByName(string name)
    {
        var mod = Mods.FirstOrDefault(m => string.Equals(m.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                  ?? Mods.FirstOrDefault(m => m.DisplayName.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (mod == null) return;
        SelectedMod = mod;
        PageIndex = 1;
    }

    public void SafeLaunch()
    {
        if (GameRunner.LaunchNoMods(Game, Settings, out var error))
        {
            GameRunning = true;
            SetStatus("已用纯净模式启动，本次不加载任何模组", StatusKind.Good);
        }
        else
        {
            SetStatus(error, StatusKind.Error);
        }
    }

    // ---------------- 存档快照 ----------------

    private SaveService.SaveInfo? _saveInfo;

    public string SaveInfoText => _saveInfo is { } info
        ? $"{info.FileName} · {info.Modified:yyyy-MM-dd HH:mm} · {info.Size / 1024.0:0.#} KB"
        : (SaveService.Exists ? "存档目录里还没有存档" : "没有找到存档目录");

    public string SaveCountText => _saveInfo is { } info
        ? $"共 {info.Count} 个存档 · {info.TotalSize / 1024.0 / 1024.0:0.#} MB"
        : "";

    public string SaveFolderText
    {
        get
        {
            var dir = SaveService.SaveDir;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrWhiteSpace(home)
                ? dir
                : dir.Replace(home, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool AutoSaveBeforeProfile
    {
        get => Settings.AutoSaveBeforeProfile;
        set
        {
            Settings.AutoSaveBeforeProfile = value;
            Settings.Save();
            OnPropertyChanged();
            SetStatus(value ? "应用方案前会自动存一份存档快照" : "已关闭应用方案前的自动快照", StatusKind.Info);
        }
    }

    public void RefreshSaves()
    {
        _saveInfo = SaveService.CurrentInfo();
        SaveSnapshots.Clear();
        foreach (var snapshot in SaveService.List()) SaveSnapshots.Add(snapshot);

        OnPropertyChanged(nameof(SaveInfoText));
        OnPropertyChanged(nameof(SaveCountText));
        OnPropertyChanged(nameof(SaveSnapshotCount));
    }

    public int SaveSnapshotCount => SaveSnapshots.Count;

    public void CreateSaveSnapshot(string label = "手动")
    {
        try
        {
            var snapshot = SaveService.Create(label);
            RefreshSaves();
            SetStatus($"存档快照已保存：{snapshot.CreatedText}（{snapshot.SizeText}）", StatusKind.Good);
        }
        catch (Exception ex)
        {
            SetStatus("存档快照失败：" + ex.Message, StatusKind.Error);
        }
    }

    public void RestoreSaveSnapshot(SaveSnapshot snapshot)
    {
        if (GameRunner.IsRunning())
        {
            SetStatus("游戏正在运行，先关掉再还原存档", StatusKind.Error);
            return;
        }

        try
        {
            SaveService.Create("还原前");
            SaveService.Restore(snapshot);
            RefreshSaves();
            SetStatus($"存档已还原到 {snapshot.CreatedText}", StatusKind.Good);
        }
        catch (Exception ex)
        {
            SetStatus("还原存档失败：" + ex.Message, StatusKind.Error);
        }
    }

    public void DeleteSaveSnapshot(SaveSnapshot snapshot)
    {
        SaveService.Delete(snapshot);
        RefreshSaves();
        SetStatus("存档快照已删除", StatusKind.Info);
    }

    public void OpenSaveFolder() => GameRunner.OpenInExplorer(SaveService.SaveDir);

    // ---------------- 批量与排序 ----------------

    public void SetAllMods(bool enable)
    {
        if (GameRunner.IsRunning())
        {
            SetStatus("游戏正在运行，先关掉再批量改", StatusKind.Error);
            return;
        }

        int changed = 0, failed = 0;
        foreach (var mod in Mods.ToList())
        {
            if (mod.Enabled == enable) continue;
            var (ok, _) = ModScanner.SetEnabled(mod, enable);
            if (ok) changed++;
            else failed++;
        }

        Rescan(silent: true);
        if (changed == 0)
            SetStatus($"已经全部{(enable ? "启用" : "停用")}", StatusKind.Info);
        else
            SetStatus($"{(enable ? "启用" : "停用")}了 {changed} 个" + (failed > 0 ? $"，{failed} 个失败" : ""),
                failed > 0 ? StatusKind.Warn : StatusKind.Good);
    }

    private ModSortMode _sortMode = ModSortMode.Name;
    public string SortModeText => _sortMode switch
    {
        ModSortMode.Modified => "修改时间",
        ModSortMode.Enabled => "启用优先",
        ModSortMode.Status => "问题优先",
        _ => "名称"
    };

    public string SortTooltip => $"排序：{SortModeText}（点击切换）";

    public void CycleSort()
    {
        _sortMode = (ModSortMode)(((int)_sortMode + 1) % 4);
        ApplySort();
        OnPropertyChanged(nameof(SortModeText));
        OnPropertyChanged(nameof(SortTooltip));
        SetStatus("排序方式：" + SortModeText, StatusKind.Info);
    }

    private void ApplySort()
    {
        ModsView.SortDescriptions.Clear();
        switch (_sortMode)
        {
            case ModSortMode.Modified:
                ModsView.SortDescriptions.Add(new SortDescription(nameof(ModItem.Modified), ListSortDirection.Descending));
                break;
            case ModSortMode.Enabled:
                ModsView.SortDescriptions.Add(new SortDescription(nameof(ModItem.Enabled), ListSortDirection.Descending));
                ModsView.SortDescriptions.Add(new SortDescription(nameof(ModItem.DisplayName), ListSortDirection.Ascending));
                break;
            case ModSortMode.Status:
                ModsView.SortDescriptions.Add(new SortDescription(nameof(ModItem.HasError), ListSortDirection.Descending));
                ModsView.SortDescriptions.Add(new SortDescription(nameof(ModItem.HasWarning), ListSortDirection.Descending));
                ModsView.SortDescriptions.Add(new SortDescription(nameof(ModItem.DisplayName), ListSortDirection.Ascending));
                break;
            default:
                ModsView.SortDescriptions.Add(new SortDescription(nameof(ModItem.DisplayName), ListSortDirection.Ascending));
                break;
        }
        ModsView.Refresh();
    }

    public void ExportLog(string path)
    {
        try
        {
            File.WriteAllLines(path, Log.Lines.Select(l => $"[{l.Time}] {l.Text}"), new UTF8Encoding(false));
            SetStatus("日志已导出：" + Path.GetFileName(path), StatusKind.Good);
        }
        catch (Exception ex)
        {
            SetStatus("导出日志失败：" + ex.Message, StatusKind.Error);
        }
    }

    // ---------------- N 网绑定与更新 ----------------

    public bool BindNexusLink(string? url)
    {
        var mod = SelectedMod;
        if (mod == null) return false;

        url = url?.Trim();
        if (!string.IsNullOrWhiteSpace(url) && NexusService.ParseLink(url) == null) return false;

        NexusLinkStore.Set(mod.Key, string.IsNullOrWhiteSpace(url) ? null : url);
        mod.ManualLink = string.IsNullOrWhiteSpace(url) ? null : url;
        mod.NexusLoaded = false;
        mod.NexusState = "";
        mod.NexusAuthor = "";
        mod.NexusDescription = "";
        mod.NexusVersion = "";
        mod.HasUpdate = false;
        mod.Cover = null;

        LoadNexusForSelected();
        SetStatus(string.IsNullOrWhiteSpace(url) ? "已取消 N 网绑定" : "已绑定 N 网页面", StatusKind.Good);
        return true;
    }

    public async Task CheckUpdatesAsync()
    {
        if (string.IsNullOrWhiteSpace(Settings.NexusApiKey))
        {
            SetStatus("先在设置里填 Nexus API key 才能查更新", StatusKind.Warn);
            return;
        }

        if (GameRunner.IsRunning())
        {
            SetStatus("游戏正在运行，先关掉再查更新", StatusKind.Error);
            return;
        }

        var targets = Mods.Where(m => m.HasLink).ToList();
        if (targets.Count == 0)
        {
            SetStatus("没有绑定 N 网页面的模组", StatusKind.Info);
            return;
        }

        SetStatus($"正在检查 {targets.Count} 个模组的 N 网版本…", StatusKind.Info);
        int checkedCount = 0, updates = 0, failed = 0, index = 0;

        foreach (var mod in targets)
        {
            index++;
            SetStatus($"正在检查 N 网更新 {index}/{targets.Count}：{mod.DisplayName}", StatusKind.Info);

            var parsed = NexusService.ParseLink(mod.NexusLink);
            if (parsed == null) continue;

            try
            {
                var info = await NexusService.GetModInfoAsync(parsed.Value.game, parsed.Value.id,
                    Settings.NexusApiKey, CancellationToken.None, force: true);
                if (info == null)
                {
                    failed++;
                    continue;
                }

                mod.NexusVersion = info.Version;
                mod.HasUpdate = NexusService.IsNewer(info.Version, string.IsNullOrWhiteSpace(mod.MelonVersion)
                    ? mod.AssemblyVersion
                    : mod.MelonVersion);
                checkedCount++;
                if (mod.HasUpdate) updates++;
            }
            catch (NexusAuthException)
            {
                SetStatus("Nexus API key 无效，检查一下设置", StatusKind.Error);
                return;
            }
            catch (NexusRateLimitException)
            {
                SetStatus("Nexus 接口请求太频繁，过一会儿再查", StatusKind.Warn);
                return;
            }
            catch
            {
                failed++;
            }

            await Task.Delay(220);
        }

        SetStatus($"检查完成：{checkedCount} 个查到，{updates} 个有新版" + (failed > 0 ? $"，{failed} 个失败" : ""),
            updates > 0 ? StatusKind.Good : StatusKind.Info);
    }

    // ---------------- N 网搜索 ----------------

    private CancellationTokenSource? _searchCts;
    private int _searchGeneration;
    private NexusSearchSort _searchSort = NexusSearchSort.Relevance;
    private string _searchQuery = "";
    private string _searchState = "idle";
    private string _searchError = "";
    private int _searchTotal;
    private bool _searchBusy;

    public string SearchQuery
    {
        get => _searchQuery;
        set => Set(ref _searchQuery, value);
    }

    public bool SearchBusy
    {
        get => _searchBusy;
        private set
        {
            if (!Set(ref _searchBusy, value)) return;
            NotifySearchState();
        }
    }

    public string SearchSortText => _searchSort switch
    {
        NexusSearchSort.Downloads => "下载最多",
        NexusSearchSort.Endorsements => "点赞最多",
        NexusSearchSort.Updated => "最近更新",
        _ => "相关度"
    };

    public string SearchSortButtonText => "排序：" + SearchSortText;

    public string SearchState
    {
        get => _searchState;
        private set
        {
            if (_searchState == value) return;
            _searchState = value;
            NotifySearchState();
        }
    }

    public string SearchErrorText
    {
        get => _searchError;
        private set
        {
            _searchError = value;
            OnPropertyChanged();
        }
    }

    public bool ShowSearchResults => _searchState == "ready" && SearchResults.Count > 0;
    public bool ShowSearchNoKey => _searchState == "no-key";
    public bool ShowSearchEmpty => _searchState == "empty";
    public bool ShowSearchError => _searchState is "error" or "bad-key" or "rate";
    public bool ShowSearchIdle => _searchState == "idle";
    public bool ShowSearchLoading => _searchState == "loading";
    public bool SearchNeedsKey => string.IsNullOrWhiteSpace(Settings.NexusApiKey);
    public bool HasMoreSearchResults => SearchResults.Count < _searchTotal;

    public string SearchTotalText => _searchTotal > 0
        ? $"共 {_searchTotal} 个结果，已显示 {SearchResults.Count} 个"
        : "";

    public void NotifySearchState()
    {
        OnPropertyChanged(nameof(SearchState));
        OnPropertyChanged(nameof(SearchBusy));
        OnPropertyChanged(nameof(SearchTotalText));
        OnPropertyChanged(nameof(HasMoreSearchResults));
        OnPropertyChanged(nameof(ShowSearchResults));
        OnPropertyChanged(nameof(ShowSearchNoKey));
        OnPropertyChanged(nameof(ShowSearchEmpty));
        OnPropertyChanged(nameof(ShowSearchError));
        OnPropertyChanged(nameof(ShowSearchIdle));
        OnPropertyChanged(nameof(ShowSearchLoading));
        OnPropertyChanged(nameof(SearchNeedsKey));
        OnPropertyChanged(nameof(SearchSortText));
        OnPropertyChanged(nameof(SearchSortButtonText));
    }

    public void RefreshSearchAvailability()
    {
        if (SearchNeedsKey && SearchState is "idle" or "ready")
            SearchState = "no-key";
        else if (!SearchNeedsKey && SearchState == "no-key")
            SearchState = "idle";

        NotifySearchState();
    }

    public void OpenNexusSearchPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://www.nexusmods.com/probablystolen/mods") { UseShellExecute = true });
        }
        catch { }
    }

    public void GoToNexusKeySettings() => PageIndex = 9;

    public void CycleSearchSort()
    {
        _searchSort = (NexusSearchSort)(((int)_searchSort + 1) % 4);
        OnPropertyChanged(nameof(SearchSortText));
        OnPropertyChanged(nameof(SearchSortButtonText));

        if (_searchState is "ready" or "empty") _ = SearchNexusAsync(false);
    }

    public async Task SearchNexusAsync(bool append = false)
    {
        if (string.IsNullOrWhiteSpace(Settings.NexusApiKey))
        {
            SearchState = "no-key";
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;
        var generation = ++_searchGeneration;

        SearchBusy = true;
        SearchState = "loading";

        if (!append)
        {
            SearchResults.Clear();
            _searchTotal = 0;
        }

        NotifySearchState();

        try
        {
            var offset = append ? SearchResults.Count : 0;
            var page = await NexusService.SearchAsync("probablystolen", SearchQuery.Trim(), _searchSort,
                offset, 20, Settings.NexusApiKey, ct);

            if (generation != _searchGeneration) return;

            _searchTotal = page.Total;
            foreach (var result in page.Results) SearchResults.Add(result);

            SearchState = SearchResults.Count == 0 ? "empty" : "ready";
            NotifySearchState();

            _ = LoadSearchThumbnailsAsync(page.Results, ct);
        }
        catch (OperationCanceledException)
        {
            if (generation == _searchGeneration && SearchState == "loading")
                SearchState = SearchResults.Count > 0 ? "ready" : "idle";
        }
        catch (NexusAuthException)
        {
            if (generation != _searchGeneration) return;
            SearchErrorText = "Nexus API key 无效或过期了，去设置里检查一下。";
            SearchState = "bad-key";
        }
        catch (NexusRateLimitException)
        {
            if (generation != _searchGeneration) return;
            SearchErrorText = "Nexus 接口请求太频繁，过一会儿再搜。";
            SearchState = "rate";
        }
        catch (Exception ex)
        {
            if (generation != _searchGeneration) return;
            SearchErrorText = "搜索失败：" + ex.Message;
            SearchState = "error";
        }
        finally
        {
            if (generation == _searchGeneration)
            {
                SearchBusy = false;
                NotifySearchState();
            }
        }
    }

    private async Task LoadSearchThumbnailsAsync(IReadOnlyList<NexusSearchResult> items, CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(4);
        var tasks = items.Select(async item =>
        {
            try
            {
                await gate.WaitAsync(ct);
                var cover = await NexusService.GetSearchThumbnailAsync(item, ct);
                if (cover != null) item.Cover = cover;
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                try { gate.Release(); } catch { }
            }
        });

        try { await Task.WhenAll(tasks); } catch { }
    }

    public void OpenSearchResult(NexusSearchResult result)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(result.PageUrl) { UseShellExecute = true });
        }
        catch { }
    }

    // ---------------- 外观与音乐 ----------------

    private ImageSource? _backgroundImage;
    public ImageSource? BackgroundImage
    {
        get => _backgroundImage;
        private set
        {
            if (!Set(ref _backgroundImage, value)) return;
            OnPropertyChanged(nameof(HasBackground));
            OnPropertyChanged(nameof(BackgroundStateText));
        }
    }

    public bool HasBackground => BackgroundImage != null;

    public string BackgroundStateText => HasBackground ? "已启用自定义背景" : "默认深色背景";

    public double BackgroundDim
    {
        get => Settings.BackgroundDim;
        set
        {
            var clamped = Math.Clamp(value, 0, 0.92);
            if (Math.Abs(clamped - Settings.BackgroundDim) < 0.001) return;
            Settings.BackgroundDim = clamped;
            Settings.Save();
            OnPropertyChanged();
        }
    }

    public double BackgroundBlur
    {
        get => Settings.BackgroundBlur;
        set
        {
            var clamped = Math.Clamp(value, 0, 30);
            if (Math.Abs(clamped - Settings.BackgroundBlur) < 0.001) return;
            Settings.BackgroundBlur = clamped;
            Settings.Save();
            OnPropertyChanged();
        }
    }

    public void SetBackground(string path)
    {
        var imported = BackgroundService.Import(path);
        if (imported == null)
        {
            SetStatus("这个图片格式不支持，换 png / jpg / bmp 试试", StatusKind.Warn);
            return;
        }

        Settings.BackgroundPath = imported;
        Settings.Save();
        BackgroundImage = BackgroundService.Load(imported);
        SetStatus("背景已更新", StatusKind.Good);
    }

    public void ClearBackground()
    {
        BackgroundService.ClearFiles();
        Settings.BackgroundPath = "";
        Settings.Save();
        BackgroundImage = null;
        SetStatus("已恢复默认背景", StatusKind.Info);
    }

    private void LoadBackground()
    {
        if (string.IsNullOrWhiteSpace(Settings.BackgroundPath) || !File.Exists(Settings.BackgroundPath))
        {
            Settings.BackgroundPath = "";
            return;
        }

        BackgroundImage = BackgroundService.Load(Settings.BackgroundPath);
    }

    public ObservableCollection<MusicTrack> MusicTracks { get; } = new();

    private MusicTrack? _selectedMusicTrack;
    public MusicTrack? SelectedMusicTrack
    {
        get => _selectedMusicTrack;
        set => Set(ref _selectedMusicTrack, value);
    }

    public bool HasMusic => MusicTracks.Count > 0;
    public bool IsMusicPlaying => MusicService.IsPlaying;
    public string CurrentTrackName => string.IsNullOrWhiteSpace(MusicService.CurrentName)
        ? "还没有正在播放的曲目"
        : MusicService.CurrentName;
    public string MusicPlayPauseText => IsMusicPlaying ? "暂停" : "播放";

    public double MusicVolume
    {
        get => Settings.MusicVolume;
        set
        {
            var clamped = Math.Clamp(value, 0, 1);
            if (Math.Abs(clamped - Settings.MusicVolume) < 0.001) return;
            Settings.MusicVolume = clamped;
            Settings.Save();
            MusicService.SetVolume(clamped);
            OnPropertyChanged();
        }
    }

    public bool MusicLoop
    {
        get => Settings.MusicLoop;
        set
        {
            if (Settings.MusicLoop == value) return;
            Settings.MusicLoop = value;
            Settings.Save();
            MusicService.Loop = value;
            OnPropertyChanged();
        }
    }

    public bool MusicAutoPlay
    {
        get => Settings.MusicAutoPlay;
        set
        {
            if (Settings.MusicAutoPlay == value) return;
            Settings.MusicAutoPlay = value;
            Settings.Save();
            OnPropertyChanged();

            if (value && HasMusic && !IsMusicPlaying) MusicService.Play(0);
        }
    }

    private void LoadMusic()
    {
        MusicService.StateChanged += NotifyMusic;

        var files = Settings.MusicFiles.Where(File.Exists).ToList();
        foreach (var file in files) MusicTracks.Add(new MusicTrack { Path = file });
        Settings.MusicFiles = files;
        Settings.Save();

        MusicService.Load(files, Settings.MusicVolume, Settings.MusicLoop);
        NotifyMusic();

        if (Settings.MusicAutoPlay && files.Count > 0) MusicService.Play(0);
    }

    private void NotifyMusic()
    {
        var app = Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(new Action(NotifyMusic));
            return;
        }

        OnPropertyChanged(nameof(HasMusic));
        OnPropertyChanged(nameof(IsMusicPlaying));
        OnPropertyChanged(nameof(CurrentTrackName));
        OnPropertyChanged(nameof(MusicPlayPauseText));
    }

    public void AddMusicFiles(IEnumerable<string> paths)
    {
        var start = MusicTracks.Count;

        foreach (var path in paths)
        {
            var imported = MusicService.Import(path);
            if (imported == null) continue;
            var track = new MusicTrack { Path = imported };
            MusicTracks.Add(track);
        }

        if (MusicTracks.Count == start)
        {
            SetStatus("没有添加任何音乐（支持 mp3 / wav / wma / m4a / aac）", StatusKind.Warn);
            return;
        }

        Settings.MusicFiles = MusicTracks.Select(t => t.Path).ToList();
        Settings.Save();
        MusicService.Load(Settings.MusicFiles, Settings.MusicVolume, Settings.MusicLoop);
        MusicService.Play(start);
        NotifyMusic();
        SetStatus($"已添加 {MusicTracks.Count - start} 首背景音乐", StatusKind.Good);
    }

    public void PlaySelectedMusic()
    {
        if (MusicTracks.Count == 0) return;
        var index = SelectedMusicTrack != null ? MusicTracks.IndexOf(SelectedMusicTrack) : 0;
        MusicService.Play(Math.Max(index, 0));
        NotifyMusic();
    }

    public void ToggleMusic()
    {
        if (!HasMusic) return;
        MusicService.Toggle();
        NotifyMusic();
    }

    public void NextMusic()
    {
        MusicService.Next();
        NotifyMusic();
    }

    public void PreviousMusic()
    {
        MusicService.Previous();
        NotifyMusic();
    }

    public void RemoveSelectedMusic()
    {
        if (SelectedMusicTrack is not { } track) return;

        MusicService.Stop();
        MusicService.DeleteFiles(new[] { track.Path });
        MusicTracks.Remove(track);
        SelectedMusicTrack = null;

        Settings.MusicFiles = MusicTracks.Select(t => t.Path).ToList();
        Settings.Save();
        MusicService.Load(Settings.MusicFiles, Settings.MusicVolume, Settings.MusicLoop);
        NotifyMusic();
        SetStatus("已移除这首音乐", StatusKind.Info);
    }

    public void ClearMusic()
    {
        MusicService.Stop();
        MusicService.DeleteFiles(MusicTracks.Select(t => t.Path));
        MusicTracks.Clear();
        SelectedMusicTrack = null;

        Settings.MusicFiles = new List<string>();
        Settings.Save();
        MusicService.Load(Array.Empty<string>(), Settings.MusicVolume, Settings.MusicLoop);
        NotifyMusic();
        SetStatus("已清空背景音乐", StatusKind.Info);
    }

    // ---------------- 设置 ----------------

    public void SaveSettings()
    {
        Settings.Save();
        ThemeService.ApplyStyle(Settings.UiStyle, Settings.Accent);
        SetStatus("设置已保存", StatusKind.Good);
    }

    public void SetUiStyle(string style)
    {
        if (style is not ("terminal" or "night" or "graphite")) return;

        Settings.UiStyle = style;
        Settings.Accent = style == "graphite" ? "#5F86C0" : "#E0A15A";
        Settings.Save();
        ThemeService.ApplyStyle(style, Settings.Accent);
        if (Application.Current?.MainWindow is { } window) ThemeMetrics.SetUiStyle(window, style);
        Rescan(silent: true);
        RefreshLog();
        OnPropertyChanged(nameof(UiStyleText));
        SetStatus("界面风格已切换", StatusKind.Info);
    }

    public string UiStyleText => Settings.UiStyle switch
    {
        "night" => "夜班",
        "graphite" => "石墨",
        _ => "终端"
    };

    public void ApplyAccent(string hex)
    {
        Settings.Accent = hex;
        Settings.Save();
        ThemeService.ApplyStyle(Settings.UiStyle, hex);
    }

    // ---------------- 状态栏 ----------------

    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    private StatusKind _statusTone;
    public StatusKind StatusTone
    {
        get => _statusTone;
        private set
        {
            if (!Set(ref _statusTone, value)) return;
            OnPropertyChanged(nameof(StatusBrush));
        }
    }

    public Brush StatusBrush => _statusTone switch
    {
        StatusKind.Good => new SolidColorBrush(Color.FromRgb(0x2E, 0x9E, 0x6B)),
        StatusKind.Warn => new SolidColorBrush(Color.FromRgb(0xD0, 0x84, 0x20)),
        StatusKind.Error => new SolidColorBrush(Color.FromRgb(0xD9, 0x48, 0x3B)),
        _ => new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0xA3))
    };

    public void SetStatus(string text, StatusKind kind)
    {
        StatusText = text;
        StatusTone = kind;
    }

    public void Tick()
    {
        var running = GameRunner.IsRunning();
        if (running != GameRunning) GameRunning = running;
    }

    /// <summary>--dump 调试用：把扫描结果整体吐成 JSON，方便核对元数据解析。</summary>
    public string DumpJson()
    {
        var data = new
        {
            game = new
            {
                dir = Game.GameDir,
                found = Game.Found,
                version = Game.Version,
                loader = Game.Loader,
                type = Game.GameType,
                developer = Game.GameDeveloper
            },
            log = new
            {
                exists = Log.Exists,
                loaded = Log.LoadedDlls.OrderBy(x => x).ToList(),
                failed = Log.FailedDlls,
                errors = Log.ErrorCount,
                warnings = Log.WarnCount,
                header = Log.Header
            },
            config = new
            {
                path = Config.Path,
                exists = Config.Exists,
                sections = Config.Sections.Count,
                entries = Config.AllEntries.Count,
                editable = Config.EditableCount,
                error = Config.LoadError
            },
            backupCount = Backups.Count,
            loader = new
            {
                installed = Loader.Installed,
                version = Loader.Version,
                disabled = Loader.Disabled,
                assemblies = Loader.AssembliesGenerated,
                cachedZip = Loader.CacheExists,
                cacheSize = Loader.CacheSize
            },
            mods = Mods.Select(m => new
            {
                name = m.DisplayName,
                en = m.EnglishName,
                file = m.FileName,
                folder = m.IsFolder,
                assembly = m.AssemblyName,
                version = m.VersionText,
                author = m.Author,
                enabled = m.Enabled,
                hasMelonInfo = m.HasMelonInfo,
                melonGame = m.GameName,
                deps = m.Dependencies,
                logState = m.LogState,
                summary = m.CatalogSummary,
                link = m.CatalogLink,
                issues = m.Issues.Select(i => $"{i.Level}: {i.Text}")
            }),
            music = new
            {
                files = MusicTracks.Select(t => t.FileName).ToList(),
                playing = IsMusicPlaying,
                current = CurrentTrackName,
                volume = MusicVolume,
                loop = MusicLoop,
                autoPlay = MusicAutoPlay
            },
            background = new
            {
                custom = HasBackground,
                dim = BackgroundDim,
                blur = BackgroundBlur
            },
            uiStyle = Settings.UiStyle,
            accent = Settings.Accent
        };

        return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
}
