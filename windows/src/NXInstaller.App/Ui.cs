using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace NXInstaller.App;

public sealed class InstallerApplication : Application
{
    public override void Initialize() { RequestedThemeVariant = ThemeVariant.Dark; Styles.Add(new FluentTheme()); }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            int p = Array.IndexOf(Program.Arguments, "--root");
            var window = new MainWindow(p >= 0 && p + 1 < Program.Arguments.Length ? Program.Arguments[p + 1] : null,Program.Arguments.Contains("--smoke-ui"));
            desktop.MainWindow = window;
            if(Program.Arguments.Contains("--smoke-ui"))window.Opened+=async(_,_)=>
            {
                await Task.Delay(500);int i=Array.IndexOf(Program.Arguments,"--snapshot");
                if(i>=0&&i+1<Program.Arguments.Length)
                {
                    using var image=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)(window.ClientSize.Width*window.RenderScaling),(int)(window.ClientSize.Height*window.RenderScaling)),new Vector(96*window.RenderScaling,96*window.RenderScaling));
                    image.Render(window);image.Save(Program.Arguments[i+1]);
                }
                window.Close();
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
public static class Ui
{
    public const string Version = "2.0.2";
    public const string Creator = "Easy Installer Created by iSolution";
    public const string Original = "Original NXNativeWindows by zenvyzenshiro";
    public const string Repository = "https://github.com/zenvyzenshiro/NXNativeWindows";
    public static IBrush Bg = Brush.Parse("#0D111A"), Card = Brush.Parse("#171E2B"), Field = Brush.Parse("#101620"),
        Border = Brush.Parse("#303B51"), Text = Brush.Parse("#F1F4FC"), Muted = Brush.Parse("#9CAEC6"), Accent = Brush.Parse("#8275FF"),
        Cyan = Brush.Parse("#70D8E7"), Good = Brush.Parse("#67DEAD"), Warn = Brush.Parse("#F2C377"), Bad = Brush.Parse("#FF859A");
    public static TextBlock Label(string text, double size = 13, IBrush? brush = null, bool bold = false) => new()
    { Text = text, FontSize = size, Foreground = brush ?? Text, FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap };
    public static Button Button(string text, bool primary = false) => new()
    {
        Content = text, Padding = new Thickness(15, 10), MinHeight = 40, FontSize = 13, FontWeight = FontWeight.SemiBold,
        CornerRadius = new CornerRadius(8), Background = primary ? Accent : Card, Foreground = Text,
        BorderBrush = primary ? Accent : Border, BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Center
    };
    public static Border Box(Control content, double padding = 14) => new()
    { Child = content, Padding = new Thickness(padding), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), BorderBrush = Border, Background = Card };
    public static StackPanel Stack(params Control[] items)
    { var s = new StackPanel { Spacing = 10 }; foreach (var i in items) s.Children.Add(i); return s; }
    public static async Task Alert(Window owner, string title, string message)
    {
        var close = Button("Close", true); var panel = Stack(Label(message));
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(22), RowSpacing = 18 };
        layout.Children.Add(new ScrollViewer { Content = panel }); Grid.SetRow(close, 1); layout.Children.Add(close);
        var w = new Window { Title = title, Width = 600, Height = 360, MinWidth = 400, MinHeight = 240, Background = Bg,
            Content = layout, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        close.Click += (_, _) => w.Close(); await w.ShowDialog(owner);
    }
    public static void Open(string url) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
}
