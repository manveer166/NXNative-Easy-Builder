using System.Diagnostics;
using System.Text.Json;

namespace NXInstaller.Core;

public sealed class InstallerEngine(ICommandRunner runner, Downloads downloads)
{
    public event Action<InstallEvent>? Progress;
    public event Action<string>? Log;
    public Func<AppleSelection, CancellationToken, Task<AppleSelection?>>? RequestAppleFiles { get; set; }
    private string? logFile;
    private readonly object logGate = new();
    private void Write(string text)
    {
        var line = DateTimeOffset.Now.ToString("HH:mm:ss") + "  " + text;
        lock (logGate) { if (logFile != null) File.AppendAllText(logFile, line + Environment.NewLine); }
        Log?.Invoke(line);
    }
    private void Report(string id, string title, StepStatus status, string detail, double? fraction = null) => Progress?.Invoke(new(id, title, status, detail, fraction));
    public Task<RepositoryVersion> CheckForUpdatesAsync(CancellationToken ct) => new RepositoryInstaller(downloads).CheckLatestAsync(ct);
    public async Task<RunSummary> InstallAsync(InstallOptions options, CancellationToken ct)
    {
        var root = StateStore.ValidateRoot(options.Root);
        Directory.CreateDirectory(root);
        // Cross-process file lock also protects two different copies of the installer targeting the same folder.
        using var installLock = new FileStream(Path.Combine(root, "install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        logFile = Path.Combine(root, "logs", "install-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log");
        Write("NXNativeWindows Easy Installer " + typeof(InstallerEngine).Assembly.GetName().Version?.ToString(3) + " · " + options.Mode);
        string currentId = "host", currentTitle = "Compatibility";
        void Start(string id, string title, string detail) { currentId = id; currentTitle = title; Report(id, title, StepStatus.Running, detail); Write(title + ": " + detail); }
        void Done(string detail) => Report(currentId, currentTitle, StepStatus.Done, detail);
        var state = StateStore.Load(root);
        state.Ready = false; state.Apple = options.Apple;
        StateStore.Save(root, state);
        try
        {
            Start("host", "Compatibility", "Checking Windows version, CPU architecture, disk space and write access");
            var host = HostPolicy.Current;
            if (!host.Allowed) throw new PlatformNotSupportedException(host.Detail);
            state.ExperimentalArm64 = host.Experimental;
            var drive = new DriveInfo(Path.GetPathRoot(root)!);
            if (drive.AvailableFreeSpace < 12L * 1024 * 1024 * 1024) throw new IOException("At least 12 GiB free disk space is required for tools and staging. Game builds need additional space.");
            Done(host.Detail); Write(host.Detail);

            Start("repository", "GitHub repository", "Checking the latest default-branch commit before downloading");
            var version = await CheckForUpdatesAsync(ct);
            Write($"Installed: {(state.Commit.Length == 0 ? "none" : state.Commit)}; latest: {version.Commit}. {version.Message}");
            var repository = await new RepositoryInstaller(downloads).FetchAsync(root, version, Write, _ => { }, ct,
                p => Report("repository", "GitHub repository", StepStatus.Running, p.Detail, p.Fraction));
            state.Repository = repository.Directory; state.Commit = repository.Commit;
            StateStore.Save(root, state); Done("Latest commit " + version.Commit[..8] + " · " + version.Branch);

            Start("winget", "Windows package manager", "Checking App Installer / WinGet");
            var packages = new WindowsPackages(runner, downloads, Write);
            await packages.EnsureWingetAsync(root, ct); Done("WinGet is available");
            foreach (var package in PackagePlan.Packages)
            {
                Start(PackagePlan.StepId(package.Id), package.Title, package.Description);
                await packages.InstallAsync(package, options.Mode, ct); Done("Installed and execution checked");
            }
            Start("packages", "Python environment", "Preparing an isolated environment and Python packages");
            var venv = Path.Combine(root, "runtime", ".venv");
            state.Python = Path.Combine(venv, "Scripts", "python.exe");
            var env = new Dictionary<string, string> { ["PYTHONUTF8"] = "1", ["PYTHONUNBUFFERED"] = "1", ["PYTHONHOME"] = "", ["PYTHONPATH"] = "" };
            async Task RunChecked(string file, params string[] args)
            {
                var result = await runner.RunAsync(new(file, args, root, env, TimeSpan.FromMinutes(30)), Write, ct);
                if (result.ExitCode != 0) throw new IOException($"{Path.GetFileName(file)} failed with exit code {result.ExitCode}. See the session log.");
            }
            bool venvOk = false;
            if (File.Exists(state.Python))
            {
                try { venvOk = (await runner.RunAsync(new(state.Python, ["-c", "import sys,tkinter,pip; assert sys.version_info[:2] == (3,13)"], Environment: env, Timeout: TimeSpan.FromMinutes(1)), null, ct)).ExitCode == 0; }
                catch (Exception e) when (e is not OperationCanceledException) { Write("Virtual environment needs repair: " + e.Message); }
            }
            if (!venvOk)
            {
                if (Directory.Exists(venv)) Directory.Move(venv, venv + ".backup-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"));
                await RunChecked(ToolLocator.Python() ?? throw new IOException("Python 3.13 was not found after installation."), "-m", "venv", venv);
            }
            await RunChecked(state.Python, "-m", "pip", "install", "--disable-pip-version-check", "--upgrade", "pip");
            var imports = await runner.RunAsync(new(state.Python, ["-c", "import lz4.block,capstone,fontTools,PIL,unicorn,backports.zstd"], root, env, TimeSpan.FromMinutes(1)), null, ct);
            var pipArgs = new List<string> { "-m", "pip", "install", "--disable-pip-version-check", "--upgrade" };
            if (imports.ExitCode != 0) { pipArgs.Add("--force-reinstall"); Write("Missing or broken Python modules detected; reinstalling the package set."); }
            pipArgs.AddRange(["lz4", "capstone", "fonttools", "pillow", "unicorn", "backports.zstd; python_version < '3.14'"]);
            await RunChecked(state.Python, pipArgs.ToArray());
            StateStore.Save(root, state); Done("lz4 · capstone · fonttools · pillow · unicorn · zstd");

            Start("apple", "Apple developer tools", "Official downloads and SDK authorization required");
            var appleAutomation = new AppleToolsAutomation(root, runner, Write);
            appleAutomation.Detect(state.Apple, state.Repository);
            if (state.Apple.CanUseSdk && state.Apple.MetalLicenseAccepted) await appleAutomation.PrepareMetalAsync(state.Apple, ct);
            if (!state.Apple.CanUseSdk || (!AppleToolsAutomation.ValidSdkRoot(state.Apple.SdkFolder) && !File.Exists(state.Apple.XcodeXip)) || !AppleToolsAutomation.ValidMetalRoot(state.Apple.MetalFolder))
            {
                Report("apple", "Apple developer tools", StepStatus.ActionRequired, "Complete the integrated Apple tools step to continue");
                if (RequestAppleFiles != null)
                {
                    var selected = await RequestAppleFiles(state.Apple, ct);
                    if (selected != null) state.Apple = selected;
                }
            }
            StateStore.Save(root, state);
            if (!state.Apple.CanUseSdk || (!AppleToolsAutomation.ValidSdkRoot(state.Apple.SdkFolder) && !File.Exists(state.Apple.XcodeXip)) || !AppleToolsAutomation.ValidMetalRoot(state.Apple.MetalFolder))
            {
                var message = "Open-source dependencies are prepared. Apple tools are pending. Configure authorized Apple SDK / Metal files, then run Install / Repair to resume.";
                Report("apple", "Apple developer tools", StepStatus.ActionRequired, message);
                var blocked = new[] { new HealthCheck("Apple SDK / Metal setup", false, message) };
                SaveReport(root, state, blocked); return new(false, message, blocked);
            }
            // An authorization attestation is required even for an SDK that was already present.
            if (!AppleToolsAutomation.ValidSdkRoot(state.Apple.SdkFolder))
            {
                Report("apple", "Apple developer tools", StepStatus.Running, "Extracting only the iPhoneOS SDK from the selected official Xcode XIP");
                state.Apple.SdkFolder = await new SdkExtractor(root, runner, Write).ExtractAsync(state.Python, state.Repository, state.Apple, ct);
                StateStore.Save(root, state);
            }
            Done("User-provided Apple tools configured; authorization recorded locally");

            Start("environment", "Toolchain configuration", "Saving explicit tool paths and user environment variables");
            var configPath = Path.Combine(state.Repository, "win", "toolchain.json");
            Dictionary<string, JsonElement> config = new();
            if (File.Exists(configPath))
            {
                try { config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await File.ReadAllTextAsync(configPath, ct)) ?? new(); }
                catch (JsonException)
                {
                    var backup = configPath + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                    File.Copy(configPath, backup); Write("Invalid toolchain JSON backed up to " + backup + "; regenerating managed settings.");
                }
            }
            state.Toolchain = new()
            {
                ["llvm"] = Directory.GetParent(Path.GetDirectoryName(ToolLocator.Clang())!)!.FullName,
                ["metal_tools"] = state.Apple.MetalFolder, ["ios_sdk"] = state.Apple.SdkFolder,
                ["spirv_cross_dir"] = ToolLocator.VulkanBin()!, ["glslangValidator_dir"] = ToolLocator.VulkanBin()!,
                ["zstd_dir"] = Path.GetDirectoryName(ToolLocator.Zstd())!,
                ["std_font"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
                ["ios_deployment"] = "26.0", ["metal_ios_target"] = "18.0", ["metal_macos_target"] = "15.0"
            };
            foreach (var entry in state.Toolchain) config[entry.Key] = JsonSerializer.SerializeToElement(entry.Value);
            StateStore.AtomicJson(configPath, config);
            PersistEnvironment(root, state);
            StateStore.Save(root, state); Done("toolchain.json and NXW_* variables configured");

            Start("verify", "Installation checks", "Running upstream doctor and real compiler / shader / compression probes");
            var checks = await new HealthVerifier(runner, Write).VerifyAsync(root, state, true, ct);
            state.Ready = checks.All(c => !c.Required || c.Ok) && !packages.RebootRequired;
            state.VerifiedAt = DateTimeOffset.UtcNow;
            StateStore.Save(root, state); SaveReport(root, state, checks);
            if (!state.Ready)
            {
                var detail = packages.RebootRequired ? "A dependency requires a Windows restart. Restart, then run Install / Repair." : "Some checks failed. Open the log, fix the reported item, then run Install / Repair.";
                Report("verify", "Installation checks", StepStatus.Failed, detail);
                return new(false, detail, checks);
            }
            Done("All required execution checks passed" + (state.ExperimentalArm64 ? " · ARM64 remains experimental" : ""));
            return new(true, "Setup checks passed. NXNativeWindows IPA Builder is ready to launch.", checks);
        }
        catch (OperationCanceledException)
        {
            state.Ready = false; StateStore.Save(root, state);
            Report(currentId, currentTitle, StepStatus.Cancelled, "Stopped. Run Install / Repair to resume; completed tools will be checked and reused.");
            Write("Cancelled by the user. External MSI / UAC operations may finish independently."); throw;
        }
        catch (Exception e)
        {
            state.Ready = false; StateStore.Save(root, state);
            Report(currentId, currentTitle, StepStatus.Failed, e.Message); Write(e.ToString());
            SaveReport(root, state, [new(currentTitle, false, e.Message)]); throw;
        }
    }
    public static void SaveReport(string root, InstallState state, IReadOnlyList<HealthCheck> checks) => StateStore.AtomicJson(Path.Combine(root, "health-report.json"),
        new { GeneratedAt = DateTimeOffset.UtcNow, state.Commit, state.Ready, state.ExperimentalArm64, Platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            Scope = "Installation probes only; no game IPA build or iPhone playback tested.", Checks = checks });
    public static void PersistEnvironment(string root, InstallState state)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        foreach (var variable in state.Toolchain) Environment.SetEnvironmentVariable("NXW_" + variable.Key.ToUpperInvariant(), variable.Value, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable("NXW_HOME", root, EnvironmentVariableTarget.User);
        var env = HealthVerifier.BuildEnvironment(state);
        var existing = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        var paths = new[] { Path.GetDirectoryName(state.Python)!, Path.Combine(state.Toolchain["llvm"], "bin"), state.Toolchain["spirv_cross_dir"], state.Toolchain["zstd_dir"] };
        var combined = string.Join(';', existing.Split(';', StringSplitOptions.RemoveEmptyEntries).Concat(paths).Distinct(StringComparer.OrdinalIgnoreCase));
        if (combined.Length > 30000) throw new IOException("User PATH is too long. Shorten it and retry; it has not been overwritten.");
        Environment.SetEnvironmentVariable("PATH", combined, EnvironmentVariableTarget.User);
        foreach (var variable in env) Environment.SetEnvironmentVariable(variable.Key, variable.Value);
    }
    public static CommandSpec LaunchCommand(string root)
    {
        var state = StateStore.Load(root);
        if (!state.Ready || !state.Apple.CanUseSdk) throw new InvalidOperationException("Complete setup and its required checks before launching.");
        var pythonw = Path.Combine(Path.GetDirectoryName(state.Python)!, "pythonw.exe");
        var gui = Path.Combine(state.Repository, "win", "gui.pyw");
        if (!File.Exists(pythonw) || !File.Exists(gui)) throw new FileNotFoundException("Builder files are missing. Run Install / Repair.");
        return new(pythonw, [gui], state.Repository, HealthVerifier.BuildEnvironment(state));
    }
    public static void Launch(string root)
    {
        var command = LaunchCommand(root);
        var info = new ProcessStartInfo(command.File) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = command.WorkingDirectory! };
        foreach (var arg in command.Args) info.ArgumentList.Add(arg);
        foreach (var kv in command.Environment!) info.Environment[kv.Key] = kv.Value;
        Process.Start(info)?.Dispose();
    }
}
