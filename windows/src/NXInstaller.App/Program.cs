using Avalonia;
using NXInstaller.Core;

namespace NXInstaller.App;

public static class Program
{
    public static string[] Arguments { get; private set; } = [];
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<InstallerApplication>().UsePlatformDetect().WithInterFont();
    [STAThread]
    public static int Main(string[] args)
    {
        Arguments = args;
        string? Value(string name) { int p = Array.IndexOf(args, name); return p >= 0 && p + 1 < args.Length ? args[p + 1] : null; }
        string? root = Value("--root");
        if (args.Contains("--verify") && root != null)
        {
            var state = StateStore.Load(root);
            var checks = new HealthVerifier(new ProcessRunner(), _ => { }).VerifyAsync(root, state, true, default).GetAwaiter().GetResult();
            state.Ready = checks.All(x => !x.Required || x.Ok); StateStore.Save(root, state); InstallerEngine.SaveReport(root, state, checks); return state.Ready ? 0 : 1;
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnMainWindowClose);
    }
}
