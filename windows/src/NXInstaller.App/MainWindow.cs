using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NXInstaller.Core;

namespace NXInstaller.App;

public sealed class ComponentRow : Border
{
    public TextBlock TitleText { get; }
    public TextBlock DetailText { get; }
    public TextBlock BadgeText { get; } = Ui.Label("QUEUED", 10, Ui.Muted, true);
    public StepStatus Status { get; private set; }
    public ComponentRow(string title, string detail)
    {
        Padding = new Thickness(12, 8); CornerRadius = new CornerRadius(7); Background = Ui.Field;
        BorderBrush = Ui.Border; BorderThickness = new Thickness(1); Margin = new Thickness(0, 0, 0, 6);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        TitleText = Ui.Label(title, 12, bold: true); DetailText = Ui.Label(detail, 11, Ui.Muted);
        grid.Children.Add(new StackPanel { Spacing = 3, Children = { TitleText, DetailText } });
        var badge = new Border { Child = BadgeText, Padding = new Thickness(8, 5), Background = Ui.Card, CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(badge, 1); grid.Children.Add(badge); Child = grid;
    }
    public void Update(InstallEvent e)
    {
        Status = e.Status; TitleText.Text = e.Title; DetailText.Text = e.Detail.Length > 170 ? e.Detail[..167] + "…" : e.Detail;
        BadgeText.Text = e.Status switch { StepStatus.Done => "READY", StepStatus.Running => "WORKING", StepStatus.ActionRequired => "ACTION", StepStatus.Failed => "FAILED", StepStatus.Cancelled => "STOPPED", _ => "QUEUED" };
        BadgeText.Foreground = e.Status switch { StepStatus.Done => Ui.Good, StepStatus.Running => Ui.Cyan, StepStatus.Failed => Ui.Bad, StepStatus.ActionRequired or StepStatus.Cancelled => Ui.Warn, _ => Ui.Muted };
    }
}

public sealed class MainWindow : Window
{
    public readonly Grid RootGrid = new() { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"), RowSpacing = 12, Margin = new Thickness(20) };
    public readonly Grid Workspace = new() { RowDefinitions = new RowDefinitions("0.40*,8,0.60*") };
    public readonly TextBox RootPath = new() { FontSize = 12, MinHeight = 36, Background = Ui.Field };
    public readonly TextBox Log = new() { IsReadOnly = true, AcceptsReturn = true, FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Background = Ui.Field, BorderThickness = new Thickness(0), Padding = new Thickness(12) };
    public readonly Border LogPanel;
    public readonly Border ComponentsPanel;
    public readonly GridSplitter LogSplitter = new() { ResizeDirection = GridResizeDirection.Rows, ResizeBehavior = GridResizeBehavior.PreviousAndNext, Background = Ui.Border, HorizontalAlignment = HorizontalAlignment.Stretch };
    public readonly Dictionary<string, ComponentRow> Components = new();
    public readonly Button Install = Ui.Button("Install Everything", true), UpdateButton = Ui.Button("Check for Updates"), AppleButton = Ui.Button("Apple Setup"), StopButton = Ui.Button("Stop"), Launch = Ui.Button("Launch NXNativeWindows IPA Builder", true);
    private readonly TextBlock headline = Ui.Label("NXNativeWindows Easy Installer", 21, bold: true);
    private readonly TextBlock credits = Ui.Label(Ui.Creator + "  ·  " + Ui.Original, 10, Ui.Muted);
    private readonly TextBlock update = Ui.Label("Checking the latest repository…", 11, Ui.Muted);
    private readonly TextBlock status = Ui.Label("Ready when you are.", 13, bold: true), detail = Ui.Label("One setup flow, with real checks before launch.", 11, Ui.Muted), percent = Ui.Label("0%", 12, Ui.Cyan, true);
    private readonly TextBlock counter = Ui.Label("0 / 13 ready", 10, Ui.Muted);
    private readonly ProgressBar progress = new() { Height = 5, Minimum = 0, Maximum = 100, Foreground = Ui.Accent };
    private readonly ComboBox mode = new() { ItemsSource = new[] { "Install / Repair", "Update Everything" }, SelectedIndex = 0, MinHeight = 36, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
    public readonly Button Browse = Ui.Button("Browse");
    private readonly Downloads downloads = new();
    private readonly InstallerEngine engine;
    private readonly ConcurrentQueue<string> logQueue = new();
    private readonly DispatcherTimer logTimer;
    private CancellationTokenSource? cancellation;
    private AppleSelection apple = new();
    private bool busy, checking;
    private readonly bool preview;
    private string Preferences => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXNativeWindowsEasyInstaller", "preferences.json");
    public MainWindow(string? installRoot = null, bool preview = false)
    {
        this.preview = preview; Title = "NXNativeWindows Easy Installer v" + Ui.Version + " · iSolution";
        Width = 1100; Height = 900; MinWidth = 480; MinHeight = 480; Background = Ui.Bg; Foreground = Ui.Text; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        using(var icon=Avalonia.Platform.AssetLoader.Open(new Uri("avares://"+typeof(MainWindow).Assembly.GetName().Name+"/Assets/installer.ico")))Icon=new WindowIcon(icon);
        RootPath.Text = installRoot ?? DefaultRoot(); engine = new(new ProcessRunner(), downloads);
        engine.Log += AppendLog; engine.Progress += OnProgress;
        engine.RequestAppleFiles = (selection, ct) => Dispatcher.UIThread.InvokeAsync(async () => await OpenAppleAsync(selection, ct));
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        var brand = new StackPanel { Spacing = 5 }; brand.Children.Add(headline); brand.Children.Add(credits); header.Children.Add(brand);
        var about = Ui.Button("Credits"); about.Click += async (_, _) => await ShowCredits(); Grid.SetColumn(about, 1); header.Children.Add(about); RootGrid.Children.Add(header);

        var setup = Ui.Stack(); setup.Spacing = 8;
        var location = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        RootPath.Watermark = "Installation folder"; location.Children.Add(RootPath); Grid.SetColumn(Browse, 1); location.Children.Add(Browse); setup.Children.Add(location);
        var tools = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8 };
        tools.Children.Add(mode); Grid.SetColumn(UpdateButton, 1); tools.Children.Add(UpdateButton); Grid.SetColumn(AppleButton, 2); tools.Children.Add(AppleButton); setup.Children.Add(tools); setup.Children.Add(update);
        Grid.SetRow(setup, 1); RootGrid.Children.Add(setup);
        var statusGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        statusGrid.Children.Add(status); Grid.SetColumn(percent, 1); statusGrid.Children.Add(percent);
        var statusBox = Ui.Box(Ui.Stack(statusGrid, detail, progress), 12); Grid.SetRow(statusBox, 2); RootGrid.Children.Add(statusBox);

        var componentContent = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 10 };
        var componentHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }; componentHeader.Children.Add(Ui.Label("Components", 12, bold: true)); Grid.SetColumn(counter, 1); componentHeader.Children.Add(counter); componentContent.Children.Add(componentHeader);
        var componentRows = new StackPanel();
        foreach (var (id, name, info) in new[] {
            ("host","Compatibility","Windows version and CPU architecture"), ("repository","GitHub repository","Latest commit, download and extraction"),
            ("winget","Windows package manager","Microsoft App Installer / WinGet"), ("vcredist","VC++ runtime","Native runtime libraries"),
            ("python","Python 3.13","Python runtime and Tcl/Tk"), ("llvm","LLVM / Clang","Compiler and Mach-O linker"),
            ("dotnet",".NET SDK 10","Extractor and shader translator"), ("vulkan","Vulkan SDK","SPIRV-Cross and glslangValidator"),
            ("zstd","Zstandard","Compression tools"), ("packages","Python environment","Packages and isolated virtual environment"),
            ("apple","Apple developer tools","Xcode XIP extraction and automatic Metal setup"), ("environment","Toolchain configuration","Automatic tool paths and environment variables"),
            ("verify","Installation checks","Doctor and actual execution checks") })
        { var row = new ComponentRow(name, info); Components[id] = row; componentRows.Children.Add(row); }
        var componentScroll = new ScrollViewer { Content = componentRows, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(componentScroll, 1); componentContent.Children.Add(componentScroll);
        ComponentsPanel = Ui.Box(componentContent, 12); ComponentsPanel.MinHeight = 85; Workspace.Children.Add(ComponentsPanel);
        Grid.SetRow(LogSplitter, 1); Workspace.Children.Add(LogSplitter);
        ToolTip.SetTip(LogSplitter,"Drag this divider up to enlarge the Activity Log");

        var logContent = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8 };
        var logHead = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), ColumnSpacing = 8 };
        logHead.Children.Add(Ui.Label("Activity Log", 12, bold: true));
        var wrap = new CheckBox { Content = "Wrap", IsChecked = true, FontSize = 11, MinHeight = 28 }; wrap.IsCheckedChanged += (_, _) => { Log.TextWrapping = wrap.IsChecked == true ? TextWrapping.Wrap : TextWrapping.NoWrap; ScrollViewer.SetHorizontalScrollBarVisibility(Log,wrap.IsChecked==true?ScrollBarVisibility.Disabled:ScrollBarVisibility.Auto); };
        Grid.SetColumn(wrap, 1); logHead.Children.Add(wrap);
        var clear = Ui.Button("Clear"); clear.Padding = new Thickness(10, 4); clear.MinHeight = 28; clear.Click += (_, _) => Log.Text = ""; Grid.SetColumn(clear, 2); logHead.Children.Add(clear);
        var folder = Ui.Button("Open Logs"); folder.Padding = new Thickness(10, 4); folder.MinHeight = 28; folder.Click += async (_, _) => await OpenLogs(); Grid.SetColumn(folder, 3); logHead.Children.Add(folder); logContent.Children.Add(logHead);
        ScrollViewer.SetVerticalScrollBarVisibility(Log, ScrollBarVisibility.Auto); ScrollViewer.SetHorizontalScrollBarVisibility(Log, ScrollBarVisibility.Disabled);
        Grid.SetRow(Log, 1); logContent.Children.Add(Log); LogPanel = Ui.Box(logContent, 12); LogPanel.MinHeight = 150; Grid.SetRow(LogPanel, 2); Workspace.Children.Add(LogPanel);
        Grid.SetRow(Workspace, 3); RootGrid.Children.Add(Workspace);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 }; Install.HorizontalAlignment = HorizontalAlignment.Stretch; footer.Children.Add(Install); Grid.SetColumn(StopButton, 1); footer.Children.Add(StopButton); StopButton.IsEnabled = false;
        Launch.HorizontalAlignment = HorizontalAlignment.Stretch; Launch.IsVisible = false;
        var footerStack = Ui.Stack(Launch, footer); Grid.SetRow(footerStack, 4); RootGrid.Children.Add(footerStack);
        Content = new ScrollViewer { Content = RootGrid, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        SizeChanged += (_, _) => Reflow();
        Install.Click += async (_, _) => await InstallAsync(); UpdateButton.Click += async (_, _) => await CheckUpdates();
        AppleButton.Click += async (_, _) => { var selected = await OpenAppleAsync(apple, default); if (selected != null) { apple = selected; var state = StateStore.Load(RootPath.Text!); state.Apple = apple; state.Ready = false; StateStore.Save(RootPath.Text!, state); Launch.IsVisible = false; } };
        Browse.Click += async (_, _) => await BrowseAsync();
        StopButton.Click += (_, _) => { StopButton.IsEnabled = false; cancellation?.Cancel(); }; Launch.Click += async (_, _) => await LaunchAsync();
        mode.SelectionChanged += (_, _) => Install.Content = mode.SelectedIndex == 1 ? "Install Update" : "Install Everything";
        Closing += (_, e) => { if (busy) { e.Cancel = true; status.Text = "Press Stop before closing the installer."; } };
        Opened += async (_, _) =>
        {
            if(!preview && (Screens.ScreenFromWindow(this)??Screens.Primary) is {} screen)
            {
                double scale=RenderScaling;
                MinWidth=Math.Min(480,screen.WorkingArea.Width/scale-24);MinHeight=Math.Min(480,screen.WorkingArea.Height/scale-32);
                Width=Math.Min(1100,screen.WorkingArea.Width/scale-24);Height=Math.Min(900,screen.WorkingArea.Height/scale-32);
                Position=new PixelPoint(screen.WorkingArea.X+(int)((screen.WorkingArea.Width-Width*scale)/2),screen.WorkingArea.Y+(int)((screen.WorkingArea.Height-Height*scale)/2));
            }
            Reflow();
            if (!preview)
            {
                LoadState(); AppendLog("NXNativeWindows Easy Installer " + Ui.Version); AppendLog(Ui.Creator); AppendLog(Ui.Original); AppendLog(HostPolicy.Current.Detail);
                if (Program.Arguments.Contains("--launch")) await LaunchAsync(); else await CheckUpdates();
            }
        };
        logTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) }; logTimer.Tick += (_, _) => FlushLog(); logTimer.Start();
        Closed += (_, _) => { logTimer.Stop(); downloads.Dispose(); cancellation?.Dispose(); };
    }
    private string DefaultRoot()
    {
        try { var data = JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Preferences)); if (data?.GetValueOrDefault("root") is { } found) return found; } catch { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXNativeWindows");
    }
    private void Reflow()
    {
        RootGrid.Height = Math.Max(540, ClientSize.Height - 40);
        bool compact = ClientSize.Height < 700; credits.IsVisible = !compact; update.IsVisible = !compact; detail.IsVisible = !compact;
        headline.Text = ClientSize.Width < 800 ? "NXNativeWindows  ·  v" + Ui.Version : "NXNativeWindows Easy Installer  ·  v" + Ui.Version;
        headline.FontSize = ClientSize.Width < 800 ? 18 : 21;
        RootGrid.RowSpacing = compact ? 8 : 12; LogPanel.MinHeight = compact ? 180 : 220;
        if (ClientSize.Width < 600) { UpdateButton.Content = "Updates"; AppleButton.Content = "Apple Setup"; } else UpdateButton.Content = "Check for Updates";
    }
    private void LoadState()
    { try { var state = StateStore.Load(RootPath.Text!); apple = state.Apple; Launch.IsVisible = state.Ready && state.Apple.CanUseSdk; } catch (Exception e) { AppendLog(e.Message); } }
    public async Task BrowseAsync()
    {
        if (busy) return;
        Browse.IsEnabled = false;
        try
        {
            var selected = await PathPickerWindow.PickAsync(this, RootPath.Text);
            if (selected == null) return;
            RootPath.Text = selected; LoadState();
            if (!preview) StateStore.AtomicJson(Preferences, new Dictionary<string, string> { ["root"] = selected });
            AppendLog("Installation folder selected: " + selected);
        }
        catch (Exception e) { AppendLog("Folder selection: " + e); await Ui.Alert(this, "Folder selection", e.Message + " You can also type the installation path directly."); }
        finally { Browse.IsEnabled = !busy; }
    }
    public void AppendLog(string text) => logQueue.Enqueue(text);
    public void FlushLog()
    {
        var lines = new List<string>(); while (logQueue.TryDequeue(out var line)) lines.Add(line);
        if (lines.Count == 0) return;
        string text = (Log.Text ?? "") + string.Join(Environment.NewLine, lines) + Environment.NewLine;
        if (text.Length > 200000) text = text[^160000..]; Log.Text = text; Log.CaretIndex = text.Length;
    }
    private void OnProgress(InstallEvent e) => Dispatcher.UIThread.Post(() =>
    {
        if (Components.TryGetValue(e.Id, out var row)) row.Update(e);
        status.Text = e.Title; detail.Text = e.Detail.Length > 300 ? e.Detail[..297] + "…" : e.Detail;
        int done = Components.Values.Count(x => x.Status == StepStatus.Done); counter.Text = $"{done} / 13 ready";
        progress.Value = 100.0 * (done + (e.Status == StepStatus.Running ? Math.Min(e.Fraction ?? .1, .99) : 0)) / 13;
        progress.IsIndeterminate = e.Status == StepStatus.Running && e.Id == "repository" && e.Fraction == null;
        percent.Text = ((int)progress.Value) + "%";
    });
    private void Busy(bool value)
    { busy = value; Install.IsEnabled = UpdateButton.IsEnabled = AppleButton.IsEnabled = Browse.IsEnabled = mode.IsEnabled = !value; RootPath.IsReadOnly = value; StopButton.IsEnabled = value; if (!value) progress.IsIndeterminate = false; }
    private async Task CheckUpdates()
    {
        if (busy || checking) return; checking = true; UpdateButton.IsEnabled = Install.IsEnabled = false;
        try { var latest = await engine.CheckForUpdatesAsync(default); var current = StateStore.Load(RootPath.Text!); update.Text = current.Commit == latest.Commit ? "Up to date · " + latest.Branch + " / " + latest.Commit[..8] : "Latest GitHub commit: " + latest.Commit[..8] + " · ready to install"; update.Foreground = Ui.Good; AppendLog("GitHub " + latest.Commit + " · " + latest.Message); if (current.Commit.Length > 0 && current.Commit != latest.Commit) mode.SelectedIndex = 1; }
        catch (Exception e) { update.Text = "Update check failed. See the activity log."; update.Foreground = Ui.Warn; AppendLog(e.Message); }
        finally { checking = false; UpdateButton.IsEnabled = Install.IsEnabled = true; }
    }
    private async Task InstallAsync()
    {
        if (busy) return;
        try
        {
            string root = StateStore.ValidateRoot(RootPath.Text!); StateStore.AtomicJson(Preferences, new Dictionary<string,string> { ["root"] = root });
            cancellation = new(); Busy(true); Launch.IsVisible = false;
            var options = new InstallOptions(root, mode.SelectedIndex == 1 ? InstallMode.Update : InstallMode.InstallRepair, apple);
            var result = await Task.Run(() => engine.InstallAsync(options, cancellation.Token));
            LoadState(); status.Text = result.Ready ? "All required checks passed." : "Setup needs your attention."; detail.Text = result.Message; AppendLog(result.Message);
            if (result.Ready) { progress.Value = 100; percent.Text = "100%"; await Shortcut(root); }
        }
        catch (OperationCanceledException) { status.Text = "Setup stopped. Run Install / Repair to resume."; }
        catch (Exception e) { status.Text = "Setup could not finish. See the log."; AppendLog(e.ToString()); await Ui.Alert(this, "Setup needs attention", e.Message); }
        finally { Busy(false); cancellation?.Dispose(); cancellation = null; }
    }
    private async Task<AppleSelection?> OpenAppleAsync(AppleSelection selection, CancellationToken ct)
    { var dialog = new AppleSetupWindow(RootPath.Text!, selection, AppendLog); using var reg = ct.Register(() => Dispatcher.UIThread.Post(() => dialog.Close())); return await dialog.ShowDialog<AppleSelection?>(this); }
    private async Task LaunchAsync()
    {
        try { string root = RootPath.Text!; var state = StateStore.Load(root); var checks = await new HealthVerifier(new ProcessRunner(), AppendLog).VerifyAsync(root, state, false, default); if (checks.Any(c => c.Required && !c.Ok)) throw new InvalidOperationException("A prerequisite failed. Run Install / Repair."); InstallerEngine.Launch(root); status.Text = "IPA Builder launched."; }
        catch (Exception e) { AppendLog(e.Message); await Ui.Alert(this, "Builder launch", e.Message); }
    }
    private async Task Shortcut(string root)
    {
        try
        {
            string exe = Environment.ProcessPath!, dest = Path.Combine(root, "NXNativeWindowsEasyInstaller.exe"); if (!Path.GetFullPath(exe).Equals(Path.GetFullPath(dest),StringComparison.OrdinalIgnoreCase)) File.Copy(exe, dest, true);
            string link = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NXNativeWindows IPA Builder.lnk");
            string script = "$s=(New-Object -ComObject WScript.Shell).CreateShortcut(" + PowerShell.Literal(link) + "); $s.TargetPath=" + PowerShell.Literal(dest) + "; $s.Arguments=" + PowerShell.Literal("--launch --root \""+root+"\"") + "; $s.Save()";
            await new ProcessRunner().RunAsync(PowerShell.Command(script), AppendLog, default);
        }
        catch (Exception e) { AppendLog("Optional shortcut: " + e.Message); }
    }
    private async Task OpenLogs()
    { string path = Path.Combine(RootPath.Text!, "logs"); if (Directory.Exists(path)) Ui.Open(path); else await Ui.Alert(this, "Activity Log", "The large log panel shows live output. Session files appear after the first installation run."); }
    private async Task ShowCredits() => await Ui.Alert(this, "Credits & Licenses", Ui.Creator + "\n" + Ui.Original + "\n" + Ui.Repository + "\n\nBased on NXNative by seedds. Independent community installer. No Apple files are bundled.\n\n" + ReadNotices());
    private static string ReadNotices() { var asm = typeof(MainWindow).Assembly; var name = asm.GetManifestResourceNames().Single(n => n.EndsWith("ThirdPartyNotices.txt")); using var s = asm.GetManifestResourceStream(name)!; using var r = new StreamReader(s); return r.ReadToEnd(); }
    public void Preview(bool longText = false)
    {
        update.Text = "Up to date · main / 269d61ff"; update.Foreground = Ui.Good;
        if (longText) foreach (var (id,row) in Components) row.Update(new(id,row.TitleText.Text!,id=="apple"?StepStatus.ActionRequired:StepStatus.Running,"A longer component message that wraps below its title and stays clear of the separate status badge."));
        for(int i=0;i<32;i++) AppendLog($"[{i:00}:12:40]  {(i%3==0?"[OK]":"[INFO]")}  {(i%2==0?"Checking the latest repository and validating installed tool paths.":"Python environment is ready. Compiler and shader checks are queued.")}");
        FlushLog();
    }
}
