namespace NXInstaller.Core;

public sealed class SdkExtractor(string root, ICommandRunner runner, Action<string> log)
{
    public const string CompletionFile = ".nxw-sdk-complete.json";
    public static bool IsStagingDirectory(string folder) => Path.GetFileName(folder).StartsWith("extract-", StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(folder).StartsWith(".extract-", StringComparison.OrdinalIgnoreCase);
    public static bool UsableSdk(string? folder, string installRoot)
    {
        if (!AppleToolsAutomation.ValidSdkRoot(folder)) return false;
        var sdkBase = Path.GetFullPath(Path.Combine(installRoot, "sdk")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var current = Path.GetFullPath(folder!);
        if (!current.StartsWith(sdkBase, StringComparison.OrdinalIgnoreCase)) return true;
        for (var parent = Directory.GetParent(current); parent != null && parent.FullName.StartsWith(sdkBase, StringComparison.OrdinalIgnoreCase); parent = parent.Parent)
            if (IsStagingDirectory(parent.FullName)) return false;
        return true;
    }
    public static string InstallHelper(string root)
    {
        var folder = Path.Combine(root, "runtime", "helpers"); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "sdk_extract_windows.py");
        using var resource = typeof(SdkExtractor).Assembly.GetManifestResourceStream("NXInstaller.Core.Resources.sdk_extract_windows.py")!;
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = File.Create(temporary)) resource.CopyTo(file);
            File.Move(temporary, path, true); return path;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public async Task<string> ExtractAsync(string python, string repository, AppleSelection selection, CancellationToken ct)
    {
        if (!selection.CanUseSdk) throw new InvalidOperationException("Applicable Apple SDK authorization is required before extraction.");
        if (!File.Exists(selection.XcodeXip)) throw new FileNotFoundException("Choose an existing, completed Xcode .xip file.", selection.XcodeXip);
        string stage = Path.Combine(root, "sdk", ".extract-" + Guid.NewGuid().ToString("N")[..8]);
        string target = Path.Combine(root, "sdk", "iPhoneOS.sdk");
        var helper = InstallHelper(root);
        try
        {
            log("Extracting Xcode XIP with long-path support; SDK staging: " + stage);
            Directory.CreateDirectory(stage);
            var result = await runner.RunAsync(new(python, [helper, Path.Combine(repository, "win", "sdk", "extract_xip.py"), selection.XcodeXip, stage], root,
                new Dictionary<string,string> { ["PYTHONUTF8"] = "1", ["PYTHONUNBUFFERED"] = "1", ["PYTHONHOME"] = "", ["PYTHONPATH"] = "" }, TimeSpan.FromHours(2)), log, ct);
            if (result.ExitCode != 0) throw new IOException("SDK extraction did not complete (exit " + result.ExitCode + "). The incomplete SDK was not activated. See the preceding extraction error, then run Install / Repair again.");
            ct.ThrowIfCancellationRequested();
            string sdk = Directory.GetDirectories(stage, "iPhoneOS*.sdk").OrderBy(p => Path.GetFileName(p) == "iPhoneOS.sdk" ? 0 : 1).FirstOrDefault()
                ?? throw new IOException("The XIP did not contain an iPhoneOS SDK.");
            if (!AppleToolsAutomation.ValidSdkRoot(sdk)) throw new IOException("Extracted SDK is incomplete: iOS 26+, SDKSettings.json and Foundation/UIKit headers are required.");
            StateStore.AtomicJson(Path.Combine(sdk, CompletionFile), new { InstallerVersion = typeof(SdkExtractor).Assembly.GetName().Version?.ToString(3), CompletedAt = DateTimeOffset.UtcNow, SourceXip = Path.GetFileName(selection.XcodeXip) });
            string? backup = null;
            if (Directory.Exists(target))
            { backup = target + ".backup-" + Guid.NewGuid().ToString("N")[..8]; Directory.Move(target, backup); }
            try { Directory.Move(sdk, target); }
            catch { if (backup != null) Directory.Move(backup, target); throw; }
            if (backup != null) log("Previous SDK retained at " + backup);
            log("SDK extracted and validated: " + target); return target;
        }
        finally
        {
            if (Directory.Exists(stage))
                try { Directory.Delete(stage, true); }
                catch (IOException e) { log("Staging cleanup deferred; this incomplete folder will not be reused: " + e.Message); }
                catch (UnauthorizedAccessException e) { log("Staging cleanup deferred; this incomplete folder will not be reused: " + e.Message); }
        }
    }
}
