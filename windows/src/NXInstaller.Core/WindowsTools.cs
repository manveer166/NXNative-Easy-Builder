using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NXInstaller.Core;

public static class ToolLocator
{
    public static void RefreshPath()
    {
        if (!OperatingSystem.IsWindows()) return;
        var parts = new[] { Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine),
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User), Environment.GetEnvironmentVariable("PATH") };
        Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator, parts.Where(x => !string.IsNullOrEmpty(x))));
        foreach (var key in new[] { "VULKAN_SDK" })
            Environment.SetEnvironmentVariable(key, Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User) ?? Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Machine));
    }
    public static string? Find(string file, params string[] folders)
    {
        var paths = folders.Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        foreach (var folder in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var p = Path.GetFullPath(Path.Combine(Environment.ExpandEnvironmentVariables(folder.Trim('"')), file));
                if (File.Exists(p) && !p.Contains(@"Microsoft\WindowsApps\python", StringComparison.OrdinalIgnoreCase)) return p;
            }
            catch (Exception e) when (e is ArgumentException or IOException or NotSupportedException) { }
        }
        return null;
    }
    public static string ProgramFiles => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    public static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string? Python() => Find("python.exe", Path.Combine(Local, @"Programs\Python\Python313"), Path.Combine(ProgramFiles, "Python313"));
    public static string? DotNet() => Find("dotnet.exe", Path.Combine(ProgramFiles, @"dotnet\x64"), Path.Combine(ProgramFiles, "dotnet"));
    public static string? Clang() => Find("clang.exe", Path.Combine(ProgramFiles, @"LLVM\bin"));
    public static string? VulkanBin()
    {
        var root = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "VulkanSDK");
        var folders = Directory.Exists(root) ? Directory.GetDirectories(root).OrderDescending().Select(p => Path.Combine(p, "Bin")) : [];
        return Find("spirv-cross.exe", new[] { Path.Combine(Environment.GetEnvironmentVariable("VULKAN_SDK") ?? "not-installed", "Bin") }.Concat(folders).ToArray()) is { } file ? Path.GetDirectoryName(file) : null;
    }
    public static string? Zstd()
    {
        var p = Find("zstd.exe", Path.Combine(Local, @"Microsoft\WinGet\Links"));
        if (p != null) return p;
        var baseDir = Path.Combine(Local, @"Microsoft\WinGet\Packages");
        if (!Directory.Exists(baseDir)) return null;
        return Directory.GetDirectories(baseDir, "Meta.Zstandard*").SelectMany(d => Directory.EnumerateFiles(d, "zstd.exe", SearchOption.AllDirectories)).FirstOrDefault();
    }
    public static string? MetalRoot()
    {
        foreach (var root in new[] { ProgramFiles, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }.Where(Directory.Exists))
        { var hit = Directory.GetDirectories(root, "Metal Developer Tools*").OrderDescending().FirstOrDefault(); if (hit != null) return hit; }
        return null;
    }
    public static string? MetalExe(string root, string target, string name)
    {
        if (!Directory.Exists(root)) return null;
        var unified = Path.Combine(root, "bin", name + ".exe");
        if (File.Exists(unified)) return unified;
        var direct = Path.Combine(root, target, "bin", name + ".exe");
        if (File.Exists(direct)) return direct;
        return Directory.EnumerateFiles(root, name + ".exe", SearchOption.AllDirectories)
            .FirstOrDefault(p => p.Replace('\\', '/').Contains("/" + target + "/", StringComparison.OrdinalIgnoreCase));
    }
    public static bool VcRuntimePresent()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
        return key?.GetValue("Installed") is int installed && installed == 1;
    }
}

