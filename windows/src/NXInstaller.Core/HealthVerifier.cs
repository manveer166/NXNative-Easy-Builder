using System.Text.Json;

namespace NXInstaller.Core;

public sealed class HealthVerifier(ICommandRunner runner, Action<string> log)
{
    public static Dictionary<string, string> BuildEnvironment(InstallState state)
    {
        var env = state.Toolchain.ToDictionary(kv => "NXW_" + kv.Key.ToUpperInvariant(), kv => kv.Value);
        var paths = new[] { Path.GetDirectoryName(state.Python), ToolLocator.DotNet() is { } dn ? Path.GetDirectoryName(dn) : null,
            state.Toolchain.GetValueOrDefault("llvm") is { } ll ? Path.Combine(ll, "bin") : null,
            state.Toolchain.GetValueOrDefault("spirv_cross_dir"), state.Toolchain.GetValueOrDefault("glslangValidator_dir"),
            state.Toolchain.GetValueOrDefault("zstd_dir"), Environment.GetEnvironmentVariable("PATH") };
        env["PATH"] = string.Join(Path.PathSeparator, paths.Where(p => !string.IsNullOrEmpty(p)));
        env["VIRTUAL_ENV"] = Path.GetDirectoryName(Path.GetDirectoryName(state.Python))!;
        env["PYTHONUTF8"] = "1"; env["PYTHONUNBUFFERED"] = "1";
        env["PYTHONHOME"] = ""; env["PYTHONPATH"] = "";
        env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        return env;
    }
    public static IReadOnlyList<HealthCheck> ParseDoctor(string text, int exitCode)
    {
        try
        {
            using var json = JsonDocument.Parse(text.Trim());
            var checks = json.RootElement.EnumerateArray().Select(row => new HealthCheck(
                row.GetProperty("name").GetString()!, row.GetProperty("ok").GetBoolean(),
                row.GetProperty("detail").GetString() ?? "", !row.TryGetProperty("required", out var req) || req.GetBoolean())).ToList();
            if (checks.Count == 0) checks.Add(new("Upstream doctor output", false, "Doctor returned no checks."));
            if (exitCode != 0 && checks.All(c => !c.Required || c.Ok)) checks.Add(new("Upstream doctor exit code", false, $"Unexpected exit code {exitCode}."));
            return checks;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException)
        { return [new("Upstream doctor output", false, "Doctor output was invalid. " + e.Message)]; }
    }
    public async Task<IReadOnlyList<HealthCheck>> VerifyAsync(string root, InstallState state, bool full, CancellationToken ct)
    {
        var checks = new List<HealthCheck>();
        if (!state.Apple.CanUseSdk)
            return [new("Apple SDK authorization", false, "Standard Xcode terms do not grant use on Windows. Separate applicable authorization is required.")];
        var env = BuildEnvironment(state);
        var working = Path.Combine(root, "work", "health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(working);
        async Task Probe(string title, Func<Task<bool>> run)
        {
            ct.ThrowIfCancellationRequested();
            try { var ok = await run(); checks.Add(new(title, ok, ok ? "Execution check passed" : "Execution check failed; see session log")); }
            catch (Exception e) when (e is not OperationCanceledException) { checks.Add(new(title, false, e.Message)); log(title + ": " + e.Message); }
        }
        Task<CommandResult> Run(string file, params string[] args) => runner.RunAsync(new(file, args, state.Repository, env, TimeSpan.FromMinutes(12)), log, ct);
        var doctor = await runner.RunAsync(new(state.Python, [Path.Combine(state.Repository, "win", "doctor.py"), "--json"], state.Repository, env, TimeSpan.FromMinutes(6)), null, ct);
        log(doctor.Output); checks.AddRange(ParseDoctor(doctor.Output, doctor.ExitCode));
        await Probe("Python modules and Tk GUI", async () => (await Run(state.Python, "-c",
            "import lz4.block,capstone,fontTools,PIL,backports.zstd,tkinter; w=tkinter.Tk(); w.withdraw(); w.update_idletasks(); w.destroy(); print('modules and Tcl/Tk OK')")).ExitCode == 0);
        if (!full) return checks;
        await Probe(".NET extractor build and execution", async () =>
        {
            var dotnet = ToolLocator.DotNet() ?? "dotnet.exe";
            var dir = Path.Combine(working, "extractor");
            var build = await Run(dotnet, "build", Path.Combine(state.Repository, "dm", "tools", "nx_extract"), "-c", "Release", "-o", dir, "--nologo");
            if (build.ExitCode != 0) return false;
            var run = await Run(dotnet, Path.Combine(dir, "nx_extract.dll"));
            return run.ExitCode == 2 && run.Output.Contains("usage: nx_extract", StringComparison.OrdinalIgnoreCase);
        });
        await Probe("Vulkan shader tools", async () =>
        {
            var src = Path.Combine(working, "smoke.comp"); var spv = Path.Combine(working, "smoke.spv");
            await File.WriteAllTextAsync(src, "#version 450\nlayout(local_size_x=1) in; void main() {}\n", ct);
            var vk = state.Toolchain["glslangValidator_dir"]; var sc = state.Toolchain["spirv_cross_dir"];
            return (await Run(Path.Combine(vk, "glslangValidator.exe"), "-V", src, "-o", spv)).ExitCode == 0 &&
                (await Run(Path.Combine(sc, "spirv-cross.exe"), spv, "--msl", "--output", Path.Combine(working, "spirv.metal"))).ExitCode == 0;
        });
        await Probe("Zstandard round trip", async () =>
        {
            var file = Path.Combine(working, "zstd.txt"); var decoded = file + ".decoded";
            const string content = "NXNativeWindows dependency round trip\n";
            await File.WriteAllTextAsync(file, content, ct);
            var zstd = Path.Combine(state.Toolchain["zstd_dir"], "zstd.exe");
            return (await Run(zstd, "-f", file, "-o", file + ".zst")).ExitCode == 0 &&
                (await Run(zstd, "-f", "-d", file + ".zst", "-o", decoded)).ExitCode == 0 && await File.ReadAllTextAsync(decoded, ct) == content;
        });
        await Probe("Clang + iOS SDK + Mach-O linker", async () =>
        {
            var src = Path.Combine(working, "smoke.m"); var obj = Path.Combine(working, "smoke.o");
            await File.WriteAllTextAsync(src, "#import <Foundation/Foundation.h>\n#import <UIKit/UIKit.h>\nint nxw_smoke(void) { return [UIView class] != 0; }\n", ct);
            var bin = Path.Combine(state.Toolchain["llvm"], "bin"); var sdk = state.Toolchain["ios_sdk"];
            if ((await Run(Path.Combine(bin, "clang.exe"), "-target", "arm64-apple-ios26.0", "-isysroot", sdk, "-fobjc-arc", "-c", src, "-o", obj)).ExitCode != 0) return false;
            return (await Run(Path.Combine(bin, "ld64.lld.exe"), "-arch", "arm64", "-platform_version", "ios", "26.0", "26.0", "-syslibroot", sdk,
                "-dylib", obj, "-lSystem", "-framework", "Foundation", "-framework", "UIKit", "-o", Path.Combine(working, "smoke.dylib"))).ExitCode == 0;
        });
        foreach (var target in new[] { ("ios", "air64-apple-ios18.0"), ("macos", "air64-apple-macos15.0") })
            await Probe("Metal shader compile + link (" + target.Item1 + ")", async () =>
            {
                var src = Path.Combine(working, "smoke.metal"); var air = Path.Combine(working, target.Item1 + ".air"); var lib = Path.Combine(working, target.Item1 + ".metallib");
                await File.WriteAllTextAsync(src, "#include <metal_stdlib>\nusing namespace metal; kernel void nxw_smoke(device uint *out [[buffer(0)]], uint id [[thread_position_in_grid]]) { out[id]=id; }\n", ct);
                var metal = ToolLocator.MetalExe(state.Apple.MetalFolder, target.Item1, "metal");
                var metallib = ToolLocator.MetalExe(state.Apple.MetalFolder, target.Item1, "metallib");
                return metal != null && metallib != null && (await Run(metal, "-target", target.Item2, "-c", src, "-o", air)).ExitCode == 0 &&
                    (await Run(metallib, air, "-o", lib)).ExitCode == 0 && new FileInfo(lib).Length > 0;
            });
        return checks;
    }
}
