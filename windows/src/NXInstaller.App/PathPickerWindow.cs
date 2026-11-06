using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NXInstaller.Core;

namespace NXInstaller.App;

// Managed controls throughout: opening Browse never loads Windows Explorer shell extensions or COM file dialogs.
public sealed class PathPickerWindow : Window
{
    public readonly TextBox Location = new() { MinHeight = 38, FontSize = 12, Background = Ui.Field };
    public readonly TextBox SelectionPath = new() { MinHeight = 38, FontSize = 12, Background = Ui.Field };
    public readonly TextBlock FileName = Ui.Label("No file selected", 12, Ui.Muted);
    public readonly ListBox Entries = new() { Background = Ui.Field, FontSize = 13 };
    public readonly TextBlock Message = Ui.Label("", 12, Ui.Muted);
    public readonly Button Confirm;
    public readonly Button CancelButton = Ui.Button("Cancel");
    public Grid Layout { get; }
    public string? SelectedPath { get; private set; }
    public Task Loading { get; private set; } = Task.CompletedTask;
    private readonly bool xipOnly;
    private readonly string initial;
    private CancellationTokenSource? navigation;
    private bool closed;
    public PathPickerWindow(string? path, bool xipOnly = false)
    {
        this.xipOnly = xipOnly; initial = PathBrowser.InitialDirectory(path);
        Title = xipOnly ? "Open Xcode .xip file" : "Choose installation folder";
        Width = 760; Height = 600; MinWidth = 440; MinHeight = 440; Background = Ui.Bg; Foreground = Ui.Text;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Location.Text = initial;
        SelectionPath.Text = xipOnly ? "" : string.IsNullOrWhiteSpace(path) ? initial : path;
        Confirm = Ui.Button(xipOnly ? "Open" : "Use this folder", true); Confirm.IsDefault = true; Confirm.IsEnabled = !xipOnly;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"), RowSpacing = 12, Margin = new Thickness(20) }; Layout = root;
        root.Children.Add(Ui.Stack(Ui.Label(Title, 20, bold: true), Ui.Label(xipOnly ? "Select one .xip file, then click Open. Double click a file to open it directly." : "Open a folder with a double click, or paste a path below.", 12, Ui.Muted)));
        var toolbar = new StackPanel { Spacing = 8 };
        var pathRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8 };
        var go = Ui.Button("Go"); var up = Ui.Button("Up"); pathRow.Children.Add(Location); Grid.SetColumn(go, 1); pathRow.Children.Add(go); Grid.SetColumn(up, 2); pathRow.Children.Add(up); toolbar.Children.Add(pathRow);
        var shortcuts = new WrapPanel { Orientation = Orientation.Horizontal };
        void Shortcut(string label, string destination)
        {
            if (string.IsNullOrWhiteSpace(destination)) return;
            var button = Ui.Button(label); button.Margin = new Thickness(0, 0, 6, 4);
            button.Padding = new Thickness(10, 6); button.MinHeight = 32;
            button.Click += (_, _) => Loading = NavigateAsync(destination); shortcuts.Children.Add(button);
        }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Shortcut("Home", home); Shortcut("Downloads", Path.Combine(home, "Downloads"));
        foreach (var drive in DriveInfo.GetDrives().Where(d => OperatingSystem.IsWindows() || d.Name == Path.GetPathRoot(initial)).Take(16)) Shortcut(drive.Name, drive.Name);
        toolbar.Children.Add(shortcuts); Grid.SetRow(toolbar, 1); root.Children.Add(toolbar);
        ScrollViewer.SetHorizontalScrollBarVisibility(Entries, ScrollBarVisibility.Auto);
        var listing = Ui.Box(Entries, 0); Grid.SetRow(listing, 2); root.Children.Add(listing);
        SelectionPath.IsVisible = !xipOnly; FileName.IsVisible = xipOnly;
        FileName.TextWrapping = TextWrapping.NoWrap; FileName.TextTrimming = TextTrimming.CharacterEllipsis;
        var selection = Ui.Stack(Ui.Label(xipOnly ? "File name" : "Installation path (a new folder is allowed)", 12, bold: true), SelectionPath, FileName, Message);
        Grid.SetRow(selection, 3); root.Children.Add(selection);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        footer.Children.Add(CancelButton); Confirm.HorizontalAlignment = HorizontalAlignment.Stretch; Grid.SetColumn(Confirm, 1); footer.Children.Add(Confirm); Grid.SetRow(footer, 4); root.Children.Add(footer);
        root.Height = xipOnly ? 380 : 560;
        Content = new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        SizeChanged += (_, _) => root.Height = Math.Max(xipOnly ? 380 : 560, ClientSize.Height - 40);
        go.Click += (_, _) => Loading = NavigateAsync(Location.Text ?? "");
        Location.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Loading = NavigateAsync(Location.Text ?? ""); } };
        up.Click += (_, _) =>
        {
            try { string value = PathBrowser.FullPath(Location.Text ?? ""); Loading = NavigateAsync(Path.GetDirectoryName(value) ?? value); }
            catch (Exception e) { Message.Text = e.Message; Message.Foreground = Ui.Bad; }
        };
        Entries.SelectionChanged += (_, _) =>
        {
            if (xipOnly)
            {
                bool file = Entries.SelectedItem is BrowserEntry { IsDirectory: false };
                FileName.Text = file ? ((BrowserEntry)Entries.SelectedItem!).Name : "No file selected";
                Confirm.IsEnabled = file;
                Message.Text = file ? "Ready to open the selected Xcode .xip file." : "Select a .xip file, then click Open.";
                Message.Foreground = file ? Ui.Good : Ui.Muted;
                ToolTip.SetTip(FileName, file ? ((BrowserEntry)Entries.SelectedItem!).Path : null);
            }
            else if (Entries.SelectedItem is BrowserEntry entry) SelectionPath.Text = entry.Path;
        };
        Entries.DoubleTapped += (_, _) =>
        { if (Entries.SelectedItem is BrowserEntry entry) { if (entry.IsDirectory) Loading = NavigateAsync(entry.Path); else ConfirmSelection(); } };
        Confirm.Click += (_, _) => ConfirmSelection(); CancelButton.Click += (_, _) => Close((string?)null);
        Opened += (_, _) =>
        {
            if ((Screens.ScreenFromWindow(this) ?? Screens.Primary) is {} screen)
            { Width = Math.Min(760, screen.WorkingArea.Width / RenderScaling - 24); Height = Math.Min(600, screen.WorkingArea.Height / RenderScaling - 32); }
            Loading = NavigateAsync(initial, keepSelection: true);
        };
        Closed += (_, _) => { closed = true; navigation?.Cancel(); };
    }
    public async Task NavigateAsync(string text, bool keepSelection = false)
    {
        navigation?.Cancel(); var current = new CancellationTokenSource(); navigation = current;
        Message.Text = "Loading local folders…"; Message.Foreground = Ui.Muted;
        try
        {
            var folder = PathBrowser.FullPath(text); string? selectedFile = null;
            if (xipOnly && File.Exists(folder))
            {
                if (!folder.EndsWith(".xip", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose a completed .xip file.");
                selectedFile = folder; folder = Path.GetDirectoryName(folder)!;
            }
            var items = await PathBrowser.ReadAsync(folder, xipOnly, current.Token);
            if (closed || current.IsCancellationRequested) return;
            Entries.ItemsSource = items; Location.Text = folder;
            if (!keepSelection) SelectionPath.Text = xipOnly ? selectedFile ?? "" : folder;
            if (selectedFile != null) { Entries.SelectedItem = items.FirstOrDefault(x => x.Path == selectedFile); SelectionPath.Text = selectedFile; }
            Message.Text = items.Count == 0 ? (xipOnly ? "No completed .xip here. Open another folder or paste its full path." : "This folder is empty. You can use it for installation.") : $"{items.Count:N0} item(s) · double click a folder to open it";
            Message.Foreground = Ui.Muted;
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested) { }
        catch (Exception e) { if (!closed && !current.IsCancellationRequested) { Message.Text = "Cannot open this path: " + e.Message; Message.Foreground = Ui.Bad; } }
        finally { if (ReferenceEquals(navigation, current)) navigation = null; current.Dispose(); }
    }
    public void ConfirmSelection()
    {
        try
        {
            string path;
            if (xipOnly)
            {
                if (Entries.SelectedItem is not BrowserEntry { IsDirectory: false } file) throw new ArgumentException("Select a .xip file in the file list.");
                path = PathBrowser.FullPath(file.Path);
                if (!path.EndsWith(".xip", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) throw new ArgumentException("Choose an existing, completed Xcode .xip file.");
            }
            else
            { path = StateStore.ValidateRoot(PathBrowser.FullPath(SelectionPath.Text ?? "")); if (File.Exists(path)) throw new ArgumentException("The installation path must be a folder, not a file."); }
            SelectedPath = path; Close(path);
        }
        catch (Exception e) { Message.Text = e.Message; Message.Foreground = Ui.Bad; }
    }
    public static Task<string?> PickAsync(Window owner, string? path, bool xipOnly = false) => new PathPickerWindow(path, xipOnly).ShowDialog<string?>(owner);
}