public sealed record ToolPackage(string Id, string Title, string Description);
public static class PackagePlan
{
    // Use one consistent x64 pipeline on both hosts. ARM64 Windows 11 must execute the same probes via x64 emulation.
    public static readonly ToolPackage[] Packages = [
        new("Microsoft.VCRedist.2015+.x64", "VC++ runtime", "Native runtime used by the compilers and Python modules"),
        new("Python.Python.3.13", "Python 3.13", "Includes Tcl/Tk for the upstream IPA Builder GUI"),
        new("LLVM.LLVM", "LLVM / Clang", "Clang 20+ and the ld64.lld Mach-O linker"),
        new("Microsoft.DotNet.SDK.10", ".NET SDK 10", "Builds and runs the upstream shader translator and extractor"),
        new("KhronosGroup.VulkanSDK", "Vulkan SDK", "SPIRV-Cross and glslangValidator"),
        new("Meta.Zstandard", "Zstandard", "Compression utility")
    ];
    public static string StepId(string id) => id switch
    { "Python.Python.3.13" => "python", "LLVM.LLVM" => "llvm", "Microsoft.DotNet.SDK.10" => "dotnet", "KhronosGroup.VulkanSDK" => "vulkan", "Meta.Zstandard" => "zstd", _ => "vcredist" };
    public static IReadOnlyList<string> Arguments(ToolPackage package, bool upgrade) => [
        upgrade ? "upgrade" : "install", "--id", package.Id, "--exact", "--source", "winget", "--architecture", "x64",
        "--silent", "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity"
    ];
}

public sealed class WindowsPackages(ICommandRunner runner, Downloads downloads, Action<string> log)
{
    public string? Winget { get; private set; }
    public bool RebootRequired { get; private set; }
    public async Task EnsureWingetAsync(string root, CancellationToken ct)
    {
        ToolLocator.RefreshPath();
        Winget = ToolLocator.Find("winget.exe", Path.Combine(ToolLocator.Local, @"Microsoft\WindowsApps"));
        if (Winget != null && (await runner.RunAsync(new(Winget, ["--version"], Timeout: TimeSpan.FromSeconds(30)), null, ct)).ExitCode == 0) return;
        log("App Installer is missing. Downloading the official Microsoft WinGet bundle and architecture-specific dependencies.");
        using var json = await downloads.JsonAsync("https://api.github.com/repos/microsoft/winget-cli/releases/latest", ct);
        var assets = json!.RootElement.GetProperty("assets").EnumerateArray().ToArray();
        async Task<string> Asset(string name)
        {
            var asset = assets.Single(a => a.GetProperty("name").GetString() == name);
            var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
            if (digest == null || !digest.StartsWith("sha256:")) throw new InvalidDataException("Microsoft release asset lacks a SHA-256 digest; refusing bootstrap.");
            return await downloads.FileAsync(asset.GetProperty("browser_download_url").GetString()!, Path.Combine(root, "downloads", name), digest, null, ct);
        }
        var deps = await Asset("DesktopAppInstaller_Dependencies.zip");
        var bundle = await Asset("Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle");
        var folder = Path.Combine(root, "work", "winget-" + Guid.NewGuid().ToString("N"));
        SafeZip.Extract(deps, folder, ct);
        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var packages = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(p => (p.EndsWith(".appx") || p.EndsWith(".msix")) && (p.Contains("/" + arch + "/") || p.Contains("\\" + arch + "\\") || Path.GetFileName(p).Contains("_" + arch + "_"))).ToArray();
        if (packages.Length == 0) throw new InvalidDataException("Cannot find the correct WinGet bootstrap dependencies for " + arch);
        // Add-AppxPackage validates Microsoft package signatures. Existing newer framework dependencies are not downgraded.
        var dependencyPaths = "@(" + string.Join(",", packages.Select(PowerShell.Literal)) + ")";
        var script = "Add-AppxPackage -Path " + PowerShell.Literal(bundle) + " -DependencyPath " + dependencyPaths +
            "; (Get-AppxPackage Microsoft.DesktopAppInstaller).InstallLocation";
        var result = await runner.RunAsync(PowerShell.Command(script, TimeSpan.FromMinutes(10)), log, ct);
        if (result.ExitCode != 0) throw new IOException("App Installer registration failed. Install App Installer from Microsoft Store, then retry. " + result.Output);
        ToolLocator.RefreshPath();
        Winget = ToolLocator.Find("winget.exe", Path.Combine(ToolLocator.Local, @"Microsoft\WindowsApps"));
        if (Winget == null)
        {
            var location = result.Output.Split('\n').Select(p => p.Trim()).LastOrDefault(Directory.Exists);
            Winget = location == null ? null : ToolLocator.Find("winget.exe", location);
        }
        if (Winget == null) throw new IOException("App Installer is registered; restart this installer to refresh the Windows app execution alias.");
        var check = await runner.RunAsync(new(Winget, ["--version"], Timeout: TimeSpan.FromSeconds(30)), log, ct);
        if (check.ExitCode != 0) throw new IOException("WinGet cannot execute on this host. " + check.Output);
    }

