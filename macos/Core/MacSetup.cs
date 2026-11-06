using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace NXNativeEasyBuilder;

public sealed record MacTools(string Developer, string Python, string Dotnet, string DotnetRoot)
{
    public Dictionary<string, string?> Environment(Release release) => new()
    {
        ["DEVELOPER_DIR"] = Developer,
        ["PATH"] = string.Join(':', Path.GetDirectoryName(Python), Path.GetDirectoryName(Dotnet), MacSetup.ToolPath),
        ["DOTNET_ROOT"] = DotnetRoot,
        ["DOTNET_ROOT_ARM64"] = DotnetRoot,
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        ["VIRTUAL_ENV"] = Path.GetDirectoryName(Path.GetDirectoryName(Python)),
        ["SPIRV_CROSS"] = release.SpirvCross,
        ["PYTHONUTF8"] = "1",
        ["PYTHONUNBUFFERED"] = "1",
        ["PYTHONNOUSERSITE"] = "1",
        ["PYTHONPATH"] = null,
        ["PYTHONHOME"] = null,
        ["DM_ALLOW_OTHER_METAL"] = null,
        ["DMSH_TARGETS"] = null
    };
}

public sealed class MacSetup(string root, Action<string> log, CancellationToken cancellation)
{
    public const string Brew = "/opt/homebrew/bin/brew";
    public const string ToolPath = "/opt/homebrew/bin:/opt/homebrew/sbin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin";
    private const string BrewPython = "/opt/homebrew/opt/python@3.13/bin/python3.13";
    private const string NativePython = "import platform,sys; assert platform.machine()=='arm64' and sys.version_info >= (3,12), (platform.machine(),sys.version)";
    private const string PythonModules = NativePython + "; import lz4.block,capstone,fontTools,nsz,zbic; assert capstone.__version__.split('.')[0]=='5'; " +
        "exec('if sys.version_info < (3,14):\\n from backports import zstd')";
    private const string VerifyLock = "import sys\nsys.path.insert(0,sys.argv[1])\nimport own_compilers\n" +
        "bad=own_compilers.check_tools()\nfor line in bad: print(line)\nsys.exit(bool(bad))";
    private static readonly string[] Packages = ["lz4", "capstone>=5,<6", "fonttools", "nsz", "zbic", "backports.zstd; python_version < '3.14'"];
    private string Venv => Path.Combine(root, "Runtime", "venv");
    private string Python => Path.Combine(Venv, "bin/python3");

