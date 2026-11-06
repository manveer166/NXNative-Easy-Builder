using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace NXNativeEasyBuilder;

public sealed class MainWindow : Window
{
    private static readonly IBrush Page = Brush.Parse("#10131C");
    private static readonly IBrush Panel = Brush.Parse("#191E2A");
    private static readonly IBrush Edge = Brush.Parse("#2C3546");
    private static readonly IBrush Ink = Brush.Parse("#F1F3FA");
    private static readonly IBrush Muted = Brush.Parse("#A9B5CE");
    private static readonly IBrush Aqua = Brush.Parse("#78E7D2");
    private static readonly IBrush Purple = Brush.Parse("#7464F3");
    private static readonly IBrush Warning = Brush.Parse("#FFC29A");

    private readonly string root;
    private readonly Repository repo;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(45) };
    private readonly ConcurrentQueue<string> logQueue = new();
    private readonly DispatcherTimer logTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly List<Control> actions = [];
    private readonly TextBox game = PathBox("Choose a game folder or .nup file");
    private readonly TextBox keys = PathBox("Required for NSP, XCI, NSZ and XCZ");
    private readonly TextBox output = PathBox("Same folder as the game");
    private readonly CheckBox prebuild = new() { Content = "Prebuild optional GPU pipelines", IsChecked = false };
    private readonly CheckBox follow = new() { Content = "Follow log", IsChecked = true };
    private readonly TextBlock repoStatus = Label("Checking GitHub…", 13, Aqua);
    private readonly TextBlock status = Label("Ready when you are", 14, Ink, FontWeight.SemiBold);
    private readonly ProgressBar progress = new() { Height = 6, Minimum = 0, Maximum = 100, Foreground = Aqua };
    private readonly TextBox log = new()
    {
        Name = "ActivityLog", IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Menlo, monospace"), FontSize = 12.5, Foreground = Ink,
        Background = Brush.Parse("#0D1018"), BorderBrush = Edge, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(9), Padding = new Thickness(12)
    };
    private readonly Button stop = Button("Stop");
    private readonly Button reveal = Button("Reveal Output");
    private readonly Button getXcode = Button("Get Xcode");
    private string expectedXcode = "27.2";
    private string? artifact;
    private CancellationTokenSource? operation;
    private bool closeAfterStop;

    public MainWindow(bool preview = false, string? dataRoot = null)
    {
        root = dataRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library/Application Support/iSolution/NXNative Easy Builder");
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NXNativeEasyBuilder-iSolution/1.0.0");
        repo = new Repository(root, http);
        Title = "NXNative Easy Builder · iSolution";
        Width = 1040;
        Height = 800;
        MinWidth = 640;
        MinHeight = 540;
        Background = Page;
        Foreground = Ink;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        using (var icon = AssetLoader.Open(new Uri("avares://NXNativeEasyBuilder/Assets/App.png")))
            Icon = new WindowIcon(icon);

        var main = new Grid { Margin = new Thickness(22, 16), RowDefinitions = new RowDefinitions("Auto,11,5*,7,4*"), };
        main.RowDefinitions[2].MinHeight = 188;
        main.RowDefinitions[4].MinHeight = 148;
        Place(main, Header(), 0);
        var forms = new StackPanel { Spacing = 10 };
        forms.Children.Add(SetupCard());
        forms.Children.Add(GameCard());
        Place(main, new ScrollViewer { Content = forms, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 2);
        Place(main, new GridSplitter
        {
            Height = 7, Background = Page, HorizontalAlignment = HorizontalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Rows, ResizeBehavior = GridResizeBehavior.PreviousAndNext
        }, 3);
        Place(main, new Border
        {
            Width = 42, Height = 3, CornerRadius = new CornerRadius(2), Background = Edge,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        }, 3);
        Place(main, LogCard(), 4);
        Content = main;

        logTimer.Tick += (_, _) => FlushLog();
        Closing += (_, e) =>
        {
            if (operation == null) return;
            e.Cancel = true;
            closeAfterStop = true;
            operation.Cancel();
        };
        Closed += (_, _) => { logTimer.Stop(); http.Dispose(); };
        if (preview) return;
        Opened += async (_, _) =>
        {
            LoadSettings();
            logTimer.Start();
            Log("NXNative Easy Builder 1.0.0 · Created by iSolution · NXNative by seedds");
            await CheckUpdatesAsync();
        };
    }

    private Grid Header()
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 13 };
        using var icon = AssetLoader.Open(new Uri("avares://NXNativeEasyBuilder/Assets/App.png"));
        header.Children.Add(new Image { Source = new Avalonia.Media.Imaging.Bitmap(icon), Width = 52, Height = 52 });
        var title = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(Label("NXNative Easy Builder", 25, Ink, FontWeight.Bold));
        title.Children.Add(Label("Created by iSolution  ·  Original NXNative by seedds", 12, Muted));
        Grid.SetColumn(title, 1);
        header.Children.Add(title);
        var badge = new Border
        {
            Background = Panel, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(11, 6), VerticalAlignment = VerticalAlignment.Center,
            Child = Label("APPLE SILICON", 11, Aqua, FontWeight.SemiBold)
        };
        Grid.SetColumn(badge, 2);
        header.Children.Add(badge);
        return header;
    }

    private Border SetupCard()
    {
        var card = new StackPanel { Spacing = 8 };
        card.Children.Add(Label("01   Toolchain", 17, Ink, FontWeight.Bold));
        card.Children.Add(repoStatus);
        var install = ActionButton("Install Everything", InstallAsync, primary: true);
        var check = ActionButton("Check Updates", CheckUpdatesAsync);
        getXcode.Click += (_, _) => Open($"https://developer.apple.com/download/all/?q=Xcode%20{expectedXcode}");
        actions.Add(getXcode);
        var brew = ActionButton("Set Up Homebrew", BrewSetupAsync);
        card.Children.Add(Wrap(install, check, getXcode, brew));
        return Card(card);
    }

    private Border GameCard()
    {
        var card = new StackPanel { Spacing = 7 };
        card.Children.Add(Label("02   Build a game", 17, Ink, FontWeight.Bold));
        card.Children.Add(PathRow("Game", game, ActionButton("Folder", () => PickFolderAsync(game, "Select the folder for one game")),
            ActionButton(".nup File", () => PickFileAsync(game, "Select a .nup game", new FilePickerFileType("NXNative NUP") { Patterns = ["*.nup"] }))));
        card.Children.Add(PathRow("prod.keys", keys, ActionButton("Browse", () => PickFileAsync(keys, "Select your prod.keys file", FilePickerFileTypes.All)),
            ActionButton("Clear", () => ClearAsync(keys))));
        card.Children.Add(PathRow("Output", output, ActionButton("Change", () => PickFolderAsync(output, "Select the output folder")),
            ActionButton("Reset", () => ClearAsync(output))));
        ToolTip.SetTip(prebuild, "Prebuild GPU pipelines during the build. With this off, the game creates the pipelines on first launch.");
        prebuild.IsCheckedChanged += (_, _) => SaveSettings();
        actions.Add(prebuild);
        card.Children.Add(prebuild);
        stop.IsEnabled = false;
        stop.Click += (_, _) => { status.Text = "Stopping…"; operation?.Cancel(); };
        reveal.IsEnabled = false;
        reveal.Click += (_, _) => { if (artifact != null) Open(artifact, revealInFinder: true); };
        card.Children.Add(Wrap(ActionButton("Build IPA", () => BuildAsync(true), primary: true),
            ActionButton("Build Mac App", () => BuildAsync(false), primary: true), stop, reveal));
        return Card(card);
    }

    private Border LogCard()
    {
        var card = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), RowSpacing = 7 };
        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 9 };
        var label = Label("Activity Log", 16, Ink, FontWeight.Bold);
        label.VerticalAlignment = VerticalAlignment.Center;
        toolbar.Children.Add(label);
        Grid.SetColumn(follow, 1);
        toolbar.Children.Add(follow);
        var open = Button("Open Logs");
        open.Click += (_, _) =>
        {
            var folder = Path.Combine(root, "Logs");
            try { Directory.CreateDirectory(folder); Open(folder); }
            catch (Exception error) { Fail(error); }
        };
        Grid.SetColumn(open, 2);
        toolbar.Children.Add(open);
        Place(card, toolbar, 0);
        status.MaxLines = 2;
        status.TextTrimming = TextTrimming.CharacterEllipsis;
        Place(card, status, 1);
        Place(card, progress, 2);
        ScrollViewer.SetVerticalScrollBarVisibility(log, ScrollBarVisibility.Auto);
        Place(card, log, 3);
        return Card(card);
    }

    private static Grid PathRow(string label, TextBox field, Button first, Button second)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("68,*,Auto,Auto"), ColumnSpacing = 8 };
        var text = Label(label, 13, Muted, FontWeight.SemiBold);
        text.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(text);
        foreach (var (child, column) in new (Control, int)[] { (field, 1), (first, 2), (second, 3) })
        {
            Grid.SetColumn(child, column);
            row.Children.Add(child);
        }
        return row;
    }

    private static WrapPanel Wrap(params Control[] controls)
    {
        var row = new WrapPanel();
        foreach (var control in controls)
        {
            control.Margin = new Thickness(0, 0, 7, 4);
            row.Children.Add(control);
        }
        return row;
    }

    private static Border Card(Control child) => new()
    {
        Background = Panel, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13),
        Padding = new Thickness(13, 10), Child = child
    };

    private static void Place(Grid grid, Control child, int row)
    {
        Grid.SetRow(child, row);
        grid.Children.Add(child);
    }

    private static TextBox PathBox(string watermark) => new()
    {
        Watermark = watermark, IsReadOnly = true, FontSize = 13, Padding = new Thickness(8, 4),
        Background = Page, BorderBrush = Edge, Foreground = Ink, MinHeight = 32
    };

    private static TextBlock Label(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal) => new()
    {
        Text = text, FontSize = size, Foreground = brush, FontWeight = weight, TextWrapping = TextWrapping.Wrap
    };

    private static Button Button(string label, bool primary = false) => new()
    {
        Content = label, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Ink, MinHeight = 32,
        Padding = new Thickness(11, 5), CornerRadius = new CornerRadius(8), Background = primary ? Purple : Page,
        BorderBrush = primary ? Purple : Edge, BorderThickness = new Thickness(1)
    };

    private Button ActionButton(string label, Func<Task> action, bool primary = false)
    {
        var button = Button(label, primary);
        button.Click += async (_, _) => { try { await action(); } catch (Exception error) { Fail(error); } };
        actions.Add(button);
        return button;
    }

    private async Task PickFolderAsync(TextBox field, string title)
    {
        var selection = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title });
        if (selection.FirstOrDefault()?.TryGetLocalPath() is { } path) { field.Text = path; SaveSettings(); }
    }

    private async Task PickFileAsync(TextBox field, string title, FilePickerFileType type)
    {
        var selection = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, FileTypeFilter = [type] });
        if (selection.FirstOrDefault()?.TryGetLocalPath() is { } path) { field.Text = path; SaveSettings(); }
    }

    private Task ClearAsync(TextBox field)
    {
        field.Text = "";
        SaveSettings();
        return Task.CompletedTask;
    }

    private async Task CheckUpdatesAsync() => await RunAsync("Checking GitHub", exclusive: false, async (ct, write) =>
    {
        var active = repo.Active();
        CommitInfo latest;
        try { latest = await repo.LatestAsync(ct); }
        catch
        {
            Dispatch(() =>
            {
                if (active != null) SetXcode(active.XcodeVersion);
                repoStatus.Text = active == null ? "GitHub check unavailable" : $"Active · {active.ShortCommit} · GitHub check unavailable";
            });
            throw;
        }
        write($"GitHub main {latest.Sha} · {latest.Subject}");
        Dispatch(() =>
        {
            if (active != null) SetXcode(active.XcodeVersion);
            repoStatus.Text = active?.Commit == latest.Sha ? $"Current · {active.ShortCommit}" :
                active == null ? $"Available on GitHub · {latest.Sha[..8]}" : $"Update available · {active.ShortCommit} → {latest.Sha[..8]}";
        });
        return active == null ? "Click Install Everything to set up the toolchain." : "Choose a game to build, or run Install Everything to update and repair.";
    });

    private async Task InstallAsync() => await RunAsync("Checking this Mac and GitHub", exclusive: true, async (ct, write) =>
    {
        MacSetup.CheckHost();
        var active = repo.Active();
        CommitInfo latest;
        try { latest = await repo.LatestAsync(ct); }
        catch (Exception error) when (active != null && error is HttpRequestException or TimeoutException)
        {
            write($"GitHub check unavailable. Repairing the active commit {active.ShortCommit}. {error.Message}");
            latest = new CommitInfo(active.Commit, "Offline repair");
        }
        write($"Selected {latest.Sha} · {latest.Subject}");
        var free = new DriveInfo(root).AvailableFreeSpace;
        write($"Available space for toolchain and repository · {free / 1073741824.0:N1} GiB");
        if (free < 2L * 1024 * 1024 * 1024 && active?.Commit != latest.Sha)
            throw new IOException("Free at least 2 GiB on the installation volume before downloading a new checkout.");
        ShowProgress("Preparing repository", null);
        var release = await repo.StageAsync(latest, write, fraction => ShowProgress("Preparing repository", fraction), ct);
        Dispatch(() => SetXcode(release.XcodeVersion));
        var setup = new MacSetup(root, write, ct);
        var ipa = await setup.InstallAsync(release, stage => { write(stage); ShowProgress(stage, null); });
        ct.ThrowIfCancellationRequested();
        repo.Activate(release);
        Dispatch(() => repoStatus.Text = $"Active · {release.ShortCommit} · Xcode {release.XcodeVersion}");
        return ipa ? "Setup complete. Build IPA and Build Mac App are ready." : "Mac App is ready. Install the iOS platform in Xcode for IPA builds.";
    });

    private async Task BuildAsync(bool ipa)
    {
        var request = new BuildRequest(game.Text ?? "", keys.Text, output.Text, ipa, prebuild.IsChecked == true);
        await RunAsync(ipa ? "Preparing IPA build" : "Preparing Mac app build", exclusive: true, async (ct, write) =>
        {
            MacSetup.CheckHost();
            var release = repo.Active() ?? throw new IOException("Click Install Everything and finish setup first.");
            var args = request.Arguments(release);
            write($"Game · {request.Game}");
            write($"Target · {(ipa ? "iOS IPA" : "macOS app")} · NXNative {release.ShortCommit}");
            var tools = await new MacSetup(root, write, ct).ResolveAsync(release, ipa);
            var events = new BuildProgress(write, ShowProgress);
            var path = await GameBuild.RunAsync(request, release, tools, args, events, ct);
            Dispatch(() => { artifact = path; reveal.IsEnabled = true; });
            return $"Build complete · {Path.GetFileName(path)}";
        });
    }

    private async Task RunAsync(string initial, bool exclusive, Func<CancellationToken, Action<string>, Task<string>> action)
    {
        if (operation != null) return;
        using var stopSource = new CancellationTokenSource();
        operation = stopSource;
        SetBusy(true);
        status.Foreground = Ink;
        status.Text = initial;
        progress.IsIndeterminate = true;
        try
        {
            Directory.CreateDirectory(root);
            using var useLock = exclusive ? new FileStream(Path.Combine(root, "operation.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None) : null;
            var folder = Path.Combine(root, "Logs");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");
            using var writer = TextWriter.Synchronized(new StreamWriter(file) { AutoFlush = true });
            void Write(string message)
            {
                var line = $"{DateTime.Now:HH:mm:ss}  {message}";
                writer.WriteLine(line);
                logQueue.Enqueue(line);
            }
            Write(initial);
            try
            {
                var result = await Task.Run(() => action(stopSource.Token, Write));
                Write(result);
                status.Text = result;
                status.Foreground = Aqua;
                progress.Value = 100;
            }
            catch (OperationCanceledException) when (stopSource.IsCancellationRequested)
            {
                status.Text = "Stopped. You can retry with the same game folder.";
                Write(status.Text);
                progress.Value = 0;
            }
            catch (Exception error)
            {
                Write(error.ToString());
                status.Text = error.Message;
                status.Foreground = Warning;
                progress.Value = 0;
            }
        }
        catch (Exception error) { Fail(error); }
        finally
        {
            progress.IsIndeterminate = false;
            operation = null;
            SetBusy(false);
            FlushLog();
            if (closeAfterStop) Close();
        }
    }

    private Task BrewSetupAsync()
    {
        MacSetup.CheckHost();
        if (File.Exists(MacSetup.Brew)) { status.Text = "Apple Silicon Homebrew is installed."; return Task.CompletedTask; }
        var folder = Path.Combine(root, "Runtime");
        Directory.CreateDirectory(folder);
        var script = Path.Combine(folder, "Install Homebrew.command");
        File.WriteAllText(script, """
            #!/bin/bash
            set -e
            installer=$(/usr/bin/mktemp -t nxnative-homebrew)
            trap '/bin/rm -f "$installer"' EXIT
            /usr/bin/curl --fail --location --silent --show-error https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh --output "$installer"
            /bin/bash "$installer"
            """ + "\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Open(script, terminal: true);
        status.Text = "Finish Homebrew setup in Terminal, then click Install Everything.";
        return Task.CompletedTask;
    }

    private void Open(string target, bool terminal = false, bool revealInFinder = false)
    {
        try
        {
            var start = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
            if (terminal) { start.ArgumentList.Add("-a"); start.ArgumentList.Add("Terminal"); }
            if (revealInFinder) start.ArgumentList.Add("-R");
            start.ArgumentList.Add(target);
            Process.Start(start)?.Dispose();
        }
        catch (Exception error) { Fail(error); }
    }

    private void SetXcode(string version)
    {
        expectedXcode = version;
        getXcode.Content = $"Get Xcode {version}";
    }

    private void SetBusy(bool busy)
    {
        foreach (var control in actions) control.IsEnabled = !busy;
        stop.IsEnabled = busy;
    }

    private void ShowProgress(string label, double? fraction) => Dispatch(() =>
    {
        status.Text = label;
        progress.IsIndeterminate = !fraction.HasValue;
        if (fraction.HasValue) progress.Value = 100 * Math.Clamp(fraction.Value, 0, 1);
    });

    private static void Dispatch(Action action) => Dispatcher.UIThread.Post(action);

    private void Fail(Exception error)
    {
        status.Text = error.Message;
        status.Foreground = Warning;
        Log(error.Message);
    }

    private void Log(string message) => logQueue.Enqueue($"{DateTime.Now:HH:mm:ss}  {message}");

    private void FlushLog()
    {
        var lines = new List<string>();
        while (lines.Count < 400 && logQueue.TryDequeue(out var line)) lines.Add(line);
        if (lines.Count == 0) return;
        var text = (log.Text ?? "") + string.Join('\n', lines) + "\n";
        log.Text = text.Length > 180_000 ? text[^150_000..] : text;
        if (follow.IsChecked == true) log.CaretIndex = log.Text.Length;
    }

    private sealed record Settings(string? Game, string? Keys, string? Output, bool Prebuild);
    private string SettingsFile => Path.Combine(root, "settings.json");
    private bool loadingSettings;

    private void LoadSettings()
    {
        if (!File.Exists(SettingsFile)) return;
        loadingSettings = true;
        try
        {
            var saved = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsFile));
            if (saved == null) return;
            game.Text = saved.Game;
            keys.Text = saved.Keys;
            output.Text = saved.Output;
            prebuild.IsChecked = saved.Prebuild;
        }
        catch (Exception error) { Log($"Saved selections could not be loaded. {error.Message}"); }
        finally { loadingSettings = false; }
    }

    private void SaveSettings()
    {
        if (loadingSettings) return;
        var temporary = SettingsFile + "." + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Settings(game.Text, keys.Text, output.Text, prebuild.IsChecked == true)));
            File.Move(temporary, SettingsFile, overwrite: true);
        }
        catch (Exception error) { Log($"Could not save selections. {error.Message}"); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void Preview()
    {
        game.Text = "/Volumes/Build Drive/Games/Super Mario Odyssey";
        keys.Text = "/Users/Example/Keys/prod.keys";
        output.Text = "/Users/Example/Downloads/NXNative Builds";
        repoStatus.Text = "Active · 6d928fd1 · Xcode 27.2";
        status.Text = "iOS shaders · Metal compilation · 12,582/27,210 shaders";
        progress.Value = 56;
        for (var i = 0; i < 34; i++) Log($"Metal shader compilation · checked {i * 370:N0} cached outputs for the selected build.");
        FlushLog();
    }
}