    public async Task<bool> ProbeAsync(string id, CancellationToken ct)
    {
        ToolLocator.RefreshPath();
        if (id == "Microsoft.VCRedist.2015+.x64") return ToolLocator.VcRuntimePresent();
        var (file, args) = id switch
        {
            "Python.Python.3.13" => (ToolLocator.Python(), new[] { "-c", "import sys,tkinter,lzma,venv; assert sys.version_info[:2] == (3,13); print(sys.version)" }),
            "LLVM.LLVM" => (ToolLocator.Clang(), new[] { "--version" }),
            "Microsoft.DotNet.SDK.10" => (ToolLocator.DotNet(), new[] { "--list-sdks" }),
            "KhronosGroup.VulkanSDK" => (ToolLocator.VulkanBin() is { } b ? Path.Combine(b, "spirv-cross.exe") : null, new[] { "--revision" }),
            "Meta.Zstandard" => (ToolLocator.Zstd(), new[] { "--version" }),
            _ => (null, Array.Empty<string>())
        };
        if (file == null) return false;
        try
        {
            var r = await runner.RunAsync(new(file, args, Timeout: TimeSpan.FromSeconds(60)), null, ct);
            if (r.ExitCode != 0) return false;
            if (id == "LLVM.LLVM")
            {
                var m = Regex.Match(r.Output, @"clang version (\d+)");
                return m.Success && int.Parse(m.Groups[1].Value) >= 20 && File.Exists(Path.Combine(Path.GetDirectoryName(file)!, "ld64.lld.exe"));
            }
            if (id == "Microsoft.DotNet.SDK.10") return Regex.IsMatch(r.Output, @"(?m)^10\.");
            if (id == "KhronosGroup.VulkanSDK") return File.Exists(Path.Combine(Path.GetDirectoryName(file)!, "glslangValidator.exe"));
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or TimeoutException) { log("Probe: " + e.Message); return false; }
    }
    public async Task InstallAsync(ToolPackage package, InstallMode mode, CancellationToken ct)
    {
        var present = await ProbeAsync(package.Id, ct);
        if (present && mode == InstallMode.InstallRepair) { log(package.Title + " is already usable; reusing it."); return; }
        if (Winget == null) throw new InvalidOperationException("WinGet has not been initialized.");
        var args = PackagePlan.Arguments(package, upgrade: present && mode == InstallMode.Update);
        log("WinGet " + string.Join(' ', args));
        var result = await runner.RunAsync(new(Winget, args, Timeout: TimeSpan.FromMinutes(90)), log, ct);
        var needsReboot = result.ExitCode == 3010 || unchecked((uint)result.ExitCode) is 0x80070BC2 or 0x8A150109 or 0x8A15010A;
        RebootRequired |= needsReboot;
        var noUpgrade = unchecked((uint)result.ExitCode) == 0x8A15002B;
        if (result.ExitCode != 0 && !noUpgrade && !needsReboot)
            throw new IOException($"{package.Title} installation failed (0x{unchecked((uint)result.ExitCode):X8}). Review the log and retry.");
        var usable = await ProbeAsync(package.Id, ct);
        if (!usable && noUpgrade)
        {
            log(package.Title + " is registered but unusable. Asking WinGet to repair the package.");
            var repair = new[] { "repair", "--id", package.Id, "--exact", "--source", "winget", "--silent", "--accept-source-agreements", "--disable-interactivity" };
            var fixedResult = await runner.RunAsync(new(Winget, repair, Timeout: TimeSpan.FromMinutes(60)), log, ct);
            if (fixedResult.ExitCode != 0) log($"WinGet repair returned 0x{unchecked((uint)fixedResult.ExitCode):X8}; verifying the tool again.");
            usable = await ProbeAsync(package.Id, ct);
        }
        if (!usable)
            throw new IOException(package.Title + " is installed but its execution check failed. A restart, UAC approval, or a compatible x64 tool may be needed.");
    }
}