    public static void CheckHost()
    {
        if (!OperatingSystem.IsMacOS() || RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
            throw new PlatformNotSupportedException("Open the Apple Silicon build on an Apple Silicon Mac.");
        if (!OperatingSystem.IsMacOSVersionAtLeast(26, 6))
            throw new PlatformNotSupportedException("NXNative requires macOS 26.6 or later for this toolchain.");
    }

    public async Task<bool> InstallAsync(Release release, Action<string> stage)
    {
        CheckHost();
        stage("Checking Xcode");
        var developer = await FindXcodeAsync(release.XcodeVersion);
        var env = BaseEnvironment(developer);
        var firstLaunch = await Probe("/usr/bin/xcodebuild", ["-checkFirstLaunchStatus"], env);
        if (firstLaunch.ExitCode != 0)
            throw new IOException($"Open Xcode {release.XcodeVersion} and finish its first-launch setup, then retry.\n{firstLaunch.Output.Trim()}");

        stage("Preparing Homebrew tools");
        if (!File.Exists(Brew)) throw new IOException("Click Set Up Homebrew, finish the setup in Terminal, then click Install Everything again.");
        var missing = new List<string>();
        if (!File.Exists(BrewPython)) missing.Add("python@3.13");
        if (!File.Exists("/opt/homebrew/bin/xcodegen")) missing.Add("xcodegen");
        if (!File.Exists("/opt/homebrew/bin/zstd")) missing.Add("zstd");
        if (!File.Exists("/opt/homebrew/bin/glslangValidator")) missing.Add("glslang");
        var dotnet = await FindDotnetAsync(env);
        if (dotnet == null) missing.Add("dotnet@10");
        if (missing.Count > 0)
        {
            log("Homebrew will install " + string.Join(", ", missing) + ".");
            await RunChecked(Brew, ["install", .. missing], env, "Homebrew could not install the tools.");
        }
        else log("Homebrew tools are already present.");
        dotnet ??= await FindDotnetAsync(env);
        if (dotnet == null) throw new IOException("The first dotnet on the upstream tool path must be an ARM64 .NET 10 SDK. See the log.");

        stage("Preparing Python environment");
        await EnsurePythonAsync(env);
        await CheckUtilitiesAsync(env);

        stage("Checking Metal Toolchain");
        if ((await Metal("macosx", env)).ExitCode != 0)
        {
            log($"Downloading the Metal Toolchain through Xcode {release.XcodeVersion}.");
            await RunChecked("/usr/bin/xcodebuild", ["-downloadComponent", "MetalToolchain"], env,
                "Metal setup needs attention. Open Xcode → Settings → Components.");
        }

        stage("Checking iPhoneOS SDK");
        if (!await HasIosAsync(env))
        {
            log("Downloading the iOS platform through Xcode. This can take a while.");
            var iosDownload = await ProcessRunner.RunAsync("/usr/bin/xcodebuild", ["-downloadPlatform", "iOS"],
                cancellation, log, environment: env, timeout: TimeSpan.FromHours(2));
            if (iosDownload.ExitCode != 0) log("iOS platform download needs attention. Use Xcode → Settings → Components.");
        }

        stage("Verifying shader toolchain");
        var tools = new MacTools(developer, Python, dotnet.Value.Exe, dotnet.Value.Root);
        var ipa = await HasIosAsync(env);
        await VerifyAsync(release, tools, ipa);
        return ipa;
    }

    public async Task<MacTools> ResolveAsync(Release release, bool ipa)
    {
        CheckHost();
        var developer = await FindXcodeAsync(release.XcodeVersion);
        var env = BaseEnvironment(developer);
        if (!File.Exists(Python) || (await Probe(Python, ["-c", PythonModules], env)).ExitCode != 0)
            throw new IOException("Python environment needs repair. Click Install Everything.");
        var dotnet = await FindDotnetAsync(env) ?? throw new IOException("ARM64 .NET 10 SDK is required. Click Install Everything.");
        await CheckUtilitiesAsync(env);
        var tools = new MacTools(developer, Python, dotnet.Exe, dotnet.Root);
        await VerifyAsync(release, tools, ipa);
        return tools;
    }

    private async Task EnsurePythonAsync(Dictionary<string, string?> env)
    {
        if (!File.Exists(BrewPython) || (await Probe(BrewPython, ["-c", NativePython], env)).ExitCode != 0)
            throw new IOException("Homebrew Python 3.13 ARM64 did not start.");
        if (!File.Exists(Python) || (await Probe(Python, ["-c", NativePython], env)).ExitCode != 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Venv)!);
            var backup = Venv + ".previous-" + Guid.NewGuid().ToString("N");
            if (Directory.Exists(Venv)) Directory.Move(Venv, backup);
            try { await RunChecked(BrewPython, ["-m", "venv", Venv], env, "Could not create the Python environment."); }
            catch
            {
                if (Directory.Exists(Venv)) Directory.Delete(Venv, recursive: true);
                if (Directory.Exists(backup)) Directory.Move(backup, Venv);
                throw;
            }
            if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
        }
        if ((await Probe(Python, ["-c", PythonModules], env)).ExitCode != 0)
            await RunChecked(Python, ["-m", "pip", "--disable-pip-version-check", "install", "--no-input", .. Packages],
                env, "Could not install the Python packages.");
        await RunChecked(Python, ["-c", PythonModules], env, "Python dependency check failed.");
        log("Python ARM64 and packages passed their import checks.");
    }

    private async Task VerifyAsync(Release release, MacTools tools, bool ipa)
    {
        var env = tools.Environment(release);
        if (!Repository.FingerprintsMatch(release))
            throw new IOException("Repository shader tools differ from their lock. Click Install Everything to repair the checkout.");
        if ((await Metal("macosx", env)).ExitCode != 0)
            throw new IOException("macOS Metal compiler is missing. Click Install Everything.");
        if (ipa && !await HasIosAsync(env))
            throw new IOException("Install the iOS platform in Xcode → Settings → Components or click Install Everything.");
        await RunChecked(release.SpirvCross, ["--revision"], env, "Bundled SPIRV-Cross did not run.");
        env["DMSH_TARGETS"] = ipa ? "mac,ios" : "mac";
        await RunChecked(tools.Python, ["-c", VerifyLock, release.ShaderTools], env,
            "Shader toolchain check failed. See the exact fingerprint or Xcode requirement in the log.");
        log("The repository shader tools and Xcode Metal passed their lock checks.");
    }

    private async Task CheckUtilitiesAsync(Dictionary<string, string?> env)
    {
        foreach (var tool in new[] { "xcodegen", "zstd", "glslangValidator" })
        {
            var file = "/opt/homebrew/bin/" + tool;
            if (!File.Exists(file) || (await Probe(file, ["--version"], env)).ExitCode != 0)
                throw new IOException($"{tool} is missing or unusable. Click Install Everything and inspect Homebrew's output.");
        }
    }

    private async Task<string> FindXcodeAsync(string required)
    {
        var selected = await Probe("/usr/bin/xcode-select", ["-p"], BaseEnvironment());
        var candidates = new List<string>();
        if (selected.ExitCode == 0) candidates.Add(selected.Output.Trim());
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        foreach (var directory in new[] { "/Applications", Path.Combine(home, "Applications") })
        {
            if (!Directory.Exists(directory)) continue;
            candidates.AddRange(Directory.EnumerateDirectories(directory, "Xcode*.app")
                .Select(path => Path.Combine(path, "Contents/Developer")));
        }
        foreach (var candidate in candidates.Distinct())
        {
            if (!File.Exists(Path.Combine(candidate, "usr/bin/xcodebuild"))) continue;
            var result = await Probe("/usr/bin/xcodebuild", ["-version"], BaseEnvironment(candidate));
            var version = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => line.StartsWith("Xcode ", StringComparison.Ordinal));
            if (result.ExitCode == 0 && version?.Trim() == "Xcode " + required)
            {
                log($"Xcode {required} · {candidate}");
                return candidate;
            }
            if (!string.IsNullOrWhiteSpace(version)) log($"Found {version.Trim()} at {candidate}");
        }
        throw new IOException($"This NXNative commit expects Xcode {required}. Click Get Xcode and install it in Applications, then open it once.");
    }

    private async Task<(string Exe, string Root)?> FindDotnetAsync(Dictionary<string, string?> env)
    {
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        // Match the upstream PATH order so its subprocesses use the SDK we check.
        var executable = new[] { "/opt/homebrew/bin/dotnet", "/usr/local/bin/dotnet", "/usr/local/share/dotnet/dotnet",
            Path.Combine(home, ".dotnet/dotnet") }.FirstOrDefault(File.Exists);
        if (executable == null) return null;
        var sdk = await Probe(executable, ["--list-sdks"], env);
        if (sdk.ExitCode != 0 || !Regex.IsMatch(sdk.Output, @"(?m)^10\.\d+\.\d+\s+\[")) return null;
        var info = await Probe(executable, ["--info"], env);
        if (info.ExitCode != 0 || !Regex.IsMatch(info.Output, @"(?mi)^\s*Architecture:\s*arm64\s*$"))
        {
            log($"dotnet at {executable} is not an ARM64 SDK.");
            return null;
        }
        var runtime = await Probe(executable, ["--list-runtimes"], env);
        var match = Regex.Match(runtime.Output, @"(?m)^Microsoft\.NETCore\.App 10\.[^\s]+ \[(.+)/shared/Microsoft\.NETCore\.App\]\s*$");
        if (!match.Success) return null;
        log($".NET 10 ARM64 · {executable}");
        return (executable, match.Groups[1].Value);
    }

    private async Task<bool> HasIosAsync(Dictionary<string, string?> env)
    {
        var sdk = await Probe("/usr/bin/xcrun", ["--sdk", "iphoneos", "--show-sdk-path"], env);
        return sdk.ExitCode == 0 && sdk.Output.Split('\n').Any(line => Directory.Exists(line.Trim())) &&
            (await Metal("iphoneos", env)).ExitCode == 0;
    }

    private Task<CommandResult> Metal(string sdk, Dictionary<string, string?> env) =>
        Probe("/usr/bin/xcrun", ["--sdk", sdk, "metal", "--version"], env);

    private static Dictionary<string, string?> BaseEnvironment(string? developer = null) => new()
    {
        ["PATH"] = ToolPath,
        ["DEVELOPER_DIR"] = developer,
        ["PYTHONPATH"] = null,
        ["PYTHONHOME"] = null,
        ["PYTHONNOUSERSITE"] = "1",
        ["DOTNET_ROOT"] = null,
        ["DOTNET_ROOT_ARM64"] = null,
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    };

    private Task<CommandResult> Probe(string executable, string[] args, Dictionary<string, string?> env) =>
        ProcessRunner.RunAsync(executable, args, cancellation, environment: env, timeout: TimeSpan.FromSeconds(30));

    private async Task RunChecked(string executable, string[] args, Dictionary<string, string?> env, string failure)
    {
        var result = await ProcessRunner.RunAsync(executable, args, cancellation, log, environment: env,
            timeout: TimeSpan.FromHours(2));
        if (result.ExitCode != 0) throw new IOException($"{failure} (exit {result.ExitCode})");
    }
}
